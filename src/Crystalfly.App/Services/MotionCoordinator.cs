using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.ViewModels;
using Crystalfly.Core.Configuration;
using Ursa.Controls;

namespace Crystalfly.App.Services;

public sealed class MotionCoordinator
{
    private readonly Window owner;
    private readonly Func<bool> isFollowSystemMotion;
    private readonly Func<bool> isReducedMotion;
    private readonly List<TranslateTransform> knightWalkTransforms = [];
    private readonly List<LoadingContainer> knightLoadingHosts = [];
    private readonly List<Control> entranceAnimationTargets = [];
    private readonly Dictionary<Control, long> entranceAnimationGenerations = [];
    private readonly Dictionary<Control, EntranceMotion> entranceAnimationTransforms = [];
    private readonly Dictionary<Control, CancellationTokenSource> entranceAnimationCancellations = [];
    private readonly Dictionary<Control, EntranceFrameState> activeEntranceAnimations = [];
    private readonly List<EntranceFrameState> completedEntranceAnimations = [];
    private readonly Dictionary<Control, Transitions?> motionBaseTransitions = [];
    private readonly Dictionary<Control, MicroMotion> microInteractionTransforms = [];
    private long entranceAnimationGeneration;
    private DispatcherTimer? knightWalkTimer;
    private bool entranceAnimationFrameScheduled;
    private int knightWalkFrame;

    internal MotionCoordinator(
        Window owner,
        Func<bool> isFollowSystemMotion,
        Func<bool> isReducedMotion)
    {
        this.owner = owner;
        this.isFollowSystemMotion = isFollowSystemMotion;
        this.isReducedMotion = isReducedMotion;
    }

    internal void Start()
    {
        SubscribeEntranceAnimations();
        StartKnightWalkAnimation();
    }

    internal void RegisterMotionTargets(bool animateNewTargets)
    {
        foreach (var control in owner.GetVisualDescendants().OfType<Control>().Where(IsEntranceAnimationTarget))
        {
            RegisterMotionTarget(control, animateNewTargets);
        }
        ConfigureMicroInteractionTransitions();
    }

    internal void RegisterMotionTarget(Control control, bool animate)
    {
        if (entranceAnimationTargets.Contains(control))
        {
            return;
        }
        entranceAnimationTargets.Add(control);
        control.PropertyChanged += OnEntranceTargetPropertyChanged;
        if (animate && control.IsEffectivelyVisible && IsOpacityEntranceTarget(control))
        {
            QueueEntranceAnimation(control);
        }
    }

    internal void UpdateMotionPreference(bool replayVisiblePages = false)
    {
        ConfigureMicroInteractionTransitions();
        if (!IsMotionEnabled())
        {
            CancelEntranceAnimations();
        }
        else if (replayVisiblePages)
        {
            foreach (var control in entranceAnimationTargets.Where(control =>
                         control.IsVisible && control.Classes.Contains("cfp-page")))
            {
                QueueEntranceAnimation(control);
            }
        }
        UpdateKnightWalkAnimationState();
    }

    internal void AnimateSpeedrunTabIndicator(Control indicator)
    {
        if (indicator.RenderTransform is not TranslateTransform transform)
        {
            return;
        }

        double target = owner.DataContext is MainViewModel { IsSpeedrunActivityTab: true }
            ? indicator.Bounds.Width
            : 0d;
        transform.Transitions = IsFullMotionEnabled()
            ? new Transitions
            {
                new DoubleTransition
                {
                    Property = TranslateTransform.XProperty,
                    Duration = TimeSpan.FromMilliseconds(220),
                    Easing = new CubicEaseOut()
                }
            }
            : null;
        transform.X = target;
    }

    internal void Shutdown()
    {
        owner.PropertyChanged -= OnWindowPropertyChanged;
        foreach (var target in entranceAnimationTargets)
        {
            target.PropertyChanged -= OnEntranceTargetPropertyChanged;
        }
        RestoreMicroInteractionTransforms();
        entranceAnimationTargets.Clear();
        entranceAnimationGenerations.Clear();
        entranceAnimationFrameScheduled = false;
        activeEntranceAnimations.Clear();
        completedEntranceAnimations.Clear();
        entranceAnimationTransforms.Clear();
        foreach (var cancellation in entranceAnimationCancellations.Values)
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }
        entranceAnimationCancellations.Clear();
        if (knightWalkTimer is not null)
        {
            knightWalkTimer.Stop();
            knightWalkTimer.Tick -= OnKnightWalkTick;
            knightWalkTimer = null;
        }

        foreach (var host in knightLoadingHosts)
        {
            host.PropertyChanged -= OnKnightLoadingHostPropertyChanged;
        }

        knightLoadingHosts.Clear();
        knightWalkTransforms.Clear();
    }

    private void StartKnightWalkAnimation()
    {
        foreach (var (imageName, hostName) in new[]
        {
            ("GlobalKnightWalkImage", "GlobalLoadingHost"),
            ("SaveEditorKnightWalkImage", "SaveEditorLoadingHost")
        })
        {
            var image = owner.FindControl<Image>(imageName);
            var host = owner.FindControl<LoadingContainer>(hostName);
            if (image is null || host is null)
            {
                continue;
            }

            var transform = new TranslateTransform();
            image.RenderTransform = transform;
            knightWalkTransforms.Add(transform);
            knightLoadingHosts.Add(host);
            host.PropertyChanged += OnKnightLoadingHostPropertyChanged;
        }

        if (knightWalkTransforms.Count == 0)
        {
            return;
        }

        knightWalkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        knightWalkTimer.Tick += OnKnightWalkTick;
        owner.PropertyChanged += OnWindowPropertyChanged;
        UpdateKnightWalkAnimationState();
    }

    private void OnKnightLoadingHostPropertyChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs eventArgs) =>
        UpdateKnightWalkAnimationState();

    private void OnWindowPropertyChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.Property == Visual.IsVisibleProperty)
        {
            UpdateKnightWalkAnimationState();
        }
    }

    private void OnKnightWalkTick(object? sender, EventArgs eventArgs)
    {
        if (!ShouldAnimateKnight())
        {
            UpdateKnightWalkAnimationState();
            return;
        }

        knightWalkFrame = (knightWalkFrame + 1) % 4;
        var offset = -knightWalkFrame * 45;
        foreach (var transform in knightWalkTransforms)
        {
            transform.X = offset;
        }
    }

    private bool ShouldAnimateKnight() =>
        IsFullMotionEnabled()
        && owner.IsVisible
        && knightLoadingHosts.Any(host => host.IsLoading && host.IsEffectivelyVisible);

    private void UpdateKnightWalkAnimationState()
    {
        if (knightWalkTimer is null)
        {
            return;
        }

        if (ShouldAnimateKnight())
        {
            knightWalkTimer.Start();
            return;
        }

        knightWalkTimer.Stop();
        knightWalkFrame = 0;
        foreach (var transform in knightWalkTransforms)
        {
            transform.X = 0;
        }
    }

    private bool AreClientAreaAnimationsEnabled()
    {
        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        return !SystemParametersInfo(
                SpiGetClientAreaAnimation,
                0,
                out var enabled,
                0)
            || enabled != 0;
    }

    private static readonly TimeSpan PageEntranceDuration = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan PageEntranceFluentDuration = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan PageOpacityDuration = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan TransientEntranceDuration = TimeSpan.FromMilliseconds(180);
    private static readonly TimeSpan ReducedEntranceDuration = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan MicroOpacityDuration = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan MicroPressDuration = TimeSpan.FromMilliseconds(80);
    private static readonly TimeSpan MicroReleaseDuration = TimeSpan.FromMilliseconds(220);
    private const double PageEntranceOffset = -16d;
    private const double PageEntranceFluentOffset = -5d;
    private const double PageEntranceBackOffset = -11d;
    private const double TransientEntranceOffset = 8d;
    private const double TransientEntranceStartOpacity = 0.82d;
    private const double ReducedEntranceStartOpacity = 0.9d;
    private const double MicroPressedScale = 0.955d;

    private sealed record EntranceMotion(
        ITransform? BaseTransform,
        RelativePoint BaseOrigin,
        TranslateTransform Translate);

    private sealed record MicroMotion(
        ITransform? BaseTransform,
        RelativePoint BaseOrigin,
        ScaleTransform Scale,
        Transitions PressTransitions,
        Transitions ReleaseTransitions);

    private sealed class EntranceFrameState
    {
        public required Control Control { get; init; }
        public required long Generation { get; init; }
        public EntranceMotion? Motion { get; init; }
        public required CancellationTokenSource Cancellation { get; init; }
        public required bool OpacityOnly { get; init; }
        public long StartedTimestamp { get; set; }
        public bool HasStarted { get; set; }
        public required TimeSpan Delay { get; init; }
        public required TimeSpan Duration { get; init; }
    }

    private void SubscribeEntranceAnimations()
    {
        RegisterMotionTargets(animateNewTargets: false);
        UpdateMotionPreference(replayVisiblePages: true);
    }

    private static bool IsOpacityEntranceTarget(Control control) =>
        control.Classes.Contains("cfp-dialog-motion")
        || control.Classes.Contains("cfp-toast-motion");

    private static bool IsEntranceAnimationTarget(Control control) =>
        control.Classes.Contains("cfp-subpage")
        || control.Classes.Contains("cfp-mod-bulk-bar")
        || control.Classes.Contains("cfp-page")
        || control.Classes.Contains("cfp-dialog-motion")
        || control.Classes.Contains("cfp-toast-motion");

    private void ConfigureMicroInteractionTransitions()
    {
        foreach (var control in owner.GetVisualDescendants().OfType<Control>().Where(control =>
                     control.Classes.Contains("cfp-local-nav")
                     || control.Classes.Contains("cfp-instance-actions")
                     || control.Classes.Contains("cfp-installed-mod-actions")
                     || control.Classes.Contains("cfp-installed-mod-accent")
                     || IsMicroInteractionTarget(control)))
        {
            if (!motionBaseTransitions.ContainsKey(control))
            {
                motionBaseTransitions.Add(control, control.Transitions);
            }
            if (!IsMotionEnabled())
            {
                control.Transitions = motionBaseTransitions[control];
                continue;
            }
            control.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = Visual.OpacityProperty,
                    Duration = MicroOpacityDuration,
                    Easing = new CubicEaseOut()
                }
            };
            ConfigureMicroInteractionTransform(control);
        }
        if (!IsFullMotionEnabled())
        {
            RestoreMicroInteractionTransforms();
        }
    }

    private static bool IsMicroInteractionTarget(Control control) => control is Button
        && (control.Classes.Contains("cfp-nav")
            || control.Classes.Contains("cfp-primary")
            || control.Classes.Contains("cfp-accent")
            || control.Classes.Contains("cfp-secondary")
            || control.Classes.Contains("cfp-icon")
            || control.Classes.Contains("cfp-instance-main")
            || control.Classes.Contains("cfp-market-item")
            || control.Classes.Contains("cfp-instance-action")
            || control.Classes.Contains("cfp-mod-action")
            || control.Classes.Contains("cfp-quick-action"));

    private void ConfigureMicroInteractionTransform(Control control)
    {
        if (!IsFullMotionEnabled()
            || microInteractionTransforms.ContainsKey(control)
            || control.RenderTransform is not null)
        {
            return;
        }
        var scale = new ScaleTransform { ScaleX = 1, ScaleY = 1 };
        var pressTransitions = CreateScaleTransitions(MicroPressDuration);
        var releaseTransitions = CreateScaleTransitions(MicroReleaseDuration);
        scale.Transitions = releaseTransitions;
        var motion = new MicroMotion(
            control.RenderTransform,
            control.RenderTransformOrigin,
            scale,
            pressTransitions,
            releaseTransitions);
        microInteractionTransforms.Add(control, motion);
        control.RenderTransform = scale;
        control.RenderTransformOrigin = RelativePoint.Center;
        control.PointerExited += OnMicroInteractionPointerExited;
        control.PointerPressed += OnMicroInteractionPointerPressed;
        control.PointerReleased += OnMicroInteractionPointerReleased;
    }

    private static Transitions CreateScaleTransitions(TimeSpan duration) =>
    [
        new DoubleTransition
        {
            Property = ScaleTransform.ScaleXProperty,
            Duration = duration,
            Easing = new CubicEaseOut()
        },
        new DoubleTransition
        {
            Property = ScaleTransform.ScaleYProperty,
            Duration = duration,
            Easing = new CubicEaseOut()
        }
    ];

    private void OnMicroInteractionPointerExited(object? sender, PointerEventArgs eventArgs) =>
        SetMicroInteractionScale(sender as Control, 1d, pressed: false);

    private void OnMicroInteractionPointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (eventArgs.GetCurrentPoint(owner).Properties.IsLeftButtonPressed)
        {
            SetMicroInteractionScale(sender as Control, MicroPressedScale, pressed: true);
        }
    }

    private void OnMicroInteractionPointerReleased(object? sender, PointerReleasedEventArgs eventArgs) =>
        SetMicroInteractionScale(sender as Control, 1d, pressed: false);

    private void SetMicroInteractionScale(Control? control, double scale, bool pressed)
    {
        if (IsFullMotionEnabled()
            && control is not null
            && microInteractionTransforms.TryGetValue(control, out var motion))
        {
            motion.Scale.Transitions = pressed ? motion.PressTransitions : motion.ReleaseTransitions;
            motion.Scale.ScaleX = scale;
            motion.Scale.ScaleY = scale;
        }
    }

    private void RestoreMicroInteractionTransforms()
    {
        foreach (var (control, motion) in microInteractionTransforms)
        {
            control.PointerExited -= OnMicroInteractionPointerExited;
            control.PointerPressed -= OnMicroInteractionPointerPressed;
            control.PointerReleased -= OnMicroInteractionPointerReleased;
            control.RenderTransform = motion.BaseTransform;
            control.RenderTransformOrigin = motion.BaseOrigin;
        }
        microInteractionTransforms.Clear();
    }

    private bool IsFullMotionEnabled() =>
        isFollowSystemMotion() && AreClientAreaAnimationsEnabled();

    private bool IsReducedMotionEnabled() =>
        isReducedMotion() && AreClientAreaAnimationsEnabled();

    private bool IsMotionEnabled() => IsFullMotionEnabled() || IsReducedMotionEnabled();

    private void CancelEntranceAnimations()
    {
        activeEntranceAnimations.Clear();
        completedEntranceAnimations.Clear();
        entranceAnimationFrameScheduled = false;
        foreach (var cancellation in entranceAnimationCancellations.Values)
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }
        entranceAnimationCancellations.Clear();
        foreach (var control in entranceAnimationTargets)
        {
            entranceAnimationGenerations[control] = Interlocked.Increment(ref entranceAnimationGeneration);
            control.Opacity = 1;
            if (entranceAnimationTransforms.TryGetValue(control, out var motion))
            {
                control.RenderTransform = motion.BaseTransform;
                control.RenderTransformOrigin = motion.BaseOrigin;
            }
        }
        entranceAnimationTransforms.Clear();
        RestoreMicroInteractionTransforms();
        motionBaseTransitions.Clear();
    }

    private void OnEntranceTargetPropertyChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.Property == Visual.IsVisibleProperty
            && sender is Control { IsVisible: true } control)
        {
            QueueEntranceAnimation(control);
        }
    }

    private void QueueEntranceAnimation(Control control)
    {
        StopEntranceAnimation(control);
        if (!IsMotionEnabled())
        {
            ResetEntranceVisual(control);
            control.Opacity = 1;
            return;
        }
        var generation = Interlocked.Increment(ref entranceAnimationGeneration);
        entranceAnimationGenerations[control] = generation;
        ResetEntranceVisual(control);
        if (IsReducedMotionEnabled())
        {
            // Prime the visual before the next render; page transitions stay opaque
            // and use transform motion, while dialogs and toasts may fade.
            control.Opacity = IsOpacityEntranceTarget(control)
                ? ReducedEntranceStartOpacity
                : 1;
        }
        else
        {
            PrepareEntranceVisual(control);
        }
        var cancellation = new CancellationTokenSource();
        entranceAnimationCancellations[control] = cancellation;
        if (control.IsEffectivelyVisible)
        {
            RegisterEntranceAnimation(control, generation, cancellation);
        }
        else
        {
            _ = RunEntranceAnimationAfterLayoutAsync(control, generation, cancellation);
        }
    }

    private void StopEntranceAnimation(Control control)
    {
        activeEntranceAnimations.Remove(control, out var active);
        active?.Cancellation.Cancel();
        if (entranceAnimationCancellations.Remove(control, out var cancellation))
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }
    }

    private void PrepareEntranceVisual(Control control)
    {
        var baseTransform = control.RenderTransform;
        var baseOrigin = control.RenderTransformOrigin;
        var motion = new EntranceMotion(
            baseTransform,
            baseOrigin,
            new TranslateTransform { Y = EntranceOffsetFor(control) });
        entranceAnimationTransforms[control] = motion;
        ApplyEntranceTransform(control, motion);
        control.Opacity = IsOpacityEntranceTarget(control)
            ? TransientEntranceStartOpacity
            : 1;
    }

    private void ResetEntranceVisual(Control control)
    {
        if (entranceAnimationTransforms.Remove(control, out var motion))
        {
            control.RenderTransform = motion.BaseTransform;
            control.RenderTransformOrigin = motion.BaseOrigin;
        }
    }

    private static void ApplyEntranceTransform(Control control, EntranceMotion motion)
    {
        var transforms = new TransformGroup();
        if (motion.BaseTransform is Transform transform)
        {
            transforms.Children.Add(transform);
        }
        transforms.Children.Add(motion.Translate);
        control.RenderTransform = transforms;
        control.RenderTransformOrigin = motion.BaseOrigin;
    }

    private async Task RunEntranceAnimationAfterLayoutAsync(
        Control control,
        long generation,
        CancellationTokenSource cancellation)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
            cancellation.Token.ThrowIfCancellationRequested();
            if (!control.IsEffectivelyVisible
                || !entranceAnimationGenerations.TryGetValue(control, out var latestGeneration)
                || latestGeneration != generation)
            {
                return;
            }

            RegisterEntranceAnimation(control, generation, cancellation);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (!activeEntranceAnimations.TryGetValue(control, out var active)
                || !ReferenceEquals(active.Cancellation, cancellation))
            {
                if (entranceAnimationCancellations.TryGetValue(control, out var current)
                    && ReferenceEquals(current, cancellation))
                {
                    entranceAnimationCancellations.Remove(control);
                }
                cancellation.Dispose();
            }
        }
    }

    private void RegisterEntranceAnimation(
        Control control,
        long generation,
        CancellationTokenSource cancellation)
    {
        if (cancellation.IsCancellationRequested
            || !IsMotionEnabled()
            || !control.IsEffectivelyVisible)
        {
            DisposeEntranceCancellation(control, cancellation);
            return;
        }

        var opacityOnly = IsReducedMotionEnabled();
        if (opacityOnly && !IsOpacityEntranceTarget(control))
        {
            control.Opacity = 1;
            DisposeEntranceCancellation(control, cancellation);
            return;
        }

        EntranceMotion? motion = null;
        if (!opacityOnly
            && !entranceAnimationTransforms.TryGetValue(control, out motion))
        {
            DisposeEntranceCancellation(control, cancellation);
            return;
        }

        activeEntranceAnimations[control] = new EntranceFrameState
        {
            Control = control,
            Generation = generation,
            Motion = motion,
            Cancellation = cancellation,
            OpacityOnly = opacityOnly,
            Delay = EntranceDelayFor(control),
            Duration = opacityOnly ? ReducedEntranceDuration : EntranceDurationFor(control)
        };
        EnsureEntranceAnimationTimer();
    }

    private void DisposeEntranceCancellation(Control control, CancellationTokenSource cancellation)
    {
        if (entranceAnimationCancellations.TryGetValue(control, out var current)
            && ReferenceEquals(current, cancellation))
        {
            entranceAnimationCancellations.Remove(control);
            cancellation.Dispose();
        }
    }

    private void EnsureEntranceAnimationTimer()
    {
        if (entranceAnimationFrameScheduled)
        {
            return;
        }

        entranceAnimationFrameScheduled = true;
        owner.RequestAnimationFrame(OnEntranceAnimationFrame);
    }

    private void OnEntranceAnimationFrame(TimeSpan _)
    {
        entranceAnimationFrameScheduled = false;
        if (!IsMotionEnabled())
        {
            CancelEntranceAnimations();
            return;
        }

        var fluentEasing = new CubicEaseOut();
        completedEntranceAnimations.Clear();
        foreach (var animation in activeEntranceAnimations.Values)
        {
            if (animation.Cancellation.IsCancellationRequested
                || !animation.Control.IsEffectivelyVisible
                || !entranceAnimationGenerations.TryGetValue(animation.Control, out var latestGeneration)
                || latestGeneration != animation.Generation)
            {
                completedEntranceAnimations.Add(animation);
                continue;
            }

            // Anchor the animation timeline to the first rendered frame instead
            // of the registration instant. When a page switch triggers a slow
            // first layout, the frame callback can arrive hundreds of
            // milliseconds after the page became visible; measuring from the
            // registration would mark the whole entrance as already complete
            // and the page would appear to jump instead of animating.
            if (!animation.HasStarted)
            {
                animation.StartedTimestamp = Stopwatch.GetTimestamp();
                animation.HasStarted = true;
            }

            var elapsed = Stopwatch.GetElapsedTime(animation.StartedTimestamp) - animation.Delay;
            if (elapsed < TimeSpan.Zero)
            {
                continue;
            }

            var progress = Math.Clamp(
                elapsed.TotalMilliseconds / animation.Duration.TotalMilliseconds,
                0d,
                1d);
            if (!animation.OpacityOnly && animation.Motion is not null)
            {
                animation.Motion.Translate.Y = IsPageEntranceTarget(animation.Control)
                    ? CalculatePageEntranceOffset(elapsed, fluentEasing)
                    : TransientEntranceOffset * (1d - fluentEasing.Ease(progress));
            }
            if (animation.OpacityOnly || IsOpacityEntranceTarget(animation.Control))
            {
                var opacityDuration = animation.OpacityOnly
                    ? ReducedEntranceDuration
                    : PageOpacityDuration;
                var opacityProgress = Math.Clamp(
                    elapsed.TotalMilliseconds / opacityDuration.TotalMilliseconds,
                    0d,
                    1d);
                var opacityStart = animation.OpacityOnly
                    ? ReducedEntranceStartOpacity
                    : TransientEntranceStartOpacity;
                animation.Control.Opacity = opacityStart
                    + ((1d - opacityStart) * fluentEasing.Ease(opacityProgress));
            }

            if (progress >= 1d)
            {
                completedEntranceAnimations.Add(animation);
            }
        }

        foreach (var animation in completedEntranceAnimations)
        {
            CompleteEntranceAnimation(animation);
        }
        completedEntranceAnimations.Clear();

        if (activeEntranceAnimations.Count == 0)
        {
            return;
        }

        entranceAnimationFrameScheduled = true;
        owner.RequestAnimationFrame(OnEntranceAnimationFrame);
    }

    private static double CalculatePageEntranceOffset(TimeSpan elapsed, CubicEaseOut fluentEasing)
    {
        var fluentProgress = Math.Clamp(
            elapsed.TotalMilliseconds / PageEntranceFluentDuration.TotalMilliseconds,
            0d,
            1d);
        var backProgress = Math.Clamp(
            elapsed.TotalMilliseconds / PageEntranceDuration.TotalMilliseconds,
            0d,
            1d);
        return PageEntranceFluentOffset * (1d - fluentEasing.Ease(fluentProgress))
            + PageEntranceBackOffset * (1d - EasePclWeakBack(backProgress));
    }

    internal static double EasePclWeakBack(double progress)
    {
        var value = Math.Clamp(progress, 0d, 1d);
        return 1d - Math.Pow(1d - value, 2d) * Math.Cos(1.5d * Math.PI * value);
    }

    private static bool IsPageEntranceTarget(Control control) =>
        control.Classes.Contains("cfp-page") || control.Classes.Contains("cfp-subpage");

    private static TimeSpan EntranceDurationFor(Control control) =>
        IsPageEntranceTarget(control) ? PageEntranceDuration : TransientEntranceDuration;

    private static double EntranceOffsetFor(Control control) =>
        IsPageEntranceTarget(control) ? PageEntranceOffset : TransientEntranceOffset;

    private void CompleteEntranceAnimation(EntranceFrameState animation)
    {
        if (!activeEntranceAnimations.Remove(animation.Control, out var current)
            || !ReferenceEquals(current, animation))
        {
            return;
        }

        ResetEntranceVisual(animation.Control);
        if (animation.Control.IsEffectivelyVisible
            && entranceAnimationGenerations.TryGetValue(animation.Control, out var latestGeneration)
            && latestGeneration == animation.Generation)
        {
            animation.Control.Opacity = 1;
        }
        DisposeEntranceCancellation(animation.Control, animation.Cancellation);
    }

    private TimeSpan EntranceDelayFor(Control control)
    {
        if (control.Classes.Contains("cfp-page"))
        {
            return TimeSpan.Zero;
        }
        if (control.Classes.Contains("cfp-subpage")
            || control.Classes.Contains("cfp-dialog-motion")
            || control.Classes.Contains("cfp-toast-motion"))
        {
            return TimeSpan.Zero;
        }
        return TimeSpan.FromMilliseconds(25);
    }

    private const uint SpiGetClientAreaAnimation = 0x1042;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(
        uint uiAction,
        uint uiParam,
        out int pvParam,
        uint fWinIni);
}

using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.ViewModels;
using Crystalfly.App.Views.Controls;
using Crystalfly.Core.Configuration;
using Ursa.Controls;

namespace Crystalfly.App.Services;

public sealed class MotionCoordinator
{
    private readonly Window owner;
    private readonly Func<bool> isFollowSystemMotion;
    private readonly Func<bool> isReducedMotion;
    private readonly Func<bool>? clientAreaAnimationsEnabled;
    private readonly List<TranslateTransform> knightWalkTransforms = [];
    private readonly List<LoadingContainer> knightLoadingHosts = [];
    private readonly HashSet<Control> entranceAnimationTargets = [];
    private readonly Dictionary<Control, long> entranceAnimationGenerations = [];
    private readonly Dictionary<Control, EntranceMotion> entranceAnimationTransforms = [];
    private readonly Dictionary<Control, CancellationTokenSource> entranceAnimationCancellations = [];
    private readonly Dictionary<Control, EntranceFrameState> activeEntranceAnimations = [];
    private readonly List<EntranceFrameState> completedEntranceAnimations = [];
    private readonly Dictionary<Control, (INotifyPropertyChanged Source, PropertyChangedEventHandler Handler)> downloadMotionSources = [];
    private readonly Dictionary<Control, string?> downloadMotionStageTexts = [];
    private readonly Dictionary<Control, Transitions?> motionBaseTransitions = [];
    private readonly Dictionary<Control, MicroMotion> microInteractionTransforms = [];
    private readonly Dictionary<DependencyGraphEdges, (Visual[] Ancestors, EventHandler<AvaloniaPropertyChangedEventArgs> Handler)> graphVisibilityObservers = [];
    private long entranceAnimationGeneration;
    private DispatcherTimer? knightWalkTimer;
    private bool entranceAnimationFrameScheduled;
    private bool isShutdown;
    private int knightWalkFrame;
    private IDisposable? loadedSubscription;
    private long rowBatchTimestamp;
    private int rowBatchCount;

    internal MotionCoordinator(
        Window owner,
        Func<bool> isFollowSystemMotion,
        Func<bool> isReducedMotion,
        Func<bool>? clientAreaAnimationsEnabled = null)
    {
        this.owner = owner;
        this.isFollowSystemMotion = isFollowSystemMotion;
        this.isReducedMotion = isReducedMotion;
        this.clientAreaAnimationsEnabled = clientAreaAnimationsEnabled;
    }

    internal void Start()
    {
        if (isShutdown) return;
        loadedSubscription ??= Control.LoadedEvent.AddClassHandler<Control>((control, _) =>
        {
            if (TopLevel.GetTopLevel(control) != owner) return;
            if (IsEntranceAnimationTarget(control)) RegisterMotionTarget(control, animate: true);
            ConfigureControlTransitions(control);
        });
        SubscribeEntranceAnimations();
        StartKnightWalkAnimation();
    }

    internal void RegisterMotionTargets(bool animateNewTargets)
    {
        foreach (var control in owner.GetVisualDescendants().OfType<Control>().Where(IsEntranceAnimationTarget))
        {
            RegisterMotionTarget(control, animateNewTargets);
        }
        // Static XAML surfaces only attach to the visual tree once their page
        // is shown (ScrollViewer templates apply lazily), so visual-descendant
        // scans miss subpages on pages that were never opened. Walk the logical
        // tree too so every static motion target is registered up front and
        // still animates when it first becomes visible.
        foreach (var control in EnumerateLogicalControls(owner).Where(IsEntranceAnimationTarget))
        {
            RegisterMotionTarget(control, animateNewTargets);
        }
        ConfigureMicroInteractionTransitions();
    }

    private static IEnumerable<Control> EnumerateLogicalControls(StyledElement root)
    {
        if (root is not ILogical logical)
        {
            yield break;
        }

        foreach (var child in logical.LogicalChildren)
        {
            if (child is StyledElement styledChild)
            {
                if (styledChild is Control control)
                {
                    yield return control;
                }

                foreach (var descendant in EnumerateLogicalControls(styledChild))
                {
                    yield return descendant;
                }
            }
        }
    }

    internal void RegisterMotionTarget(Control control, bool animate)
    {
        if (isShutdown || !entranceAnimationTargets.Add(control))
        {
            return;
        }
        control.PropertyChanged += OnEntranceTargetPropertyChanged;
        if (control is DependencyGraphEdges edges)
        {
            edges.AttachedToVisualTree += OnGraphEdgesAttached;
            ObserveGraphVisibility(edges);
        }
        if (control.Classes.Contains("cfp-download-motion"))
        {
            AttachDownloadMotionSource(control);
        }
        if (IsDynamicMotionTarget(control))
        {
            control.DetachedFromVisualTree += OnDynamicMotionTargetDetached;
        }
        if (animate && control.IsEffectivelyVisible
            && (IsOpacityEntranceTarget(control) || IsRowEntranceTarget(control)))
        {
            QueueEntranceAnimation(control);
        }
    }

    internal void UpdateMotionPreference(bool replayVisiblePages = false)
    {
        // A preference change must stop an already-running spatial animation too.
        CancelEntranceAnimations();
        UpdateGameAvatar(animate: false);
        UpdateTopNavigationIndicator(animate: false);
        ConfigureMicroInteractionTransitions();
        if (IsMotionEnabled() && replayVisiblePages)
        {
            foreach (var control in entranceAnimationTargets.Where(control =>
                         control.IsVisible && control.Classes.Contains("cfp-page")))
            {
                QueueEntranceAnimation(control);
            }
        }
        UpdateKnightWalkAnimationState();
    }

    internal void UpdateGameAvatar(bool animate)
    {
        if (owner.FindControl<Image>("KnightGameAvatar") is not { } knight
            || owner.FindControl<Image>("HornetGameAvatar") is not { } hornet)
            return;
        var silksong = owner.DataContext is MainViewModel { IsSilksong: true };
        var enabled = animate && owner.IsVisible
            && owner.DataContext is MainViewModel { IsLaunchPage: true } && IsFullMotionEnabled();
        foreach (var (image, active) in new[] { (knight, !silksong), (hornet, silksong) })
        {
            image.Transitions = enabled
                ? new Transitions { new DoubleTransition { Property = Visual.OpacityProperty,
                    Duration = TimeSpan.FromMilliseconds(180), Easing = new CubicEaseOut() } }
                : null;
            if (image.RenderTransform is ScaleTransform scale)
            {
                scale.Transitions = enabled ? CreateScaleTransitions(TimeSpan.FromMilliseconds(180)) : null;
                scale.ScaleX = scale.ScaleY = active || !enabled ? 1 : 0.94;
            }
            image.Opacity = active ? 1 : 0;
        }
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
                    Duration = TimeSpan.FromMilliseconds(280),
                    Easing = new CubicEaseOut()
                }
            }
            : null;
        transform.X = target;
    }

    internal void UpdateTopNavigationIndicator(bool animate)
    {
        if (owner.FindControl<Border>("TopNavigationIndicator") is not { RenderTransform: TranslateTransform transform } indicator
            || owner.FindControl<StackPanel>("TopNavigationButtons") is not { } buttons) return;
        var selected = buttons.Children.OfType<Button>().FirstOrDefault(button => button.Classes.Contains("active"));
        var enabled = animate && indicator.IsVisible && owner.IsVisible && IsFullMotionEnabled();
        transform.Transitions = enabled ? new Transitions { new DoubleTransition
        {
            Property = TranslateTransform.XProperty, Duration = TimeSpan.FromMilliseconds(280), Easing = new CubicEaseOut()
        } } : null;
        indicator.Transitions = enabled ? new Transitions { new DoubleTransition
        {
            Property = Control.WidthProperty, Duration = TimeSpan.FromMilliseconds(280), Easing = new CubicEaseOut()
        } } : null;
        if (selected is null || selected.Bounds.Width <= 0)
        {
            indicator.IsVisible = false;
            return;
        }
        indicator.Width = selected.Bounds.Width;
        transform.X = selected.TranslatePoint(default, buttons)?.X ?? 0;
        indicator.IsVisible = true;
    }

    internal void Shutdown()
    {
        isShutdown = true;
        loadedSubscription?.Dispose();
        loadedSubscription = null;
        UpdateTopNavigationIndicator(animate: false);
        owner.PropertyChanged -= OnWindowPropertyChanged;
        foreach (var target in entranceAnimationTargets.ToArray())
        {
            UnregisterMotionTarget(target);
        }
        RestoreMicroInteractionTransforms();
        foreach (var control in motionBaseTransitions.Keys.ToArray()) RestoreControlTransitions(control);
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
        if (knightWalkTimer is not null) return;
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
        if (clientAreaAnimationsEnabled is not null)
        {
            return clientAreaAnimationsEnabled();
        }

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

    private static readonly TimeSpan PageEntranceDuration = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan DialogEntranceDuration = TimeSpan.FromMilliseconds(440);
    private static readonly TimeSpan GraphEntranceDuration = TimeSpan.FromMilliseconds(650);
    private static readonly TimeSpan PageOpacityDuration = TimeSpan.FromMilliseconds(180);
    private static readonly TimeSpan TransientEntranceDuration = TimeSpan.FromMilliseconds(280);
    private static readonly TimeSpan ReducedEntranceDuration = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan MicroOpacityDuration = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan MicroPressDuration = TimeSpan.FromMilliseconds(80);
    private static readonly TimeSpan MicroReleaseDuration = TimeSpan.FromMilliseconds(320);
    private const double PageEntranceOffset = 12d;
    private const double TransientEntranceOffset = 8d;
    private const double TransientEntranceStartOpacity = 0.82d;
    private const double ReducedEntranceStartOpacity = 0.9d;
    private const double MicroPressedScale = 0.96d;

    private sealed record EntranceMotion(
        ITransform? BaseTransform,
        RelativePoint BaseOrigin,
        TranslateTransform Translate,
        ScaleTransform? Scale);

    private sealed class SoftReturnEasing : Easing
    {
        public override double Ease(double progress) => 1 - SoftEntranceOffset(progress) / PageEntranceOffset;
    }

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
        control is DependencyGraphEdges
        || control.Classes.Contains("cfp-state-motion")
        || control.Classes.Contains("cfp-dialog-motion")
        || control.Classes.Contains("cfp-toast-motion")
        || control.Classes.Contains("cfp-market-subpage")
        || control.Classes.Contains("cfp-market-cover")
        || control.Classes.Contains("cfp-download-motion");

    private static bool IsProductMotionOpacityTarget(Control control) =>
        control.Classes.Contains("cfp-market-subpage")
        || control.Classes.Contains("cfp-market-cover")
        || control.Classes.Contains("cfp-download-motion");

    private static bool IsEntranceAnimationTarget(Control control) =>
        control is DependencyGraphEdges
        || IsRowEntranceTarget(control)
        || control.Classes.Contains("cfp-state-motion")
        || control.Classes.Contains("cfp-subpage")
        || control.Classes.Contains("cfp-mod-bulk-bar")
        || control.Classes.Contains("cfp-page")
        || control.Classes.Contains("cfp-dialog-motion")
        || control.Classes.Contains("cfp-toast-motion")
        || control.Classes.Contains("cfp-market-subpage")
        || control.Classes.Contains("cfp-market-cover")
        || control.Classes.Contains("cfp-download-motion");

    private static bool IsDynamicMotionTarget(Control control) =>
        control is DependencyGraphEdges
        || IsRowEntranceTarget(control)
        || control.Classes.Contains("cfp-dialog-motion")
        || control.Classes.Contains("cfp-toast-motion")
        || control.Classes.Contains("cfp-market-cover")
        || control.Classes.Contains("cfp-download-motion");

    private static bool IsRowEntranceTarget(Control control) =>
        control is ListBoxItem
        || control.Classes.Contains("cfp-motion-item")
        || control.Classes.Contains("cfp-instance-surface")
        || control.Classes.Contains("cfp-directory-surface")
        || control.Classes.Contains("cfp-installed-mod-row")
        || control.Classes.Contains("cfp-speedrun-activity-row");

    private static bool IsTransformEntranceTarget(Control control) =>
        control is not DependencyGraphEdges
        && !control.Classes.Contains("cfp-dependency-node")
        && !control.Classes.Contains("cfp-market-cover")
        && !control.Classes.Contains("cfp-download-motion");

    private void ConfigureMicroInteractionTransitions()
    {
        foreach (var control in owner.GetVisualDescendants().OfType<Control>())
        {
            ConfigureControlTransitions(control);
        }
        if (!IsFullMotionEnabled())
        {
            RestoreMicroInteractionTransforms();
        }
    }

    private void ConfigureControlTransitions(Control control)
    {
        if (isShutdown || !(control is Button or ListBoxItem or TextBox or ComboBox
            || control.Classes.Contains("cfp-instance-action-frame")
            || control.Classes.Contains("cfp-installed-mod-actions")
            || control.Classes.Contains("cfp-installed-mod-accent")
            || control.Classes.Contains("cfp-graph-selection-viewport")
            || IsRowEntranceTarget(control))) return;

        if (!IsMotionEnabled() || control.Classes.Contains("cfp-graph-selection-viewport") && !IsFullMotionEnabled())
        {
            RestoreControlTransitions(control);
            return;
        }
        if (!motionBaseTransitions.ContainsKey(control))
        {
            motionBaseTransitions.Add(control, control.Transitions);
            control.DetachedFromVisualTree += OnFeedbackTargetDetached;
            var transitions = new Transitions();
            if (control.Transitions is { } existing)
                foreach (var transition in existing) transitions.Add(transition);
            transitions.Add(new DoubleTransition
            {
                Property = Visual.OpacityProperty, Duration = MicroOpacityDuration, Easing = new CubicEaseOut()
            });
            foreach (var property in control switch
            {
                TemplatedControl => new[] { TemplatedControl.BackgroundProperty, TemplatedControl.BorderBrushProperty },
                Border => new[] { Border.BackgroundProperty, Border.BorderBrushProperty },
                Panel => new[] { Panel.BackgroundProperty },
                _ => Array.Empty<StyledProperty<IBrush?>>()
            })
                transitions.Add(new BrushTransition
                {
                    Property = property, Duration = TimeSpan.FromMilliseconds(160), Easing = new CubicEaseOut()
                });
            if (control.Classes.Contains("cfp-graph-selection-viewport"))
                transitions.Add(new ThicknessTransition
                {
                    Property = Control.MarginProperty, Duration = TimeSpan.FromMilliseconds(280), Easing = new CubicEaseOut()
                });
            control.Transitions = transitions;
        }
        if (IsMicroInteractionTarget(control)) ConfigureMicroInteractionTransform(control);
    }

    private void RestoreControlTransitions(Control control)
    {
        if (motionBaseTransitions.Remove(control, out var original)) control.Transitions = original;
        control.DetachedFromVisualTree -= OnFeedbackTargetDetached;
        RestoreMicroInteractionTransform(control);
    }

    private void OnFeedbackTargetDetached(object? sender, VisualTreeAttachmentEventArgs args)
    {
        if (sender is Control control) RestoreControlTransitions(control);
    }

    private static bool IsMicroInteractionTarget(Control control) => control is Button and not ToggleButton
        && !control.Classes.Contains("cfp-dependency-node")
        && !control.Classes.Contains("cfp-window-button");

    private void ConfigureMicroInteractionTransform(Control control)
    {
        if (!IsFullMotionEnabled()
            || microInteractionTransforms.ContainsKey(control)
            || control.RenderTransform is { Value.IsIdentity: false })
        {
            return;
        }
        var scale = new ScaleTransform { ScaleX = 1, ScaleY = 1 };
        var pressTransitions = CreateScaleTransitions(MicroPressDuration);
        var releaseTransitions = CreateScaleTransitions(MicroReleaseDuration, new SoftReturnEasing());
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
        control.PropertyChanged += OnMicroInteractionPropertyChanged;
    }

    private static Transitions CreateScaleTransitions(TimeSpan duration, Easing? easing = null) =>
    [
        new DoubleTransition
        {
            Property = ScaleTransform.ScaleXProperty,
            Duration = duration,
            Easing = easing ?? new CubicEaseOut()
        },
        new DoubleTransition
        {
            Property = ScaleTransform.ScaleYProperty,
            Duration = duration,
            Easing = easing ?? new CubicEaseOut()
        }
    ];

    private void OnMicroInteractionPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (sender is Button button && args.Property == Button.IsPressedProperty)
        {
            var pressed = button.IsPressed && button.IsEffectivelyEnabled;
            // Large row buttons should not shrink their text like small icon buttons.
            var scale = button.Bounds.Width > 160 ? 0.99d : MicroPressedScale;
            SetMicroInteractionScale(button, pressed ? scale : 1d, pressed);
        }
    }

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
        foreach (var control in microInteractionTransforms.Keys.ToArray()) RestoreMicroInteractionTransform(control);
    }

    private void RestoreMicroInteractionTransform(Control control)
    {
        if (microInteractionTransforms.Remove(control, out var motion))
        {
            control.PropertyChanged -= OnMicroInteractionPropertyChanged;
            control.RenderTransform = motion.BaseTransform;
            control.RenderTransformOrigin = motion.BaseOrigin;
        }
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
        // RequestAnimationFrame cannot be withdrawn; keep its slot until that callback runs.
        foreach (var cancellation in entranceAnimationCancellations.Values)
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }
        entranceAnimationCancellations.Clear();
        foreach (var control in entranceAnimationTargets)
        {
            entranceAnimationGenerations[control] = Interlocked.Increment(ref entranceAnimationGeneration);
            RestoreEntranceOpacity(control);
            ResetEntranceVisual(control);
        }
        entranceAnimationTransforms.Clear();
        RestoreMicroInteractionTransforms();
    }

    private void AttachDownloadMotionSource(Control control)
    {
        if (downloadMotionSources.Remove(control, out var previous))
        {
            previous.Source.PropertyChanged -= previous.Handler;
        }

        var stage = DownloadStageText(control);
        if (control.DataContext is not INotifyPropertyChanged source)
        {
            downloadMotionStageTexts.Remove(control);
            return;
        }

        downloadMotionStageTexts[control] = stage;
        PropertyChangedEventHandler handler = (_, args) =>
        {
            if (args.PropertyName != nameof(DownloadQueueGroupItemViewModel.StageText))
            {
                return;
            }

            if (Dispatcher.UIThread.CheckAccess())
            {
                UpdateDownloadMotionStage(control, source);
            }
            else
            {
                Dispatcher.UIThread.Post(
                    () => UpdateDownloadMotionStage(control, source),
                    DispatcherPriority.Render);
            }
        };
        source.PropertyChanged += handler;
        downloadMotionSources[control] = (source, handler);
    }

    private void UpdateDownloadMotionStage(Control control, INotifyPropertyChanged source)
    {
        if (!entranceAnimationTargets.Contains(control)
            || !downloadMotionSources.TryGetValue(control, out var current)
            || !ReferenceEquals(current.Source, source))
        {
            return;
        }

        var next = DownloadStageText(control);
        if (string.Equals(downloadMotionStageTexts.GetValueOrDefault(control), next, StringComparison.Ordinal))
        {
            return;
        }

        downloadMotionStageTexts[control] = next;
        if (control.IsEffectivelyVisible)
        {
            QueueEntranceAnimation(control);
        }
    }

    private static string? DownloadStageText(Control control) => control.DataContext switch
    {
        DownloadQueueGroupItemViewModel group => group.StageText,
        DownloadQueueItemViewModel item => item.StageText,
        _ => null
    };

    private void UnregisterMotionTarget(Control control)
    {
        if (!entranceAnimationTargets.Remove(control))
        {
            return;
        }

        control.PropertyChanged -= OnEntranceTargetPropertyChanged;
        control.DetachedFromVisualTree -= OnDynamicMotionTargetDetached;
        if (control is DependencyGraphEdges edges)
        {
            edges.AttachedToVisualTree -= OnGraphEdgesAttached;
            if (graphVisibilityObservers.Remove(edges, out var observer))
                foreach (var ancestor in observer.Ancestors) ancestor.PropertyChanged -= observer.Handler;
        }
        StopEntranceAnimation(control);
        ResetEntranceVisual(control);
        RestoreEntranceOpacity(control);
        entranceAnimationGenerations.Remove(control);
        completedEntranceAnimations.RemoveAll(animation => ReferenceEquals(animation.Control, control));
        if (downloadMotionSources.Remove(control, out var source))
        {
            source.Source.PropertyChanged -= source.Handler;
        }
        downloadMotionStageTexts.Remove(control);
    }

    private void OnDynamicMotionTargetDetached(object? sender, VisualTreeAttachmentEventArgs eventArgs)
    {
        if (sender is not Control control)
        {
            return;
        }

        UnregisterMotionTarget(control);
        var coordinator = new WeakReference<MotionCoordinator>(this);
        EventHandler<VisualTreeAttachmentEventArgs> reattachHandler = null!;
        reattachHandler = (attachedSender, _) =>
        {
            if (attachedSender is Control attached)
            {
                attached.AttachedToVisualTree -= reattachHandler;
                if (coordinator.TryGetTarget(out var activeCoordinator) && !activeCoordinator.isShutdown)
                {
                    activeCoordinator.RegisterMotionTarget(attached, animate: !IsRowEntranceTarget(attached));
                }
            }
        };
        control.AttachedToVisualTree += reattachHandler;
    }

    private void OnGraphEdgesAttached(object? sender, VisualTreeAttachmentEventArgs args)
    {
        if (sender is DependencyGraphEdges edges) ObserveGraphVisibility(edges);
    }

    private void ObserveGraphVisibility(DependencyGraphEdges edges)
    {
        if (graphVisibilityObservers.ContainsKey(edges) || TopLevel.GetTopLevel(edges) is null) return;
        var ancestors = edges.GetVisualAncestors().ToArray();
        EventHandler<AvaloniaPropertyChangedEventArgs> handler = (_, args) =>
        {
            if (args.Property != Visual.IsVisibleProperty) return;
            if (edges.IsEffectivelyVisible) QueueEntranceAnimation(edges);
            else
            {
                StopEntranceAnimation(edges);
                ResetEntranceVisual(edges);
                RestoreEntranceOpacity(edges);
            }
        };
        graphVisibilityObservers.Add(edges, (ancestors, handler));
        foreach (var ancestor in ancestors) ancestor.PropertyChanged += handler;
        if (edges.IsEffectivelyVisible) QueueEntranceAnimation(edges);
    }

    private void OnEntranceTargetPropertyChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs eventArgs)
    {
        if (sender is not Control control)
        {
            return;
        }

        if ((eventArgs.Property == TextBlock.TextProperty && control.Classes.Contains("cfp-state-motion")
             || eventArgs.Property == DependencyGraphEdges.EdgesProperty)
            && control.IsEffectivelyVisible)
        {
            QueueEntranceAnimation(control);
        }
        else if (eventArgs.Property == StyledElement.DataContextProperty && IsRowEntranceTarget(control))
        {
            // A virtualized row may now represent another item; never carry its old motion over.
            StopEntranceAnimation(control);
            ResetEntranceVisual(control);
        }
        else if (eventArgs.Property == StyledElement.DataContextProperty
            && control.Classes.Contains("cfp-download-motion"))
        {
            AttachDownloadMotionSource(control);
        }
        else if (eventArgs.Property == Image.SourceProperty
            && control.Classes.Contains("cfp-market-cover"))
        {
            if (control is Image { Source: not null } && control.IsEffectivelyVisible)
            {
                QueueEntranceAnimation(control);
            }
            else
            {
                StopEntranceAnimation(control);
                ResetEntranceVisual(control);
                RestoreEntranceOpacity(control);
            }
        }
        else if (eventArgs.Property == Visual.IsVisibleProperty)
        {
            if (control.IsVisible && IsEntranceAnimationTarget(control))
            {
                QueueEntranceAnimation(control);
                // Static targets are registered at startup; Loaded handles newly materialized controls.
            }
            else if (!control.IsVisible)
            {
                StopEntranceAnimation(control);
                ResetEntranceVisual(control);
                RestoreEntranceOpacity(control);
                foreach (var hidden in entranceAnimationCancellations.Keys.Where(target => !target.IsEffectivelyVisible).ToArray())
                {
                    StopEntranceAnimation(hidden);
                    ResetEntranceVisual(hidden);
                    RestoreEntranceOpacity(hidden);
                }
            }
        }
    }

    private void QueueEntranceAnimation(Control control)
    {
        StopEntranceAnimation(control);
        ResetEntranceVisual(control);
        if (isShutdown
            || !IsMotionEnabled()
            || control is not DependencyGraphEdges
                && control.GetVisualAncestors().OfType<Control>().Any(activeEntranceAnimations.ContainsKey)
            || control is DependencyGraphEdges { Edges: null or { Count: 0 } }
            || control.Classes.Contains("cfp-market-cover") && control is not Image { Source: not null })
        {
            ResetEntranceVisual(control);
            RestoreEntranceOpacity(control);
            return;
        }
        var delay = TimeSpan.Zero;
        if (IsRowEntranceTarget(control))
        {
            if (Stopwatch.GetElapsedTime(rowBatchTimestamp).TotalMilliseconds > 100)
            {
                rowBatchTimestamp = Stopwatch.GetTimestamp();
                rowBatchCount = 0;
            }
            // Keep cached rows visible and cap work for large lists at six entrances per burst.
            if (rowBatchCount >= 6) return;
            delay = TimeSpan.FromMilliseconds(rowBatchCount++ * 24);
        }
        foreach (var child in activeEntranceAnimations.Keys
                     .Where(target => target is not DependencyGraphEdges && target.GetVisualAncestors().Contains(control)).ToArray())
        {
            StopEntranceAnimation(child);
            ResetEntranceVisual(child);
            RestoreEntranceOpacity(child);
        }
        var generation = Interlocked.Increment(ref entranceAnimationGeneration);
        entranceAnimationGenerations[control] = generation;
        ResetEntranceVisual(control);
        if (IsReducedMotionEnabled())
        {
            // Prime the visual before the next render; page transitions stay opaque
            // and use transform motion, while dialogs and toasts may fade.
            if (!IsRowEntranceTarget(control))
                control.Opacity = IsOpacityEntranceTarget(control) ? ReducedEntranceStartOpacity : 1;
        }
        else
        {
            PrepareEntranceVisual(control);
        }
        var cancellation = new CancellationTokenSource();
        entranceAnimationCancellations[control] = cancellation;
        if (control.IsEffectivelyVisible)
        {
            RegisterEntranceAnimation(control, generation, cancellation, delay);
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
        if (control is DependencyGraphEdges edges) edges.RevealProgress = 0;
        if (!IsTransformEntranceTarget(control))
        {
            if (!IsRowEntranceTarget(control))
                control.Opacity = IsOpacityEntranceTarget(control) ? TransientEntranceStartOpacity : 1;
            return;
        }

        var baseTransform = control.RenderTransform;
        var baseOrigin = control.RenderTransformOrigin;
        var motion = new EntranceMotion(
            baseTransform,
            baseOrigin,
            new TranslateTransform { Y = EntranceOffsetFor(control) },
            control.Classes.Contains("cfp-dialog-motion") ? new ScaleTransform(0.975, 0.975) : null);
        entranceAnimationTransforms[control] = motion;
        ApplyEntranceTransform(control, motion);
        if (!IsRowEntranceTarget(control))
            control.Opacity = IsOpacityEntranceTarget(control) ? TransientEntranceStartOpacity : 1;
    }

    private static void RestoreEntranceOpacity(Control control)
    {
        // Disabled/dimmed rows own their opacity through styles, not entrance animation.
        if (!IsRowEntranceTarget(control)) control.Opacity = 1;
    }

    private void ResetEntranceVisual(Control control)
    {
        if (control is DependencyGraphEdges edges) edges.RevealProgress = 1;
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
        if (motion.Scale is not null) transforms.Children.Add(motion.Scale);
        control.RenderTransform = transforms;
        control.RenderTransformOrigin = motion.Scale is null ? motion.BaseOrigin : RelativePoint.Center;
    }

    private async Task RunEntranceAnimationAfterLayoutAsync(
        Control control,
        long generation,
        CancellationTokenSource cancellation)
    {
        var token = cancellation.Token;
        try
        {
            await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
            token.ThrowIfCancellationRequested();
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
                    ResetEntranceVisual(control);
                    RestoreEntranceOpacity(control);
                }
                cancellation.Dispose();
            }
        }
    }

    private void RegisterEntranceAnimation(
        Control control,
        long generation,
        CancellationTokenSource cancellation,
        TimeSpan delay = default)
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
            RestoreEntranceOpacity(control);
            DisposeEntranceCancellation(control, cancellation);
            return;
        }

        EntranceMotion? motion = null;
        if (!opacityOnly
            && IsTransformEntranceTarget(control)
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
            Delay = delay,
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
        if (isShutdown || !IsMotionEnabled())
        {
            CancelEntranceAnimations();
            return;
        }

        var fluentEasing = new CubicEaseOut();
        var timestamp = Stopwatch.GetTimestamp();
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
                animation.StartedTimestamp = timestamp;
                animation.HasStarted = true;
            }

            var elapsed = Stopwatch.GetElapsedTime(animation.StartedTimestamp, timestamp) - animation.Delay;
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
                animation.Motion.Translate.Y = EntranceOffsetFor(animation.Control)
                    * SoftEntranceOffset(progress) / PageEntranceOffset;
                if (animation.Motion.Scale is { } scale)
                    scale.ScaleX = scale.ScaleY = 1 - 0.025 * SoftEntranceOffset(progress) / PageEntranceOffset;
            }
            if (!animation.OpacityOnly && animation.Control is DependencyGraphEdges edges)
                edges.RevealProgress = progress * progress * (3 - 2 * progress);
            if (animation.OpacityOnly || IsOpacityEntranceTarget(animation.Control))
            {
                var opacityDuration = animation.OpacityOnly
                    ? ReducedEntranceDuration
                    : IsProductMotionOpacityTarget(animation.Control)
                        ? animation.Duration
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

        EnsureEntranceAnimationTimer();
    }

    internal static double SoftEntranceOffset(double progress)
    {
        var value = Math.Clamp(progress, 0d, 1d);
        // 12 -> -1 -> 0, with a stationary join and exact final layout position.
        if (value <= 0.76)
        {
            var travel = value / 0.76;
            return 12 - 13 * (travel + travel * travel - travel * travel * travel);
        }
        var settle = (value - 0.76) / 0.24;
        return -1 + settle * settle * (3 - 2 * settle);
    }

    private static bool IsPageEntranceTarget(Control control) =>
        control.Classes.Contains("cfp-page") || control.Classes.Contains("cfp-subpage")
        || control.Classes.Contains("cfp-market-subpage");

    private static TimeSpan EntranceDurationFor(Control control) =>
        control is DependencyGraphEdges ? GraphEntranceDuration
            : IsPageEntranceTarget(control) ? PageEntranceDuration
            : control.Classes.Contains("cfp-dialog-motion") ? DialogEntranceDuration : TransientEntranceDuration;

    private static double EntranceOffsetFor(Control control) =>
        IsRowEntranceTarget(control) ? 4d : IsPageEntranceTarget(control) ? PageEntranceOffset : TransientEntranceOffset;

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
            RestoreEntranceOpacity(animation.Control);
        }
        DisposeEntranceCancellation(animation.Control, animation.Cancellation);
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

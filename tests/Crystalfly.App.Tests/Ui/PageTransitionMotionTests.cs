using System.Collections;
using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.Downloads;
using Crystalfly.App.Services;
using Crystalfly.App.ViewModels;
using Crystalfly.App.Views;

namespace Crystalfly.App.Tests.Ui;

public sealed class PageTransitionMotionTests : IDisposable
{
    private readonly Func<bool>? previousAnimationOverride = MainWindow.ClientAreaAnimationsOverride;

    // Headless Windows runners may disable system animations.
    public PageTransitionMotionTests() => MainWindow.ClientAreaAnimationsOverride = () => true;

    public void Dispose() => MainWindow.ClientAreaAnimationsOverride = previousAnimationOverride;

    [AvaloniaTheory]
    [InlineData(900, Crystalfly.Core.Configuration.UiLanguage.SimplifiedChinese, false)]
    [InlineData(1100, Crystalfly.Core.Configuration.UiLanguage.English, true)]
    public void Top_navigation_indicator_slides_between_real_button_bounds_and_respects_motion_settings(
        int width, Crystalfly.Core.Configuration.UiLanguage language, bool dark)
    {
        var (window, viewModel, _) = CreateWindow();
        try
        {
            window.Width = width;
            window.RequestedThemeVariant = dark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
            viewModel.Loc.Apply(language);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var indicator = window.FindControl<Border>("TopNavigationIndicator");
            Assert.NotNull(indicator);
            var buttons = window.FindControl<StackPanel>("TopNavigationButtons")!;
            var transform = Assert.IsType<TranslateTransform>(indicator.RenderTransform);
            foreach (var page in new[] { "Settings", "Speedrun", "Downloads", "Launch", "Settings" })
            {
                viewModel.CurrentPage = page;
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                Assert.True(indicator.IsVisible);
                var selected = buttons.Children.OfType<Button>().Single(button => button.CommandParameter?.ToString() == page);
                Assert.Equal(selected.Bounds.Width, indicator.GetBaseValue(Control.WidthProperty).Value, 2);
                Assert.Equal(selected.TranslatePoint(default, buttons)!.Value.X, transform.GetBaseValue(TranslateTransform.XProperty).Value, 2);
                Assert.NotNull(transform.Transitions);
                var transition = Assert.IsType<Avalonia.Animation.DoubleTransition>(Assert.Single(transform.Transitions));
                Assert.Equal(TranslateTransform.XProperty, transition.Property);
                Assert.Equal(TimeSpan.FromMilliseconds(220), transition.Duration);
                Assert.False(indicator.IsHitTestVisible);
            }
            foreach (var preference in new[] { Crystalfly.Core.Configuration.UiMotionPreference.Reduced, Crystalfly.Core.Configuration.UiMotionPreference.Off })
            {
                viewModel.SelectedMotionPreference = new(preference, preference.ToString());
                viewModel.CurrentPage = "Launch";
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                Assert.Null(transform.Transitions);
                var selected = buttons.Children.OfType<Button>().Single(button => button.CommandParameter?.ToString() == "Launch");
                Assert.Equal(selected.TranslatePoint(default, buttons)!.Value.X, transform.X, 2);
            }
            viewModel.CurrentPage = "Manage";
            Dispatcher.UIThread.RunJobs();
            Assert.False(indicator.IsVisible);
            viewModel.CurrentPage = "Versions";
            Dispatcher.UIThread.RunJobs();
            Assert.False(indicator.IsVisible);
            viewModel.CurrentPage = "Settings";
            viewModel.ErrorMessage = language == Crystalfly.Core.Configuration.UiLanguage.English ? "Example notification" : "示例通知";
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            Dispatcher.UIThread.RunJobs();
            var settings = buttons.Children.OfType<Button>().Single(button => button.CommandParameter?.ToString() == "Settings");
            Assert.Equal(settings.Bounds.Width, indicator.Bounds.Width, 2);
            var center = settings.TranslatePoint(new Point(settings.Bounds.Width / 2, settings.Bounds.Height / 2), window)!.Value;
            var hit = window.InputHitTest(center) as Control;
            Assert.True(hit == settings || hit?.GetVisualAncestors().Contains(settings) == true,
                $"Navigation center {center} hit {hit?.GetType().Name}; enabled={settings.IsEffectivelyEnabled}, busy={viewModel.IsBusy}.");
            if (Environment.GetEnvironmentVariable("CRYSTALFLY_NAV_SCREENSHOTS") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                using var frame = window.GetLastRenderedFrame();
                Assert.NotNull(frame);
                frame.Save(Path.Combine(output, $"navigation-{width}-{language}-{(dark ? "dark" : "light")}.png"), new PngBitmapEncoderOptions());
            }
        }
        finally
        {
            CloseImmediately(window);
            CleanupRoot(viewModel);
        }
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Page_switch_respects_the_system_animation_policy(bool animationsEnabled)
    {
        MainWindow.ClientAreaAnimationsOverride = () => animationsEnabled;
        var (window, viewModel, coordinator) = CreateWindow();

        try
        {
            viewModel.CurrentPage = "Downloads";
            var downloads = window.GetVisualDescendants()
                .OfType<Grid>()
                .Single(grid => grid.IsVisible && grid.Classes.Contains("cfp-page"));

            Assert.Equal(animationsEnabled, HasActiveAnimation(coordinator, downloads));
            if (animationsEnabled)
            {
                var transform = Assert.IsType<TranslateTransform>(FindTranslate(downloads.RenderTransform));
                Assert.InRange(transform.Y, -17.5d, -14.5d);
            }
            else
            {
                Assert.Null(FindTranslate(downloads.RenderTransform));
                Assert.Equal(1, downloads.Opacity);
            }
        }
        finally
        {
            CloseImmediately(window);
            CleanupRoot(viewModel);
        }
    }

    [AvaloniaTheory]
    [InlineData(true, false, 180, true)]
    [InlineData(false, true, 120, false)]
    [InlineData(false, false, 0, false)]
    public void Market_pane_transition_obeys_motion_preference_and_cancels_on_fast_return(
        bool fullMotion,
        bool reducedMotion,
        int expectedDuration,
        bool expectTransform)
    {
        var catalog = new Border { Classes = { "cfp-market-subpage" } };
        var detail = new Border { Classes = { "cfp-market-subpage" }, IsVisible = false };
        var window = new Window { Content = new StackPanel { Children = { catalog, detail } } };
        NameScope.SetNameScope(window, new NameScope());
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var coordinator = new MotionCoordinator(window, () => fullMotion, () => reducedMotion, () => true);
        coordinator.Start();

        try
        {
            catalog.IsVisible = false;
            detail.IsVisible = true;
            Assert.Equal(1, catalog.Opacity);
            if (expectedDuration == 0)
            {
                Assert.Equal(1, detail.Opacity);
                Assert.False(HasActiveAnimation(coordinator, detail));
                Assert.Null(FindTranslate(detail.RenderTransform));
            }
            else
            {
                var first = GetActiveState(coordinator, detail);
                Assert.Equal(TimeSpan.FromMilliseconds(expectedDuration), (TimeSpan)first.GetType().GetProperty("Duration")!.GetValue(first)!);
                Assert.Equal(expectTransform, FindTranslate(detail.RenderTransform) is not null);
                Assert.True(detail.Opacity < 1);
            }

            detail.IsVisible = false;
            catalog.IsVisible = true;
            Assert.Equal(1, detail.Opacity);
            Assert.Null(FindTranslate(detail.RenderTransform));
            if (expectedDuration == 0)
            {
                Assert.False(HasActiveAnimation(coordinator, catalog));
            }
            else
            {
                var returned = GetActiveState(coordinator, catalog);
                Assert.Equal(TimeSpan.FromMilliseconds(expectedDuration), (TimeSpan)returned.GetType().GetProperty("Duration")!.GetValue(returned)!);
            }
        }
        finally
        {
            coordinator.Shutdown();
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Download_stage_feedback_ignores_progress_updates_with_the_same_stage()
    {
        var localization = new LocalizationViewModel();
        var group = new DownloadQueueGroup { Id = "group", Name = "Mod", State = DownloadQueueGroupState.Running, Stage = "Downloading" };
        var viewModel = new DownloadQueueGroupItemViewModel(group, localization);
        var status = new TextBlock { Classes = { "cfp-download-motion" }, DataContext = viewModel };
        var (window, coordinator) = CreateMotionWindow(status, fullMotion: true, reducedMotion: false);

        try
        {
            viewModel.Update(group with { CompletedBytes = 1 }, localization);
            Assert.False(HasActiveAnimation(coordinator, status));

            viewModel.Update(group with { Stage = "Installing", CompletedBytes = 2 }, localization);
            Assert.True(HasActiveAnimation(coordinator, status));
            Assert.True(status.Opacity < 1);
            Assert.Null(FindTranslate(status.RenderTransform));
        }
        finally
        {
            coordinator.Shutdown();
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Market_cover_only_fades_after_a_source_arrives_and_clear_resets_it()
    {
        var cover = new Image { Classes = { "cfp-market-cover" } };
        var (window, coordinator) = CreateMotionWindow(cover, fullMotion: true, reducedMotion: false);

        try
        {
            Assert.False(HasActiveAnimation(coordinator, cover));
            using var bitmap = new WriteableBitmap(
                new PixelSize(1, 1), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            cover.Source = bitmap;
            Assert.True(HasActiveAnimation(coordinator, cover));
            Assert.True(cover.Opacity < 1);
            Assert.Null(FindTranslate(cover.RenderTransform));

            cover.Source = null;
            Assert.False(HasActiveAnimation(coordinator, cover));
            Assert.Equal(1, cover.Opacity);
        }
        finally
        {
            coordinator.Shutdown();
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Detached_motion_targets_are_unregistered_and_shutdown_blocks_reattachment()
    {
        var status = new TextBlock { Classes = { "cfp-download-motion" } };
        var host = new StackPanel { Children = { status } };
        var (window, coordinator) = CreateMotionWindow(host, fullMotion: false, reducedMotion: false);

        try
        {
            Assert.Contains(status, entranceAnimationTargets(coordinator));
            host.Children.Remove(status);
            Assert.DoesNotContain(status, entranceAnimationTargets(coordinator));
            host.Children.Add(status);
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(status, entranceAnimationTargets(coordinator));
            host.Children.Remove(status);
            coordinator.Shutdown();
            host.Children.Add(status);
            Dispatcher.UIThread.RunJobs();
            Assert.DoesNotContain(status, entranceAnimationTargets(coordinator));
            Assert.Equal(1, status.Opacity);
        }
        finally
        {
            coordinator.Shutdown();
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Page_switch_primes_the_entrance_offset_before_rendering()
    {
        var (window, viewModel, _) = CreateWindow();

        try
        {
            // Switch pages and inspect the very first instant, before any frame
            // callback runs: the incoming page must already carry the primed
            // entrance offset instead of being left at its settled transform.
            viewModel.CurrentPage = "Downloads";
            var downloads = window.GetVisualDescendants()
                .OfType<Grid>()
                .Single(grid => grid.IsVisible && grid.Classes.Contains("cfp-page"));

            var y = FindTranslate(downloads.RenderTransform)?.Y ?? double.NaN;
            Assert.True(
                y is >= -17.5d and <= -14.5d,
                $"The incoming page should start near the primed offset, got Y={y:F1}");
        }
        finally
        {
            CloseImmediately(window);
            CleanupRoot(viewModel);
        }
    }

    [AvaloniaFact]
    public void First_rendered_frame_reanchors_the_entrance_timeline()
    {
        var (window, viewModel, coordinator) = CreateWindow();

        try
        {
            // Register the entrance without letting a frame render yet: the
            // page is visible but no RequestAnimationFrame callback has fired.
            viewModel.CurrentPage = "Downloads";
            var downloads = window.GetVisualDescendants()
                .OfType<Grid>()
                .Single(grid => grid.IsVisible && grid.Classes.Contains("cfp-page"));

            // Simulate a slow first layout that pushes the first frame callback
            // far beyond the whole entrance duration. Any timeline measured from
            // the registration instant would consider the animation finished
            // before its first frame ever rendered.
            var state = GetActiveState(coordinator, downloads);
            Assert.False(
                StateHasStarted(state),
                "The entrance should not be anchored until the first frame renders.");
            StateSetStartedTimestamp(state, Stopwatch.GetTimestamp() - 5 * Stopwatch.Frequency);

            // Render the first frame now.
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
            Dispatcher.UIThread.RunJobs();

            var y = FindTranslate(downloads.RenderTransform)?.Y ?? double.NaN;

            // The first rendered frame must re-anchor the timeline so the page
            // starts from the primed offset instead of appearing already settled.
            Assert.True(
                y < -1d,
                $"First frame after a slow layout should restart from the primed offset, got Y={y:F1}");
        }
        finally
        {
            CloseImmediately(window);
            CleanupRoot(viewModel);
        }
    }

    [AvaloniaFact]
    public void Manage_subpages_are_registered_before_they_attach()
    {
        var (window, viewModel, coordinator) = CreateWindow();

        try
        {
            // Never enter the Manage page: its ScrollViewer content stays
            // unattached, but every static tab panel must already be a motion
            // target so switching tabs animates once the page appears.
            var registered = entranceAnimationTargets(coordinator);
            var registeredSubpages = registered
                .Where(control => control.Classes.Contains("cfp-subpage"))
                .ToArray();

            var logicalSubpages = EnumerateLogical(window)
                .OfType<Control>()
                .Where(control => control.Classes.Contains("cfp-subpage"))
                .ToArray();
            var missing = logicalSubpages
                .Where(panel => !registered.Contains(panel))
                .ToArray();

            Assert.True(
                missing.Length == 0,
                $"{missing.Length} of {logicalSubpages.Length} static subpages were never registered "
                + $"as motion targets; first missing: "
                + string.Join(",", missing.Take(3).Select(p => string.Join(" ", p.Classes))));
        }
        finally
        {
            CloseImmediately(window);
            CleanupRoot(viewModel);
        }
    }

    [AvaloniaFact]
    public void Manage_tab_switches_prime_each_subpage_entrance()
    {
        var (window, viewModel, coordinator) = CreateWindow();

        try
        {
            // Enter the Manage page and let its content attach and lay out.
            viewModel.CurrentPage = "Manage";
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
            Dispatcher.UIThread.RunJobs();

            var managePage = window.GetVisualDescendants()
                .OfType<Grid>()
                .Single(grid => grid.IsVisible && grid.Classes.Contains("cfp-page"));
            var subpages = managePage.GetVisualDescendants()
                .OfType<Control>()
                .Where(panel => panel.Classes.Contains("cfp-subpage"))
                .ToArray();
            Assert.NotEmpty(subpages);

            // Drive through every manage tab and check that the newly visible
            // subpage is primed with an entrance transform before any frame.
            foreach (var tab in new[] { "Loader", "Mods", "Presets", "Snapshots", "Logs", "Config", "Overview" })
            {
                viewModel.CurrentManageTab = tab;
                var visiblePanels = subpages.Where(panel => panel.IsVisible).ToArray();
                Assert.True(
                    visiblePanels.Length == 1,
                    $"After switching to '{tab}', expected one visible subpage, got {visiblePanels.Length}.");
                var y = FindTranslate(visiblePanels[0].RenderTransform)?.Y ?? double.NaN;
                Assert.True(
                    y is >= -17.5d and <= -14.5d,
                    $"Manage tab '{tab}' was not primed with an entrance offset, got Y={y:F1}");
            }
        }
        finally
        {
            CloseImmediately(window);
            CleanupRoot(viewModel);
        }
    }

    private static object GetActiveState(MotionCoordinator coordinator, Control control)
    {
        var field = typeof(MotionCoordinator).GetField(
            "activeEntranceAnimations",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var active = (IDictionary)field.GetValue(coordinator)!;
        Assert.True(active.Contains(control), "Entrance animation was not registered.");
        return active[control]!;
    }

    private static bool HasActiveAnimation(MotionCoordinator coordinator, Control control)
    {
        var field = typeof(MotionCoordinator).GetField(
            "activeEntranceAnimations",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return ((IDictionary)field.GetValue(coordinator)!).Contains(control);
    }

    private static (Window Window, MotionCoordinator Coordinator) CreateMotionWindow(
        Control content,
        bool fullMotion,
        bool reducedMotion)
    {
        var window = new Window { Content = content, Width = 240, Height = 180 };
        NameScope.SetNameScope(window, new NameScope());
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var coordinator = new MotionCoordinator(window, () => fullMotion, () => reducedMotion, () => true);
        coordinator.Start();
        return (window, coordinator);
    }

    private static bool StateHasStarted(object state)
    {
        var property = state.GetType().GetProperty("HasStarted")!;
        return (bool)property.GetValue(state)!;
    }

    private static void StateSetStartedTimestamp(object state, long timestamp)
    {
        var property = state.GetType().GetProperty("StartedTimestamp")!;
        property.SetValue(state, timestamp);
    }

    private static Control[] entranceAnimationTargets(MotionCoordinator coordinator)
    {
        var field = typeof(MotionCoordinator).GetField(
            "entranceAnimationTargets",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return ((IEnumerable)field.GetValue(coordinator)!).Cast<Control>().ToArray();
    }

    private static IEnumerable<object> EnumerateLogical(object parent)
    {
        foreach (var child in EnumerateChildren(parent))
        {
            yield return child;
            foreach (var descendant in EnumerateLogical(child))
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<object> EnumerateChildren(object parent)
    {
        if (parent is Avalonia.LogicalTree.ILogical logical)
        {
            foreach (var child in logical.LogicalChildren)
            {
                yield return child;
            }
        }
    }

    private static (MainWindow Window, MainViewModel ViewModel, MotionCoordinator Coordinator) CreateWindow()
    {
        var root = Path.Combine(Path.GetTempPath(), $"crystalfly-motion-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var viewModel = new MainViewModel(root);
        var window = new MainWindow { Width = 1100, Height = 720 };
        window.Show();
        window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();
        var coordinator = GetCoordinator(window);
        coordinator.Start();
        Dispatcher.UIThread.RunJobs();
        return (window, viewModel, coordinator);
    }

    private static void CleanupRoot(MainViewModel viewModel)
    {
        var root = GetApplicationDataRoot(viewModel);
        if (root is not null)
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static string? GetApplicationDataRoot(MainViewModel viewModel)
    {
        var pathsField = typeof(MainViewModel).GetField(
            "paths",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var paths = pathsField?.GetValue(viewModel);
        var rootField = paths?.GetType().GetProperty("ApplicationDataRoot");
        return rootField?.GetValue(paths) as string;
    }

    private static MotionCoordinator GetCoordinator(MainWindow window)
    {
        var field = typeof(MainWindow).GetField(
            "motionCoordinator",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (MotionCoordinator)field.GetValue(window)!;
    }

    private static TranslateTransform? FindTranslate(ITransform? transform)
    {
        if (transform is TranslateTransform translate)
        {
            return translate;
        }
        if (transform is TransformGroup group)
        {
            return group.Children.OfType<TranslateTransform>().FirstOrDefault();
        }
        return null;
    }

    private static void CloseImmediately(MainWindow window)
    {
        typeof(MainWindow)
            .GetField("closeAfterDispose", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(window, true);
        window.Close();
    }
}

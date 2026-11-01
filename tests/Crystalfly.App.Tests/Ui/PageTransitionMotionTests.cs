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
using Crystalfly.App.Views.Controls;
using Crystalfly.App.ViewModels.DependencyGraph;

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
                Assert.Equal(TimeSpan.FromMilliseconds(280), transition.Duration);
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
                Assert.InRange(transform.Y, 11.5d, 12.5d);
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
    [InlineData(true, false, 300, true)]
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
                y is >= 11.5d and <= 12.5d,
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
                y > 1d,
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
            FinishEntrances(coordinator);

            // Drive through every manage tab and check that the newly visible
            // subpage is primed with an entrance transform before any frame.
            foreach (var tab in new[] { "Loader", "Mods", "Presets", "Snapshots", "Logs", "Config", "Overview" })
            {
                viewModel.CurrentManageTab = tab;
                var visiblePanels = subpages.Where(panel => panel.IsEffectivelyVisible).ToArray();
                Assert.True(
                    visiblePanels.Length == 1,
                    $"After switching to '{tab}', expected one visible subpage, got {visiblePanels.Length}.");
                var y = FindTranslate(visiblePanels[0].RenderTransform)?.Y ?? double.NaN;
                Assert.True(
                    y is >= 11.5d and <= 12.5d,
                    $"Manage tab '{tab}' was not primed with an entrance offset, got Y={y:F1}");
            }
        }
        finally
        {
            CloseImmediately(window);
            CleanupRoot(viewModel);
        }
    }

    [AvaloniaFact]
    public void Soft_entrances_settle_exactly_and_reduced_motion_stops_spatial_motion_immediately()
    {
        var full = true;
        var reduced = false;
        var page = new Border { Classes = { "cfp-page" }, IsVisible = false };
        var dialog = new Border { Classes = { "cfp-dialog-motion" }, IsVisible = false };
        var edges = new DependencyGraphEdges();
        var window = new Window { Content = new StackPanel { Children = { page, dialog, edges } } };
        NameScope.SetNameScope(window, new NameScope());
        window.Show();
        var coordinator = new MotionCoordinator(window, () => full, () => reduced, () => true);
        coordinator.Start();
        try
        {
            page.IsVisible = true;
            dialog.IsVisible = true;
            Assert.Equal(12, FindTranslate(page.RenderTransform)!.Y);
            Assert.Null((page.RenderTransform as TransformGroup)?.Children.OfType<ScaleTransform>().SingleOrDefault());
            Assert.Equal(0.975, Assert.IsType<TransformGroup>(dialog.RenderTransform).Children.OfType<ScaleTransform>().Single().ScaleX);
            FinishEntrances(coordinator);
            Assert.Null(page.RenderTransform);
            Assert.Null(dialog.RenderTransform);
            Assert.Equal(1, dialog.Opacity);

            page.IsVisible = false;
            page.IsVisible = true;
            edges.Edges = CreateConnectedGraph().Edges;
            Assert.Equal(0, edges.RevealProgress);
            full = false;
            reduced = true;
            coordinator.UpdateMotionPreference();
            Assert.Null(page.RenderTransform);
            Assert.Equal(1, edges.RevealProgress);
            Assert.False(HasActiveAnimation(coordinator, page));
        }
        finally { coordinator.Shutdown(); window.Close(); }
    }

    [AvaloniaFact]
    public void Nested_surfaces_share_one_entrance_and_new_rows_have_a_bounded_stagger()
    {
        var subpage = new Border { Classes = { "cfp-subpage" }, IsVisible = false };
        var page = new Border { Classes = { "cfp-page" }, Child = subpage, IsVisible = false };
        var host = new StackPanel { Children = { page } };
        var (window, coordinator) = CreateMotionWindow(host, true, false);
        try
        {
            page.IsVisible = true;
            subpage.IsVisible = true;
            Assert.NotNull(FindTranslate(page.RenderTransform));
            Assert.Null(FindTranslate(subpage.RenderTransform));
            FinishEntrances(coordinator);

            var rows = Enumerable.Range(0, 20).Select(_ => new Border { Classes = { "cfp-motion-item" } }).ToArray();
            foreach (var row in rows)
            {
                host.Children.Add(row);
                coordinator.RegisterMotionTarget(row, animate: true);
                Assert.Equal(1, row.Opacity);
            }
            Assert.Equal(6, rows.Count(row => HasActiveAnimation(coordinator, row)));
            Assert.Equal(TimeSpan.FromMilliseconds(120), (TimeSpan)GetActiveState(coordinator, rows[5]).GetType().GetProperty("Delay")!.GetValue(GetActiveState(coordinator, rows[5]))!);
            rows[0].DataContext = new object();
            Assert.False(HasActiveAnimation(coordinator, rows[0]));
            Assert.Null(rows[0].RenderTransform);
            host.Children.Remove(rows[1]);
            Assert.DoesNotContain(rows[1], entranceAnimationTargets(coordinator));
        }
        finally { coordinator.Shutdown(); window.Close(); }
    }

    [AvaloniaFact]
    public void Graph_lines_animate_inside_an_entering_page_and_replay_when_its_cached_canvas_reopens()
    {
        var view = new DependencyGraphView { DataContext = CreateConnectedGraph() };
        var canvasHost = new Border { Child = view, IsVisible = false };
        var page = new Border { Classes = { "cfp-page" }, Child = canvasHost, IsVisible = false };
        var (window, coordinator) = CreateMotionWindow(page, true, false);
        try
        {
            page.IsVisible = true;
            canvasHost.IsVisible = true;
            Dispatcher.UIThread.RunJobs();
            var edges = Assert.Single(view.GetVisualDescendants().OfType<DependencyGraphEdges>());
            Assert.True(HasActiveAnimation(coordinator, page));
            Assert.True(HasActiveAnimation(coordinator, edges));
            Assert.True(edges.RevealProgress < 0.1);
            FinishEntrances(coordinator);
            Assert.Equal(1, edges.RevealProgress);

            canvasHost.IsVisible = false;
            Assert.False(HasActiveAnimation(coordinator, edges));
            canvasHost.IsVisible = true;
            Assert.Equal(0, edges.RevealProgress);
            var state = GetActiveState(coordinator, edges);
            state.GetType().GetProperty("HasStarted")!.SetValue(state, true);
            StateSetStartedTimestamp(state, Stopwatch.GetTimestamp() - (long)(Stopwatch.Frequency * 0.325));
            typeof(MotionCoordinator).GetMethod("OnEntranceAnimationFrame", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(coordinator, [TimeSpan.Zero]);
            Assert.InRange(edges.RevealProgress, 0.48, 0.55);
            FinishEntrances(coordinator);
            Assert.Equal(1, edges.RevealProgress);
            canvasHost.Child = null;
            Assert.DoesNotContain(edges, entranceAnimationTargets(coordinator));
            Assert.Empty((IDictionary)typeof(MotionCoordinator).GetField("graphVisibilityObservers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(coordinator)!);
        }
        finally { coordinator.Shutdown(); window.Close(); }
    }

    private static DependencyGraphModel CreateConnectedGraph() => DependencyGraphModel.Create(
        [new("a", "A", "", "Enabled", DependencyGraphNodeState.Normal), new("b", "B", "", "Enabled", DependencyGraphNodeState.Normal)],
        [new DependencyGraphEdgeDefinition("a", "b")]);

    [AvaloniaFact]
    public void Cancelling_motion_keeps_the_already_queued_frame_slot_until_it_runs()
    {
        var full = true;
        var page = new Border { Classes = { "cfp-page" }, IsVisible = false };
        var window = new Window { Content = page };
        NameScope.SetNameScope(window, new NameScope());
        window.Show();
        var coordinator = new MotionCoordinator(window, () => full, () => false, () => true);
        coordinator.Start();
        var scheduled = typeof(MotionCoordinator).GetField("entranceAnimationFrameScheduled", BindingFlags.Instance | BindingFlags.NonPublic)!;
        try
        {
            page.IsVisible = true;
            Assert.True((bool)scheduled.GetValue(coordinator)!);
            full = false;
            coordinator.UpdateMotionPreference();
            Assert.True((bool)scheduled.GetValue(coordinator)!);
            Assert.False(HasActiveAnimation(coordinator, page));
            full = true;
            coordinator.UpdateMotionPreference(replayVisiblePages: true);
            Assert.True(HasActiveAnimation(coordinator, page));
            FinishEntrances(coordinator);
            Assert.False((bool)scheduled.GetValue(coordinator)!);
        }
        finally { coordinator.Shutdown(); window.Close(); }
    }

    [AvaloniaFact]
    public void Late_controls_register_on_load_without_a_page_wide_rescan()
    {
        var host = new StackPanel();
        var (window, coordinator) = CreateMotionWindow(host, true, false);
        try
        {
            var button = new Button { Content = "操作", Classes = { "cfp-directory-action" } };
            var row = new Border { Classes = { "cfp-motion-item" }, Child = button };
            host.Children.Add(row);
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(row, entranceAnimationTargets(coordinator));
            Assert.IsType<ScaleTransform>(button.RenderTransform);
            host.Children.Remove(row);
            Dispatcher.UIThread.RunJobs();
            Assert.DoesNotContain(row, entranceAnimationTargets(coordinator));
        }
        finally { coordinator.Shutdown(); window.Close(); }
    }

    [AvaloniaFact]
    public void Updating_a_hidden_subpage_does_not_leave_an_offset_when_its_parent_later_opens()
    {
        var child = new Border { Classes = { "cfp-subpage" }, IsVisible = false };
        var parent = new Border { Child = child, IsVisible = false };
        var (window, coordinator) = CreateMotionWindow(parent, true, false);
        try
        {
            child.IsVisible = true;
            Dispatcher.UIThread.RunJobs();
            Assert.Null(child.RenderTransform);
            parent.IsVisible = true;
            Dispatcher.UIThread.RunJobs();
            Assert.Null(child.RenderTransform);
        }
        finally { coordinator.Shutdown(); window.Close(); }
    }

    [AvaloniaFact]
    public void Directory_actions_get_keyboard_feedback_but_hover_frames_never_scale_and_detach_cleans_up()
    {
        var button = new Button { Classes = { "cfp-directory-action" }, Width = 40, Height = 40 };
        var frame = new Border { Classes = { "cfp-instance-action-frame" }, Child = button };
        var host = new StackPanel { Children = { frame } };
        var (window, coordinator) = CreateMotionWindow(host, true, false);
        try
        {
            Assert.Null(frame.RenderTransform);
            Assert.Contains(frame.Transitions!, item => item is Avalonia.Animation.DoubleTransition);
            var scale = Assert.IsType<ScaleTransform>(button.RenderTransform);
            Assert.Contains(button.Transitions!, item => item is Avalonia.Animation.BrushTransition);
            button.Focus();
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
            Assert.True(button.IsPressed);
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
            Assert.False(button.IsPressed);
            var release = Assert.IsType<Avalonia.Animation.DoubleTransition>(scale.Transitions![0]);
            Assert.True(release.Easing.Ease(0.76) > 1);
            host.Children.Remove(frame);
            Assert.True(button.RenderTransform is null || button.RenderTransform.Value.IsIdentity);
            Assert.Null(frame.RenderTransform);
            Assert.False(((IDictionary)typeof(MotionCoordinator).GetField("microInteractionTransforms", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(coordinator)!).Contains(button));
        }
        finally { coordinator.Shutdown(); window.Close(); }
    }

    [Fact]
    public void Soft_entrance_has_one_small_overshoot_and_an_exact_landing()
    {
        Assert.Equal(12, MotionCoordinator.SoftEntranceOffset(0));
        Assert.Equal(-1, MotionCoordinator.SoftEntranceOffset(0.76), 6);
        Assert.Equal(0, MotionCoordinator.SoftEntranceOffset(1), 6);
        // Keep meaningful travel after the first 100ms instead of finishing most motion in a flash.
        Assert.InRange(MotionCoordinator.SoftEntranceOffset(100d / 440d), 6, 9);
        foreach (var step in Enumerable.Range(0, 101))
            Assert.InRange(MotionCoordinator.SoftEntranceOffset(step / 100d), -1, 12);
    }

    private static void FinishEntrances(MotionCoordinator coordinator)
    {
        var active = (IDictionary)typeof(MotionCoordinator).GetField("activeEntranceAnimations", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(coordinator)!;
        foreach (var state in active.Values)
        {
            state!.GetType().GetProperty("HasStarted")!.SetValue(state, true);
            StateSetStartedTimestamp(state, Stopwatch.GetTimestamp() - Stopwatch.Frequency);
        }
        typeof(MotionCoordinator).GetMethod("OnEntranceAnimationFrame", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(coordinator, [TimeSpan.Zero]);
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

using System.Collections;
using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.Services;
using Crystalfly.App.ViewModels;
using Crystalfly.App.Views;

namespace Crystalfly.App.Tests.Ui;

public sealed class PageTransitionMotionTests
{
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
        var window = new MainWindow { Width = 1100, Height = 720, DataContext = viewModel };
        window.Show();
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

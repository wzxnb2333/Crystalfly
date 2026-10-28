using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.ViewModels;
using Crystalfly.App.Views;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Models;

namespace Crystalfly.App.Tests.Ui;

public sealed class SpeedrunWorkspaceLayoutTests
{
    [AvaloniaTheory]
    [InlineData(900, 0, UiLanguage.SimplifiedChinese)]
    [InlineData(900, 24, UiLanguage.English)]
    [InlineData(1400, 8, UiLanguage.SimplifiedChinese)]
    public async Task Favorites_use_an_aligned_right_sidebar_and_scroll_independently(
        double width, int count, UiLanguage language)
    {
        await using var viewModel = new MainViewModel(Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N")))
        {
            CurrentPage = "Speedrun"
        };
        viewModel.Loc.Apply(language);
        for (int index = 0; index < count; index++)
        {
            viewModel.LiveSplitFavorites.Add(new(Path.Combine(Path.GetTempPath(), $"very-long-splits-file-name-{index}.lss")));
        }
        var window = new MainWindow { Width = width, Height = 720 };
        window.Show();
        window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);

        try
        {
            var favorites = window.FindControl<Border>("LiveSplitFavoritesPanel")!;
            var items = window.FindControl<ItemsControl>("LiveSplitFavoritesItems")!;
            var workspace = window.FindControl<ScrollViewer>("SpeedrunWorkspace")!;
            var environment = window.FindControl<Grid>("SpeedrunEnvironmentWorkspace")!;
            var favoritesScroll = window.FindControl<ScrollViewer>("LiveSplitFavoritesScroll")!;
            var rail = window.FindControl<Border>("SpeedrunEnvironmentRail")!;
            Assert.True(favorites.IsEffectivelyVisible);
            Assert.Equal(240, favorites.Bounds.Width);
            Assert.Equal(workspace.Bounds.Height, favorites.Bounds.Height);
            var workspaceOrigin = workspace.TranslatePoint(default, window)!.Value;
            var favoritesOrigin = favorites.TranslatePoint(default, window)!.Value;
            var railOrigin = rail.TranslatePoint(default, window)!.Value;
            Assert.Equal(16, workspaceOrigin.X - railOrigin.X - rail.Bounds.Width, 1);
            Assert.Equal(16, favoritesOrigin.X - workspaceOrigin.X - workspace.Bounds.Width, 1);
            Assert.Equal(16, window.ClientSize.Width - favoritesOrigin.X - favorites.Bounds.Width, 1);
            Assert.Equal(workspaceOrigin.Y, favoritesOrigin.Y);
            Assert.Equal(environment.Bounds.Height, favorites.Bounds.Height);
            Assert.Equal(0, workspace.Extent.Height - workspace.Viewport.Height, 1);
            Assert.Equal(count > 0, items.IsEffectivelyVisible);
            var buttons = items.GetVisualDescendants().OfType<Button>()
                .Where(button => button.Tag is string && !button.Classes.Contains("cfp-icon"))
                .ToArray();
            Assert.Equal(count, buttons.Length);
            foreach (var button in buttons)
            {
                var origin = Assert.IsType<Point>(button.TranslatePoint(default, favorites));
                Assert.True(origin.X >= 0 && origin.X + button.Bounds.Width <= favorites.Bounds.Width);
                Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)));
            }
            if (count > 1)
            {
                Assert.Equal(buttons[0].TranslatePoint(default, favorites)!.Value.X,
                    buttons[1].TranslatePoint(default, favorites)!.Value.X);
                Assert.True(buttons[1].TranslatePoint(default, favorites)!.Value.Y >=
                    buttons[0].TranslatePoint(default, favorites)!.Value.Y + buttons[0].Bounds.Height + 8);
            }
            if (count == 24)
            {
                Assert.True(favoritesScroll.Extent.Height > favoritesScroll.Viewport.Height);
                favoritesScroll.Offset = new Vector(0, 150);
                Dispatcher.UIThread.RunJobs();
                Assert.True(favoritesScroll.Offset.Y > 0);
                Assert.Equal(0, workspace.Offset.Y);
                Assert.Equal(favoritesOrigin, favorites.TranslatePoint(default, window)!.Value);
            }
            var output = Environment.GetEnvironmentVariable("CRYSTALFLY_UI_SCAN_DIR");
            if (!string.IsNullOrWhiteSpace(output))
            {
                Directory.CreateDirectory(output);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
                using var frame = window.CaptureRenderedFrame();
                Assert.NotNull(frame);
                frame.Save(Path.Combine(output, $"SpeedrunEnvironment-{width}-{count}.png"), new PngBitmapEncoderOptions());
            }
        }
        finally
        {
            CloseImmediately(window);
        }
    }

    [AvaloniaFact]
    public async Task Official_environment_rename_action_is_visible_and_disabled_during_mutations()
    {
        string root = Path.Combine(Path.GetTempPath(), "crystalfly-ui", Guid.NewGuid().ToString("N"));
        await using var viewModel = new MainViewModel(root) { CurrentPage = "Speedrun" };
        var record = new InstanceRecord
        {
            Id = "layout-speedrun", Name = "正式速通环境", RootPath = Path.Combine(root, "instance"),
            BuildId = "1.5.78.11833", Purpose = InstancePurpose.OfficialSpeedrun,
            SpeedrunTemplateId = "runtime-patches-1578"
        };
        viewModel.SelectedSpeedrunInstance = new(record, record.BuildId, "Vanilla", 0);
        var window = new MainWindow { Width = 900, Height = 600 };
        window.Show();
        window.DataContext = viewModel;
        Dispatcher.UIThread.RunJobs();
        try
        {
            var button = Assert.Single(window.GetVisualDescendants().OfType<Button>(), candidate =>
                AutomationProperties.GetName(candidate) == viewModel.Loc["SpeedrunRename"]);
            Assert.True(button.IsEffectivelyVisible);
            Assert.True(button.IsEnabled);
            var origin = Assert.IsType<Point>(button.TranslatePoint(default, window));
            Assert.True(origin.X >= 0 && origin.X + button.Bounds.Width <= window.ClientSize.Width);
            Assert.True(origin.Y >= 0 && origin.Y + button.Bounds.Height <= window.ClientSize.Height);
            var summary = window.FindControl<Border>("SpeedrunEnvironmentSummary")!;
            var favorites = window.FindControl<Border>("LiveSplitFavoritesPanel")!;
            Assert.Equal(summary.TranslatePoint(default, window)!.Value.Y,
                favorites.TranslatePoint(default, window)!.Value.Y);
            foreach (var action in summary.GetVisualDescendants().OfType<Button>().Where(action => action.IsEffectivelyVisible))
            {
                var actionOrigin = action.TranslatePoint(default, summary)!.Value;
                Assert.InRange(actionOrigin.X, 0, summary.Bounds.Width - action.Bounds.Width);
            }
            if (Environment.GetEnvironmentVariable("CRYSTALFLY_UI_SCAN_DIR") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
                using var frame = window.CaptureRenderedFrame();
                Assert.NotNull(frame);
                frame.Save(Path.Combine(output, "SpeedrunEnvironment-selected.png"), new PngBitmapEncoderOptions());
            }
            viewModel.IsBusy = true;
            Dispatcher.UIThread.RunJobs();
            Assert.False(button.IsEnabled);
            viewModel.IsBusy = false;
            viewModel.IsGameRunning = true;
            Dispatcher.UIThread.RunJobs();
            Assert.False(button.IsEnabled);
        }
        finally
        {
            CloseImmediately(window);
        }
    }

    private static void CloseImmediately(MainWindow window)
    {
        typeof(MainWindow).GetField("closeAfterDispose", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, true);
        window.Close();
    }
}

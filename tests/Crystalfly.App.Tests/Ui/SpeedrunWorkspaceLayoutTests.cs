using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
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
    [InlineData(900, 1, UiLanguage.SimplifiedChinese)]
    [InlineData(900, 8, UiLanguage.English)]
    [InlineData(1400, 8, UiLanguage.SimplifiedChinese)]
    public async Task Favorites_use_the_workspace_width_and_wrap_without_a_full_height_sidebar(
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
            Assert.True(favorites.IsEffectivelyVisible);
            Assert.True(favorites.Bounds.Width >= workspace.Bounds.Width - 35,
                $"Favorites width {favorites.Bounds.Width} did not use workspace width {workspace.Bounds.Width}.");
            Assert.InRange(favorites.Bounds.Height, 40, count <= 1 ? 150 : 350);
            Assert.Equal(count > 0, items.IsEffectivelyVisible);
            var buttons = items.GetVisualDescendants().OfType<Button>()
                .Where(button => button.Tag is string && !button.Classes.Contains("cfp-icon"))
                .ToArray();
            Assert.Equal(count, buttons.Length);
            foreach (var button in buttons)
            {
                var origin = Assert.IsType<Point>(button.TranslatePoint(default, favorites));
                Assert.True(origin.X >= 0 && origin.X + button.Bounds.Width <= favorites.Bounds.Width);
                Assert.True(origin.Y >= 0 && origin.Y + button.Bounds.Height <= favorites.Bounds.Height);
                Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)));
            }
            if (count > 1)
            {
                Assert.Equal(buttons[0].TranslatePoint(default, favorites)!.Value.Y,
                    buttons[1].TranslatePoint(default, favorites)!.Value.Y);
                Assert.True(buttons[^1].TranslatePoint(default, favorites)!.Value.Y >
                    buttons[0].TranslatePoint(default, favorites)!.Value.Y);
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

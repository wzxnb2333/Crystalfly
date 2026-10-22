using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Crystalfly.App.ViewModels;
using Crystalfly.App.Views;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Models;

namespace Crystalfly.App.Tests.Ui;

public sealed class MultiGameWorkspaceUiTests
{
    [AvaloniaTheory]
    [InlineData(900, 600)]
    [InlineData(1100, 720)]
    public async Task Actual_shell_routes_management_tabs_and_restores_selected_game_portrait(int width, int height)
    {
        var root = Path.Combine(Path.GetTempPath(), "Crystalfly.Shell.Ui", Guid.NewGuid().ToString("N"));
        await CrystalflySettingsStore.SaveAsync(Path.Combine(root, "settings.json"), new()
        {
            SelectedGameId = GameId.Silksong, OfflineMode = true, CheckForUpdates = false,
            OnboardingCompleted = true, ErrorReportingEnabled = false
        });
        await using var model = new MainViewModel(root);
        await model.InitializeAsync();
        var window = new MainWindow { Width = width, Height = height };
        window.Show();
        window.DataContext = model;
        try
        {
            model.CurrentPage = "Manage";
            foreach (var tab in new[] { "Mods", "Loader", "Presets" })
            {
                model.CurrentManageTab = tab;
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                var manager = window.FindControl<ThunderstoreView>("ThunderstoreManager")!;
                Assert.True(manager.IsVisible);
                Assert.Equal(tab, manager.Workspace);
                Assert.True(manager.Bounds.Width > 600);
                var name = tab == "Mods" ? "InstalledWorkspace" : tab + "Workspace";
                Assert.True(manager.FindControl<Control>(name)!.IsVisible);
            }
            Assert.Null(window.FindControl<Control>("GameSelector"));
            Assert.Same(model.ToggleGameCommand, window.FindControl<Button>("GameAvatarButton")!.Command);
            Assert.Equal(model.SelectedGameName, window.FindControl<TextBlock>("CurrentGameLabel")!.Text);
            Assert.Equal(1, window.FindControl<Image>("HornetGameAvatar")!.Opacity);
            Assert.Equal(0, window.FindControl<Image>("KnightGameAvatar")!.Opacity);
            model.Thunderstore.BrowseMarketCommand.Execute(null);
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Assert.Equal("Downloads", model.CurrentPage);
            Assert.Equal("ModMarket", model.CurrentDownloadSection);
            Assert.True(window.FindControl<ThunderstoreView>("ThunderstoreMarket")!.IsVisible);
            model.Thunderstore.ChooseInstanceCommand.Execute(null);
            Assert.Equal("Launch", model.CurrentPage);
        }
        finally
        {
            typeof(MainWindow).GetField("closeAfterDispose", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, true);
            window.Close();
            await model.DisposeAsync();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}

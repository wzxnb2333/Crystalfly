using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.ViewModels;
using Crystalfly.App.Views;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Models;
using Ursa.Controls;

namespace Crystalfly.App.Tests.Ui;

public sealed class ModUiParityTests
{
    [AvaloniaTheory]
    [InlineData(GameId.HollowKnight, 900)]
    [InlineData(GameId.Silksong, 900)]
    [InlineData(GameId.HollowKnight, 1100)]
    [InlineData(GameId.Silksong, 1100)]
    public async Task Download_page_has_no_cache_controls_and_market_header_has_no_instance_or_refresh_buttons(GameId game, int width)
    {
        var root = Path.Combine(Path.GetTempPath(), "Crystalfly.UiParity", Guid.NewGuid().ToString("N"));
        var model = new MainViewModel(root);
        model.IsOfflineMode = true;
        model.SelectedMotionPreference = new(UiMotionPreference.Off, "Off");
        var window = new MainWindow { Width = width, Height = 720 };
        window.Show();
        window.DataContext = model;
        try
        {
            if (game == GameId.Silksong) await model.ToggleGameCommand.ExecuteAsync(null);
            model.CurrentPage = "Downloads";
            model.CurrentDownloadSection = "GameVersions";
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.Null(window.FindControl<Control>("SteamChunkCacheCard"));
            Assert.Null(window.FindControl<Control>("SteamChunkCacheClearButton"));

            model.CurrentDownloadSection = "ModMarket";
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Control header = game == GameId.Silksong
                ? window.FindControl<ThunderstoreView>("ThunderstoreMarket")!.FindControl<Grid>("WorkspaceHeader")!
                : (Control)window.GetVisualDescendants().OfType<TextBlock>().First(text =>
                    text.Text == model.Loc["MarketTitle"] && text.IsEffectivelyVisible).Parent!;
            Assert.True(header.IsEffectivelyVisible);
            Assert.DoesNotContain(header.GetVisualDescendants().OfType<Button>(), button => button.IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
            for (var attempt = 0; attempt < 200 && window.IsVisible; attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }
            Assert.False(window.IsVisible);
            await model.DisposeAsync();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [AvaloniaFact]
    public async Task Thunderstore_errors_use_one_top_notification_and_can_repeat_after_retry()
    {
        var root = Path.Combine(Path.GetTempPath(), "Crystalfly.UiParity", Guid.NewGuid().ToString("N"));
        var model = new MainViewModel(root);
        model.SelectedMotionPreference = new(UiMotionPreference.Off, "Off");
        var window = new MainWindow { Width = 1100, Height = 720 };
        window.Show();
        window.DataContext = model;
        try
        {
            model.Thunderstore.Error = "package load failed";
            Dispatcher.UIThread.RunJobs();
            var toast = Assert.Single(window.GetVisualDescendants().OfType<ToastCard>());
            Assert.Equal(NotificationType.Error, toast.NotificationType);
            Assert.Equal("package load failed", model.ErrorMessage);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), text =>
                text.Text == "package load failed" && text.GetVisualAncestors().OfType<ThunderstoreView>().Any());
            Assert.True(WindowToastManager.TryGetToastManager(window, out var manager));
            manager!.CloseAll();
            for (var attempt = 0; attempt < 100 && window.GetVisualDescendants().OfType<ToastCard>().Any(); attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }
            Assert.Empty(window.GetVisualDescendants().OfType<ToastCard>());
            model.Thunderstore.Error = null;
            model.Thunderstore.Error = "package load failed";
            Dispatcher.UIThread.RunJobs();
            Assert.Single(window.GetVisualDescendants().OfType<ToastCard>());
        }
        finally
        {
            window.Close();
            for (var attempt = 0; attempt < 200 && window.IsVisible; attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }
            Assert.False(window.IsVisible);
            await model.DisposeAsync();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}

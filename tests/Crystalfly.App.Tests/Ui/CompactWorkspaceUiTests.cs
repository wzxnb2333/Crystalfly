using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.ViewModels;
using Crystalfly.App.Views;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Models;

namespace Crystalfly.App.Tests.Ui;

public sealed class CompactWorkspaceUiTests
{
    [AvaloniaTheory]
    [InlineData(900, UiLanguage.English, true)]
    [InlineData(1280, UiLanguage.SimplifiedChinese, false)]
    public async Task Both_games_keep_workspace_controls_within_the_available_width(
        int width, UiLanguage language, bool dark)
    {
        var root = Path.Combine(Path.GetTempPath(), "Crystalfly.CompactWorkspace", Guid.NewGuid().ToString("N"));
        await CrystalflySettingsStore.SaveAsync(Path.Combine(root, "settings.json"), new()
        {
            OfflineMode = true, CheckForUpdates = false, ErrorReportingEnabled = false,
            OnboardingCompleted = true, MotionPreference = UiMotionPreference.Off, Language = language
        });
        await using var model = new MainViewModel(root);
        await model.InitializeAsync();
        var window = new MainWindow
        {
            Width = width, Height = 720,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        window.DataContext = model;
        try
        {
            foreach (var game in new[] { GameId.HollowKnight, GameId.Silksong })
            {
                await model.SelectGameCommand.ExecuteAsync(game.ToString());
                Check("Launch");
                Check("Versions");
                model.SelectedInstance = new InstanceItemViewModel(new InstanceRecord
                {
                    Id = "compact-" + game, GameId = game, Name = "Compact layout test instance",
                    RootPath = Path.Combine(root, "game"), BuildId = "unknown"
                }, model.Loc["UnknownVersion"], "Vanilla", 0);
                foreach (var tab in new[] { "Overview", "Loader", "Mods", "Presets", "Snapshots", "Logs", "Config" })
                {
                    model.CurrentManageTab = tab;
                    Check("Manage");
                }
                foreach (var section in new[] { "GameVersions", "ModMarket", "DownloadQueue" })
                {
                    model.CurrentDownloadSection = section;
                    Check("Downloads");
                }
                foreach (var section in new[] { "General", "Network", "Catalog", "Updates", "About" })
                {
                    model.Settings.CurrentSettingsSection = section;
                    Check("Settings");
                }
            }
            model.CurrentPage = "Speedrun";
            foreach (var tab in new[] { "Environment", "Activity" })
            {
                model.SelectSpeedrunTabCommand.Execute(tab);
                Check("Speedrun");
            }
        }
        finally
        {
            typeof(MainWindow).GetField("closeAfterDispose", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(window, true);
            window.Close();
            await model.DisposeAsync();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }

        void Check(string page)
        {
            model.CurrentPage = page;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            var context = $"{model.SelectedGameId}/{page}/{model.CurrentManageTab}/{model.CurrentDownloadSection}/{model.Settings.CurrentSettingsSection}";
            var buttons = window.GetVisualDescendants().OfType<Button>()
                .Where(button => button.IsEffectivelyVisible && button.Bounds.Width > 0).ToArray();
            Assert.NotEmpty(buttons);
            foreach (var button in buttons)
            {
                var origin = button.TranslatePoint(default, window);
                Assert.NotNull(origin);
                // Offscreen virtualized/scrollable rows do not constrain the viewport.
                if (origin.Value.Y + button.Bounds.Height <= 0 || origin.Value.Y >= window.ClientSize.Height)
                    continue;
                Assert.True(origin.Value.X >= -0.5
                    && origin.Value.X + button.Bounds.Width <= window.ClientSize.Width + 0.5,
                    $"{context}: action '{button.Content}' exceeds the {window.ClientSize.Width:F0}-DIP viewport: "
                    + $"x={origin.Value.X:F1}, width={button.Bounds.Width:F1}.");
            }
        }
    }
}

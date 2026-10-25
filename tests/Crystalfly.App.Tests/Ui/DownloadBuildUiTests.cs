using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.ViewModels;
using Crystalfly.App.Views;
using Crystalfly.Core.Configuration;

namespace Crystalfly.App.Tests.Ui;

public sealed class DownloadBuildUiTests
{
    [AvaloniaFact]
    public async Task Version_list_selects_download_target_and_survives_manifest_search_without_duplicate_picker()
    {
        var root = Path.Combine(Path.GetTempPath(), "crystalfly-build-ui", Guid.NewGuid().ToString("N"));
        var previousTheme = Application.Current!.RequestedThemeVariant;
        Application.Current.RequestedThemeVariant = ThemeVariant.Light;
        await using var viewModel = new MainViewModel(root)
        {
            CurrentPage = "Downloads", CurrentDownloadSection = "GameVersions",
            IsSteamLoggedIn = true, SteamStatus = "Steam"
        };
        viewModel.Loc.Apply(UiLanguage.SimplifiedChinese);
        var builds = new[]
        {
            new DownloadBuildOption("public", "最新版", null)
            {
                VerificationText = viewModel.Loc["DownloadBuildResolvedOnDownload"]
            },
            new DownloadBuildOption("1.5.12459", "1.5.12459", 101UL)
            {
                ReleaseDateText = "2025-09-11",
                FirstSeenDateText = "2026-03-17",
                VerificationText = viewModel.Loc["DownloadBuildFilesVerified"],
                IsDownloaded = true
            },
            new DownloadBuildOption("1.5.78.11833", "1.5.78.11833", 102UL)
            {
                VerificationText = viewModel.Loc["DownloadBuildFilesVerified"]
            },
            new DownloadBuildOption("1.4.3.2", "1.4.3.2", 103UL),
            new DownloadBuildOption("1.2.2.1", "1.2.2.1", 104UL)
        };
        foreach (var build in builds)
        {
            viewModel.DownloadBuilds.Add(build);
            viewModel.VisibleDownloadBuilds.Add(build);
        }
        // Show without a data context, then bind the fixture: no startup scans or network work.
        var window = new MainWindow { Width = 1100, Height = 720 };
        window.Show();
        window.DataContext = viewModel;
        try
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var title = window.FindControl<TextBlock>("DownloadVersionsPageTitle")!;
            var search = window.FindControl<TextBox>("DownloadBuildSearch")!;
            var list = window.FindControl<ListBox>("DownloadBuildList")!;
            var searchRow = (Grid)search.Parent!;
            var titleOrigin = title.TranslatePoint(default, window)!.Value;
            var searchOrigin = search.TranslatePoint(default, window)!.Value;
            var listOrigin = list.TranslatePoint(default, window)!.Value;
            var searchRowOrigin = searchRow.TranslatePoint(default, window)!.Value;
            Assert.InRange(Math.Abs(searchOrigin.X - listOrigin.X), 0, 0.5);
            Assert.InRange(Math.Abs(searchRowOrigin.X - listOrigin.X), 0, 0.5);
            Assert.InRange(Math.Abs(searchRow.Bounds.Width - list.Bounds.Width), 0, 0.5);
            var rowText = list.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ToArray();
            Assert.Contains("2025-09-11", rowText);
            Assert.Contains(viewModel.Loc["DownloadBuildReleaseDate"], rowText);
            Assert.Contains("2026-03-17", rowText);
            Assert.DoesNotContain(viewModel.Loc["DownloadBuildFirstSeen"], rowText);
            Assert.Contains(viewModel.Loc["DownloadBuildFilesVerified"], rowText);
            Assert.DoesNotContain(viewModel.Loc["DownloadBuildLocalPresent"], rowText);
            Assert.Contains(viewModel.Loc["DownloadBuildResolvedOnDownload"], rowText);
            var account = window.FindControl<Border>("SteamAccountCard")!;
            Assert.True(account.Bounds.Height <= 80,
                $"The signed-in account card consumed {account.Bounds.Height:F1} DIP.");
            var versions = window.FindControl<Border>("DownloadVersionsCard")!;
            var accountOrigin = account.TranslatePoint(default, window)!.Value;
            var versionsOrigin = versions.TranslatePoint(default, window)!.Value;
            Assert.InRange(Math.Abs(titleOrigin.X - versionsOrigin.X), 0, 0.5);
            Assert.InRange(Math.Abs(accountOrigin.X - versionsOrigin.X), 0, 0.5);
            Assert.InRange(Math.Abs(account.Bounds.Width - versions.Bounds.Width), 0, 0.5);
            var download = window.FindControl<Button>("DownloadSelectedBuildButton")!;
            Assert.False(download.IsEnabled);
            list.SelectedItem = builds[2];
            Assert.Same(builds[2], viewModel.SelectedDownloadBuild);
            Assert.True(download.IsEnabled);
            viewModel.SelectedDownloadBuild = builds[3];
            Assert.Same(builds[3], list.SelectedItem);
            viewModel.DownloadBuildSearchText = "104";
            Assert.Same(builds[4], Assert.Single(list.Items));
            list.SelectedItem = builds[4];
            Assert.Same(builds[4], viewModel.SelectedDownloadBuild);
            viewModel.DownloadBuildSearchText = string.Empty;
            Assert.Equal(5, list.Items.Count);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            if (Environment.GetEnvironmentVariable("CRYSTALFLY_DOWNLOADS_PREVIEW") is { Length: > 0 } preview)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(preview))!);
                using var frame = window.GetLastRenderedFrame();
                Assert.NotNull(frame);
                frame.Save(preview, new JpegBitmapEncoderOptions { Quality = 92 });
            }
        }
        finally
        {
            typeof(MainWindow).GetField("closeAfterDispose", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, true);
            window.Close();
            await viewModel.DisposeAsync();
            Application.Current.RequestedThemeVariant = previousTheme;
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}

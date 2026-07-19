using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.ViewModels;
using Crystalfly.App.Views;
using Crystalfly.Core.Catalog;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Models;
using Ursa.Controls;

namespace Crystalfly.App.Tests.Ui;

public sealed class DocumentationScreenshotTests
{
    private static readonly ScreenshotCase[] Cases =
    [
        new("crystalfly-900x600-zh.jpg", 900, 600, 1d, ScreenshotState.GameVersions),
        new("crystalfly-1280x720-zh.jpg", 1280, 720, 1d, ScreenshotState.Launch),
        new("crystalfly-1920x1080-zh.jpg", 1920, 1080, 1.5d, ScreenshotState.Settings),
        new("crystalfly-2560x1440-zh.jpg", 2560, 1440, 2d, ScreenshotState.Launch),
        new("crystalfly-mod-market-list-1280x720-zh.jpg", 1280, 720, 1d, ScreenshotState.MarketList),
        new("crystalfly-mod-market-detail-1280x720-zh.jpg", 1280, 720, 1d, ScreenshotState.MarketDetail),
        new("crystalfly-mod-install-overlay-1280x720-zh.jpg", 1280, 720, 1d, ScreenshotState.MarketInstall),
        new("crystalfly-instance-detail-900x600-zh.jpg", 900, 600, 1d, ScreenshotState.InstanceDetail)
    ];

    [AvaloniaFact]
    public async Task Documentation_fixture_renders_expected_content_and_writes_screenshots_only_when_requested()
    {
        var writeScreenshots = string.Equals(
            Environment.GetEnvironmentVariable("CRYSTALFLY_UPDATE_SCREENSHOTS"),
            "1",
            StringComparison.Ordinal);
        var outputDirectory = Path.Combine(FindRepositoryRoot(), "docs", "screenshots");

        foreach (var capture in Cases)
        {
            await using var fixture = CreateFixture();
            await fixture.PrepareAsync(capture.State);
            fixture.Window.Width = capture.Width / capture.RenderScaling;
            fixture.Window.Height = capture.Height / capture.RenderScaling;
            fixture.Window.Show();
            fixture.Window.DataContext = fixture.ViewModel;
            fixture.Window.SetRenderScaling(capture.RenderScaling);
            Dispatcher.UIThread.RunJobs();

            if (capture.State == ScreenshotState.MarketInstall)
            {
                await fixture.OpenMarketInstallAsync();
            }

            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            AssertExpectedContent(fixture, capture.State);

            using var frame = Assert.IsType<WriteableBitmap>(fixture.Window.GetLastRenderedFrame());
            Assert.Equal(capture.Width, frame.PixelSize.Width);
            Assert.Equal(capture.Height, frame.PixelSize.Height);
            if (writeScreenshots)
            {
                Directory.CreateDirectory(outputDirectory);
                frame.Save(
                    Path.Combine(outputDirectory, capture.FileName),
                    new JpegBitmapEncoderOptions { Quality = 92 });
            }
        }
    }

    private static ScreenshotFixture CreateFixture()
    {
        var previousThemeVariant = Application.Current!.RequestedThemeVariant;
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        var root = Path.Combine(Path.GetTempPath(), "crystalfly-screenshots", "fixture");
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
        var versionRoot = Path.Combine(root, "versions");
        var instanceRoot = Path.Combine(versionRoot, "practice-1578");
        Directory.CreateDirectory(instanceRoot);
        File.WriteAllText(Path.Combine(instanceRoot, "hollow_knight.exe"), "fixture");

        var mod = new ModManifest
        {
            Id = "debugmod",
            Name = "DebugMod",
            DisplayName = "DebugMod",
            Description = "面向调试、练习与路线验证的完整工具集。",
            Authors = ["TheMulhima", "Crystalfly maintainers"],
            Tags = ["调试", "练习", "工具"],
            Integrations = ["Modding API"],
            RepositoryUrl = "https://github.com/wzxnb2333/New.HK.Debug",
            IssuesUrl = "https://github.com/wzxnb2333/New.HK.Debug/issues",
            SourceName = "HK ModLinks",
            Version = "1.4.10.5-r2",
            DownloadUrl = "https://example.invalid/debugmod.zip",
            Sha256 = new string('0', 64),
            LoaderId = "modding-api-77",
            SupportedBuildIds = ["1.5.78.11833"],
            Dependencies = []
        };
        var mods = new[]
        {
            mod,
            mod with
            {
                Id = "screenshake-modifier",
                Name = "ScreenShakeModifier",
                DisplayName = "ScreenShakeModifier",
                Description = "调整游戏内镜头震动。",
                Authors = ["HK Speedrunning"],
                Tags = ["速通", "视觉"],
                Version = "1.3.0",
                Dependencies = []
            },
            mod with
            {
                Id = "additional-timelines",
                Name = "Additional Timelines and Practice Utilities",
                DisplayName = "Additional Timelines and Practice Utilities",
                Description = "提供额外计时信息和练习工具。",
                Authors = ["Community Maintainers"],
                Tags = ["计时", "练习"],
                Version = "2.1.4",
                Dependencies = ["screenshake-modifier"]
            }
        };
        var loader = new LoaderManifest
        {
            Id = "modding-api-77",
            Name = "Modding API v77",
            Version = "77",
            DownloadUrl = "https://example.invalid/modding-api-77.zip",
            Sha256 = new string('1', 64),
            SupportedBuildIds = ["1.5.78.11833"]
        };
        var catalog = new GameCatalog { Loaders = [loader], Mods = mods };
        var record = new InstanceRecord
        {
            Id = "practice-1578",
            Name = "1.5.78 日常练习",
            RootPath = instanceRoot,
            BuildId = "1.5.78.11833",
            CreatedAt = new DateTimeOffset(2026, 7, 19, 12, 0, 0, TimeSpan.Zero)
        };
        var instance = new InstanceItemViewModel(record, "1.5.78.11833", "Modding API v77", 12);
        var viewModel = new MainViewModel(Path.Combine(root, "app-data"))
        {
            VersionRoot = versionRoot,
            StatusMessage = "就绪",
            SteamStatus = "已登录 · crystalfly-fixture"
        };
        viewModel.Loc.Apply(UiLanguage.SimplifiedChinese);
        SetPrivateField(viewModel, "catalog", catalog);
        SetPrivateField(viewModel, "officialCatalogResult", new OfficialCatalogLoadResult(
            OfficialCatalogLoadStatus.Remote,
            catalog,
            "77",
            mods.Length,
            null));
        viewModel.Instances.Add(instance);
        viewModel.VisibleInstances.Add(instance);
        foreach (var marketMod in mods)
        {
            viewModel.MarketMods.Add(marketMod);
            viewModel.VisibleMarketMods.Add(marketMod);
        }
        AddMarketOption(viewModel.MarketBuildOptions, option => viewModel.SelectedMarketBuildOption = option, viewModel.Loc["FilterAll"]);
        AddMarketOption(viewModel.MarketLoaderOptions, option => viewModel.SelectedMarketLoaderOption = option, viewModel.Loc["FilterAll"]);
        AddMarketOption(viewModel.MarketSourceOptions, option => viewModel.SelectedMarketSourceOption = option, viewModel.Loc["FilterAll"]);
        AddMarketOption(viewModel.MarketTagOptions, option => viewModel.SelectedMarketTagOption = option, viewModel.Loc["FilterAll"]);
        viewModel.DownloadBuilds.Add(new("1.5.78.11833", "Hollow Knight 1.5.78.11833", 9207084990026249690));
        viewModel.SelectedDownloadBuild = viewModel.DownloadBuilds[0];
        viewModel.LanguageOptions.Add(new(UiLanguage.FollowSystem, viewModel.Loc["FollowSystem"]));
        viewModel.LanguageOptions.Add(new(UiLanguage.SimplifiedChinese, viewModel.Loc["SimplifiedChinese"]));
        viewModel.LanguageOptions.Add(new(UiLanguage.English, viewModel.Loc["English"]));
        viewModel.ThemeOptions.Add(new(UiTheme.System, viewModel.Loc["System"]));
        viewModel.ThemeOptions.Add(new(UiTheme.Light, viewModel.Loc["Light"]));
        viewModel.ThemeOptions.Add(new(UiTheme.Dark, viewModel.Loc["Dark"]));

        return new ScreenshotFixture(root, viewModel, new MainWindow(), instance, mod, previousThemeVariant);
    }

    private static void AddMarketOption(
        ICollection<SettingOption<string>> options,
        Action<SettingOption<string>> select,
        string name)
    {
        var option = new SettingOption<string>(string.Empty, name);
        options.Add(option);
        select(option);
    }

    private static void AssertExpectedContent(ScreenshotFixture fixture, ScreenshotState state)
    {
        var visibleText = fixture.Window.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(text => text.IsEffectivelyVisible)
            .Select(text => text.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToArray();
        Assert.Contains("Crystalfly", visibleText);

        switch (state)
        {
            case ScreenshotState.GameVersions:
                Assert.Contains(fixture.ViewModel.Loc["GameVersions"], visibleText);
                Assert.Contains("Hollow Knight 1.5.78.11833", visibleText);
                break;
            case ScreenshotState.Launch:
                Assert.Contains(fixture.Instance.Name, visibleText);
                Assert.Contains(fixture.ViewModel.Loc["Ready"], visibleText);
                break;
            case ScreenshotState.Settings:
                Assert.Contains(fixture.ViewModel.Loc["VersionRoot"], visibleText);
                break;
            case ScreenshotState.MarketList:
                Assert.Contains(fixture.ViewModel.Loc["MarketTitle"], visibleText);
                Assert.Contains(fixture.Mod.Name, visibleText);
                Assert.Contains("ScreenShakeModifier", visibleText);
                break;
            case ScreenshotState.MarketDetail:
                Assert.Contains(fixture.Mod.Name, visibleText);
                Assert.Contains("TheMulhima", visibleText);
                Assert.Contains(fixture.ViewModel.Loc["Install"], visibleText);
                break;
            case ScreenshotState.MarketInstall:
                Assert.Single(fixture.Window.GetVisualDescendants().OfType<CustomDialogControl>());
                Assert.Contains(fixture.Mod.Name, visibleText);
                Assert.Contains(fixture.Instance.Name, visibleText);
                Assert.Contains(fixture.ViewModel.Loc["InstallSelectedMod"], visibleText);
                break;
            case ScreenshotState.InstanceDetail:
                Assert.Contains(fixture.ViewModel.Loc["ManageInstance"], visibleText);
                Assert.Contains(fixture.ViewModel.Loc["Overview"], visibleText);
                Assert.Contains(fixture.Instance.DisplayVersion, visibleText);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, null);
        }
    }

    private static void SetPrivateField(MainViewModel viewModel, string name, object value)
    {
        var field = typeof(MainViewModel).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(viewModel, value);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Crystalfly.slnx")))
        {
            directory = directory.Parent;
        }

        return Assert.IsType<DirectoryInfo>(directory).FullName;
    }

    private sealed class ScreenshotFixture(
        string root,
        MainViewModel viewModel,
        MainWindow window,
        InstanceItemViewModel instance,
        ModManifest mod,
        ThemeVariant? previousThemeVariant) : IAsyncDisposable
    {
        public MainViewModel ViewModel { get; } = viewModel;

        public MainWindow Window { get; } = window;

        public InstanceItemViewModel Instance { get; } = instance;

        public ModManifest Mod { get; } = mod;

        public async Task PrepareAsync(ScreenshotState state)
        {
            ViewModel.SelectedMarketMod = null;
            ViewModel.CurrentManageTab = "Overview";
            ViewModel.CurrentDownloadSection = "GameVersions";
            ViewModel.CurrentPage = state switch
            {
                ScreenshotState.GameVersions => "Downloads",
                ScreenshotState.Settings => "Settings",
                ScreenshotState.MarketList or ScreenshotState.MarketDetail or ScreenshotState.MarketInstall => "Downloads",
                ScreenshotState.InstanceDetail => "Manage",
                _ => "Launch"
            };

            if (state is ScreenshotState.MarketList or ScreenshotState.MarketDetail or ScreenshotState.MarketInstall)
            {
                ViewModel.CurrentDownloadSection = "ModMarket";
                ViewModel.SelectedMarketMod = state == ScreenshotState.MarketList ? null : Mod;
            }

            if (state is ScreenshotState.Launch or ScreenshotState.InstanceDetail)
            {
                ViewModel.SelectedInstance = Instance;
                for (var attempt = 0; attempt < 100 && ViewModel.IsLoadingInstanceDetails; attempt++)
                {
                    await Task.Delay(10);
                }
                ViewModel.IsLoadingInstanceDetails = false;
                ViewModel.CurrentLoaderState = LoaderState.ModdingApi;
                ViewModel.LaunchPreflight = new(true, true, true, true);
                ViewModel.StatusMessage = ViewModel.Loc["Ready"];
            }
        }

        public async Task OpenMarketInstallAsync()
        {
            var install = Window.GetVisualDescendants()
                .OfType<Button>()
                .Single(button => button.IsEffectivelyVisible
                    && AutomationProperties.GetName(button) == ViewModel.Loc["InstallModTitle"]);
            install.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            for (var attempt = 0;
                 attempt < 100 && !Window.GetVisualDescendants().OfType<CustomDialogControl>().Any();
                 attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }
        }

        public async ValueTask DisposeAsync()
        {
            ViewModel.IsBusy = false;
            foreach (var dialog in Window.GetVisualDescendants().OfType<CustomDialogControl>().ToArray())
            {
                dialog.Close();
            }
            typeof(MainWindow)
                .GetField("closeAfterDispose", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(Window, true);
            if (Window.IsVisible)
            {
                Window.Close();
            }
            await ViewModel.DisposeAsync();
            Application.Current!.RequestedThemeVariant = previousThemeVariant;
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed record ScreenshotCase(
        string FileName,
        int Width,
        int Height,
        double RenderScaling,
        ScreenshotState State);

    private enum ScreenshotState
    {
        GameVersions,
        Launch,
        Settings,
        MarketList,
        MarketDetail,
        MarketInstall,
        InstanceDetail
    }
}

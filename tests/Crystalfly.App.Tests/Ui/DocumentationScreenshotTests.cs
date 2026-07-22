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
using Crystalfly.Core.Runtime;
using Ursa.Controls;

namespace Crystalfly.App.Tests.Ui;

public sealed class DocumentationScreenshotTests
{
    private static readonly ScreenshotCase[] Cases =
    [
        new("crystalfly-900x600-zh.jpg", 900, 600, 1d, ScreenshotState.GameVersions),
        new("crystalfly-1280x720-zh.jpg", 1280, 720, 1d, ScreenshotState.Launch),
        new("crystalfly-launch-issues-1280x720-zh.jpg", 1280, 720, 1d, ScreenshotState.LaunchIssues),
        new("crystalfly-launch-issues-overlay-1280x720-zh.jpg", 1280, 720, 1d, ScreenshotState.LaunchIssuesOverlay),
        new("crystalfly-1920x1080-zh.jpg", 1920, 1080, 1.5d, ScreenshotState.Settings),
        new("crystalfly-2560x1440-zh.jpg", 2560, 1440, 2d, ScreenshotState.Launch),
        new("crystalfly-mod-market-list-1280x720-zh.jpg", 1280, 720, 1d, ScreenshotState.MarketList),
        new("crystalfly-mod-market-detail-1280x720-zh.jpg", 1280, 720, 1d, ScreenshotState.MarketDetail),
        new("crystalfly-mod-install-overlay-1280x720-zh.jpg", 1280, 720, 1d, ScreenshotState.MarketInstall),
        new("crystalfly-instance-detail-900x600-zh.jpg", 900, 600, 1d, ScreenshotState.InstanceDetail),
        new("crystalfly-installed-mod-health-1280x720-zh.jpg", 1280, 720, 1d, ScreenshotState.InstalledModHealth),
        new("crystalfly-mod-presets-1280x720-zh.jpg", 1280, 720, 1d, ScreenshotState.ModPresets)
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
            else if (capture.State == ScreenshotState.LaunchIssuesOverlay)
            {
                await fixture.OpenLaunchIssuesAsync();
            }
            else if (capture.State == ScreenshotState.InstalledModHealth)
            {
                fixture.FocusExternalMod();
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
            Id = "hkmod:DebugMod",
            Name = "DebugMod",
            DisplayName = "DebugMod",
            Description = "Enables debugging tools and utilities for practice and route verification.",
            Authors = ["TheMulhima", "Crystalfly maintainers"],
            Tags = ["Utility"],
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
        var viewModel = new MainViewModel(
            Path.Combine(root, "app-data"),
            null,
            null,
            null,
            modContentLoadOverride: (manifest, _) => Task.FromResult(new ModContentLoadResult(
                ModContentLoadStatus.Cached,
                new ModContentDocument
                {
                    RepositoryUrl = manifest.RepositoryUrl!,
                    ReadmeMarkdown = "## DebugMod\n\n面向练习与路线验证的调试工具。",
                    ReleaseNotesMarkdown = "### 1.4.10.5-r2\n\n支持 Crystalfly 精确 Loader 安装。",
                    UpdatedAt = new DateTimeOffset(2026, 7, 22, 0, 0, 0, TimeSpan.Zero)
                },
                null)))
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
        InvokeRebuildMarketCatalog(viewModel);
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
            case ScreenshotState.LaunchIssues:
                Assert.True(fixture.ViewModel.HasLaunchIssues);
                Assert.Contains(fixture.ViewModel.LaunchIssueCountText, visibleText);
                Assert.Contains(fixture.ViewModel.Loc["NeedsAttention"], visibleText);
                Assert.Contains(fixture.Window.GetVisualDescendants().OfType<Border>(), border =>
                    border.IsEffectivelyVisible && border.Classes.Contains("cfp-launch-issue-frame"));
                break;
            case ScreenshotState.LaunchIssuesOverlay:
                Assert.True(fixture.ViewModel.HasLaunchIssues);
                Assert.Single(fixture.Window.GetVisualDescendants().OfType<CustomDialogControl>());
                Assert.Contains(fixture.ViewModel.Loc["LaunchWarningTitle"], visibleText);
                Assert.Contains(fixture.ViewModel.Loc["ForceLaunch"], visibleText);
                Assert.Contains(fixture.ViewModel.Loc["DoNotRemindLaunchWarnings"], visibleText);
                Assert.Contains(fixture.ViewModel.Loc["LaunchIssueMissingDependency"], visibleText);
                Assert.Contains(fixture.ViewModel.Loc["LaunchIssueModModifiedFile"], visibleText);
                break;
            case ScreenshotState.Settings:
                Assert.Contains(fixture.ViewModel.Loc["VersionRoot"], visibleText);
                Assert.Contains(fixture.ViewModel.Loc["OfflineMode"], visibleText);
                break;
            case ScreenshotState.MarketList:
                Assert.Contains(fixture.ViewModel.Loc["MarketTitle"], visibleText);
                Assert.Contains("调试模组", visibleText);
                Assert.Contains(fixture.Mod.Name, visibleText);
                Assert.Contains("ScreenShakeModifier", visibleText);
                break;
            case ScreenshotState.MarketDetail:
                Assert.Contains("调试模组", visibleText);
                Assert.Contains(fixture.Mod.Name, visibleText);
                Assert.Contains("官方英文说明", visibleText);
                Assert.Contains("TheMulhima", visibleText);
                Assert.Contains(fixture.ViewModel.Loc["Install"], visibleText);
                Assert.Contains(fixture.ViewModel.Loc["Readme"], visibleText);
                break;
            case ScreenshotState.MarketInstall:
                Assert.Single(fixture.Window.GetVisualDescendants().OfType<CustomDialogControl>());
                Assert.Contains(fixture.Mod.Name, visibleText);
                Assert.Contains(fixture.Instance.Name, visibleText);
                Assert.Contains(fixture.ViewModel.Loc["AddToDownloadQueue"], visibleText);
                break;
            case ScreenshotState.InstanceDetail:
                Assert.Contains(fixture.ViewModel.Loc["Overview"], visibleText);
                Assert.Contains(fixture.Instance.DisplayVersion, visibleText);
                break;
            case ScreenshotState.InstalledModHealth:
                Assert.Contains(fixture.ViewModel.Loc["InstalledMods"], visibleText);
                Assert.Contains("调试模组", visibleText);
                Assert.Contains(fixture.ViewModel.Loc["ModHealthModifiedFile"], visibleText);
                Assert.Contains(fixture.ViewModel.Loc["Pinned"], visibleText);
                Assert.Contains("External Helper", visibleText);
                Assert.Contains(fixture.ViewModel.Loc["ModHealthUnmanagedExternal"], visibleText);
                Assert.Contains(fixture.Window.GetVisualDescendants().OfType<Button>(), button =>
                    button.IsEffectivelyVisible
                    && AutomationProperties.GetName(button) == fixture.ViewModel.Loc["TakeOverMod"]);
                Assert.Contains(fixture.Window.GetVisualDescendants().OfType<StackPanel>(), panel =>
                    panel.Classes.Contains("cfp-installed-mod-actions") && panel.Opacity == 1d);
                break;
            case ScreenshotState.ModPresets:
                Assert.Contains(fixture.ViewModel.Loc["ModPresets"], visibleText);
                Assert.Contains("日常练习 · 全量 Mod", visibleText);
                Assert.Contains("DebugMod", visibleText);
                Assert.Contains("ScreenShakeModifier", visibleText);
                Assert.Contains(fixture.ViewModel.Loc["ApplyPreset"], visibleText);
                Assert.Contains(fixture.ViewModel.Loc["SharePreset"], visibleText);
                Assert.Contains(fixture.ViewModel.Loc["ImportSharedPreset"], visibleText);
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

    private static void InvokeRebuildMarketCatalog(MainViewModel viewModel)
    {
        var method = typeof(MainViewModel).GetMethod("RebuildMarketCatalog", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(viewModel, null);
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
                ScreenshotState.InstanceDetail or ScreenshotState.InstalledModHealth or ScreenshotState.ModPresets => "Manage",
                _ => "Launch"
            };

            if (state is ScreenshotState.MarketList or ScreenshotState.MarketDetail or ScreenshotState.MarketInstall)
            {
                ViewModel.CurrentDownloadSection = "ModMarket";
                ViewModel.SelectedMarketMod = state == ScreenshotState.MarketList ? null : Mod;
            }

            if (state is ScreenshotState.Launch
                or ScreenshotState.LaunchIssues
                or ScreenshotState.LaunchIssuesOverlay
                or ScreenshotState.InstanceDetail
                or ScreenshotState.InstalledModHealth
                or ScreenshotState.ModPresets)
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

            if (state is ScreenshotState.LaunchIssues or ScreenshotState.LaunchIssuesOverlay)
            {
                ViewModel.LaunchPreflight = new LaunchPreflightResult(
                    true,
                    true,
                    false,
                    true,
                    [
                        new LaunchPreflightIssue
                        {
                            Code = LaunchIssueCode.MissingDependency,
                            Severity = LaunchIssueSeverity.Forceable,
                            SubjectModId = Mod.Id,
                            Arguments = [Mod.Id, "hkmod:Satchel"]
                        },
                        new LaunchPreflightIssue
                        {
                            Code = LaunchIssueCode.ModModifiedFile,
                            Severity = LaunchIssueSeverity.Warning,
                            SubjectModId = Mod.Id,
                            RelativeFilePath = "hollow_knight_Data/Managed/Mods/DebugMod/DebugMod.dll",
                            CurrentFileSha256 = new string('A', 64),
                            Arguments = [Mod.Id, "DebugMod.dll"]
                        },
                        new LaunchPreflightIssue
                        {
                            Code = LaunchIssueCode.UnmanagedExternalMod,
                            Severity = LaunchIssueSeverity.Warning,
                            SubjectModId = "external-helper",
                            Arguments = ["external-helper"]
                        }
                    ]);
                ViewModel.StatusMessage = ViewModel.Loc["NeedsAttention"];
            }

            if (state == ScreenshotState.InstalledModHealth)
            {
                ViewModel.CurrentManageTab = "Mods";
                PrepareInstalledModHealth();
            }

            if (state == ScreenshotState.ModPresets)
            {
                ViewModel.CurrentManageTab = "Presets";
                PrepareModPresets();
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

        public async Task OpenLaunchIssuesAsync()
        {
            var showIssues = Window.GetVisualDescendants()
                .OfType<Button>()
                .Single(button => button.IsEffectivelyVisible
                    && AutomationProperties.GetName(button) == ViewModel.Loc["LaunchIssues"]);
            showIssues.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            for (var attempt = 0;
                 attempt < 100 && !Window.GetVisualDescendants().OfType<CustomDialogControl>().Any();
                 attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }
        }

        public void FocusExternalMod()
        {
            var externalItem = Window.GetVisualDescendants()
                .OfType<ListBoxItem>()
                .Single(item => item.DataContext is InstalledModItemViewModel { IsExternal: true });
            externalItem.Focus();
            Dispatcher.UIThread.RunJobs();
        }

        private void PrepareInstalledModHealth()
        {
            ViewModel.InstalledMods.Clear();
            ViewModel.VisibleInstalledMods.Clear();

            var managedReceipt = new InstalledModReceipt
            {
                Id = Mod.Id,
                Name = Mod.Name,
                Version = Mod.Version,
                LoaderId = Mod.LoaderId,
                InstallRoot = "hollow_knight_Data/Managed/Mods/DebugMod",
                Enabled = true,
                Ownership = ModOwnership.Managed,
                Pinned = true,
                EntryFiles = ["hollow_knight_Data/Managed/Mods/DebugMod/DebugMod.dll"]
            };
            var managedDiscovery = new ModDiscoveryEntry
            {
                Id = managedReceipt.Id,
                Name = managedReceipt.Name,
                LoaderId = managedReceipt.LoaderId,
                InstallRoot = managedReceipt.InstallRoot,
                Enabled = true,
                Ownership = ModOwnership.Managed,
                EntryFiles = managedReceipt.EntryFiles,
                Files = managedReceipt.EntryFiles
            };
            var managed = new InstalledModItemViewModel(
                managedDiscovery,
                managedReceipt,
                new ModHealthReport
                {
                    ModId = managedReceipt.Id,
                    Status = ModHealthStatus.ModifiedFile,
                    ModifiedFiles = managedReceipt.EntryFiles
                },
                Mod,
                static () => { },
                ViewModel.ProjectMarketMod(Mod),
                ViewModel.Loc["Installed"],
                ViewModel.Loc["ModHealthModifiedFile"])
            {
                IsSelected = true
            };

            var externalDiscovery = new ModDiscoveryEntry
            {
                Id = "external-helper",
                Name = "External Helper",
                LoaderId = Mod.LoaderId,
                InstallRoot = "hollow_knight_Data/Managed/Mods/ExternalHelper",
                Enabled = true,
                Ownership = ModOwnership.External,
                EntryFiles = ["hollow_knight_Data/Managed/Mods/ExternalHelper/ExternalHelper.dll"],
                Files = ["hollow_knight_Data/Managed/Mods/ExternalHelper/ExternalHelper.dll"]
            };
            var external = new InstalledModItemViewModel(
                externalDiscovery,
                null,
                new ModHealthReport
                {
                    ModId = externalDiscovery.Id,
                    Status = ModHealthStatus.UnmanagedExternal
                },
                null,
                static () => { },
                ownershipDisplayName: ViewModel.Loc["External"],
                healthDisplayName: ViewModel.Loc["ModHealthUnmanagedExternal"]);

            ViewModel.InstalledMods.Add(managed);
            ViewModel.InstalledMods.Add(external);
            ViewModel.VisibleInstalledMods.Add(managed);
            ViewModel.VisibleInstalledMods.Add(external);
            ViewModel.SelectedInstalledMod = external;
        }

        private void PrepareModPresets()
        {
            var exactPreset = new ModPreset
            {
                Id = "fixture-preset-exact",
                Name = "日常练习 · 全量 Mod",
                GameBuildId = Instance.Record.BuildId,
                LoaderId = Mod.LoaderId,
                ApplyMode = ModPresetApplyMode.Exact,
                Entries =
                [
                    new ModPresetEntry
                    {
                        Id = Mod.Id,
                        Name = Mod.Name,
                        Version = Mod.Version
                    },
                    new ModPresetEntry
                    {
                        Id = "screenshake-modifier",
                        Name = "ScreenShakeModifier",
                        Version = "1.3.0"
                    },
                    new ModPresetEntry
                    {
                        Id = "additional-timelines",
                        Name = "Additional Timelines and Practice Utilities",
                        Version = "2.1.4"
                    }
                ]
            };
            var speedrunPreset = exactPreset with
            {
                Id = "fixture-preset-speedrun",
                Name = "速通练习 · 计时工具",
                ApplyMode = ModPresetApplyMode.Append,
                Entries = exactPreset.Entries.Skip(1).ToArray()
            };

            ViewModel.PresetModeOptions.Clear();
            ViewModel.PresetModeOptions.Add(new(ModPresetApplyMode.Append, ViewModel.Loc["PresetModeAppend"]));
            ViewModel.PresetModeOptions.Add(new(ModPresetApplyMode.Exact, ViewModel.Loc["PresetModeExact"]));
            ViewModel.ModPresets.Clear();
            ViewModel.ModPresets.Add(exactPreset);
            ViewModel.ModPresets.Add(speedrunPreset);
            ViewModel.SelectedPreset = exactPreset;
            ViewModel.SelectedPresetModeOption = ViewModel.PresetModeOptions.Single(option =>
                option.Value == ModPresetApplyMode.Exact);
            ViewModel.HasPresetRestorePoint = true;
            ViewModel.LastPresetShareCode = "fixture-7F4D2A";
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
        LaunchIssues,
        LaunchIssuesOverlay,
        Settings,
        MarketList,
        MarketDetail,
        MarketInstall,
        InstanceDetail,
        InstalledModHealth,
        ModPresets
    }
}

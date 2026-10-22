using System.Reflection;
using Crystalfly.App.ViewModels.DependencyGraph;
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
using Crystalfly.App.ViewModels.Dialogs;
using Crystalfly.App.Views;
using Crystalfly.App.Views.Dialogs;
using Crystalfly.Core.Catalog;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;
using Crystalfly.Core.Runtime;
using Crystalfly.Core.Saves;
using Crystalfly.Core.Snapshots;
using Crystalfly.Core.Speedrun;
using Ursa.Controls;
using SkiaSharp;

namespace Crystalfly.App.Tests.Ui;

public sealed partial class DocumentationScreenshotTests
{

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
        var speedrunTemplate = new SpeedrunTemplate
        {
            Id = "runtime-patches-1578",
            Name = "RuntimePatches 1.5.78",
            BuildId = "1.5.78.11833",
            IsOfficial = true,
            RulesRevision = RuntimePatchesPolicy.RulesRevision,
            FileManifestId = "files-runtime-patches-1578",
            RequiredAssetIds = ["runtime-patches-1578-v1.0.2"]
        };
        var catalog = new GameCatalog
        {
            Loaders = [loader],
            Mods = mods,
            SpeedrunTemplates = [speedrunTemplate]
        };
        var record = new InstanceRecord
        {
            Id = "practice-1578",
            Name = "1.5.78 日常练习",
            RootPath = instanceRoot,
            BuildId = "1.5.78.11833",
            CreatedAt = new DateTimeOffset(2026, 7, 19, 12, 0, 0, TimeSpan.Zero)
        };
        var instance = new InstanceItemViewModel(record, "1.5.78.11833", "Modding API v77", 12);
        var speedrunRoot = Path.Combine(versionRoot, "speedrun-1578");
        Directory.CreateDirectory(speedrunRoot);
        File.WriteAllText(Path.Combine(speedrunRoot, "hollow_knight.exe"), "fixture");
        var speedrunRecord = record with
        {
            Id = "speedrun-1578",
            Name = "1.5.78 RuntimePatches",
            RootPath = speedrunRoot,
            Purpose = InstancePurpose.OfficialSpeedrun,
            ProvisioningMode = InstanceProvisioningMode.FullCopy,
            LoaderId = null,
            SpeedrunTemplateId = speedrunTemplate.Id,
            SpeedrunRulesRevision = speedrunTemplate.RulesRevision
        };
        var speedrunInstance = new InstanceItemViewModel(speedrunRecord, "1.5.78", "Vanilla", 0);
        File.WriteAllText(
            InstanceSidecar.GetMarkerPath(speedrunRoot),
            """
            {
              "schemaVersion": 1,
              "instanceId": "speedrun-1578"
            }
            """);
        var speedrunConfigurationPath = Path.Combine(
            versionRoot,
            ".crystalfly",
            "instances",
            speedrunRecord.Id,
            "local-low",
            RuntimePatchesConfiguration.FileName);
        Directory.CreateDirectory(Path.GetDirectoryName(speedrunConfigurationPath)!);
        File.WriteAllText(
            speedrunConfigurationPath,
            """
            {
              "ScreenShakeModifier": false,
              "MiniSaveStates": false,
              "FasterIntroSkip": false,
              "TextMasher": false
            }
            """);
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
        InvokeRebuildSettingOptions(viewModel);
        SetPrivateField(viewModel, "catalog", catalog);
        SetPrivateField(viewModel, "officialCatalogResult", new OfficialCatalogLoadResult(
            OfficialCatalogLoadStatus.Remote,
            catalog,
            "77",
            mods.Length,
            null));
        var gameDirectory = new GameDirectoryItemViewModel(new GameDirectoryRegistration
        {
            Path = versionRoot,
            DisplayName = "HK Versions",
            Source = GameDirectorySourceKind.Managed
        })
        {
            InstanceCount = 1,
            ScanStatus = viewModel.Loc["ScanReady"]
        };
        viewModel.Instances.GameDirectories.Add(gameDirectory);
        viewModel.Instances.SelectedGameDirectory = gameDirectory;
        viewModel.Instances.Instances.Add(instance);
        viewModel.Instances.Instances.Add(speedrunInstance);
        viewModel.Instances.VisibleInstances.Add(instance);
        viewModel.Instances.VisibleInstances.Add(speedrunInstance);
        viewModel.SpeedrunTemplates.Add(speedrunTemplate);
        viewModel.Instances.SpeedrunInstances.Add(speedrunInstance);
        InvokeRebuildMarketCatalog(viewModel);
        viewModel.DownloadBuilds.Add(new("1.5.78.11833", "Hollow Knight 1.5.78.11833", 9207084990026249690));
        viewModel.VisibleDownloadBuilds.Add(viewModel.DownloadBuilds[0]);
        viewModel.SelectedDownloadBuild = viewModel.DownloadBuilds[0];
        return new ScreenshotFixture(root, viewModel, new MainWindow(), instance, mod, previousThemeVariant);
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

    private static void InvokeRebuildSettingOptions(MainViewModel viewModel)
    {
        var method = typeof(MainViewModel).GetMethod(
            "RebuildSettingOptions",
            BindingFlags.Instance | BindingFlags.NonPublic);
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
            ViewModel.CurrentSettingsSection = "General";
            ViewModel.CurrentPage = state switch
            {
                ScreenshotState.GameVersions => "Downloads",
                ScreenshotState.InstanceSelection => "Versions",
                ScreenshotState.Settings
                    or ScreenshotState.SettingsAccentDialog
                    or ScreenshotState.SettingsBackground or ScreenshotState.SettingsInstanceBackground => "Settings",
                ScreenshotState.MarketList or ScreenshotState.MarketDetail or ScreenshotState.MarketInstall => "Downloads",
                ScreenshotState.Speedrun or ScreenshotState.SpeedrunActivity => "Speedrun",
                ScreenshotState.InstanceDetail
                    or ScreenshotState.InstalledModHealth
                    or ScreenshotState.ModPresets
                    or ScreenshotState.InstanceConfig
                    or ScreenshotState.SaveEditor
                    or ScreenshotState.DependencyGraph => "Manage",
                _ => "Launch"
            };

            if (state is ScreenshotState.SettingsBackground or ScreenshotState.SettingsInstanceBackground)
            {
                await PrepareBackgroundAsync(state == ScreenshotState.SettingsInstanceBackground);
            }

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
                or ScreenshotState.ModPresets
                or ScreenshotState.InstanceConfig
                or ScreenshotState.SaveEditor
                or ScreenshotState.DependencyGraph)
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

            if (state == ScreenshotState.InstanceConfig)
            {
                await PrepareInstanceConfigAsync();
            }

            if (state == ScreenshotState.SaveEditor)
            {
                await PrepareSaveEditorAsync();
            }

            if (state == ScreenshotState.DependencyGraph)
            {
                ViewModel.CurrentManageTab = "Mods";
            }

            if (state == ScreenshotState.Speedrun)
            {
                ViewModel.SelectedSpeedrunInstance = ViewModel.Instances.SpeedrunInstances.Single();
                for (var attempt = 0; attempt < 100 && ViewModel.IsLoadingInstanceDetails; attempt++)
                {
                    await Task.Delay(10);
                }
                ViewModel.SpeedrunStatus = ViewModel.Loc["SpeedrunVerified"];
            }
            if (state == ScreenshotState.SpeedrunActivity)
            {
                ViewModel.CurrentSpeedrunTab = "Activity";
                ViewModel.SpeedrunActivityStatus = ViewModel.Loc["SpeedrunActivityReady"];
                ViewModel.IsSpeedrunActivityLoading = false;
            }
        }

        private async Task PrepareInstanceConfigAsync()
        {
            var configPath = Path.Combine(
                ViewModel.VersionRoot,
                ".crystalfly",
                "instances",
                Instance.Id,
                "local-low",
                AppConfigService.FileName);
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            await File.WriteAllTextAsync(
                configPath,
                """
                [Accessibility]
                ReducedCameraShake=0.25
                ReducedControllerRumble=0.50

                [Practice]
                CustomPracticeFlag=Enabled
                """);
            ViewModel.CurrentManageTab = "Config";
            for (var attempt = 0;
                 attempt < 100 && ViewModel.GameConfig?.IsLoaded != true;
                 attempt++)
            {
                await Task.Delay(10);
            }
        }

        private async Task PrepareSaveEditorAsync()
        {
            var saveStorageRoot = Path.Combine(root, "save-editor");
            var localLow = Path.Combine(saveStorageRoot, "instances", Instance.Id, "local-low");
            Directory.CreateDirectory(localLow);
            var json =
                """
                {
                  "playerData": {
                    "geo": 1250,
                    "health": 9,
                    "hasDash": true,
                    "respawnScene": "Town"
                  }
                }
                """;
            foreach (var slot in new[] { "user1.dat", "user2.dat", "user3.dat", "user4.dat" })
            {
                await SaveFileCodec.EncryptAsync(Path.Combine(localLow, slot), json);
            }

            var editor = new SaveEditorViewModel(
                new NamedSnapshotService(saveStorageRoot, $"Crystalfly.ScreenshotTests.{Guid.NewGuid():N}"),
                Instance.Id,
                null,
                Instance.Name);
            await editor.InitializeAsync();
            ViewModel.CurrentManageTab = "Snapshots";
            ViewModel.SaveEditor = editor;
        }

        public async Task OpenDependencyGraphAsync()
        {
            var nodes = new[]
            {
                new DependencyPlanNodeViewModel(
                    "hkmod:DebugMod",
                    "调试模组",
                    "DebugMod",
                    ViewModel.Loc["Enabled"],
                    DependencyGraphNodeState.Attention,
                    ViewModel.Loc["WillDelete"],
                    PrerequisiteIds: ["hkmod:Satchel"]),


                new DependencyPlanNodeViewModel(
                    "hkmod:Satchel",
                    "Satchel",
                    "Satchel",
                    ViewModel.Loc["Enabled"],
                    DependencyGraphNodeState.Attention,
                    ViewModel.Loc["DependenciesWillBeMissing"],
                    PrerequisiteIds: ["hkmod:CommonUtilities"]),


                new DependencyPlanNodeViewModel(
                    "hkmod:CommonUtilities",
                    "共享工具库",
                    "Common Utilities",
                    ViewModel.Loc["Missing"],
                    DependencyGraphNodeState.Missing,
                    ViewModel.Loc["DependenciesWillBeMissing"])




            };
            var dialog = new DependencyPlanDialogViewModel(
                ViewModel.Loc["ConfirmModUninstallTitle"],
                ViewModel.Loc["DependencyImpact"],
                nodes,
                ViewModel.Loc["Uninstall"],
                ViewModel.Loc["Cancel"],
                canConfirm: true,
                isDangerous: true);
            _ = OverlayDialog.ShowCustomAsync<
                DependencyPlanDialogView,
                DependencyPlanDialogViewModel,
                bool>(
                dialog,
                MainWindow.OverlayHostId,
                new OverlayDialogOptions
                {
                    TopLevelHashCode = Window.GetHashCode(),
                    CanLightDismiss = false,
                    CanDragMove = false,
                    IsCloseButtonVisible = true,
                    CanResize = false
                });
            for (var attempt = 0;
                 attempt < 1000 && !Window.GetVisualDescendants().OfType<CustomDialogControl>().Any();
                 attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }
        }

        public async Task OpenAccentColorDialogAsync()
        {
            var dialog = new AccentColorDialogViewModel(
                ViewModel.Loc["AccentColorPickerTitle"],
                ViewModel.Loc["AccentOriginal"],
                ViewModel.Loc["AccentNew"],
                ViewModel.Loc["AccentHex"],
                ViewModel.Loc["AccentInvalid"],
                ViewModel.Loc["Confirm"],
                ViewModel.Loc["Cancel"],
                ViewModel.AccentColor,
                ViewModel.PreviewAccentColor);
            _ = OverlayDialog.ShowCustomAsync<
                AccentColorDialogView,
                AccentColorDialogViewModel,
                string?>(
                dialog,
                MainWindow.OverlayHostId,
                new OverlayDialogOptions
                {
                    TopLevelHashCode = Window.GetHashCode(),
                    CanLightDismiss = true,
                    CanDragMove = false,
                    IsCloseButtonVisible = true,
                    CanResize = false
                });
            for (var attempt = 0;
                 attempt < 100 && !Window.GetVisualDescendants().OfType<CustomDialogControl>().Any();
                 attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
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
            ViewModel.ModManagement.InstalledMods.Clear();
            ViewModel.ModManagement.VisibleInstalledMods.Clear();

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

            ViewModel.ModManagement.InstalledMods.Add(managed);
            ViewModel.ModManagement.InstalledMods.Add(external);
            ViewModel.ModManagement.VisibleInstalledMods.Add(managed);
            ViewModel.ModManagement.VisibleInstalledMods.Add(external);
            ViewModel.ModManagement.SelectedInstalledMod = external;
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

        private async Task PrepareBackgroundAsync(bool useInstanceOverride)
        {
            ViewModel.SelectedInstance = Instance;
            for (var attempt = 0; attempt < 100 && ViewModel.IsLoadingInstanceDetails; attempt++)
            {
                await Task.Delay(10);
            }

            ViewModel.SelectedBackgroundScope = new(
                BackgroundEditScope.Global,
                ViewModel.Loc["BackgroundScopeGlobal"]);
            await ViewModel.SetBackgroundImageAsync(CreateBackgroundImage("global-background.png", false));
            ViewModel.BackgroundOpacityPercent = 42;
            await Task.Delay(350);

            if (!useInstanceOverride)
            {
                return;
            }

            ViewModel.SelectedBackgroundScope = new(
                BackgroundEditScope.CurrentInstance,
                ViewModel.Loc["BackgroundScopeInstance"]);
            await ViewModel.SetBackgroundImageAsync(CreateBackgroundImage("instance-background.png", true));
            ViewModel.BackgroundOpacityPercent = 58;
            await Task.Delay(350);
        }

        public void ShowBackgroundSettings()
        {
            var card = Window.GetVisualDescendants()
                .OfType<Border>()
                .Single(border => AutomationProperties.GetName(border) == ViewModel.Loc["BackgroundImage"]);
            card.BringIntoView();
        }

        private string CreateBackgroundImage(string fileName, bool instance)
        {
            var path = Path.Combine(root, fileName);
            using var bitmap = new SKBitmap(1600, 900);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(instance ? new SKColor(36, 16, 52) : new SKColor(8, 35, 48));
            using var paint = new SKPaint { IsAntialias = true };
            paint.Color = instance ? new SKColor(126, 34, 206) : new SKColor(14, 116, 144);
            canvas.DrawCircle(1280, 180, 360, paint);
            paint.Color = instance ? new SKColor(190, 24, 93) : new SKColor(15, 108, 189);
            canvas.DrawRect(0, 600, 1600, 300, paint);
            paint.Color = new SKColor(255, 255, 255, 42);
            var crystal = new SKPath();
            crystal.MoveTo(220, 720);
            crystal.LineTo(520, 180);
            crystal.LineTo(760, 720);
            crystal.Close();
            canvas.DrawPath(crystal, paint);
            using var image = SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 92);
            using var stream = File.Create(path);
            encoded.SaveTo(stream);
            return path;
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

    private enum ScreenshotState
    {
        GameVersions,
        Launch,
        InstanceSelection,
        LaunchIssues,
        LaunchIssuesOverlay,
        Settings,
        SettingsAccentDialog,
        SettingsBackground,
        SettingsInstanceBackground,
        MarketList,
        MarketDetail,
        MarketInstall,
        InstanceDetail,
        InstalledModHealth,
        ModPresets,
        InstanceConfig,
        SaveEditor,
        DependencyGraph,
        Speedrun,
        SpeedrunActivity
    }
}

using Crystalfly.App.Downloads;
using Crystalfly.App.ViewModels;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Catalog;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Loaders;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;
using Crystalfly.Core.Networking;
using Crystalfly.Core.Runtime;
using Crystalfly.Core.Serialization;
using Crystalfly.Core.Speedrun;
using Crystalfly.Steam.Downloads;
using Crystalfly.Steam.Security;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class MainViewModelStateTests : IDisposable
{
    private readonly TestDirectory applicationData = new();

    [Fact]
    public async Task Speedrun_activity_tab_establishes_baseline_then_detects_new_record()
    {
        string root = applicationData.CreateDirectory("speedrun-leaderboard");
        using var policy = new NetworkPolicy();
        using var httpClient = new HttpClient(new SpeedrunResponseHandler());
        var speedrunClient = new SpeedrunComClient(
            httpClient,
            Path.Combine(root, "speedrun-cache"),
            policy);
        await using var viewModel = new MainViewModel(
            root,
            speedrunComClientOverride: speedrunClient)
        {
            CurrentPage = "Speedrun",
            CurrentSpeedrunTab = "Activity"
        };

        await viewModel.RefreshSpeedrunActivityCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsSpeedrunActivityTab);
        Assert.Empty(viewModel.SpeedrunActivities);
        Assert.True(File.Exists(Path.Combine(root, "speedrun-activity.json")));
        Assert.False(viewModel.IsSpeedrunActivityLoading);
        Assert.NotEmpty(viewModel.SpeedrunActivityStatus);
    }

    [Fact]
    public async Task Speedrun_activity_filter_keeps_only_selected_game()
    {
        string root = applicationData.CreateDirectory("speedrun-partial-leaderboard");
        using var policy = new NetworkPolicy();
        using var httpClient = new HttpClient(new PartialSpeedrunResponseHandler());
        var speedrunClient = new SpeedrunComClient(
            httpClient,
            Path.Combine(root, "speedrun-cache"),
            policy);
        await using var viewModel = new MainViewModel(
            root,
            speedrunComClientOverride: speedrunClient)
        {
            CurrentPage = "Speedrun",
            CurrentSpeedrunTab = "Activity"
        };
        viewModel.SpeedrunActivities.Add(new(new(
            "hollow", SpeedrunActivityKind.WorldRecord,
            new(SpeedrunGame.HollowKnight, "c", "Any%", null, null, []),
            new("hollow", 1, "Runner", "PT1M", 60, DateTimeOffset.UtcNow, null),
            DateTimeOffset.UtcNow), "世界纪录", "空洞骑士 · Any%"));
        viewModel.SpeedrunActivities.Add(new(new(
            "silk", SpeedrunActivityKind.SecondPlace,
            new(SpeedrunGame.Silksong, "c", "Any%", null, null, []),
            new("silk", 2, "Runner", "PT2M", 120, DateTimeOffset.UtcNow, null),
            DateTimeOffset.UtcNow), "第二名", "空洞骑士：丝之歌 · Any%"));

        viewModel.SelectSpeedrunActivityFilterCommand.Execute("HollowKnight");

        Assert.Equal("hollow", Assert.Single(viewModel.VisibleSpeedrunActivities).RunId);
    }

    [Fact]
    public async Task Speedrun_selection_projects_runtime_patches_capabilities_and_legacy_state()
    {
        string root = applicationData.CreateDirectory("speedrun-state");
        await using var viewModel = new MainViewModel(root)
        {
            VersionRoot = root
        };
        var current = new InstanceItemViewModel(
            Instance("runtime", root) with
            {
                Name = "RuntimePatches",
                BuildId = "1.5.78.11833",
                Purpose = InstancePurpose.OfficialSpeedrun,
                SpeedrunTemplateId = "runtime-patches-1578"
            },
            "1.5.78",
            "Vanilla",
            0);

        viewModel.Instances.Instances.Add(current);
        viewModel.SelectedSpeedrunInstance = current;

        Assert.True(viewModel.IsSelectedSpeedrunCurrent);
        Assert.False(viewModel.IsSelectedSpeedrunLegacy);
        Assert.False(viewModel.IsScreenShakeModifierAvailable);
        Assert.True(viewModel.IsMiniSaveStatesAvailable);
        Assert.True(viewModel.IsFasterIntroSkipAvailable);
        Assert.True(viewModel.IsTextMasherAvailable);

        viewModel.SelectedSpeedrunInstance = current with
        {
            Record = current.Record with { SpeedrunTemplateId = "race-1578" }
        };

        Assert.True(viewModel.IsSelectedSpeedrunLegacy);
        Assert.False(viewModel.IsSelectedSpeedrunCurrent);
        Assert.Equal("runtime-patches-1578", viewModel.SelectedSpeedrunTemplate?.Id);
        Assert.Equal(RuntimePatchesFeature.MiniSaveStates
            | RuntimePatchesFeature.FasterIntroSkip
            | RuntimePatchesFeature.TextMasher, viewModel.SelectedSpeedrunSupportedFeatures);
    }

    [Fact]
    public async Task Speedrun_verification_keeps_report_path_out_of_global_status()
    {
        using var test = new TestDirectory();
        string versionRoot = test.CreateDirectory("versions");
        string instanceRoot = test.CreateDirectory("versions", "speedrun-copy");
        string managedRoot = test.CreateDirectory(
            "versions",
            "speedrun-copy",
            "hollow_knight_Data",
            "Managed");
        string executablePath = Path.Combine(instanceRoot, "hollow_knight.exe");
        string unityPath = Path.Combine(instanceRoot, "UnityPlayer.dll");
        string managersPath = Path.Combine(instanceRoot, "hollow_knight_Data", "globalgamemanagers");
        string runtimePatchesPath = Path.Combine(managedRoot, "Assembly-CSharp.dll");
        await File.WriteAllTextAsync(executablePath, "game");
        await File.WriteAllTextAsync(unityPath, "unity");
        await File.WriteAllTextAsync(managersPath, "managers");
        await File.WriteAllTextAsync(runtimePatchesPath, "runtime-patches");

        const string buildId = "1.5.78.11833";
        string templateId = RuntimePatchesPolicy.GetTemplateId(buildId)!;
        string assetId = RuntimePatchesPolicy.GetAssetId(buildId)!;
        var build = new GameBuild
        {
            Id = buildId,
            DisplayVersion = buildId,
            DepotId = 367521,
            ManifestId = "1",
            ExecutableSha256 = FileSha256(executablePath),
            UnityPlayerSha256 = FileSha256(unityPath),
            GlobalGameManagersSha256 = FileSha256(managersPath)
        };
        var template = new SpeedrunTemplate
        {
            Id = templateId,
            Name = $"RuntimePatches {buildId}",
            BuildId = buildId,
            IsOfficial = true,
            RulesRevision = RuntimePatchesPolicy.RulesRevision,
            FileManifestId = $"files-{templateId}",
            RequiredAssetIds = [assetId]
        };
        var instance = Instance("speedrun-copy", instanceRoot) with
        {
            BuildId = buildId,
            Purpose = InstancePurpose.OfficialSpeedrun,
            ProvisioningMode = InstanceProvisioningMode.FullCopy,
            SpeedrunTemplateId = templateId,
            SpeedrunRulesRevision = template.RulesRevision
        };
        var fileManifest = new SpeedrunFileManifest
        {
            Id = template.FileManifestId,
            BuildId = buildId,
            RulesRevision = template.RulesRevision,
            Files =
            [
                new SpeedrunFileRule
                {
                    RelativePath = "hollow_knight_Data/Managed/Assembly-CSharp.dll",
                    Sha256 = FileSha256(runtimePatchesPath),
                    Kind = SpeedrunFileKind.Tool,
                    AssetId = assetId,
                    AssetVersion = "1.0.2"
                }
            ]
        };
        string configurationPath = Path.Combine(
            versionRoot,
            ".crystalfly",
            "instances",
            instance.Id,
            "local-low",
            RuntimePatchesConfiguration.FileName);
        await RuntimePatchesConfiguration.WriteAsync(
            configurationPath,
            new RuntimePatchesConfiguration { MiniSaveStates = true });
        await using var viewModel = new MainViewModel(test.CreateDirectory("app-data"))
        {
            VersionRoot = versionRoot,
            StatusMessage = "ready"
        };
        SetCatalog(viewModel, new GameCatalog
        {
            Builds = [build],
            SpeedrunTemplates = [template],
            SpeedrunFileManifests = [fileManifest]
        });
        var method = typeof(MainViewModel).GetMethod(
            "VerifySpeedrunLaunchAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        await Assert.IsAssignableFrom<Task>(method.Invoke(viewModel, [instance]));

        Assert.Equal(viewModel.Loc["SpeedrunVerifiedWithWarnings"], viewModel.SpeedrunStatus);
        Assert.Equal(viewModel.SpeedrunStatus, viewModel.SpeedrunReminderText);
        Assert.True(viewModel.HasSpeedrunReminder);
        Assert.True(viewModel.HasSpeedrunReport);
        Assert.Equal("ready", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Speedrun_reminder_can_be_dismissed()
    {
        using var test = new TestDirectory();
        await using var viewModel = new MainViewModel(test.CreateDirectory("app-data"))
        {
            SpeedrunReminderText = "warning"
        };

        viewModel.DismissSpeedrunReminderCommand.Execute(null);

        Assert.False(viewModel.HasSpeedrunReminder);
    }

    [Fact]
    public async Task Selecting_market_mod_loads_content_without_blocking_and_ignores_stale_result()
    {
        using var test = new TestDirectory();
        var firstCompletion = new TaskCompletionSource<ModContentLoadResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var first = MarketManifest("hkmod:First", "First");
        var second = MarketManifest("hkmod:Second", "Second");
        await using var viewModel = new MainViewModel(
            test.CreateDirectory("app-data"),
            null,
            null,
            null,
            modContentLoadOverride: (manifest, _) =>
                string.Equals(manifest.Id, first.Id, StringComparison.OrdinalIgnoreCase)
                    ? firstCompletion.Task
                    : Task.FromResult(ContentResult(manifest, "# Second README")));

        viewModel.SelectedMarketMod = first;
        Assert.True(viewModel.IsLoadingSelectedModContent);
        viewModel.SelectedMarketMod = second;

        await WaitUntilAsync(() => !viewModel.IsLoadingSelectedModContent);
        Assert.Equal("# Second README", viewModel.SelectedModReadmeMarkdown);
        Assert.Equal(ModContentLoadStatus.Remote, viewModel.SelectedModContentStatus);

        firstCompletion.SetResult(ContentResult(first, "# Stale README"));
        await Task.Yield();

        Assert.Equal("# Second README", viewModel.SelectedModReadmeMarkdown);
        viewModel.SelectedMarketMod = null;
        Assert.Equal(string.Empty, viewModel.SelectedModReadmeMarkdown);
        Assert.False(viewModel.HasSelectedModReadme);
    }

    [Theory]
    [InlineData(UiLanguage.SimplifiedChinese, "zh-CN")]
    [InlineData(UiLanguage.English, "en-US")]
    public void Localization_uses_supported_culture_for_explicit_language(
        UiLanguage language,
        string expectedCulture)
    {
        var localization = new LocalizationViewModel();

        localization.Apply(language);

        Assert.Equal(expectedCulture, localization.Culture.Name);
    }

    [Fact]
    public void Localization_normalizes_follow_system_culture()
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            var localization = new LocalizationViewModel();
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-Hans");
            localization.Apply(UiLanguage.FollowSystem);
            Assert.Equal("zh-CN", localization.Culture.Name);

            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-AU");
            localization.Apply(UiLanguage.FollowSystem);
            Assert.Equal("en-US", localization.Culture.Name);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Fact]
    public async Task Injected_application_data_root_keeps_real_settings_unchanged()
    {
        using var test = new TestDirectory();
        var applicationDataRoot = test.CreateDirectory("app-data");
        var realSettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Crystalfly",
            "settings.json");
        var before = ReadFileHash(realSettingsPath);
        var viewModel = new MainViewModel(applicationDataRoot);

        viewModel.SelectedLanguage = new(
            Crystalfly.Core.Configuration.UiLanguage.English,
            "English");
        await viewModel.DisposeAsync();

        Assert.Equal(before, ReadFileHash(realSettingsPath));
    }

    [Fact]
    public async Task Dispose_persists_latest_language_theme_and_instance()
    {
        using var test = new TestDirectory();
        var applicationDataRoot = test.CreateDirectory("app-data");
        var versionRoot = test.CreateDirectory("versions");
        var instanceRoot = test.CreateDirectory("versions", "practice");
        var viewModel = new MainViewModel(applicationDataRoot)
        {
            VersionRoot = versionRoot
        };

        viewModel.SelectedLanguage = new(UiLanguage.English, "English");
        viewModel.SelectedTheme = new(UiTheme.Dark, "Dark");
        viewModel.SelectedMotionPreference = new(UiMotionPreference.Off, "Off");
        viewModel.SetAccentColor("#BE185D");
        viewModel.SelectedGitHubRoute = new(GitHubDownloadRoute.Mirror, "GitHub mirror");
        viewModel.SelectedInstance = new(
            Instance("practice", instanceRoot),
            "1.5.78.11833",
            "Vanilla",
            0);
        await viewModel.DisposeAsync();

        var saved = await CrystalflySettingsStore.LoadAsync(
            Path.Combine(applicationDataRoot, "settings.json"));
        Assert.Equal(UiLanguage.English, saved.Language);
        Assert.Equal(UiTheme.Dark, saved.Theme);
        Assert.Equal(UiMotionPreference.Off, saved.MotionPreference);
        Assert.Equal("#BE185D", saved.AccentColor);
        Assert.Equal(GitHubDownloadRoute.Mirror, saved.GitHubDownloadRoute);
        Assert.Equal("practice", saved.CurrentInstanceId);
    }

    [Fact]
    public async Task Initialize_keeps_missing_registered_directory_visible_and_uses_existing_fallback()
    {
        using var test = new TestDirectory();
        var applicationDataRoot = test.CreateDirectory("app-data");
        var missingRoot = Path.Combine(Path.GetDirectoryName(applicationDataRoot)!, "missing");
        var existingRoot = test.CreateDirectory("existing");
        await CrystalflySettingsStore.SaveAsync(
            Path.Combine(applicationDataRoot, "settings.json"),
            new CrystalflySettings
            {
                VersionRoot = missingRoot,
                GameDirectoryDiscoveryCompleted = true,
                GameDirectories =
                [
                    new GameDirectoryRegistration { Path = missingRoot, DisplayName = "Missing" },
                    new GameDirectoryRegistration { Path = existingRoot, DisplayName = "Existing" }
                ]
            });

        await using var viewModel = new MainViewModel(applicationDataRoot);
        await viewModel.InitializeAsync();

        Assert.Equal(2, viewModel.Instances.GameDirectories.Count);
        Assert.Equal(existingRoot, viewModel.Instances.SelectedGameDirectory?.Path);
        Assert.Equal(viewModel.Loc["ScanFailed"], viewModel.Instances.GameDirectories[0].ScanStatus);
    }

    [Fact]
    public async Task Accent_color_options_expose_seven_presets_and_one_custom_choice()
    {
        var viewModel = CreateViewModel();
        InvokeRebuildSettingOptions(viewModel);

        Assert.Equal(8, viewModel.AccentColorOptions.Count);
        Assert.Equal(
            AccentColorPalette.Presets,
            viewModel.AccentColorOptions.Where(option => !option.IsCustom).Select(option => option.Hex));
        Assert.True(viewModel.AccentColorOptions[^1].IsCustom);
        Assert.True(viewModel.AccentColorOptions[0].IsSelected);

        viewModel.PreviewAccentColor("#FDE68A");

        Assert.True(viewModel.AccentColorOptions[^1].IsSelected);
        await viewModel.DisposeAsync();
    }

    [Fact]
    public async Task Initialize_applies_persisted_global_offline_mode()
    {
        using var test = new TestDirectory();
        var applicationDataRoot = test.CreateDirectory("app-data");
        await CrystalflySettingsStore.SaveAsync(
            Path.Combine(applicationDataRoot, "settings.json"),
            new CrystalflySettings { OfflineMode = true });

        await using var viewModel = new MainViewModel(applicationDataRoot);
        await viewModel.InitializeAsync();

        Assert.True(viewModel.IsOfflineMode);
    }

    [Fact]
    public async Task Initialize_does_not_wait_for_remote_catalog_refresh()
    {
        using var test = new TestDirectory();
        var catalogStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCatalog = new TaskCompletionSource<GameCatalog>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using var viewModel = new MainViewModel(test.CreateDirectory("app-data"));
        SetPrivateField(
            viewModel,
            "catalogLoader",
            new Func<CancellationToken, Task<GameCatalog>>(async cancellationToken =>
            {
                catalogStarted.TrySetResult();
                return await releaseCatalog.Task.WaitAsync(cancellationToken);
            }));
        var backgroundBuildAdded = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.DownloadBuilds.CollectionChanged += (_, args) =>
        {
            if (args.NewItems?.OfType<DownloadBuildOption>().Any(build =>
                    build.BuildId == "background-build") == true)
            {
                backgroundBuildAdded.TrySetResult();
            }
        };

        try
        {
            var initialization = viewModel.InitializeAsync();
            await catalogStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await initialization.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.False(releaseCatalog.Task.IsCompleted);

            releaseCatalog.TrySetResult(new GameCatalog
            {
                Builds =
                [
                    new GameBuild
                    {
                        Id = "background-build",
                        DisplayVersion = "Background build",
                        ManifestId = "42",
                        ExecutableSha256 = new string('A', 64),
                        GlobalGameManagersSha256 = new string('B', 64)
                    }
                ]
            });
            await backgroundBuildAdded.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            releaseCatalog.TrySetResult(new GameCatalog());
        }
    }

    [Fact]
    public async Task Background_catalog_refresh_does_not_swallow_instance_row_selection()
    {
        using var test = new TestDirectory();
        var applicationDataRoot = test.CreateDirectory("app-data");
        var versionRoot = test.CreateDirectory("versions");
        var firstRecord = Instance("first", test.CreateDirectory("versions", "first"));
        var secondRecord = Instance("second", test.CreateDirectory("versions", "second"));
        await CrystalflySettingsStore.SaveAsync(
            Path.Combine(applicationDataRoot, "settings.json"),
            new CrystalflySettings
            {
                VersionRoot = versionRoot,
                CurrentInstanceId = firstRecord.Id
            });
        var releaseCatalog = new TaskCompletionSource<GameCatalog>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var backgroundDiscoveryStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseBackgroundDiscovery = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var discoveryCalls = 0;
        await using var viewModel = new MainViewModel(applicationDataRoot);
        SetPrivateField(
            viewModel,
            "catalogLoader",
            new Func<CancellationToken, Task<GameCatalog>>(cancellationToken =>
                releaseCatalog.Task.WaitAsync(cancellationToken)));
        SetPrivateField(viewModel, "steamReconnect", new Func<Task>(() => Task.CompletedTask));
        SetPrivateField(
            viewModel,
            "instanceDiscovery",
            new Func<string, GameCatalog, CancellationToken, Task<IReadOnlyList<InstanceRecord>>>(
                async (_, _, cancellationToken) =>
                {
                    if (Interlocked.Increment(ref discoveryCalls) > 1)
                    {
                        backgroundDiscoveryStarted.TrySetResult();
                        await releaseBackgroundDiscovery.Task.WaitAsync(cancellationToken);
                    }
                    return [firstRecord, secondRecord];
                }));

        try
        {
            await viewModel.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(5));
            viewModel.CurrentPage = "Versions";
            var second = viewModel.Instances.Instances.Single(instance => instance.Id == secondRecord.Id);

            releaseCatalog.TrySetResult(new GameCatalog());
            await backgroundDiscoveryStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            viewModel.Instances.SelectInstanceForLaunchCommand.Execute(second);

            Assert.Same(second, viewModel.SelectedInstance);
            Assert.Equal("Launch", viewModel.CurrentPage);
        }
        finally
        {
            releaseCatalog.TrySetResult(new GameCatalog());
            releaseBackgroundDiscovery.TrySetResult();
        }
    }

    [Fact]
    public void Download_build_search_keeps_every_catalog_build_and_matches_manifest_ids()
    {
        using var test = new TestDirectory();
        var viewModel = new MainViewModel(test.CreateDirectory("app-data"));
        var builds = Enumerable.Range(1, 6).Select(index => new GameBuild
        {
            Id = $"build-{index}",
            DisplayVersion = $"1.5.{index}",
            ManifestId = (1000 + index).ToString(CultureInfo.InvariantCulture),
            ExecutableSha256 = new string('A', 64),
            GlobalGameManagersSha256 = new string('B', 64)
        }).ToArray();
        SetPrivateField(viewModel, "catalog", new GameCatalog { Builds = builds });
        var populate = typeof(MainViewModel).GetMethod("PopulateDownloadBuilds", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(populate);
        populate.Invoke(viewModel, null);

        Assert.Equal(7, viewModel.DownloadBuilds.Count);
        viewModel.DownloadBuildSearchText = "1005";

        var match = Assert.Single(viewModel.VisibleDownloadBuilds);
        Assert.Equal("build-5", match.BuildId);
    }

    [Fact]
    public async Task Background_catalog_failure_does_not_escape_disposal()
    {
        using var test = new TestDirectory();
        var refreshStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var viewModel = new MainViewModel(test.CreateDirectory("app-data"));
        SetPrivateField(
            viewModel,
            "catalogLoader",
            new Func<CancellationToken, Task<GameCatalog>>(_ =>
            {
                refreshStarted.TrySetResult();
                return Task.FromException<GameCatalog>(new FormatException("bad catalog"));
            }));

        await viewModel.InitializeAsync();
        await refreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await viewModel.DisposeAsync();
    }

    [Fact]
    public async Task Refresh_returns_while_version_directory_discovery_is_still_running()
    {
        using var test = new TestDirectory();
        var versionRoot = test.CreateDirectory("versions");
        await using var viewModel = new MainViewModel(test.CreateDirectory("app-data"))
        {
            VersionRoot = versionRoot
        };
        var discoveryStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDiscovery = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        SetPrivateField(
            viewModel,
            "instanceDiscovery",
            new Func<string, GameCatalog, CancellationToken, Task<IReadOnlyList<InstanceRecord>>>(
                (_, _, cancellationToken) =>
                {
                    discoveryStarted.TrySetResult();
                    releaseDiscovery.Task.Wait(cancellationToken);
                    return Task.FromResult<IReadOnlyList<InstanceRecord>>([]);
                }));

        Task? refresh = null;
        var refreshReturned = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var invokeThread = new Thread(() =>
        {
            try
            {
                refresh = InvokeRefreshAsync(viewModel);
                refreshReturned.TrySetResult();
            }
            catch (Exception exception)
            {
                refreshReturned.TrySetException(exception);
            }
        })
        {
            IsBackground = true
        };

        try
        {
            invokeThread.Start();
            await discoveryStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await refreshReturned.Task.WaitAsync(TimeSpan.FromSeconds(5));

            releaseDiscovery.TrySetResult();
            Assert.NotNull(refresh);
            await refresh.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(invokeThread.Join(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            releaseDiscovery.TrySetResult();
        }
    }

    [Fact]
    public async Task Initialize_starts_version_scan_without_waiting_for_Steam_reconnect()
    {
        using var test = new TestDirectory();
        var applicationDataRoot = test.CreateDirectory("app-data");
        var versionRoot = test.CreateDirectory("versions");
        await CrystalflySettingsStore.SaveAsync(
            Path.Combine(applicationDataRoot, "settings.json"),
            new CrystalflySettings { VersionRoot = versionRoot });
        await using var viewModel = new MainViewModel(applicationDataRoot);
        var reconnectStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseReconnect = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var discoveryStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        SetPrivateField(
            viewModel,
            "catalogLoader",
            new Func<CancellationToken, Task<GameCatalog>>(_ =>
                Task.FromResult(new GameCatalog())));
        SetPrivateField(
            viewModel,
            "steamReconnect",
            new Func<Task>(async () =>
            {
                reconnectStarted.TrySetResult();
                await releaseReconnect.Task;
            }));
        SetPrivateField(
            viewModel,
            "instanceDiscovery",
            new Func<string, GameCatalog, CancellationToken, Task<IReadOnlyList<InstanceRecord>>>(
                (_, _, _) =>
                {
                    discoveryStarted.TrySetResult();
                    return Task.FromResult<IReadOnlyList<InstanceRecord>>([]);
                }));

        try
        {
            var initialization = viewModel.InitializeAsync();
            await Task.WhenAll(
                reconnectStarted.Task,
                discoveryStarted.Task).WaitAsync(TimeSpan.FromSeconds(5));
            await initialization.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.False(releaseReconnect.Task.IsCompleted);
        }
        finally
        {
            releaseReconnect.TrySetResult();
        }
    }

    [Fact]
    public async Task Initialize_restores_queue_and_starts_catalog_load_while_version_scan_is_blocked()
    {
        using var test = new TestDirectory();
        var applicationDataRoot = test.CreateDirectory("app-data");
        var versionRoot = test.CreateDirectory("versions");
        await AtomicJsonStore.WriteAsync(
            Path.Combine(applicationDataRoot, "download-queue.json"),
            new[] { QueueGroup("restored", DownloadQueueGroupState.Completed) });
        await CrystalflySettingsStore.SaveAsync(
            Path.Combine(applicationDataRoot, "settings.json"),
            new CrystalflySettings { VersionRoot = versionRoot });
        var catalogStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var discoveryStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDiscovery = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using var viewModel = new MainViewModel(applicationDataRoot);
        SetPrivateField(
            viewModel,
            "catalogLoader",
            new Func<CancellationToken, Task<GameCatalog>>(cancellationToken =>
            {
                catalogStarted.TrySetResult();
                return Task.FromResult(new GameCatalog());
            }));
        SetPrivateField(viewModel, "steamReconnect", new Func<Task>(() => Task.CompletedTask));
        SetPrivateField(
            viewModel,
            "instanceDiscovery",
            new Func<string, GameCatalog, CancellationToken, Task<IReadOnlyList<InstanceRecord>>>(
                async (_, _, cancellationToken) =>
                {
                    discoveryStarted.TrySetResult();
                    await releaseDiscovery.Task.WaitAsync(cancellationToken);
                    return [];
                }));

        var initialization = viewModel.InitializeAsync();
        try
        {
            await discoveryStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            // The download queue restore and the catalog load start before the
            // instance scan, so both finish while the scan is still blocked.
            Assert.True(catalogStarted.Task.IsCompleted);
            await WaitUntilAsync(() => viewModel.DownloadCenter.DownloadQueue.Groups.Count == 1);
            Assert.Equal("restored", viewModel.DownloadCenter.DownloadQueue.Groups[0].Id);
            Assert.False(initialization.IsCompleted);
        }
        finally
        {
            releaseDiscovery.TrySetResult();
        }

        await initialization.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("restored", viewModel.DownloadCenter.DownloadQueue.Groups[0].Id);
    }

    [Fact]
    public async Task Offline_mode_change_is_persisted_without_clearing_other_settings()
    {
        using var test = new TestDirectory();
        var applicationDataRoot = test.CreateDirectory("app-data");
        var viewModel = new MainViewModel(applicationDataRoot);
        await viewModel.InitializeAsync();

        viewModel.IsOfflineMode = true;
        await viewModel.DisposeAsync();

        var saved = await CrystalflySettingsStore.LoadAsync(
            Path.Combine(applicationDataRoot, "settings.json"));
        Assert.True(saved.OfflineMode);
        Assert.Equal(GitHubDownloadRoute.Direct, saved.GitHubDownloadRoute);
    }

    [Fact]
    public async Task Offline_mode_prevents_manual_Steam_sign_in()
    {
        var signInCalled = false;
        await using var viewModel = new MainViewModel(
            applicationData.CreateDirectory("offline-sign-in"),
            launchOverride: null,
            downloadOverride: null,
            disposeSteamOverride: null,
            qrSignInOverride: _ =>
            {
                signInCalled = true;
                return Task.FromResult(new RefreshTokenCredential("unused", "unused"));
            })
        {
            IsOfflineMode = true
        };

        await viewModel.SignInWithQrCommand.ExecuteAsync(null);

        Assert.False(signInCalled);
        Assert.False(viewModel.IsSteamLoggedIn);
        Assert.Equal(viewModel.Loc["OfflineMode"], viewModel.SteamStatus);
        Assert.Equal(viewModel.Loc["OfflineModeHint"], viewModel.ErrorMessage);
    }

    [Fact]
    public async Task Enabling_offline_mode_cancels_running_Steam_sign_in()
    {
        var signInStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var signInCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var viewModel = new MainViewModel(
            applicationData.CreateDirectory("offline-cancels-sign-in"),
            launchOverride: null,
            downloadOverride: null,
            disposeSteamOverride: null,
            qrSignInOverride: async cancellationToken =>
            {
                signInStarted.SetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    return new RefreshTokenCredential("unused", "unused");
                }
                finally
                {
                    signInCancelled.SetResult();
                }
            });

        var signIn = viewModel.SignInWithQrCommand.ExecuteAsync(null);
        await signInStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        viewModel.IsOfflineMode = true;

        await signInCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await signIn.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(viewModel.IsSteamLoggedIn);
        Assert.Equal(viewModel.Loc["OfflineMode"], viewModel.SteamStatus);
    }

    [Fact]
    public async Task GitHub_latency_test_reports_both_routes_without_switching_selection()
    {
        var tested = false;
        await using var viewModel = new MainViewModel(
            applicationData.CreateDirectory("latency-app-data"),
            null,
            null,
            null,
            null,
            null,
            null,
            _ =>
            {
                tested = true;
                return Task.FromResult(new GitHubRouteLatencyTestResult(
                    new GitHubRouteLatencyResult(
                        GitHubDownloadRoute.Direct,
                        GitHubRouteLatencyStatus.Success,
                        TimeSpan.FromMilliseconds(42)),
                    new GitHubRouteLatencyResult(
                        GitHubDownloadRoute.Mirror,
                        GitHubRouteLatencyStatus.Timeout,
                        null)));
            });
        viewModel.SelectedGitHubRoute = new(GitHubDownloadRoute.Mirror, "GitHub mirror");

        await viewModel.TestGitHubLatencyCommand.ExecuteAsync(null);

        Assert.True(tested);
        Assert.Equal(GitHubDownloadRoute.Mirror, viewModel.SelectedGitHubRoute.Value);
        Assert.Equal("42 ms", viewModel.GitHubDirectLatency);
        Assert.Equal(viewModel.Loc["LatencyTimeout"], viewModel.GitHubMirrorLatency);
        Assert.False(viewModel.IsTestingGitHubLatency);
    }

    [Fact]
    public async Task Selecting_instance_defaults_to_its_only_compatible_loader()
    {
        var viewModel = CreateViewModel();
        var loader = new LoaderManifest
        {
            Id = "modding-api-77",
            Name = "Modding API",
            Version = "77",
            DownloadUrl = "https://example.invalid/loader.zip",
            Sha256 = new string('A', 64),
            SupportedBuildIds = ["1.5.78.11833"]
        };
        SetCatalog(viewModel, new GameCatalog { Loaders = [loader] });

        viewModel.SelectedInstance = new InstanceItemViewModel(
            Instance("practice", applicationData.CreateDirectory("instance")) with
            {
                BuildId = "1.5.78.11833"
            },
            "1.5.78.11833",
            "Vanilla",
            0);

        Assert.Same(loader, viewModel.SelectedLoader);
        await viewModel.DisposeAsync();
    }

    [Fact]
    public async Task External_modding_api_install_error_remains_visible_after_failed_refresh()
    {
        using var test = new TestDirectory();
        var applicationDataRoot = test.CreateDirectory("app-data");
        var versionRoot = test.CreateDirectory("versions");
        var instanceRoot = test.CreateDirectory("versions", "1578");
        InstallExternalModdingApi(instanceRoot);
        var loader = new LoaderManifest
        {
            Id = "modding-api-77",
            Name = "Modding API",
            Version = "77",
            DownloadUrl = "https://example.invalid/loader.zip",
            Sha256 = new string('A', 64),
            SupportedBuildIds = ["1.5.78.11833"]
        };
        var record = Instance("1578", instanceRoot) with { BuildId = "1.5.78.11833" };
        await using var viewModel = new MainViewModel(applicationDataRoot) { VersionRoot = versionRoot };
        SetCatalog(viewModel, new GameCatalog { Loaders = [loader] });
        viewModel.SelectedInstance = new InstanceItemViewModel(record, record.BuildId, "Drifted", 0);
        viewModel.SelectedLoader = loader;

        await viewModel.InstallOrSwitchLoaderCommand.ExecuteAsync(null);

        Assert.Contains("未由 Crystalfly 管理", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task External_bepinex_without_receipt_shows_loader_block_reason()
    {
        using var test = new TestDirectory();
        var instanceRoot = test.CreateDirectory("versions", "1578");
        InstallExternalBepInEx(instanceRoot);
        var record = Instance("1578", instanceRoot) with { BuildId = "1.5.78.11833" };
        await using var viewModel = new MainViewModel(test.CreateDirectory("app-data"))
        {
            VersionRoot = test.CreateDirectory("versions"),
            SelectedInstance = new InstanceItemViewModel(record, record.BuildId, "BepInEx", 0)
        };

        await InvokeLoadInstanceDetailsAsync(viewModel, record, 1);

        Assert.Equal(LoaderState.BepInEx, viewModel.CurrentLoaderState);
        Assert.Equal(viewModel.Loc["ExternalLoaderBlocked"], viewModel.LoaderVerificationStatus);
    }

    [Fact]
    public async Task External_loader_conflict_without_receipt_shows_conflict_reason()
    {
        using var test = new TestDirectory();
        var instanceRoot = test.CreateDirectory("versions", "1578");
        InstallExternalBepInEx(instanceRoot);
        InstallExternalModdingApi(instanceRoot);
        var record = Instance("1578", instanceRoot) with { BuildId = "1.5.78.11833" };
        await using var viewModel = new MainViewModel(test.CreateDirectory("app-data"))
        {
            VersionRoot = test.CreateDirectory("versions"),
            SelectedInstance = new InstanceItemViewModel(record, record.BuildId, "Conflict", 0)
        };

        await InvokeLoadInstanceDetailsAsync(viewModel, record, 1);

        Assert.Equal(LoaderState.Conflict, viewModel.CurrentLoaderState);
        Assert.Equal(viewModel.Loc["LoaderConflict"], viewModel.LoaderVerificationStatus);
    }

    [Fact]
    public async Task External_bepinex_loader_switch_keeps_loader_block_reason()
    {
        using var test = new TestDirectory();
        var versionRoot = test.CreateDirectory("versions");
        var instanceRoot = test.CreateDirectory("versions", "1578");
        InstallExternalBepInEx(instanceRoot);
        var loader = new LoaderManifest
        {
            Id = "modding-api-77",
            Name = "Modding API",
            Version = "77",
            DownloadUrl = "https://example.invalid/loader.zip",
            Sha256 = new string('A', 64),
            SupportedBuildIds = ["1.5.78.11833"]
        };
        var record = Instance("1578", instanceRoot) with { BuildId = "1.5.78.11833" };
        await using var viewModel = new MainViewModel(test.CreateDirectory("app-data"))
        {
            VersionRoot = versionRoot
        };
        SetCatalog(viewModel, new GameCatalog { Loaders = [loader] });
        viewModel.SelectedInstance = new InstanceItemViewModel(record, record.BuildId, "BepInEx", 0);
        viewModel.SelectedLoader = loader;

        await viewModel.InstallOrSwitchLoaderCommand.ExecuteAsync(null);

        Assert.Contains(viewModel.Loc["ExternalLoaderBlocked"], viewModel.ErrorMessage);
        Assert.DoesNotContain("There is no installed loader receipt", viewModel.ErrorMessage);
    }

    [Theory]
    [InlineData("modding-api-77", "bepinex-5.4.23.4", true, false)]
    [InlineData("bepinex-5.4.23.4", "modding-api-77", true, false)]
    [InlineData("modding-api-77", "bepinex-5.4.23.4", false, false)]
    [InlineData("bepinex-5.4.23.4", "modding-api-77", false, true)]
    public async Task Loader_switch_with_managed_mod_receipt_is_blocked_without_changing_files(
        string currentLoaderId,
        string targetLoaderId,
        bool modEnabled,
        bool isLocal)
    {
        using var test = new TestDirectory();
        var versionRoot = test.CreateDirectory("versions");
        var instanceRoot = test.CreateDirectory("versions", "practice");
        var stateRoot = test.CreateDirectory("versions", ".crystalfly", "instances", "practice");
        var transactionRoot = test.CreateDirectory("versions", ".crystalfly", "transactions");
        var packageCacheRoot = test.CreateDirectory("versions", ".crystalfly", "packages");
        var packageRoot = test.CreateDirectory("packages");
        var currentPackage = Path.Combine(packageRoot, "current.zip");
        var targetPackage = Path.Combine(packageRoot, "target.zip");
        CreateZip(currentPackage, (LoaderPackageEntry(currentLoaderId), "current-loader"));
        CreateZip(targetPackage, (LoaderPackageEntry(targetLoaderId), "target-loader"));
        var currentLoader = LoaderManifestFor(currentLoaderId, currentPackage);
        var targetLoader = LoaderManifestFor(targetLoaderId, targetPackage);
        var loaderReceiptPath = Path.Combine(stateRoot, "loader.json");
        var loaderManager = new LoaderManager(
            instanceRoot,
            transactionRoot,
            loaderReceiptPath,
            packageCacheRoot);
        await loaderManager.InstallFromFileAsync(currentLoader, currentPackage);
        File.Copy(targetPackage, Path.Combine(packageCacheRoot, $"{targetLoader.Sha256}.zip"));

        var modRelativePath = currentLoaderId.StartsWith("bepinex-", StringComparison.OrdinalIgnoreCase)
            ? "BepInEx/plugins/Sample/mod.dll"
            : "hollow_knight_Data/Managed/Mods/Sample/mod.dll";
        var modPath = Path.Combine(instanceRoot, modRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(modPath)!);
        await File.WriteAllTextAsync(modPath, "managed-mod");
        var modReceiptPath = Path.Combine(test.CreateDirectory(
            "versions", ".crystalfly", "instances", "practice", "mods"), "sample.json");
        await AtomicJsonStore.WriteAsync(modReceiptPath, Receipt("sample", "1.0.0", modEnabled, isLocal) with
        {
            LoaderId = currentLoaderId,
            InstallRoot = Path.GetDirectoryName(modRelativePath)!.Replace('\\', '/'),
            Files =
            [
                new InstalledFileReceipt
                {
                    RelativePath = modRelativePath,
                    Sha256 = FileSha256(modPath)
                }
            ]
        });
        var originalLoaderReceipt = await File.ReadAllBytesAsync(loaderReceiptPath);
        var originalModReceipt = await File.ReadAllBytesAsync(modReceiptPath);
        var originalLoaderFile = await File.ReadAllBytesAsync(
            Path.Combine(instanceRoot, LoaderInstalledPath(currentLoaderId)));
        var originalModFile = await File.ReadAllBytesAsync(modPath);

        var record = Instance("practice", instanceRoot) with { BuildId = "1.5.78.11833" };
        await using var viewModel = new MainViewModel(test.CreateDirectory("app-data"))
        {
            VersionRoot = versionRoot
        };
        SetCatalog(viewModel, new GameCatalog { Loaders = [currentLoader, targetLoader] });
        viewModel.SelectedInstance = new InstanceItemViewModel(record, record.BuildId, currentLoaderId, 1);
        viewModel.SelectedLoader = targetLoader;

        await viewModel.InstallOrSwitchLoaderCommand.ExecuteAsync(null);

        Assert.Contains(viewModel.Loc["LoaderSwitchBlockedByMods"], viewModel.ErrorMessage);
        Assert.Equal(originalLoaderReceipt, await File.ReadAllBytesAsync(loaderReceiptPath));
        Assert.Equal(originalModReceipt, await File.ReadAllBytesAsync(modReceiptPath));
        Assert.Equal(originalLoaderFile, await File.ReadAllBytesAsync(
            Path.Combine(instanceRoot, LoaderInstalledPath(currentLoaderId))));
        Assert.Equal(originalModFile, await File.ReadAllBytesAsync(modPath));
    }
    [Fact]
    public async Task Repair_loader_uses_receipted_manifest_instead_of_selected_target()
    {
        using var test = new TestDirectory();
        var versionRoot = test.CreateDirectory("versions");
        var instanceRoot = test.CreateDirectory("versions", "practice");
        var stateRoot = test.CreateDirectory("versions", ".crystalfly", "instances", "practice");
        var transactionRoot = test.CreateDirectory("versions", ".crystalfly", "transactions");
        var packageCacheRoot = test.CreateDirectory("versions", ".crystalfly", "packages");
        var packageRoot = test.CreateDirectory("packages");
        var currentPackage = Path.Combine(packageRoot, "current.zip");
        var targetPackage = Path.Combine(packageRoot, "target.zip");
        CreateZip(currentPackage, ("MMHOOK_Assembly-CSharp.dll", "current-loader"));
        CreateZip(targetPackage, ("BepInEx/core/BepInEx.dll", "target-loader"));
        var currentLoader = LoaderManifestFor("modding-api-77", currentPackage);
        var targetLoader = LoaderManifestFor("bepinex-5.4.23.4", targetPackage);
        var loaderReceiptPath = Path.Combine(stateRoot, "loader.json");
        var manager = new LoaderManager(
            instanceRoot,
            transactionRoot,
            loaderReceiptPath,
            packageCacheRoot);
        await manager.InstallFromFileAsync(currentLoader, currentPackage);
        File.Copy(currentPackage, Path.Combine(packageCacheRoot, $"{currentLoader.Sha256}.zip"));
        var installedPath = Path.Combine(instanceRoot, LoaderInstalledPath(currentLoader.Id));
        await File.WriteAllTextAsync(installedPath, "drifted");

        var record = Instance("practice", instanceRoot) with { BuildId = "1.5.78.11833" };
        await using var viewModel = new MainViewModel(test.CreateDirectory("app-data"))
        {
            VersionRoot = versionRoot,
            SelectedInstance = new InstanceItemViewModel(record, record.BuildId, "Drifted", 0),
            SelectedLoader = targetLoader
        };
        SetCatalog(viewModel, new GameCatalog { Loaders = [currentLoader, targetLoader] });

        await viewModel.RepairLoaderCommand.ExecuteAsync(null);

        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal("current-loader", await File.ReadAllTextAsync(installedPath));
        Assert.Equal(currentLoader.Id, (await manager.GetReceiptAsync())?.PackageId);
    }

    [Fact]
    public async Task Repair_orphaned_loader_uses_installed_mod_loader_receipt()
    {
        using var test = new TestDirectory();
        var versionRoot = test.CreateDirectory("versions");
        var instanceRoot = test.CreateDirectory("versions", "practice");
        var stateRoot = test.CreateDirectory("versions", ".crystalfly", "instances", "practice");
        var packageCacheRoot = test.CreateDirectory("versions", ".crystalfly", "packages");
        var packageRoot = test.CreateDirectory("packages");
        var currentPackage = Path.Combine(packageRoot, "current.zip");
        var targetPackage = Path.Combine(packageRoot, "target.zip");
        CreateZip(currentPackage, ("BepInEx/core/BepInEx.dll", "current-loader"));
        CreateZip(targetPackage, ("MMHOOK_Assembly-CSharp.dll", "target-loader"));
        var currentLoader = LoaderManifestFor("bepinex-5.4.23.4", currentPackage);
        var targetLoader = LoaderManifestFor("modding-api-77", targetPackage);
        File.Copy(currentPackage, Path.Combine(packageCacheRoot, $"{currentLoader.Sha256}.zip"));
        var modRelativePath = "BepInEx/plugins/Sample/mod.dll";
        var modPath = Path.Combine(instanceRoot, modRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(modPath)!);
        await File.WriteAllTextAsync(modPath, "managed-mod");
        var modReceiptRoot = test.CreateDirectory(
            "versions", ".crystalfly", "instances", "practice", "mods");
        await AtomicJsonStore.WriteAsync(Path.Combine(modReceiptRoot, "sample.json"),
            Receipt("sample", "1.0.0", enabled: true) with
            {
                LoaderId = currentLoader.Id,
                InstallRoot = "BepInEx/plugins/Sample",
                Files =
                [
                    new InstalledFileReceipt
                    {
                        RelativePath = modRelativePath,
                        Sha256 = FileSha256(modPath)
                    }
                ]
            });

        var record = Instance("practice", instanceRoot) with { BuildId = "1.5.78.11833" };
        await using var viewModel = new MainViewModel(test.CreateDirectory("app-data"))
        {
            VersionRoot = versionRoot,
            SelectedInstance = new InstanceItemViewModel(record, record.BuildId, "Drifted", 1),
            SelectedLoader = targetLoader
        };
        SetCatalog(viewModel, new GameCatalog { Loaders = [currentLoader, targetLoader] });

        SetPrivateField(viewModel, "detailsLoadGeneration", 1L);
        await InvokeLoadInstanceDetailsAsync(viewModel, record, 1);
        Assert.Equal(viewModel.Loc["LoaderReceiptRecoveryAvailable"], viewModel.LoaderVerificationStatus);

        await viewModel.RepairLoaderCommand.ExecuteAsync(null);

        Assert.Null(viewModel.ErrorMessage);
        Assert.True(File.Exists(Path.Combine(instanceRoot, LoaderInstalledPath(currentLoader.Id))));
        Assert.True(File.Exists(modPath));
        var recovered = await AtomicJsonStore.ReadAsync<InstalledPackageReceipt>(Path.Combine(stateRoot, "loader.json"));
        Assert.Equal(currentLoader.Id, recovered.PackageId);
    }

    [Fact]
    public async Task Loader_uninstall_with_managed_mod_receipt_is_blocked_before_loader_changes()
    {
        using var test = new TestDirectory();
        var versionRoot = test.CreateDirectory("versions");
        var instanceRoot = test.CreateDirectory("versions", "practice");
        var stateRoot = test.CreateDirectory("versions", ".crystalfly", "instances", "practice");
        var transactionRoot = test.CreateDirectory("versions", ".crystalfly", "transactions");
        var packageRoot = test.CreateDirectory("packages");
        var package = Path.Combine(packageRoot, "loader.zip");
        CreateZip(package, ("BepInEx/core/BepInEx.dll", "loader"));
        var loader = LoaderManifestFor("bepinex-5.4.23.4", package);
        var manager = new LoaderManager(
            instanceRoot,
            transactionRoot,
            Path.Combine(stateRoot, "loader.json"));
        await manager.InstallFromFileAsync(loader, package);
        var modPath = Path.Combine(instanceRoot, "BepInEx", "plugins", "Sample", "mod.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(modPath)!);
        await File.WriteAllTextAsync(modPath, "managed-mod");
        var modReceiptRoot = test.CreateDirectory(
            "versions", ".crystalfly", "instances", "practice", "mods");
        await AtomicJsonStore.WriteAsync(Path.Combine(modReceiptRoot, "sample.json"),
            Receipt("sample", "1.0.0", enabled: true) with
            {
                LoaderId = loader.Id,
                InstallRoot = "BepInEx/plugins/Sample"
            });
        var originalReceipt = await File.ReadAllBytesAsync(Path.Combine(stateRoot, "loader.json"));

        var record = Instance("practice", instanceRoot) with { BuildId = "1.5.78.11833" };
        await using var viewModel = new MainViewModel(test.CreateDirectory("app-data"))
        {
            VersionRoot = versionRoot,
            SelectedInstance = new InstanceItemViewModel(record, record.BuildId, "BepInEx", 1)
        };
        SetCatalog(viewModel, new GameCatalog { Loaders = [loader] });

        await viewModel.UninstallLoaderCommand.ExecuteAsync(null);

        Assert.Contains(viewModel.Loc["LoaderUninstallBlockedByMods"], viewModel.ErrorMessage);
        Assert.Equal(originalReceipt, await File.ReadAllBytesAsync(Path.Combine(stateRoot, "loader.json")));
        Assert.True(File.Exists(Path.Combine(instanceRoot, LoaderInstalledPath(loader.Id))));
        Assert.True(File.Exists(modPath));
    }

    [Fact]
    public async Task Dispose_is_idempotent_and_waits_for_running_commands_before_releasing_steam()
    {
        var launchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLaunch = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var downloadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var downloadCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDownloadCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var events = new ConcurrentQueue<string>();
        var viewModel = new MainViewModel(
            applicationData.CreateDirectory("app-data"),
            async () =>
            {
                launchStarted.SetResult();
                await releaseLaunch.Task;
                events.Enqueue("launch-complete");
            },
            async cancellationToken =>
            {
                downloadStarted.SetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                finally
                {
                    downloadCancelled.SetResult();
                    await releaseDownloadCleanup.Task;
                    events.Enqueue("download-complete");
                }
            },
            () =>
            {
                events.Enqueue("steam-disposed");
                return Task.CompletedTask;
            });

        var launch = viewModel.LaunchGameCommand.ExecuteAsync(null);
        var download = viewModel.DownloadBuildCommand.ExecuteAsync(null);
        await Task.WhenAll(launchStarted.Task, downloadStarted.Task);

        var firstDispose = viewModel.DisposeAsync().AsTask();
        var secondDispose = viewModel.DisposeAsync().AsTask();

        Assert.Same(firstDispose, secondDispose);
        await downloadCancelled.Task;
        Assert.False(firstDispose.IsCompleted);

        releaseLaunch.SetResult();
        await launch;
        Assert.False(firstDispose.IsCompleted);

        releaseDownloadCleanup.SetResult();
        await Task.WhenAll(download, firstDispose);

        Assert.Equal(
            ["launch-complete", "download-complete", "steam-disposed"],
            events.ToArray());
    }

    [Fact]
    public async Task Steam_sign_in_failure_is_reported_without_faulting_the_command()
    {
        var viewModel = new MainViewModel(
            applicationData.CreateDirectory("app-data"),
            launchOverride: null,
            downloadOverride: null,
            disposeSteamOverride: null,
            qrSignInOverride: _ => Task.FromException<RefreshTokenCredential>(new Exception("poll failed")));

        await viewModel.SignInWithQrCommand.ExecuteAsync(null);

        Assert.Equal("Steam: poll failed", viewModel.ErrorMessage);
        Assert.Equal("Not signed in", viewModel.SteamStatus);
        Assert.False(viewModel.IsSteamLoggedIn);
    }

    [Fact]
    public async Task Steam_sign_in_retries_transient_connection_failures_before_showing_the_QR_code()
    {
        var attempts = 0;
        await using var viewModel = new MainViewModel(
            applicationData.CreateDirectory("steam-retry"),
            qrSignInOverride: _ => Interlocked.Increment(ref attempts) < 3
                ? Task.FromException<RefreshTokenCredential>(new HttpRequestException("connection reset"))
                : Task.FromResult(new RefreshTokenCredential("runner", "token")));

        await viewModel.SignInWithQrCommand.ExecuteAsync(null);

        Assert.Equal(3, attempts);
        Assert.True(viewModel.IsSteamLoggedIn);
        Assert.Equal("runner", viewModel.SteamStatus);
    }

    [Fact]
    public async Task System_proxy_change_pauses_Steam_work_and_reconnects_the_saved_account()
    {
        string root = applicationData.CreateDirectory("proxy-change");
        File.WriteAllText(Path.Combine(root, "steam-token.dat"), "fixture");
        var snapshots = new Queue<SystemProxySnapshot>(
        [
            SystemProxySnapshot.Direct,
            new(true, "HTTP", "127.0.0.1:7890", null)
        ]);
        using var proxy = new SystemProxyService(
            WebRequest.GetSystemWebProxy,
            () => snapshots.Dequeue(),
            startMonitoring: false);
        await using var viewModel = new MainViewModel(root, systemProxyOverride: proxy);
        var reconnects = 0;
        SetPrivateField(
            viewModel,
            "steamReconnect",
            new Func<Task>(() =>
            {
                Interlocked.Increment(ref reconnects);
                return Task.CompletedTask;
            }));

        proxy.Refresh();

        await WaitUntilAsync(() => Volatile.Read(ref reconnects) == 1);
        Assert.Equal(viewModel.Loc["SteamProxyDetected"], viewModel.SteamNetworkStatus);
    }

    [Fact]
    public async Task System_proxy_change_automatically_requests_a_fresh_QR_code()
    {
        string root = applicationData.CreateDirectory("proxy-change-qr");
        var snapshots = new Queue<SystemProxySnapshot>(
        [
            SystemProxySnapshot.Direct,
            new(true, "HTTP", "127.0.0.1:7890", null)
        ]);
        using var proxy = new SystemProxyService(
            WebRequest.GetSystemWebProxy,
            () => snapshots.Dequeue(),
            startMonitoring: false);
        var firstAttemptStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        await using var viewModel = new MainViewModel(
            root,
            qrSignInOverride: async cancellationToken =>
            {
                if (Interlocked.Increment(ref attempts) == 1)
                {
                    firstAttemptStarted.SetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                return new RefreshTokenCredential("runner", "token");
            },
            systemProxyOverride: proxy);

        await SignInAndChangeProxyAsync();

        await WaitUntilAsync(() => Volatile.Read(ref attempts) == 2 && viewModel.IsSteamLoggedIn);
        Assert.Equal("runner", viewModel.SteamStatus);

        async Task SignInAndChangeProxyAsync()
        {
            Task signIn = viewModel.SignInWithQrCommand.ExecuteAsync(null);
            await firstAttemptStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            proxy.Refresh();
            await signIn;
        }
    }

    [Fact]
    public async Task Dispose_cancels_and_waits_for_running_Steam_sign_in()
    {
        var signInStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var signInCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSignInCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var viewModel = new MainViewModel(
            applicationData.CreateDirectory("app-data"),
            launchOverride: null,
            downloadOverride: null,
            disposeSteamOverride: null,
            qrSignInOverride: async cancellationToken =>
            {
                signInStarted.SetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    return new RefreshTokenCredential("unused", "unused");
                }
                finally
                {
                    signInCancelled.SetResult();
                    await releaseSignInCleanup.Task;
                }
            });

        var signIn = viewModel.SignInWithQrCommand.ExecuteAsync(null);
        await signInStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var dispose = viewModel.DisposeAsync().AsTask();

        await signInCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(dispose.IsCompleted);
        releaseSignInCleanup.SetResult();
        await Task.WhenAll(signIn, dispose).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("Not signed in", viewModel.SteamStatus);
    }

    [Fact]
    public async Task Steam_sign_in_does_not_start_after_disposal()
    {
        var signInCalled = false;
        var viewModel = new MainViewModel(
            applicationData.CreateDirectory("app-data"),
            launchOverride: null,
            downloadOverride: null,
            disposeSteamOverride: null,
            qrSignInOverride: _ =>
            {
                signInCalled = true;
                return Task.FromResult(new RefreshTokenCredential("unused", "unused"));
            });

        await viewModel.DisposeAsync();
        await viewModel.SignInWithQrCommand.ExecuteAsync(null);

        Assert.False(signInCalled);
    }

    [Fact]
    public async Task Steam_download_failure_is_reported_without_faulting_the_command()
    {
        var viewModel = new MainViewModel(
            applicationData.CreateDirectory("app-data"),
            launchOverride: null,
            downloadOverride: _ => Task.FromException(new HttpRequestException("CDN unavailable")),
            disposeSteamOverride: null);

        await viewModel.DownloadBuildCommand.ExecuteAsync(null);

        Assert.Equal("Steam: CDN unavailable", viewModel.ErrorMessage);
        Assert.Equal("Failed", viewModel.DownloadStatus);
    }

    [Fact]
    public async Task Steam_download_command_enqueues_selected_build_and_deduplicates_target()
    {
        using var test = new TestDirectory();
        var applicationDataRoot = test.CreateDirectory("app-data");
        var versionRoot = test.CreateDirectory("versions");
        var executor = new WaitingQueueExecutor();
        var queue = new DownloadQueueService(
            Path.Combine(applicationDataRoot, "download-queue.json"),
            executor,
            static () => false,
            TimeSpan.FromMilliseconds(10));
        await using var viewModel = new MainViewModel(
            applicationDataRoot,
            launchOverride: null,
            downloadOverride: null,
            disposeSteamOverride: null,
            qrSignInOverride: null,
            downloadQueueOverride: queue,
            steamLoggedOnOverride: static () => true)
        {
            VersionRoot = versionRoot,
            IsSteamLoggedIn = true,
            SelectedDownloadBuild = new DownloadBuildOption(
                "1.5.78.11833",
                "1.5.78",
                123456789UL)
        };

        await viewModel.DownloadBuildCommand.ExecuteAsync(null);
        await executor.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await viewModel.DownloadBuildCommand.ExecuteAsync(null);
        InvokeApplyPendingDownloadQueueProjection(viewModel);

        var group = Assert.Single(queue.Groups);
        Assert.Equal(DownloadQueueGroupKind.AssetInstall, group.Kind);
        Assert.Equal(
            Path.Combine(versionRoot, "Hollow Knight 1.5.78"),
            group.TargetInstanceRoot);
        var item = Assert.Single(group.Items);
        Assert.Equal("steam:1.5.78.11833", item.PackageId);
        Assert.Equal("123456789", item.PackagePath);
        Assert.Equal("steam-depot", item.LoaderId);
        Assert.Single(viewModel.DownloadCenter.DownloadQueueGroups);
        Assert.Equal(viewModel.Loc["QueueTaskAlreadyExists"], viewModel.DownloadStatus);
    }

    [Fact]
    public void Download_status_shows_speed_size_progress_and_current_file()
    {
        var progress = new SteamDownloadProgress(
            CompletedBytes: 486L * 1024 * 1024,
            TotalBytes: (long)(2.1 * 1024 * 1024 * 1024),
            Fraction: 0.23,
            CurrentFile: "current-file.dat")
        {
            BytesPerSecond = 12.4 * 1024 * 1024
        };

        var status = MainViewModel.FormatDownloadStatus(progress);

        Assert.Equal("12.4 MB/s · 486 MB / 2.1 GB · 23%\ncurrent-file.dat", status);
    }

    [Fact]
    public void Clone_name_rejects_whitespace()
    {
        var viewModel = CreateViewModel();
        viewModel.CloneInstanceName = "   ";

        Assert.False(viewModel.CanCloneInstance);

        viewModel.CloneInstanceName = "Practice";

        Assert.True(viewModel.CanCloneInstance);
    }

    [Fact]
    public async Task Clone_success_requests_one_completion_toast()
    {
        using var test = new TestDirectory();
        var applicationDataRoot = test.CreateDirectory("app-data");
        var versionRoot = test.CreateDirectory("versions");
        var instanceRoot = test.CreateDirectory("versions", "source");
        await File.WriteAllTextAsync(Path.Combine(instanceRoot, "hollow_knight.exe"), string.Empty);
        await File.WriteAllTextAsync(
            Path.Combine(test.CreateDirectory("versions", "source", "hollow_knight_Data"), "globalgamemanagers"),
            string.Empty);
        var record = Instance("source", instanceRoot) with { Name = "Source" };
        await InstanceSidecar.SaveAsync(record);
        await using var viewModel = new MainViewModel(applicationDataRoot)
        {
            VersionRoot = versionRoot,
            SelectedInstance = new InstanceItemViewModel(record, record.BuildId, "Vanilla", 0),
            CloneInstanceName = "Clone",
            CurrentPage = "Versions"
        };
        var notifications = new List<string>();
        viewModel.ToastRequested += notifications.Add;

        await viewModel.Instances.CloneSelectedInstanceCommand.ExecuteAsync(null);

        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal(viewModel.Loc["OperationComplete"], Assert.Single(notifications));
        Assert.Equal("Clone", viewModel.SelectedInstance?.Name);
        Assert.NotEqual(record.Id, viewModel.SelectedInstance?.Id);
        Assert.Equal("Launch", viewModel.CurrentPage);
    }

    [Fact]
    public void Mod_market_jump_uses_exact_current_loader_inspection_and_clears_unknown_loader()
    {
        var viewModel = CreateViewModel();
        var record = Instance("practice", applicationData.CreateDirectory("market-jump", "practice"));
        SetPrivateField(
            viewModel,
            "<SelectedInstance>k__BackingField",
            new InstanceItemViewModel(record, record.BuildId, "BepInEx", 1));
        SetCatalog(viewModel, new GameCatalog
        {
            Mods =
            [
                Manifest("modding", "1.0.0") with
                {
                    LoaderId = "modding-api-77",
                    SupportedBuildIds = [record.BuildId]
                },
                Manifest("plugin", "1.0.0") with
                {
                    LoaderId = "bepinex-5.4.23.4",
                    SupportedBuildIds = [record.BuildId]
                }
            ]
        });
        InvokeRebuildMarketCatalog(viewModel);
        SetCurrentLoaderInspection(viewModel, new LoaderInspection
        {
            State = LoaderState.BepInEx,
            PackageId = "bepinex-5.4.23.4",
            Version = "5.4.23.4",
            Ownership = LoaderOwnership.External
        });

        viewModel.OpenModMarketForSelectedInstanceCommand.Execute(null);

        Assert.Equal("bepinex-5.4.23.4", viewModel.SelectedMarketLoaderOption?.Value);

        SetCurrentLoaderInspection(viewModel, new LoaderInspection
        {
            State = LoaderState.Drifted,
            Ownership = LoaderOwnership.External
        });
        viewModel.OpenModMarketForSelectedInstanceCommand.Execute(null);

        Assert.Null(viewModel.SelectedMarketLoaderOption);
    }

    [Theory]
    [InlineData(@"..\..\outside")]
    [InlineData(@"C:\outside")]
    [InlineData(@"\\server\share\outside")]
    [InlineData("nested/child")]
    [InlineData(@"nested\child")]
    public void Download_instance_name_rejects_invalid_catalog_display_version(string displayVersion)
    {
        using var test = new TestDirectory();
        var viewModel = CreateViewModel();
        viewModel.VersionRoot = test.CreateDirectory("versions");

        var exception = Assert.Throws<TargetInvocationException>(() =>
            InvokeUniqueInstanceName(viewModel, displayVersion));

        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    [Fact]
    public void Download_instance_name_reserves_space_for_conflict_suffix()
    {
        using var test = new TestDirectory();
        var versionRoot = test.CreateDirectory("versions");
        var viewModel = CreateViewModel();
        viewModel.VersionRoot = versionRoot;
        var displayVersion = new string('x', 255 - "Hollow Knight ".Length);
        File.WriteAllText(Path.Combine(versionRoot, $"Hollow Knight {displayVersion}"), "occupied");

        var name = InvokeUniqueInstanceName(viewModel, displayVersion);

        Assert.EndsWith(" (2)", name, StringComparison.Ordinal);
        Assert.True(name.Length <= 255);
    }

    [Fact]
    public void Download_instance_name_skips_an_existing_file()
    {
        using var test = new TestDirectory();
        var versionRoot = test.CreateDirectory("versions");
        File.WriteAllText(Path.Combine(versionRoot, "Hollow Knight 1.5"), "occupied");
        var viewModel = CreateViewModel();
        viewModel.VersionRoot = versionRoot;

        var name = InvokeUniqueInstanceName(viewModel, "1.5");

        Assert.Equal("Hollow Knight 1.5 (2)", name);
    }

    [Fact]
    public void Busy_or_running_state_locks_navigation()
    {
        var viewModel = CreateViewModel();
        Assert.True(viewModel.CanNavigate);

        viewModel.IsBusy = true;
        Assert.False(viewModel.CanNavigate);

        viewModel.IsBusy = false;
        viewModel.IsGameRunning = true;
        Assert.False(viewModel.CanNavigate);
    }

    [Fact]
    public async Task Instance_row_selection_returns_to_launch_and_settings_targets_its_row()
    {
        await using var viewModel = CreateViewModel();
        var first = new InstanceItemViewModel(
            Instance("first", applicationData.CreateDirectory("versions", "first")),
            "1.5.78.11833",
            "Vanilla",
            0);
        var second = new InstanceItemViewModel(
            Instance("second", applicationData.CreateDirectory("versions", "second")),
            "1.4.3.2",
            "Vanilla",
            0);
        viewModel.SelectedInstance = first;
        viewModel.CurrentPage = "Versions";

        viewModel.Instances.SelectInstanceForLaunchCommand.Execute(second);

        Assert.Same(second, viewModel.SelectedInstance);
        Assert.Equal("Launch", viewModel.CurrentPage);

        viewModel.OpenInstanceSettingsCommand.Execute(first);

        Assert.Same(first, viewModel.SelectedInstance);
        Assert.Equal("Manage", viewModel.CurrentPage);
        Assert.Equal("Overview", viewModel.CurrentManageTab);
    }

    [Fact]
    public async Task Favorite_instances_sort_first_within_the_active_directory()
    {
        await using var viewModel = CreateViewModel();
        var alpha = new InstanceItemViewModel(
            Instance("alpha", applicationData.CreateDirectory("favorite-versions", "alpha")),
            "1.5.78.11833",
            "Vanilla",
            0);
        var beta = new InstanceItemViewModel(
            Instance("beta", applicationData.CreateDirectory("favorite-versions", "beta")),
            "1.5.78.11833",
            "Vanilla",
            0);
        viewModel.Instances.Instances.Add(alpha);
        viewModel.Instances.Instances.Add(beta);

        viewModel.Instances.ToggleFavoriteInstanceCommand.Execute(beta);

        Assert.True(viewModel.Instances.VisibleInstances[0].IsFavorite);
        Assert.Equal("beta", viewModel.Instances.VisibleInstances[0].Id);
        Assert.False(viewModel.Instances.VisibleInstances[1].IsFavorite);
    }

    [Fact]
    public async Task Delete_instance_runs_condition_check_inside_coordinator_and_selects_next()
    {
        InstanceRecord? deleted = null;
        InstanceDeletionConditions? evaluated = null;
        await using var viewModel = new MainViewModel(
            applicationData.CreateDirectory("delete-app-data"),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            async (record, conditionEvaluator, cancellationToken) =>
            {
                deleted = record;
                evaluated = await conditionEvaluator(cancellationToken);
                return new InstanceDeletionResult(
                    record.Id,
                    record.RootPath,
                    Path.Combine(record.RootPath, "pending"),
                    CleanupCompleted: true,
                    CleanupError: null);
            });
        viewModel.VersionRoot = applicationData.CreateDirectory("delete-versions");
        var first = new InstanceItemViewModel(
            Instance("first", applicationData.CreateDirectory("delete-versions", "first")),
            "1.5.78.11833",
            "Vanilla",
            0);
        var second = new InstanceItemViewModel(
            Instance("second", applicationData.CreateDirectory("delete-versions", "second")),
            "1.4.3.2",
            "Vanilla",
            0);
        viewModel.Instances.Instances.Add(first);
        viewModel.Instances.Instances.Add(second);
        viewModel.SelectedInstance = first;
        viewModel.CurrentPage = "Versions";

        await viewModel.Instances.DeleteInstanceCommand.ExecuteAsync(first);

        Assert.Same(first.Record, deleted);
        Assert.NotNull(evaluated);
        Assert.False(evaluated.HasBlockingQueueTasks);
        Assert.True(evaluated.TransactionsHealthy);
        Assert.DoesNotContain(first, viewModel.Instances.Instances);
        Assert.Same(second, viewModel.SelectedInstance);
        Assert.Equal("Launch", viewModel.CurrentPage);
    }

    [Fact]
    public void Mod_search_and_status_filter_both_lists()
    {
        var viewModel = CreateViewModel();
        var catalog = Manifest("debugmod", "2.0.0");
        viewModel.ModManagement.AvailableMods.Add(catalog);
        viewModel.ModManagement.AvailableMods.Add(Manifest("benchwarp", "1.0.0"));
        viewModel.ModManagement.InstalledMods.Add(new InstalledModItemViewModel(
            Receipt("debugmod", "1.0.0", enabled: true),
            catalog,
            static () => { }));
        viewModel.ModManagement.InstalledMods.Add(new InstalledModItemViewModel(
            Receipt("local-helper", "1.0.0", enabled: false, isLocal: true),
            null,
            static () => { }));

        viewModel.ModManagement.ModSearchText = "debug";

        Assert.Single(viewModel.ModManagement.VisibleAvailableMods);
        Assert.Single(viewModel.ModManagement.VisibleInstalledMods);

        viewModel.ModManagement.ModSearchText = string.Empty;
        viewModel.ModManagement.SelectedModStatus = ModStatusFilter.Local;

        Assert.Single(viewModel.ModManagement.VisibleInstalledMods);
        Assert.Equal("local-helper", viewModel.ModManagement.VisibleInstalledMods[0].Id);
    }

    [Fact]
    public void Installed_mod_selection_supports_single_ctrl_shift_and_global_select_all()
    {
        var viewModel = CreateViewModel();
        var first = Installed("first");
        var hidden = Installed("hidden");
        var second = Installed("second");
        var third = Installed("third");
        foreach (var item in new[] { first, hidden, second, third })
        {
            viewModel.ModManagement.InstalledMods.Add(item);
        }
        foreach (var item in new[] { first, second, third })
        {
            viewModel.ModManagement.VisibleInstalledMods.Add(item);
        }

        viewModel.ModManagement.SelectInstalledMod(first, control: false, shift: false);
        viewModel.ModManagement.SelectInstalledMod(third, control: false, shift: true);

        Assert.True(first.IsSelected);
        Assert.True(second.IsSelected);
        Assert.True(third.IsSelected);
        Assert.False(hidden.IsSelected);

        viewModel.ModManagement.SelectInstalledMod(second, control: true, shift: false);
        Assert.False(second.IsSelected);

        viewModel.ModManagement.ClearInstalledModSelectionCommand.Execute(null);
        Assert.DoesNotContain(viewModel.ModManagement.InstalledMods, item => item.IsSelected);

        viewModel.ModManagement.SelectAllInstalledModsCommand.Execute(null);
        Assert.All(viewModel.ModManagement.InstalledMods, item => Assert.True(item.IsSelected));
    }

    [Fact]
    public void Mod_removal_plan_keeps_selected_targets_and_reports_affected_dependents()
    {
        var viewModel = CreateViewModel();
        var library = new InstalledModItemViewModel(
            Receipt("library", "1.0.0", enabled: true),
            null,
            static () => { }) { IsSelected = true };
        var feature = new InstalledModItemViewModel(
            Receipt("feature", "1.0.0", enabled: true) with { Dependencies = ["library"] },
            null,
            static () => { });
        viewModel.ModManagement.InstalledMods.Add(library);
        viewModel.ModManagement.InstalledMods.Add(feature);

        var plan = viewModel.ModManagement.CreateModRemovalPlan(bulk: true);

        Assert.Equal(["library"], plan.TargetModIds);
        Assert.Contains(plan.Nodes, node =>
            node.ModId == "library" && node.Kind == ModRemovalImpactKind.WillRemove);
        Assert.Contains(plan.Nodes, node =>
            node.ModId == "feature" && node.Kind == ModRemovalImpactKind.DependencyWillBeMissing);
    }

    [Fact]
    public void Mod_dependency_repair_plan_uses_selected_instance_build_and_exact_loader()
    {
        var viewModel = CreateViewModel();
        var record = Instance("practice", applicationData.CreateDirectory("repair", "practice"));
        SetPrivateField(
            viewModel,
            "<SelectedInstance>k__BackingField",
            new InstanceItemViewModel(record, record.BuildId, "Modding API", 2));
        var libraryManifest = Manifest("library", "1.0.0") with
        {
            SupportedBuildIds = [record.BuildId]
        };
        SetCatalog(viewModel, new GameCatalog { Mods = [libraryManifest] });
        viewModel.ModManagement.InstalledMods.Add(new InstalledModItemViewModel(
            Receipt("library", "1.0.0", enabled: false),
            libraryManifest,
            static () => { }));
        viewModel.ModManagement.InstalledMods.Add(new InstalledModItemViewModel(
            Receipt("feature", "1.0.0", enabled: true) with { Dependencies = ["library"] },
            null,
            static () => { }));

        var plan = viewModel.ModManagement.CreateModDependencyRepairPlan();

        Assert.Equal(record.BuildId, plan.BuildId);
        Assert.Equal("modding-api", plan.LoaderId);
        var repair = Assert.Single(plan.Items);
        Assert.Equal("library", repair.ModId);
        Assert.Equal(ModDependencyRepairAction.ReEnable, repair.Action);
    }

    [Fact]
    public void Mod_dependency_repair_plan_rejects_mixed_loader_receipts()
    {
        var viewModel = CreateViewModel();
        var record = Instance("practice", applicationData.CreateDirectory("mixed", "practice"));
        SetPrivateField(
            viewModel,
            "<SelectedInstance>k__BackingField",
            new InstanceItemViewModel(record, record.BuildId, "Conflict", 2));
        viewModel.ModManagement.InstalledMods.Add(Installed("first"));
        viewModel.ModManagement.InstalledMods.Add(new InstalledModItemViewModel(
            Receipt("second", "1.0.0", enabled: true) with { LoaderId = "bepinex-5" },
            null,
            static () => { }));

        Assert.Throws<InvalidOperationException>(() => viewModel.ModManagement.CreateModDependencyRepairPlan());
    }
    [Fact]
    public async Task Bulk_mod_update_rechecks_instance_build_before_writing()
    {
        using var test = new TestDirectory();
        var versionRoot = test.CreateDirectory("versions");
        var instanceRoot = test.CreateDirectory("versions", "practice");
        var stateRoot = test.CreateDirectory("versions", ".crystalfly", "instances", "practice");
        var packageCacheRoot = test.CreateDirectory("versions", ".crystalfly", "packages");
        var loaderPath = Path.Combine(
            test.CreateDirectory("versions", "practice", "hollow_knight_Data", "Managed"),
            "MMHOOK_Assembly-CSharp.dll");
        await File.WriteAllTextAsync(loaderPath, "loader");
        await AtomicJsonStore.WriteAsync(Path.Combine(stateRoot, "loader.json"), new InstalledPackageReceipt
        {
            PackageId = "modding-api-77",
            LoaderState = LoaderState.ModdingApi,
            Files =
            [
                new InstalledFileReceipt
                {
                    RelativePath = "hollow_knight_Data/Managed/MMHOOK_Assembly-CSharp.dll",
                    Sha256 = FileSha256(loaderPath)
                }
            ]
        });

        var installedPackage = Path.Combine(test.CreateDirectory("packages"), "installed.zip");
        var updatePackage = Path.Combine(test.CreateDirectory("updates"), "update.zip");
        CreateZip(installedPackage, ("mod.dll", "installed"));
        CreateZip(updatePackage, ("mod.dll", "update"));
        var installedManifest = Manifest("debugmod", "1.0.0") with
        {
            LoaderId = "modding-api-77",
            SizeBytes = new FileInfo(installedPackage).Length,
            Sha256 = FileSha256(installedPackage),
            SupportedBuildIds = ["build-1"]
        };
        var updateManifest = Manifest("debugmod", "2.0.0") with
        {
            LoaderId = "modding-api-77",
            SizeBytes = new FileInfo(updatePackage).Length,
            Sha256 = FileSha256(updatePackage),
            SupportedBuildIds = ["other-build"]
        };
        File.Copy(updatePackage, Path.Combine(packageCacheRoot, $"{updateManifest.Sha256}.zip"));
        var record = Instance("practice", instanceRoot);
        var manager = new ModManager(
            instanceRoot,
            Path.Combine(versionRoot, ".crystalfly", "transactions"),
            Path.Combine(stateRoot, "mods"),
            packageCacheRoot);
        var receipt = await manager.InstallFromFileAsync(installedManifest, installedPackage);
        await using var viewModel = new MainViewModel(test.CreateDirectory("app-data"))
        {
            VersionRoot = versionRoot
        };
        SetCatalog(viewModel, new GameCatalog { Mods = [updateManifest] });
        viewModel.SelectedInstance = new InstanceItemViewModel(record, record.BuildId, "modding-api-77", 1);
        var item = new InstalledModItemViewModel(receipt, updateManifest, static () => { })
        {
            IsSelected = true
        };
        viewModel.ModManagement.InstalledMods.Add(item);

        await viewModel.ModManagement.UpdateSelectedModsCommand.ExecuteAsync(null);

        Assert.Equal("1.0.0", Assert.Single(await manager.GetInstalledAsync()).Version);
    }

    [Fact]
    public async Task Bulk_mod_update_reports_corrupt_loader_receipt_without_changing_mod()
    {
        using var test = new TestDirectory();
        var versionRoot = test.CreateDirectory("versions");
        var instanceRoot = test.CreateDirectory("versions", "practice");
        var dataRoot = test.CreateDirectory("versions", "practice", "hollow_knight_Data");
        var stateRoot = test.CreateDirectory("versions", ".crystalfly", "instances", "practice");
        await File.WriteAllTextAsync(Path.Combine(instanceRoot, "hollow_knight.exe"), string.Empty);
        await File.WriteAllTextAsync(Path.Combine(dataRoot, "globalgamemanagers"), string.Empty);
        var record = Instance("practice", instanceRoot);
        await InstanceSidecar.SaveAsync(record);
        var modRelativePath = "hollow_knight_Data/Managed/Mods/DebugMod/DebugMod.dll";
        var modPath = Path.Combine(instanceRoot, modRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(modPath)!);
        await File.WriteAllTextAsync(modPath, "installed");
        var receipt = Receipt("debugmod", "1.0.0", enabled: true) with
        {
            LoaderId = "modding-api-77",
            InstallRoot = "hollow_knight_Data/Managed/Mods/DebugMod",
            Files =
            [
                new InstalledFileReceipt
                {
                    RelativePath = modRelativePath,
                    Sha256 = FileSha256(modPath)
                }
            ],
            EntryFiles = [modRelativePath]
        };
        var receiptPath = Path.Combine(test.CreateDirectory(
            "versions", ".crystalfly", "instances", "practice", "mods"), "debugmod.json");
        await AtomicJsonStore.WriteAsync(receiptPath, receipt);
        var updateManifest = Manifest("debugmod", "2.0.0") with
        {
            LoaderId = "modding-api-77",
            SupportedBuildIds = [record.BuildId]
        };
        await using var viewModel = new MainViewModel(test.CreateDirectory("app-data"))
        {
            VersionRoot = versionRoot
        };
        SetCatalog(viewModel, new GameCatalog { Mods = [updateManifest] });
        viewModel.SelectedInstance = new InstanceItemViewModel(record, record.BuildId, "Vanilla", 1);
        for (var attempt = 0; attempt < 100 && viewModel.ModManagement.InstalledMods.Count == 0; attempt++)
        {
            await Task.Delay(10);
        }
        Assert.Single(viewModel.ModManagement.InstalledMods).IsSelected = true;
        var originalReceipt = await File.ReadAllTextAsync(receiptPath);
        await File.WriteAllTextAsync(Path.Combine(stateRoot, "loader.json"), "not-json");

        await viewModel.ModManagement.UpdateSelectedModsCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
        Assert.Equal(originalReceipt, await File.ReadAllTextAsync(receiptPath));
    }

    [Fact]
    public void Mod_market_navigation_and_search_are_independent_from_installed_mods()
    {
        var viewModel = CreateViewModel();
        var debugMod = Manifest("debugmod", "2.0.0");
        var benchwarp = Manifest("benchwarp", "1.0.0");
        SetCatalog(viewModel, new GameCatalog { Mods = [debugMod, benchwarp] });
        InvokeRebuildMarketCatalog(viewModel);
        viewModel.ModManagement.InstalledMods.Add(new InstalledModItemViewModel(
            Receipt("debugmod", "1.0.0", enabled: true),
            debugMod,
            static () => { }));

        viewModel.ModManagement.ModSearchText = "debug";
        viewModel.SelectDownloadSectionCommand.Execute("ModMarket");
        viewModel.MarketSearchText = "bench";

        Assert.True(viewModel.IsModMarketDownloadSection);
        Assert.False(viewModel.IsGameVersionsDownloadSection);
        Assert.Single(viewModel.VisibleMarketMods);
        Assert.Equal("benchwarp", viewModel.VisibleMarketMods[0].Id);
        Assert.Single(viewModel.ModManagement.VisibleInstalledMods);

        viewModel.OpenMarketModCommand.Execute(benchwarp);
        Assert.True(viewModel.IsMarketDetail);
        Assert.Same(benchwarp, viewModel.SelectedMarketMod);

        viewModel.BackToMarketCommand.Execute(null);
        Assert.True(viewModel.IsMarketList);
        Assert.Null(viewModel.SelectedMarketMod);
    }

    [Fact]
    public void Mod_market_filters_by_exact_build_loader_source_and_tag()
    {
        var viewModel = CreateViewModel();
        var debugMod = Manifest("debugmod", "2.0.0") with
        {
            LoaderId = "modding-api-77",
            SupportedBuildIds = ["1.5.78.11833"],
            SourceName = "HK ModLinks",
            Tags = ["Utility"]
        };
        var overlay = Manifest("overlay", "1.0.0") with
        {
            LoaderId = "bepinex-5.4.23.4",
            SupportedBuildIds = ["latest-stable"],
            SourceName = "custom:test",
            Tags = ["Visual"]
        };

        SetCatalog(viewModel, new GameCatalog { Mods = [debugMod, overlay] });
        InvokeRebuildMarketCatalog(viewModel);

        viewModel.SelectedMarketBuildOption = new("1.5.78.11833", "1.5.78.11833");
        viewModel.SelectedMarketLoaderOption = new("modding-api-77", "Modding API v77");
        viewModel.SelectedMarketSourceOption = new("HK ModLinks", "HK ModLinks");
        viewModel.SelectedMarketTagOption = new("Utility", "Utility");

        Assert.Single(viewModel.VisibleMarketMods);
        Assert.Equal("debugmod", viewModel.VisibleMarketMods[0].Id);
    }

    [Fact]
    public void Mod_market_exposes_1578_official_mods_for_latest_api78()
    {
        var viewModel = CreateViewModel();
        var mod = Manifest("hkmod:Benchwarp", "1.0.0") with
        {
            LoaderId = "modding-api-77",
            SupportedBuildIds = ["1.5.78.11833"],
            SourceName = "HK ModLinks"
        };
        SetCatalog(viewModel, new GameCatalog
        {

            Loaders =
            [
                new LoaderManifest
                {
                    Id = "modding-api-77",
                    Name = "Modding API",
                    Version = "77",
                    DownloadUrl = "https://example.invalid/api77.zip",
                    Sha256 = new string('A', 64),
                    SupportedBuildIds = ["1.5.78.11833"]
                },
                new LoaderManifest
                {
                    Id = "modding-api-78",
                    Name = "Modding API",
                    Version = "78",
                    DownloadUrl = "https://example.invalid/api78.zip",
                    Sha256 = new string('B', 64),
                    SupportedBuildIds = ["1.5.12620.0"]
                }
            ],
            Mods = [mod]
        });
        InvokeRebuildMarketCatalog(viewModel);

        viewModel.SelectedMarketBuildOption = new("1.5.12620.0", "1.5.12620.0");
        viewModel.SelectedMarketLoaderOption = new("modding-api-78", "Modding API 78");

        Assert.Contains(viewModel.MarketBuildOptions, option => option.Value == "1.5.12620.0");
        Assert.Contains(viewModel.MarketLoaderOptions, option => option.Value == "modding-api-78");
        Assert.Equal("hkmod:Benchwarp", Assert.Single(viewModel.VisibleMarketMods).Id);
    }
    [Fact]
    public void Installed_mod_graph_always_shows_the_complete_instance()
    {
        var viewModel = CreateViewModel();
        var library = new InstalledModItemViewModel(
            Receipt("library", "1.0.0", enabled: true),
            null,
            static () => { });
        var feature = new InstalledModItemViewModel(
            Receipt("feature", "1.0.0", enabled: true) with { Dependencies = ["library"] },
            null,
            static () => { });
        var isolated = new InstalledModItemViewModel(
            Receipt("isolated", "1.0.0", enabled: true),
            null,
            static () => { });
        viewModel.ModManagement.InstalledMods.Add(library);
        viewModel.ModManagement.InstalledMods.Add(feature);
        viewModel.ModManagement.InstalledMods.Add(isolated);
        viewModel.ModManagement.SelectedInstalledMod = feature;

        viewModel.ModManagement.ShowInstalledModGraphCommand.Execute(null);

        Assert.True(viewModel.ModManagement.IsInstalledModGraphVisible);
        Assert.Equal(["feature", "isolated", "library"], viewModel.DependencyGraph.Graph.Nodes.Select(node => node.Id).Order());
        var edge = Assert.Single(viewModel.DependencyGraph.Graph.Edges);
        Assert.Equal("library", edge.Source.Id);
        Assert.Equal("feature", edge.Target.Id);

        viewModel.ModManagement.SelectedInstalledMod = library;

        Assert.Equal(3, viewModel.DependencyGraph.Graph.Nodes.Count);
    }

    [Fact]
    public void Mod_market_filters_recent_additions_from_activity_catalog()
    {
        var viewModel = CreateViewModel();
        var recent = Manifest("hkmod:Recent", "1.0.0");
        var older = Manifest("hkmod:Older", "1.0.0");
        SetCatalog(viewModel, new GameCatalog { Mods = [recent, older] });
        SetPrivateField(viewModel, "modActivityCatalog", new ModActivityCatalog
        {
            GeneratedAt = DateTimeOffset.Parse("2026-07-22T00:00:00Z"),
            SourceRevision = "1234567",
            Entries =
            [
                new ModActivityEntry
                {
                    Id = recent.Id,
                    AddedAt = DateTimeOffset.Parse("2026-07-20T00:00:00Z"),
                    UpdatedAt = DateTimeOffset.Parse("2026-07-20T00:00:00Z")
                },
                new ModActivityEntry
                {
                    Id = older.Id,
                    AddedAt = DateTimeOffset.Parse("2025-01-01T00:00:00Z"),
                    UpdatedAt = DateTimeOffset.Parse("2025-01-01T00:00:00Z")
                }
            ]
        });
        InvokeRebuildMarketCatalog(viewModel);

        viewModel.SelectedMarketActivityOption = new(
            MarketActivityFilter.RecentlyAdded,
            "Recently added");

        Assert.Equal(recent.Id, Assert.Single(viewModel.VisibleMarketMods).Id);
    }

    [Fact]
    public async Task Market_install_target_preparation_discards_results_when_selection_changes()
    {
        using var test = new TestDirectory();
        var versionRoot = test.CreateDirectory("versions");
        var slowRoot = test.CreateDirectory("versions", "slow");
        var vanillaRoot = test.CreateDirectory("versions", "vanilla");
        var hookPath = Path.Combine(
            test.CreateDirectory("versions", "slow", "hollow_knight_Data", "Managed"),
            "MMHOOK_Assembly-CSharp.dll");
        await using (var hook = new FileStream(hookPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            hook.SetLength(64L * 1024 * 1024);
        }
        await AtomicJsonStore.WriteAsync(
            Path.Combine(test.CreateDirectory(
                "versions", ".crystalfly", "instances", "slow"), "loader.json"),
            new InstalledPackageReceipt
            {
                PackageId = "modding-api-77",
                LoaderState = LoaderState.ModdingApi,
                Files =
                [
                    new InstalledFileReceipt
                    {
                        RelativePath = "hollow_knight_Data/Managed/MMHOOK_Assembly-CSharp.dll",
                        Sha256 = new string('0', 64)
                    }
                ]
            });
        var first = Manifest("first", "1.0.0") with
        {
            LoaderId = "modding-api-77",
            SupportedBuildIds = ["build-1"]
        };
        var second = Manifest("second", "1.0.0") with
        {
            LoaderId = "bepinex-5.4.23.4",
            SupportedBuildIds = ["build-1"]
        };
        await using var viewModel = new MainViewModel(test.CreateDirectory("app-data"))
        {
            VersionRoot = versionRoot,
            SelectedMarketMod = first
        };
        SetCatalog(viewModel, new GameCatalog { Mods = [first, second] });
        viewModel.Instances.Instances.Add(new InstanceItemViewModel(
            Instance("slow", slowRoot), "build-1", "modding-api-77", 0));
        viewModel.Instances.Instances.Add(new InstanceItemViewModel(
            Instance("vanilla", vanillaRoot), "build-1", "Vanilla", 0));

        var preparation = viewModel.PrepareMarketInstallTargetsCommand.ExecuteAsync(null);
        Assert.False(preparation.IsCompleted);
        viewModel.SelectedMarketMod = second;

        await preparation;

        Assert.Empty(viewModel.MarketInstallTargets);
        Assert.Null(viewModel.SelectedMarketInstallTarget);
    }

    [Fact]
    public async Task Market_install_targets_show_loader_bootstrap_and_block_official_speedrun_instances()
    {
        using var test = new TestDirectory();
        var versionRoot = test.CreateDirectory("versions");
        var normalRoot = test.CreateDirectory("versions", "practice");
        var speedrunRoot = test.CreateDirectory("versions", "race");
        var manifest = Manifest("benchwarp", "1.0.0") with
        {
            LoaderId = "modding-api-77",
            SupportedBuildIds = ["1.5.78.11833"]
        };
        var viewModel = CreateViewModel();
        viewModel.VersionRoot = versionRoot;
        SetCatalog(viewModel, new GameCatalog
        {
            Loaders =
            [
                new LoaderManifest
                {
                    Id = "modding-api-77",
                    Name = "Modding API v77",
                    Version = "77",
                    DownloadUrl = "https://example.invalid/loader.zip",
                    Sha256 = new string('B', 64),
                    SupportedBuildIds = ["1.5.78.11833"]
                }
            ],
            Mods = [manifest]
        });
        viewModel.SelectedMarketMod = manifest;
        viewModel.Instances.Instances.Add(new InstanceItemViewModel(
            Instance("practice", normalRoot) with { BuildId = "1.5.78.11833", Name = "Practice" },
            "1.5.78.11833",
            "Vanilla",
            0));
        viewModel.Instances.Instances.Add(new InstanceItemViewModel(
            Instance("race", speedrunRoot) with
            {
                BuildId = "1.5.78.11833",
                Name = "Race",
                Purpose = InstancePurpose.OfficialSpeedrun
            },
            "1.5.78.11833",
            "Vanilla",
            0));

        await viewModel.PrepareMarketInstallTargetsCommand.ExecuteAsync(null);

        Assert.Equal(2, viewModel.MarketInstallTargets.Count);
        var practice = Assert.Single(viewModel.MarketInstallTargets, target => target.Instance.Id == "practice");
        Assert.True(practice.IsAvailable);
        Assert.True(practice.RequiresLoader);
        Assert.Contains("Modding API v77", practice.StatusText, StringComparison.OrdinalIgnoreCase);
        var race = Assert.Single(viewModel.MarketInstallTargets, target => target.Instance.Id == "race");
        Assert.False(race.IsAvailable);
        Assert.Contains(viewModel.Loc["OfficialSpeedrunModBlocked"], race.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Market_install_bootstraps_required_loader_before_installing_mod()
    {
        using var test = new TestDirectory();
        var applicationDataRoot = test.CreateDirectory("app-data");
        var versionRoot = test.CreateDirectory("versions");
        var instanceRoot = test.CreateDirectory("versions", "practice");
        var managedRoot = test.CreateDirectory("versions", "practice", "hollow_knight_Data", "Managed");
        await File.WriteAllTextAsync(Path.Combine(instanceRoot, "hollow_knight.exe"), string.Empty);
        await File.WriteAllTextAsync(
            Path.Combine(instanceRoot, "hollow_knight_Data", "globalgamemanagers"),
            string.Empty);
        await File.WriteAllTextAsync(Path.Combine(managedRoot, "Assembly-CSharp.dll"), "vanilla");
        var record = Instance("practice", instanceRoot) with
        {
            Name = "Practice",
            BuildId = "1.5.78.11833"
        };
        await InstanceSidecar.SaveAsync(record);

        var packages = test.CreateDirectory("packages");
        var loaderPackage = Path.Combine(packages, "loader.zip");
        var modPackage = Path.Combine(packages, "mod.zip");
        CreateZip(loaderPackage, ("MMHOOK_Assembly-CSharp.dll", "loader"));
        CreateZip(modPackage, ("mod.dll", "mod"));
        var loaderHash = FileSha256(loaderPackage);
        var modHash = FileSha256(modPackage);
        var cacheRoot = test.CreateDirectory("versions", ".crystalfly", "packages");
        File.Copy(loaderPackage, Path.Combine(cacheRoot, $"{loaderHash}.zip"));
        File.Copy(modPackage, Path.Combine(cacheRoot, $"{modHash}.zip"));

        var loader = new LoaderManifest
        {
            Id = "modding-api-77",
            Name = "Modding API v77",
            Version = "77",
            DownloadUrl = "https://example.invalid/loader.zip",
            SizeBytes = new FileInfo(loaderPackage).Length,
            Sha256 = loaderHash,
            SupportedBuildIds = ["1.5.78.11833"]
        };
        var mod = Manifest("sample-mod", "1.0.0") with
        {
            Name = "Sample Mod",
            LoaderId = loader.Id,
            DownloadUrl = "https://example.invalid/mod.zip",
            SizeBytes = new FileInfo(modPackage).Length,
            Sha256 = modHash,
            SupportedBuildIds = ["1.5.78.11833"]
        };
        await using var viewModel = new MainViewModel(applicationDataRoot) { VersionRoot = versionRoot };
        var notifications = new List<string>();
        viewModel.ToastRequested += notifications.Add;
        SetCatalog(viewModel, new GameCatalog { Loaders = [loader], Mods = [mod] });
        viewModel.Instances.Instances.Add(new InstanceItemViewModel(record, "1.5.78.11833", "Vanilla", 0));
        viewModel.SelectedMarketMod = mod;
        await viewModel.PrepareMarketInstallTargetsCommand.ExecuteAsync(null);

        await viewModel.InstallMarketModCommand.ExecuteAsync(null);

        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal(viewModel.Loc["AddedToDownloadQueue"], Assert.Single(notifications));
        await viewModel.DownloadCenter.DownloadQueue.WaitForIdleAsync();
        Assert.True(File.Exists(Path.Combine(managedRoot, "MMHOOK_Assembly-CSharp.dll")));
        Assert.True(File.Exists(Path.Combine(managedRoot, "Mods", "Sample Mod", "mod.dll")));
    }

    [Fact]
    public async Task Market_install_target_inspection_failure_blocks_only_that_instance()
    {
        using var test = new TestDirectory();
        var versionRoot = test.CreateDirectory("versions");
        var instanceRoot = test.CreateDirectory("versions", "broken");
        var stateRoot = test.CreateDirectory(
            "versions",
            ".crystalfly",
            "instances",
            "broken");
        await File.WriteAllTextAsync(Path.Combine(stateRoot, "loader.json"), "not-json");
        var mod = Manifest("sample-mod", "1.0.0") with
        {
            LoaderId = "modding-api-77",
            SupportedBuildIds = ["1.5.78.11833"]
        };
        var viewModel = CreateViewModel();
        viewModel.VersionRoot = versionRoot;
        SetCatalog(viewModel, new GameCatalog { Mods = [mod] });
        viewModel.SelectedMarketMod = mod;
        viewModel.Instances.Instances.Add(new InstanceItemViewModel(
            Instance("broken", instanceRoot) with { BuildId = "1.5.78.11833" },
            "1.5.78.11833",
            "Unknown",
            0));

        await viewModel.PrepareMarketInstallTargetsCommand.ExecuteAsync(null);

        var target = Assert.Single(viewModel.MarketInstallTargets);
        Assert.False(target.IsAvailable);
        Assert.Equal(viewModel.Loc["ErrorDataInvalid"], target.StatusText);
    }

    [Theory]
    [InlineData("hkmod:MissingDependency")]
    [InlineData("custom:test:MissingDependency")]
    public async Task Market_install_target_with_missing_dependency_is_unavailable(string missingId)
    {
        using var test = new TestDirectory();
        var versionRoot = test.CreateDirectory("versions");
        var instanceRoot = test.CreateDirectory("versions", "practice");
        var mod = Manifest("hkmod:Root", "1.0.0") with
        {
            LoaderId = "modding-api-77",
            SupportedBuildIds = ["1.5.78.11833"],
            Dependencies = [missingId]
        };
        var viewModel = CreateViewModel();
        viewModel.VersionRoot = versionRoot;
        SetCatalog(viewModel, new GameCatalog { Mods = [mod] });
        viewModel.SelectedMarketMod = mod;
        viewModel.Instances.Instances.Add(new InstanceItemViewModel(
            Instance("practice", instanceRoot) with { BuildId = "1.5.78.11833" },
            "1.5.78.11833",
            "Vanilla",
            0));

        await viewModel.PrepareMarketInstallTargetsCommand.ExecuteAsync(null);

        var target = Assert.Single(viewModel.MarketInstallTargets);
        Assert.False(target.IsAvailable);
        Assert.Null(viewModel.SelectedMarketInstallTarget);
        Assert.Contains(missingId, target.StatusText, StringComparison.Ordinal);
        await viewModel.DisposeAsync();
    }

    [Fact]
    public async Task Language_change_refreshes_localized_bindings_and_selected_option()
    {
        var viewModel = CreateViewModel();
        InvokeRebuildSettingOptions(viewModel);
        var english = viewModel.LanguageOptions.Single(option =>
            option.Value == Crystalfly.Core.Configuration.UiLanguage.English);
        var previousLocalization = viewModel.Loc;
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.SelectedLanguage = english;

        Assert.Contains(nameof(MainViewModel.Loc), changedProperties);
        // The LocalizationViewModel instance is reused on purpose so that bindings and
        // captured references keep working; the language values themselves must switch.
        Assert.Same(previousLocalization, viewModel.Loc);
        Assert.Equal("Ready", viewModel.Loc["StatusReady"]);
        Assert.Same(
            viewModel.LanguageOptions.Single(option => option.Value == english.Value),
            viewModel.SelectedLanguage);
        await viewModel.DisposeAsync();
    }

    [Fact]
    public async Task Language_switch_refreshes_preflight_and_status_bar_labels()
    {
        var viewModel = CreateViewModel();
        Assert.Equal("未选择实例", viewModel.LaunchReadinessTitle);

        InvokeApplyLanguage(viewModel, UiLanguage.English);

        Assert.Equal("No instance selected", viewModel.LaunchReadinessTitle);
        Assert.Equal(
            "Choose a version root to discover Hollow Knight instances.",
            viewModel.LaunchReadinessHint);
        Assert.Equal("Select an environment or create a new one.", viewModel.SelectedSpeedrunTechnicalStatus);
        await viewModel.DisposeAsync();
    }

    [Fact]
    public async Task Language_switch_reprojects_installed_mod_item_display_names()
    {
        var viewModel = CreateViewModel();
        var instanceRoot = applicationData.CreateDirectory("mods-l10n-instance");
        var record = Instance("practice", instanceRoot);
        var mods = new[]
        {
            new ModDiscoveryEntry
            {
                Id = "mod",
                Name = "Mod",
                LoaderId = "modding-api-77",
                InstallRoot = instanceRoot,
                Enabled = true,
                Ownership = ModOwnership.Managed,
                Files = [],
                EntryFiles = []
            }
        };
        viewModel.ModManagement.LoadInstalledMods(
            mods,
            [],
            [new ModHealthReport { ModId = "mod", Status = ModHealthStatus.Healthy }],
            record);
        var item = Assert.Single(viewModel.ModManagement.InstalledMods);
        Assert.Equal("已安装", item.OwnershipDisplayName);
        Assert.Equal("正常", item.HealthDisplayName);

        InvokeApplyLanguage(viewModel, UiLanguage.English);

        item = Assert.Single(viewModel.ModManagement.InstalledMods);
        Assert.Equal("Installed", item.OwnershipDisplayName);
        Assert.Equal("Healthy", item.HealthDisplayName);
        await viewModel.DisposeAsync();
    }

    [Fact]
    public async Task Language_switch_reprojects_download_queue_texts()
    {
        var viewModel = CreateViewModel();
        InvokeQueueDownloadQueueProjection(viewModel, QueueGroup("group", DownloadQueueGroupState.Completed));
        InvokeApplyPendingDownloadQueueProjection(viewModel);
        var group = Assert.Single(viewModel.DownloadCenter.DownloadQueueGroups);
        Assert.Equal("已完成", group.StateText);

        InvokeApplyLanguage(viewModel, UiLanguage.English);
        InvokeApplyPendingDownloadQueueProjection(viewModel);

        Assert.Equal("Completed", group.StateText);
        Assert.Equal("Completed", group.Items[0].StateText);
        await viewModel.DisposeAsync();
    }

    [Fact]
    public async Task Language_switch_refreshes_speedrun_activity_texts_and_items()
    {
        string root = applicationData.CreateDirectory("speedrun-l10n");
        // Seed a stale baseline so the refresh detects the returned run as a new world record.
        var baselineAt = DateTimeOffset.Parse("2026-07-31T00:00:00Z");
        var board = new SpeedrunBoardDescriptor(
            SpeedrunGame.HollowKnight,
            "category-any",
            "Any%",
            null,
            null,
            []);
        await AtomicJsonStore.WriteAsync(
            Path.Combine(root, "speedrun-activity.json"),
            new SpeedrunActivityDocument
            {
                Boards = new Dictionary<string, SpeedrunBoardBaseline>(StringComparer.Ordinal)
                {
                    [board.Key] = new(
                        board,
                        baselineAt,
                        [new("old-run", 1, "Old Runner", "PT40M", 2400, baselineAt, null)])
                }
            },
            CancellationToken.None);

        using var policy = new NetworkPolicy();
        using var httpClient = new HttpClient(new NewRecordSpeedrunResponseHandler());
        var speedrunClient = new SpeedrunComClient(
            httpClient,
            Path.Combine(root, "speedrun-cache"),
            policy);
        await using var viewModel = new MainViewModel(
            root,
            speedrunComClientOverride: speedrunClient)
        {
            CurrentPage = "Speedrun"
        };
        await viewModel.RefreshSpeedrunActivityCommand.ExecuteAsync(null);

        Assert.Equal("监控已建立", viewModel.SpeedrunActivityStatus);
        var activity = Assert.Single(viewModel.SpeedrunActivities);
        Assert.Equal("世界纪录", activity.KindText);
        Assert.Equal("空洞骑士 · Any%", activity.BoardName);

        InvokeApplyLanguage(viewModel, UiLanguage.English);

        Assert.Equal("Monitoring active", viewModel.SpeedrunActivityStatus);
        activity = Assert.Single(viewModel.SpeedrunActivities);
        Assert.Equal("World record", activity.KindText);
        Assert.Equal("Hollow Knight · Any%", activity.BoardName);
        await viewModel.DisposeAsync();
    }

    [Fact]
    public async Task Language_switch_refreshes_preset_apply_step_texts()
    {
        var viewModel = CreateViewModel();
        var plan = new PresetApplyPlan
        {
            Preset = new ModPreset
            {
                Id = "preset",
                Name = "Pack",
                GameBuildId = "build-1",
                LoaderId = "modding-api-77"
            },
            Steps =
            [
                new PresetApplyStep
                {
                    Kind = PresetApplyStepKind.Install,
                    State = PresetApplyStepState.Pending,
                    ModId = "mod",
                    Reason = string.Empty
                },
                new PresetApplyStep
                {
                    Kind = PresetApplyStepKind.Enable,
                    State = PresetApplyStepState.Satisfied,
                    ModId = "mod2",
                    Reason = string.Empty
                }
            ],
            PreApplyStates = []
        };
        var projectSteps = typeof(MainViewModel).GetMethod(
            "ProjectPresetApplySteps",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(projectSteps);
        projectSteps.Invoke(viewModel, [plan]);
        Assert.Equal("安装", viewModel.PresetApplySteps[0].Action);
        Assert.Equal("将更改", viewModel.PresetApplySteps[0].State);
        Assert.Equal("启用", viewModel.PresetApplySteps[1].Action);
        Assert.Equal("已就绪", viewModel.PresetApplySteps[1].State);

        InvokeApplyLanguage(viewModel, UiLanguage.English);

        Assert.Equal("Install", viewModel.PresetApplySteps[0].Action);
        Assert.Equal("Will change", viewModel.PresetApplySteps[0].State);
        Assert.Equal("Enable", viewModel.PresetApplySteps[1].Action);
        Assert.Equal("Ready", viewModel.PresetApplySteps[1].State);
        await viewModel.DisposeAsync();
    }

    [Fact]
    public async Task Language_switch_refreshes_settings_option_labels()
    {
        var viewModel = CreateViewModel();
        InvokeRebuildSettingOptions(viewModel);
        Assert.Contains(viewModel.LanguageOptions, option => option.Name == "简体中文");

        InvokeApplyLanguage(viewModel, UiLanguage.English);

        Assert.Contains(viewModel.LanguageOptions, option => option.Name == "Simplified Chinese");
        Assert.Contains(viewModel.Settings.ThemeOptions, option => option.Name == "Dark");
        Assert.Contains(viewModel.Settings.MotionOptions, option => option.Name == "Follow system");
        Assert.Contains(viewModel.Settings.GitHubRouteOptions, option => option.Name == "GitHub mirror");
        await viewModel.DisposeAsync();
    }

    [Fact]
    public async Task Applying_language_notifies_official_catalog_labels()
    {
        var viewModel = CreateViewModel();
        SetOfficialCatalogResult(viewModel, new(
            Crystalfly.Core.Catalog.OfficialCatalogLoadStatus.Cached,
            new GameCatalog(),
            "77",
            650,
            null));
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        InvokeApplyLanguage(viewModel, UiLanguage.English);

        Assert.Contains(nameof(MainViewModel.OfficialModCatalogStatus), changedProperties);
        Assert.Contains(nameof(MainViewModel.OfficialModCatalogSummary), changedProperties);
        Assert.Contains(nameof(MainViewModel.OfficialModCatalogError), changedProperties);
        await viewModel.DisposeAsync();
    }

    [Fact]
    public async Task Language_change_rebuilds_all_mod_market_filter_labels()
    {
        var viewModel = CreateViewModel();
        InvokeRebuildSettingOptions(viewModel);
        viewModel.SelectedLanguage = viewModel.LanguageOptions.Single(option =>
            option.Value == UiLanguage.SimplifiedChinese);
        SetCatalog(viewModel, new GameCatalog
        {
            Builds =
            [
                new GameBuild
                {
                    Id = "1.5.78.11833",
                    DisplayVersion = "1.5.78.11833",
                    ManifestId = "0",
                    ExecutableSha256 = new string('C', 64),
                    GlobalGameManagersSha256 = new string('D', 64)
                }
            ],
            Loaders =
            [
                new LoaderManifest
                {
                    Id = "modding-api-77",
                    Name = "Modding API",
                    Version = "77",
                    DownloadUrl = "https://example.invalid/api.zip",
                    Sha256 = new string('B', 64),
                    SupportedBuildIds = ["1.5.78.11833"]
                }
            ],
            Mods =
            [
                Manifest("hkmod:DebugMod", "2.0.0") with
                {
                    Name = "DebugMod",
                    DisplayName = "DebugMod",
                    LoaderId = "modding-api-77",
                    SupportedBuildIds = ["1.5.78.11833"],
                    SourceName = "HK ModLinks",
                    Tags = ["Utility"]
                }
            ]
        });
        InvokeRebuildMarketCatalog(viewModel);
        Assert.All(MarketFilterLabels(viewModel), label => Assert.Equal("全部状态", label));
        var selected = Assert.Single(viewModel.MarketMods);
        viewModel.SelectedMarketTagOption = viewModel.MarketTagOptions.Single(option => option.Value == "Utility");
        viewModel.SelectedMarketMod = selected;
        Assert.Equal("调试模组", viewModel.SelectedMarketModDisplay?.PrimaryName);

        viewModel.SelectedLanguage = viewModel.LanguageOptions.Single(option =>
            option.Value == UiLanguage.English);

        Assert.All(MarketFilterLabels(viewModel), label => Assert.Equal("All statuses", label));
        Assert.Equal("Utility", viewModel.SelectedMarketTagOption?.Value);
        Assert.Same(selected, viewModel.SelectedMarketMod);
        Assert.Equal("DebugMod", viewModel.SelectedMarketModDisplay?.PrimaryName);
        await viewModel.DisposeAsync();
    }

    [Fact]
    public async Task Language_change_rebuilds_installed_mod_projection_and_keeps_selection()
    {
        var viewModel = CreateViewModel();
        InvokeRebuildSettingOptions(viewModel);
        var manifest = Manifest("hkmod:DebugMod", "2.0.0") with
        {
            Name = "DebugMod",
            DisplayName = "DebugMod",
            Description = "Official description",
            Tags = ["Utility"]
        };
        SetCatalog(viewModel, new GameCatalog { Mods = [manifest] });
        viewModel.ModManagement.InstalledMods.Add(new InstalledModItemViewModel(
            Receipt("hkmod:DebugMod", "1.0.0", enabled: true) with { Name = "DebugMod" },
            manifest,
            static () => { },
            viewModel.ProjectMarketMod(manifest, chinese: false))
        {
            IsSelected = true
        });
        viewModel.ModManagement.SelectedInstalledMod = viewModel.ModManagement.InstalledMods[0];
        viewModel.SelectedLanguage = viewModel.LanguageOptions.Single(option =>
            option.Value == UiLanguage.SimplifiedChinese);

        var projected = Assert.Single(viewModel.ModManagement.InstalledMods);
        Assert.Equal("调试模组", projected.PrimaryName);
        Assert.Equal("DebugMod", projected.SecondaryName);
        Assert.True(projected.IsSelected);
        Assert.Same(projected, viewModel.ModManagement.SelectedInstalledMod);
        await viewModel.DisposeAsync();
    }

    [Fact]
    public async Task Instance_details_reports_access_failures_from_background_load()
    {
        using var test = new TestDirectory();
        var instanceRoot = test.CreateDirectory("instance");
        var versionRoot = test.CreateDirectory("versions");
        var record = Instance("practice", instanceRoot);
        _ = test.CreateDirectory(
            "versions",
            ".crystalfly",
            "instances",
            record.Id,
            "snapshots",
            "snapshot-1",
            "snapshot.json");
        var viewModel = CreateViewModel();
        viewModel.VersionRoot = versionRoot;
        SetPrivateField(viewModel, "detailsLoadGeneration", 1L);
        SetPrivateField(
            viewModel,
            "<SelectedInstance>k__BackingField",
            new InstanceItemViewModel(record, record.BuildId, "Vanilla", 0));

        await InvokeLoadInstanceDetailsAsync(viewModel, record, 1);

        Assert.StartsWith(viewModel.Loc["ErrorAccessDenied"], viewModel.ErrorMessage);
    }

    [Fact]
    public async Task Instance_details_waits_for_active_queue_install_before_reading_state()
    {
        using var test = new TestDirectory();
        var versionRoot = test.CreateDirectory("versions");
        var record = Instance("practice", test.CreateDirectory("versions", "practice"));
        await using var viewModel = CreateViewModel();
        viewModel.VersionRoot = versionRoot;
        SetPrivateField(
            viewModel,
            "<SelectedInstance>k__BackingField",
            new InstanceItemViewModel(record, record.BuildId, "Vanilla", 0));
        var coordinator = GetPrivateField<InstanceOperationCoordinator>(
            viewModel,
            "instanceOperationCoordinator");
        var operationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOperation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var activeOperation = coordinator.RunAsync("other-instance", async _ =>
        {
            operationStarted.TrySetResult();
            await releaseOperation.Task;
        });
        await operationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var detailsLoad = InvokeLoadInstanceDetailsAsync(viewModel, record);
        try
        {
            await Task.Delay(100);
            Assert.False(detailsLoad.IsCompleted);
        }
        finally
        {
            releaseOperation.TrySetResult();
            await activeOperation;
        }

        await detailsLoad;
    }

    [Fact]
    public async Task Refresh_holds_transaction_gate_through_instance_state_scan()
    {
        using var test = new TestDirectory();
        var versionRoot = test.CreateDirectory("versions");
        var instanceRoot = test.CreateDirectory("versions", "practice");
        _ = test.CreateDirectory("versions", "practice", "hollow_knight_Data");
        await File.WriteAllTextAsync(Path.Combine(instanceRoot, "hollow_knight.exe"), string.Empty);
        await File.WriteAllTextAsync(
            Path.Combine(instanceRoot, "hollow_knight_Data", "globalgamemanagers"),
            string.Empty);
        var record = Instance("practice", instanceRoot);
        await InstanceSidecar.SaveAsync(record);
        await using var viewModel = CreateViewModel();
        viewModel.VersionRoot = versionRoot;
        var coordinator = GetPrivateField<InstanceOperationCoordinator>(
            viewModel,
            "instanceOperationCoordinator");
        var blockerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseBlocker = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocker = coordinator.RunAsync("blocker", async _ =>
        {
            blockerEntered.SetResult();
            await releaseBlocker.Task;
        });
        await blockerEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var refresh = InvokeRefreshAsync(viewModel);
        await Task.Delay(50);
        var mutation = coordinator.RunAsync("mutation", _ =>
        {
            File.Delete(Path.Combine(instanceRoot, "hollow_knight.exe"));
            return Task.CompletedTask;
        });

        releaseBlocker.SetResult();
        await Task.WhenAll(blocker, refresh, mutation).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Contains(viewModel.Instances.Instances, instance => instance.Id == record.Id);
    }

    [Fact]
    public async Task Selecting_instance_cancels_previous_detail_load_and_dispose_waits_for_current_load()
    {
        using var test = new TestDirectory();
        var versionRoot = test.CreateDirectory("versions");
        var first = Instance("first", test.CreateDirectory("versions", "first"));
        var second = Instance("second", test.CreateDirectory("versions", "second"));
        var viewModel = CreateViewModel();
        viewModel.VersionRoot = versionRoot;
        var coordinator = GetPrivateField<InstanceOperationCoordinator>(
            viewModel,
            "instanceOperationCoordinator");
        var blockerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseBlocker = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocker = coordinator.RunAsync("blocker", async _ =>
        {
            blockerEntered.SetResult();
            await releaseBlocker.Task;
        });
        await blockerEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            viewModel.SelectedInstance = new InstanceItemViewModel(first, first.BuildId, "Vanilla", 0);
            var firstCancellation = GetPrivateAssignableField<CancellationTokenSource>(
                viewModel,
                "detailsLoadCancellation").Token;

            viewModel.SelectedInstance = new InstanceItemViewModel(second, second.BuildId, "Vanilla", 0);
            var currentLoad = GetPrivateAssignableField<Task>(viewModel, "detailsLoadTask");

            Assert.True(firstCancellation.IsCancellationRequested);
            await viewModel.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(currentLoad.IsCompleted);
        }
        finally
        {
            releaseBlocker.TrySetResult();
            await blocker;
        }
    }

    [Fact]
    public async Task Queue_retry_clears_terminal_refresh_marker_before_ui_projection_runs()
    {
        await using var viewModel = CreateViewModel();
        var refreshed = GetPrivateField<HashSet<string>>(
            viewModel.DownloadCenter,
            "refreshedTerminalQueueGroups");
        refreshed.Add("retry-group");

        InvokeQueueDownloadQueueProjection(
            viewModel,
            QueueGroup("retry-group", DownloadQueueGroupState.Pending));

        Assert.DoesNotContain("retry-group", refreshed);

        InvokeQueueDownloadQueueProjection(
            viewModel,
            QueueGroup("retry-group", DownloadQueueGroupState.Completed));

        Assert.Contains("retry-group", refreshed);
    }

    [Fact]
    public async Task Terminal_snapshot_marks_every_group_for_one_coalesced_refresh()
    {
        await using var viewModel = CreateViewModel();
        var refreshed = GetPrivateField<HashSet<string>>(
            viewModel.DownloadCenter,
            "refreshedTerminalQueueGroups");

        InvokeQueueDownloadQueueProjection(
            viewModel,
            QueueGroup("completed-group", DownloadQueueGroupState.Completed),
            QueueGroup("failed-group", DownloadQueueGroupState.Failed));

        Assert.Contains("completed-group", refreshed);
        Assert.Contains("failed-group", refreshed);
    }

    [Fact]
    public async Task Instance_detail_loading_is_cleared_only_by_current_generation()
    {
        using var test = new TestDirectory();
        var versionRoot = test.CreateDirectory("versions");
        var first = Instance("first", test.CreateDirectory("versions", "first"));
        var second = Instance("second", test.CreateDirectory("versions", "second"));
        var viewModel = CreateViewModel();
        viewModel.VersionRoot = versionRoot;
        SetPrivateField(viewModel, "detailsLoadGeneration", 2L);
        SetPrivateField(
            viewModel,
            "<SelectedInstance>k__BackingField",
            new InstanceItemViewModel(second, second.BuildId, "Vanilla", 0));
        viewModel.IsLoadingInstanceDetails = true;

        await InvokeLoadInstanceDetailsAsync(viewModel, second, 1);

        Assert.True(viewModel.IsLoadingInstanceDetails);

        await InvokeLoadInstanceDetailsAsync(viewModel, first, 2);

        Assert.True(viewModel.IsLoadingInstanceDetails);

        await InvokeLoadInstanceDetailsAsync(viewModel, second, 2);

        Assert.False(viewModel.IsLoadingInstanceDetails);
    }

    [Fact]
    public async Task Instance_details_include_external_mods_and_health_in_launch_preflight()
    {
        using var test = new TestDirectory();
        var versionRoot = test.CreateDirectory("versions");
        var instanceRoot = test.CreateDirectory("versions", "practice");
        var modRoot = Path.Combine(instanceRoot, "hollow_knight_Data", "Managed", "Mods", "ExternalHelper");
        Directory.CreateDirectory(modRoot);
        await File.WriteAllTextAsync(Path.Combine(modRoot, "ExternalHelper.dll"), "external");
        await File.WriteAllTextAsync(Path.Combine(instanceRoot, "hollow_knight.exe"), "game");
        var record = Instance("practice", instanceRoot);
        await using var viewModel = CreateViewModel();
        viewModel.VersionRoot = versionRoot;
        SetPrivateField(viewModel, "detailsLoadGeneration", 1L);
        SetPrivateField(
            viewModel,
            "<SelectedInstance>k__BackingField",
            new InstanceItemViewModel(record, record.BuildId, "Drifted", 0));

        await InvokeLoadInstanceDetailsAsync(viewModel, record, 1);

        var external = Assert.Single(viewModel.ModManagement.InstalledMods);
        Assert.True(external.IsExternal);
        Assert.Null(external.Receipt);
        Assert.Equal(ModHealthStatus.UnmanagedExternal, external.HealthStatus);
        Assert.Contains(viewModel.LaunchPreflight.Issues, issue =>
            issue.Code == LaunchIssueCode.UnmanagedExternalMod
            && issue.SubjectModId == external.Id);
        Assert.True(viewModel.HasLaunchIssues);
        Assert.True(viewModel.LaunchIssueCount > 0);
    }

    [Fact]
    public async Task Acknowledging_unchanged_launch_warnings_keeps_red_state_but_allows_normal_launch()
    {
        using var test = new TestDirectory();
        var applicationDataRoot = test.CreateDirectory("app-data");
        var instanceRoot = test.CreateDirectory("instance");
        var record = Instance("practice", instanceRoot);
        var viewModel = new MainViewModel(applicationDataRoot);
        SetPrivateField(
            viewModel,
            "<SelectedInstance>k__BackingField",
            new InstanceItemViewModel(record, record.BuildId, "Vanilla", 0));
        viewModel.LaunchPreflight = new LaunchPreflightResult(
            true,
            true,
            true,
            true,
            [
                new LaunchPreflightIssue
                {
                    Code = LaunchIssueCode.ModModifiedFile,
                    Severity = LaunchIssueSeverity.Warning,
                    SubjectModId = "debugmod",
                    RelativeFilePath = "Mods/DebugMod.dll",
                    CurrentFileSha256 = new string('A', 64)
                }
            ]);

        Assert.True(viewModel.HasLaunchIssues);
        Assert.True(viewModel.CanAttemptLaunch);
        Assert.False(viewModel.CanLaunch);

        await viewModel.AcknowledgeLaunchWarningsCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasLaunchIssues);
        Assert.True(Assert.Single(viewModel.LaunchPreflight.Issues).IsAcknowledged);
        Assert.True(viewModel.CanLaunch);
        await viewModel.DisposeAsync();
        var saved = await CrystalflySettingsStore.LoadAsync(
            Path.Combine(applicationDataRoot, "settings.json"));
        Assert.Single(saved.ModHealthAcknowledgements);
    }

    [Fact]
    public async Task Absolute_launch_blocker_disables_attempt_and_force_paths()
    {
        await using var viewModel = CreateViewModel();
        SetPrivateField(
            viewModel,
            "<SelectedInstance>k__BackingField",
            new InstanceItemViewModel(
                Instance("practice", applicationData.CreateDirectory("blocked-instance")),
                "build-1",
                "Conflict",
                0));
        viewModel.LaunchPreflight = new LaunchPreflightResult(
            false,
            false,
            true,
            true,
            [
                new LaunchPreflightIssue
                {
                    Code = LaunchIssueCode.LoaderConflict,
                    Severity = LaunchIssueSeverity.Blocking
                }
            ]);

        Assert.False(viewModel.CanAttemptLaunch);
        Assert.False(viewModel.CanLaunch);
        Assert.False(viewModel.LaunchPreflight.CanForceLaunch);
    }

    [Fact]
    public async Task Force_launch_bypasses_mod_file_issue_but_normal_launch_does_not()
    {
        using var test = new TestDirectory();
        var applicationDataRoot = test.CreateDirectory("app-data");
        var versionRoot = test.CreateDirectory("versions");
        var instanceRoot = test.CreateDirectory("versions", "practice");
        await File.WriteAllTextAsync(Path.Combine(instanceRoot, "hollow_knight.exe"), "game");
        var record = Instance("practice", instanceRoot);
        Directory.CreateDirectory(Path.Combine(
            versionRoot,
            ".crystalfly",
            "instances",
            record.Id,
            "local-low"));
        var receiptsRoot = test.CreateDirectory(
            "versions",
            ".crystalfly",
            "instances",
            record.Id,
            "mods");
        await AtomicJsonStore.WriteAsync(
            Path.Combine(receiptsRoot, "debugmod.json"),
            new InstalledModReceipt
            {
                Id = "debugmod",
                Name = "Debug Mod",
                Version = "1.0.0",
                LoaderId = "modding-api-77",
                InstallRoot = "hollow_knight_Data/Managed/Mods/DebugMod",
                EntryFiles = ["hollow_knight_Data/Managed/Mods/DebugMod/DebugMod.dll"],
                Files =
                [
                    new InstalledFileReceipt
                    {
                        RelativePath = "hollow_knight_Data/Managed/Mods/DebugMod/DebugMod.dll",
                        Sha256 = new string('A', 64)
                    }
                ]
            });
        var launched = 0;
        var viewModel = new MainViewModel(
            applicationDataRoot,
            () =>
            {
                launched++;
                return Task.CompletedTask;
            },
            null,
            null)
        {
            VersionRoot = versionRoot
        };
        SetPrivateField(
            viewModel,
            "<SelectedInstance>k__BackingField",
            new InstanceItemViewModel(record, record.BuildId, "Vanilla", 1));

        await viewModel.LaunchGameCommand.ExecuteAsync(null);
        Assert.Equal(0, launched);

        await viewModel.ForceLaunchGameCommand.ExecuteAsync(null);
        Assert.Equal(1, launched);
        Assert.Contains(viewModel.LaunchPreflight.Issues, issue =>
            issue.Code == LaunchIssueCode.ModCriticalFileMissing
            && issue.Severity == LaunchIssueSeverity.Forceable);
        await viewModel.DisposeAsync();
    }

    [Fact]
    public void Clearing_selected_instance_immediately_clears_detail_loading()
    {
        var record = Instance("practice", applicationData.CreateDirectory("instance"));
        var viewModel = CreateViewModel();
        SetPrivateField(
            viewModel,
            "<SelectedInstance>k__BackingField",
            new InstanceItemViewModel(record, record.BuildId, "Vanilla", 0));
        viewModel.IsLoadingInstanceDetails = true;

        viewModel.SelectedInstance = null;

        Assert.False(viewModel.IsLoadingInstanceDetails);
    }

    [Theory]
    [InlineData(typeof(UnauthorizedAccessException), true)]
    [InlineData(typeof(InvalidOperationException), true)]
    [InlineData(typeof(NullReferenceException), false)]
    public void Instance_details_exception_boundary_handles_only_expected_failures(
        Type exceptionType,
        bool expected)
    {
        var method = typeof(MainViewModel).GetMethod(
            "IsExpectedInstanceDetailsException",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;
        Assert.Equal(expected, method.Invoke(null, [exception]));
    }

    [Theory]
    [InlineData(typeof(IOException), true)]
    [InlineData(typeof(UnauthorizedAccessException), true)]
    [InlineData(typeof(InvalidOperationException), false)]
    public void Settings_exception_boundary_handles_only_file_failures(
        Type exceptionType,
        bool expected)
    {
        var method = typeof(MainViewModel).GetMethod(
            "IsExpectedSettingsException",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;
        Assert.Equal(expected, method.Invoke(null, [exception]));
    }

    [Fact]
    public async Task Instance_details_does_not_mask_programming_errors()
    {
        using var test = new TestDirectory();
        var viewModel = CreateViewModel();
        viewModel.VersionRoot = test.CreateDirectory("versions");
        var record = Instance("..", test.CreateDirectory("instance"));
        SetPrivateField(viewModel, "detailsLoadGeneration", 1L);
        SetPrivateField(
            viewModel,
            "<SelectedInstance>k__BackingField",
            new InstanceItemViewModel(record, record.BuildId, "Vanilla", 0));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            InvokeLoadInstanceDetailsAsync(viewModel, record, 1));
    }

    private static InstalledModReceipt Receipt(
        string id,
        string version,
        bool enabled,
        bool isLocal = false) => new()
    {
        Id = id,
        Name = id,
        Version = version,
        LoaderId = "modding-api",
        InstallRoot = $"Mods/{id}",
        Enabled = enabled,
        IsLocal = isLocal,
        Ownership = isLocal ? ModOwnership.LocalTakenOver : ModOwnership.Managed
    };

    private static InstalledModItemViewModel Installed(string id) => new(
        Receipt(id, "1.0.0", enabled: true) with { Name = id },
        null,
        static () => { });

    private static ModManifest Manifest(string id, string version) => new()
    {
        Id = id,
        Name = id,
        Version = version,
        DownloadUrl = $"https://example.invalid/{id}.zip",
        Sha256 = new string('A', 64),
        LoaderId = "modding-api"
    };

    private static ModManifest MarketManifest(string id, string name) => new()
    {
        Id = id,
        Name = name,
        Version = "1.0.0",
        DownloadUrl = $"https://example.invalid/{name}.zip",
        Sha256 = new string('A', 64),
        LoaderId = "modding-api-77",
        RepositoryUrl = $"https://github.com/example/{name}"
    };

    private static ModContentLoadResult ContentResult(ModManifest manifest, string readme) => new(
        ModContentLoadStatus.Remote,
        new ModContentDocument
        {
            RepositoryUrl = manifest.RepositoryUrl!,
            ReadmeMarkdown = readme,
            ReleaseNotesMarkdown = "## Release",
            UpdatedAt = DateTimeOffset.UtcNow
        },
        null);

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!predicate())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private static LoaderManifest LoaderManifestFor(string id, string packagePath) => new()
    {
        Id = id,
        Name = id,
        Version = id[(id.LastIndexOf('-') + 1)..],
        DownloadUrl = "https://example.invalid/loader.zip",
        SizeBytes = new FileInfo(packagePath).Length,
        Sha256 = FileSha256(packagePath),
        SupportedBuildIds = ["1.5.78.11833"]
    };

    private static string LoaderPackageEntry(string id) =>
        id.StartsWith("bepinex-", StringComparison.OrdinalIgnoreCase)
            ? "BepInEx/core/BepInEx.dll"
            : "MMHOOK_Assembly-CSharp.dll";

    private static string LoaderInstalledPath(string id) =>
        id.StartsWith("bepinex-", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine("BepInEx", "core", "BepInEx.dll")
            : Path.Combine("hollow_knight_Data", "Managed", "MMHOOK_Assembly-CSharp.dll");

    private static InstanceRecord Instance(string id, string rootPath) => new()
    {
        Id = id,
        Name = id,
        RootPath = rootPath,
        BuildId = "build-1",
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static Task InvokeLoadInstanceDetailsAsync(
        MainViewModel viewModel,
        InstanceRecord record,
        long generation = 0)
    {
        var method = typeof(MainViewModel).GetMethod(
            "LoadInstanceDetailsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsAssignableFrom<Task>(method.Invoke(
            viewModel,
            [record, generation, CancellationToken.None]));
    }

    private static Task InvokeLoadModPresetsAsync(
        MainViewModel viewModel,
        InstanceRecord record)
    {
        var method = typeof(MainViewModel).GetMethod(
            "LoadModPresetsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsAssignableFrom<Task>(method.Invoke(
            viewModel,
            [record, CancellationToken.None]));
    }

    private static string PresetFileName(string id) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id))) + ".json";

    private static Task InvokeRefreshAsync(MainViewModel viewModel)
    {
        var method = typeof(MainViewModel).GetMethod(
            "RefreshAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsAssignableFrom<Task>(method.Invoke(viewModel, null));
    }

    private static void InvokeQueueDownloadQueueProjection(
        MainViewModel viewModel,
        params DownloadQueueGroup[] groups)
    {
        var method = typeof(DownloadCenterViewModel).GetMethod(
            "QueueDownloadQueueProjection",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(viewModel.DownloadCenter, [groups]);
    }

    private static void InvokeApplyPendingDownloadQueueProjection(MainViewModel viewModel)
    {
        var method = typeof(DownloadCenterViewModel).GetMethod(
            "ApplyPendingDownloadQueueProjection",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(viewModel.DownloadCenter, null);
    }

    private static DownloadQueueGroup QueueGroup(string id, DownloadQueueGroupState state) => new()
    {
        Id = id,
        DeduplicationKey = $"instance:mod:{id}",
        Name = id,
        TargetInstanceId = "instance",
        TargetInstanceName = "Instance",
        TargetInstanceRoot = "C:\\versions\\instance",
        CreatedAt = DateTimeOffset.UtcNow,
        State = state,
        Items =
        [
            new DownloadQueueItem
            {
                Id = $"{id}:item",
                PackageId = id,
                Name = id,
                State = state switch
                {
                    DownloadQueueGroupState.Completed => DownloadQueueItemState.Completed,
                    DownloadQueueGroupState.Failed => DownloadQueueItemState.Failed,
                    DownloadQueueGroupState.Canceled => DownloadQueueItemState.Canceled,
                    _ => DownloadQueueItemState.Pending
                }
            }
        ]
    };

    private static void InstallExternalBepInEx(string instanceRoot)
    {
        var coreRoot = Path.Combine(instanceRoot, "BepInEx", "core");
        Directory.CreateDirectory(coreRoot);
        File.Copy(typeof(MainViewModel).Assembly.Location, Path.Combine(coreRoot, "BepInEx.dll"));
    }

    private static void InstallExternalModdingApi(string instanceRoot)
    {
        var managedRoot = Path.Combine(instanceRoot, "hollow_knight_Data", "Managed");
        Directory.CreateDirectory(managedRoot);
        File.WriteAllText(Path.Combine(managedRoot, "MMHOOK_Assembly-CSharp.dll"), "hook");
    }

    private static void InvokeRebuildSettingOptions(MainViewModel viewModel)
    {
        var method = typeof(MainViewModel).GetMethod(
            "RebuildSettingOptions",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(viewModel, null);
    }

    private static void InvokeApplyLanguage(MainViewModel viewModel, UiLanguage language)
    {
        var method = typeof(MainViewModel).GetMethod(
            "ApplyLanguage",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(viewModel, [language]);
    }

    private static void SetOfficialCatalogResult(
        MainViewModel viewModel,
        Crystalfly.Core.Catalog.OfficialCatalogLoadResult result)
    {
        var field = typeof(MainViewModel).GetField(
            "officialCatalogResult",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(viewModel, result);
    }

    private static void InvokeRebuildMarketCatalog(MainViewModel viewModel)
    {
        var method = typeof(MainViewModel).GetMethod(
            "RebuildMarketCatalog",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(viewModel, null);
    }

    private static string[] MarketFilterLabels(MainViewModel viewModel) =>
    [
        viewModel.MarketBuildOptions[0].Name,
        viewModel.MarketLoaderOptions[0].Name,
        viewModel.MarketSourceOptions[0].Name,
        viewModel.MarketTagOptions[0].Name
    ];

    private static void SetCatalog(MainViewModel viewModel, GameCatalog catalog)
    {
        var field = typeof(MainViewModel).GetField(
            "catalog",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(viewModel, catalog);
    }

    private static void SetPrivateField(MainViewModel viewModel, string name, object value)
    {
        var field = typeof(MainViewModel).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(viewModel, value);
    }

    private static void SetCurrentLoaderInspection(MainViewModel viewModel, LoaderInspection inspection)
    {
        var field = typeof(MainViewModel).GetField(
            "currentLoaderInspection",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(viewModel, inspection);
    }

    private static T GetPrivateField<T>(object target, string name)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field.GetValue(target));
    }

    private static T GetPrivateAssignableField<T>(MainViewModel viewModel, string name)
    {
        var field = typeof(MainViewModel).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsAssignableFrom<T>(field.GetValue(viewModel));
    }

    private static void CreateZip(string path, params (string Name, string Content)[] entries)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var item in entries)
        {
            var entry = archive.CreateEntry(item.Name);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8, leaveOpen: false);
            writer.Write(item.Content);
        }
    }

    private static string FileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string InvokeUniqueInstanceName(MainViewModel viewModel, string version)
    {
        var method = typeof(MainViewModel).GetMethod(
            "UniqueInstanceName",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<string>(method.Invoke(viewModel, [version]));
    }

    [Fact]
    public async Task Mod_pack_workspace_filters_lists_without_losing_the_selected_pack()
    {
        await using var viewModel = CreateViewModel();
        var practice = new ModPreset
        {
            Id = "practice",
            Name = "Practice Pack",
            GameBuildId = "1.5.78.11833",
            LoaderId = "modding-api-77",
            ApplyMode = ModPresetApplyMode.Append,
            Entries =
            [
                new ModPresetEntry { Id = "hkmod:Helper", Name = "Helper", Version = "1.0.0" },
                new ModPresetEntry { Id = "hkmod:Benchwarp", Name = "Benchwarp", Version = "3.2.0" }
            ]
        };
        var speedrun = practice with { Id = "speedrun", Name = "Speedrun Pack", Entries = [] };
        viewModel.ModPresets.Add(practice);
        viewModel.ModPresets.Add(speedrun);
        viewModel.SelectedPreset = practice;

        viewModel.ModPackSearchText = "speedrun";
        viewModel.ModPackEntrySearchText = "helper";
        viewModel.ToggleSelectedPresetEntriesCommand.Execute(null);

        Assert.Equal(speedrun, Assert.Single(viewModel.VisibleModPacks));
        Assert.Equal(practice, viewModel.SelectedPreset);
        Assert.Equal("hkmod:Helper", Assert.Single(viewModel.VisibleSelectedModPackEntries).Id);
        Assert.True(viewModel.IsSelectedPresetEntriesExpanded);
    }

    [Fact]
    public async Task Preset_reload_racing_instance_details_load_never_duplicates_mod_presets()
    {
        using var test = new TestDirectory();
        var applicationDataRoot = test.CreateDirectory("app-data");
        var versionRoot = test.CreateDirectory("versions");
        var instanceRoot = test.CreateDirectory("versions", "practice");
        var record = Instance("practice", instanceRoot);
        var presetsRoot = Path.Combine(versionRoot, ".crystalfly", "instances", record.Id, "presets");
        Directory.CreateDirectory(presetsRoot);
        var presets = Enumerable.Range(0, 200).Select(index => new ModPreset
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = $"Preset {index:0000}",
            GameBuildId = record.BuildId,
            LoaderId = "modding-api-77",
            ApplyMode = ModPresetApplyMode.Append,
            Entries = []
        }).ToArray();
        foreach (var preset in presets)
        {
            await File.WriteAllTextAsync(
                Path.Combine(presetsRoot, PresetFileName(preset.Id)),
                System.Text.Json.JsonSerializer.Serialize(preset, CrystalflyJson.Options));
        }
        await using var viewModel = new MainViewModel(applicationDataRoot)
        {
            VersionRoot = versionRoot
        };
        var delays = new[] { 0, 5, 10, 20, 40 };
        var worstCount = 0;
        for (var round = 0; round < 20; round++)
        {
            viewModel.SelectedInstance = new InstanceItemViewModel(
                record,
                record.BuildId,
                round % 2 == 0 ? "Vanilla" : "BepInEx",
                round);
            await Task.Delay(delays[round % delays.Length]);
            var first = InvokeLoadModPresetsAsync(viewModel, record);
            var second = InvokeLoadModPresetsAsync(viewModel, record);
            await Task.WhenAll(first, second);
            await WaitUntilAsync(() => !viewModel.IsLoadingInstanceDetails);
            worstCount = Math.Max(worstCount, viewModel.ModPresets.Count);
            Assert.True(
                viewModel.ModPresets.Count == presets.Length,
                $"round {round} delay={delays[round % delays.Length]}: "
                + $"ModPresets={viewModel.ModPresets.Count} (worst={worstCount}) "
                + $"Visible={viewModel.VisibleModPacks.Count}");
            Assert.Equal(
                presets.Length,
                viewModel.ModPresets.Select(preset => preset.Id)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count());
            Assert.Equal(viewModel.ModPresets.Count, viewModel.VisibleModPacks.Count);

            // A later reload must keep the user's selection instead of snapping
            // back to the first entry.
            viewModel.SelectedPreset = viewModel.ModPresets.First(preset => preset.Id == presets[7].Id);
            await InvokeLoadModPresetsAsync(viewModel, record);
            Assert.Equal(presets[7].Id, viewModel.SelectedPreset?.Id);
            Assert.Equal(presets.Length, viewModel.ModPresets.Count);
        }
    }

    [Fact]
    public async Task Switching_to_activity_tab_with_fresh_cache_does_not_reload()
    {
        string root = applicationData.CreateDirectory("speedrun-fresh-cache");
        using var policy = new NetworkPolicy();
        var handler = new CountingSpeedrunResponseHandler();
        using var httpClient = new HttpClient(handler);
        var speedrunClient = new SpeedrunComClient(
            httpClient,
            Path.Combine(root, "speedrun-cache"),
            policy);
        await using var viewModel = new MainViewModel(
            root,
            speedrunComClientOverride: speedrunClient)
        {
            CurrentPage = "Speedrun",
            CurrentSpeedrunTab = "Environment"
        };

        // First load populates the in-memory cache.
        await viewModel.RefreshSpeedrunActivityCommand.ExecuteAsync(null);
        int callsAfterFirstLoad = handler.RequestCount;
        Assert.True(callsAfterFirstLoad > 0);

        // Switching to the Activity tab inside the cache window must not hit the network.
        viewModel.SelectSpeedrunTabCommand.Execute("Activity");

        Assert.Equal(callsAfterFirstLoad, handler.RequestCount);
        Assert.False(viewModel.IsSpeedrunActivityLoading);
    }

    [Fact]
    public async Task Background_refresh_loop_reloads_activity_without_loading_indicator()
    {
        string root = applicationData.CreateDirectory("speedrun-refresh-loop");
        using var policy = new NetworkPolicy();
        var handler = new CountingSpeedrunResponseHandler();
        using var httpClient = new HttpClient(handler);
        var speedrunClient = new SpeedrunComClient(
            httpClient,
            Path.Combine(root, "speedrun-cache"),
            policy);
        await using var viewModel = new MainViewModel(
            root,
            speedrunComClientOverride: speedrunClient)
        {
            CurrentPage = "Speedrun",
            CurrentSpeedrunTab = "Environment"
        };

        await viewModel.RefreshSpeedrunActivityCommand.ExecuteAsync(null);
        int callsAfterFirstLoad = handler.RequestCount;

        // Shorten the production 15-minute interval so the test can observe a loop iteration.
        var intervalField = typeof(MainViewModel).GetField(
            "SpeedrunActivityRefreshInterval",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(intervalField);
        intervalField.SetValue(null, TimeSpan.FromMilliseconds(150));
        try
        {
            viewModel.StartSpeedrunActivityRefreshLoop();
            await Task.Delay(500);

            Assert.True(handler.RequestCount > callsAfterFirstLoad);
            Assert.False(viewModel.IsSpeedrunActivityLoading);
        }
        finally
        {
            // Restore the production interval so other tests that initialize the
            // view model (and thus start the loop) are not affected by this override.
            intervalField.SetValue(null, TimeSpan.FromMinutes(15));
        }
    }

    [Fact]
    public async Task Background_refresh_loop_survives_unexpected_loading_error()
    {
        string root = applicationData.CreateDirectory("speedrun-refresh-loop-errors");
        using var policy = new NetworkPolicy();
        var handler = new ThrowingSpeedrunResponseHandler();
        using var httpClient = new HttpClient(handler);
        var speedrunClient = new SpeedrunComClient(
            httpClient,
            Path.Combine(root, "speedrun-cache"),
            policy);
        await using var viewModel = new MainViewModel(
            root,
            speedrunComClientOverride: speedrunClient)
        {
            CurrentPage = "Speedrun",
            CurrentSpeedrunTab = "Environment"
        };

        var intervalField = typeof(MainViewModel).GetField(
            "SpeedrunActivityRefreshInterval",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(intervalField);
        intervalField.SetValue(null, TimeSpan.FromMilliseconds(150));
        try
        {
            viewModel.StartSpeedrunActivityRefreshLoop();
            await Task.Delay(500);

            // A non-recoverable load error is surfaced, and the loop keeps
            // refreshing instead of stopping after the first failure.
            Assert.False(string.IsNullOrWhiteSpace(viewModel.SpeedrunActivityError));
            Assert.True(
                handler.Attempts >= 2,
                $"expected at least 2 refresh attempts, got {handler.Attempts}");
        }
        finally
        {
            intervalField.SetValue(null, TimeSpan.FromMinutes(15));
        }
    }

    [Fact]
    public async Task Activity_refresh_outside_the_speedrun_page_suppresses_toast_notifications()
    {
        string root = applicationData.CreateDirectory("speedrun-toast-scope");
        // Seed a stale baseline so the refresh detects the returned run as a new world record.
        var baselineAt = DateTimeOffset.Parse("2026-07-31T00:00:00Z");
        var board = new SpeedrunBoardDescriptor(
            SpeedrunGame.HollowKnight,
            "category-any",
            "Any%",
            null,
            null,
            []);
        await AtomicJsonStore.WriteAsync(
            Path.Combine(root, "speedrun-activity.json"),
            new SpeedrunActivityDocument
            {
                Boards = new Dictionary<string, SpeedrunBoardBaseline>(StringComparer.Ordinal)
                {
                    [board.Key] = new(
                        board,
                        baselineAt,
                        [new("old-run", 1, "Old Runner", "PT40M", 2400, baselineAt, null)])
                }
            },
            CancellationToken.None);

        using var policy = new NetworkPolicy();
        using var httpClient = new HttpClient(new NewRecordSpeedrunResponseHandler());
        var speedrunClient = new SpeedrunComClient(
            httpClient,
            Path.Combine(root, "speedrun-cache"),
            policy);
        await using var viewModel = new MainViewModel(
            root,
            speedrunComClientOverride: speedrunClient)
        {
            CurrentPage = "Launch",
            CurrentSpeedrunTab = "Activity"
        };
        var toasts = new List<string>();
        viewModel.ToastRequested += toast => toasts.Add(toast);

        await viewModel.RefreshSpeedrunActivityCommand.ExecuteAsync(null);

        Assert.Empty(toasts);
    }

    [Fact]
    public async Task Activity_refresh_on_the_speedrun_page_shows_toast_for_new_record()
    {
        string root = applicationData.CreateDirectory("speedrun-toast-on-page");
        // Seed a stale baseline so the refresh detects the returned run as a new world record.
        var baselineAt = DateTimeOffset.Parse("2026-07-31T00:00:00Z");
        var board = new SpeedrunBoardDescriptor(
            SpeedrunGame.HollowKnight,
            "category-any",
            "Any%",
            null,
            null,
            []);
        await AtomicJsonStore.WriteAsync(
            Path.Combine(root, "speedrun-activity.json"),
            new SpeedrunActivityDocument
            {
                Boards = new Dictionary<string, SpeedrunBoardBaseline>(StringComparer.Ordinal)
                {
                    [board.Key] = new(
                        board,
                        baselineAt,
                        [new("old-run", 1, "Old Runner", "PT40M", 2400, baselineAt, null)])
                }
            },
            CancellationToken.None);

        using var policy = new NetworkPolicy();
        using var httpClient = new HttpClient(new NewRecordSpeedrunResponseHandler());
        var speedrunClient = new SpeedrunComClient(
            httpClient,
            Path.Combine(root, "speedrun-cache"),
            policy);
        await using var viewModel = new MainViewModel(
            root,
            speedrunComClientOverride: speedrunClient)
        {
            CurrentPage = "Speedrun"
        };
        var toasts = new List<string>();
        viewModel.ToastRequested += toast => toasts.Add(toast);

        await viewModel.RefreshSpeedrunActivityCommand.ExecuteAsync(null);

        Assert.NotEmpty(toasts);
    }

    private MainViewModel CreateViewModel() => new(applicationData.CreateDirectory("app-data"));

    public void Dispose() => applicationData.Dispose();

    private static string? ReadFileHash(string path) => File.Exists(path)
        ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))
        : null;

    private class SpeedrunResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string json = request.RequestUri!.AbsolutePath switch
            {
                var path when path.EndsWith("/categories", StringComparison.Ordinal) => """
                {
                  "data": [
                    { "id": "category-any", "name": "Any%", "weblink": "https://www.speedrun.com/hollowknight" }
                  ]
                }
                """,
                var path when path.Contains("/leaderboards/", StringComparison.Ordinal) => """
                {
                  "data": {
                    "runs": [
                      {
                        "place": 1,
                        "run": {
                          "id": "leaderboard-run",
                          "weblink": "https://www.speedrun.com/hollowknight/runs/leaderboard-run",
                          "status": { "status": "verified", "verify-date": "2026-08-01T00:00:00Z" },
                          "times": { "primary": "PT32M" },
                          "players": [{ "rel": "user", "id": "leaderboard-player" }]
                        }
                      }
                    ],
                    "players": [{ "id": "leaderboard-player", "names": { "international": "Leaderboard Runner" } }]
                  }
                }
                """,
                _ => """
                {
                  "data": [
                    {
                      "id": "recent-run",
                      "weblink": "https://www.speedrun.com/hollowknight/runs/recent-run",
                      "status": { "status": "verified", "verify-date": "2026-08-01T01:00:00Z" },
                      "times": { "primary": "PT34M" },
                      "players": [{ "rel": "guest", "name": "Recent Runner" }]
                    }
                  ]
                }
                """
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class NewRecordSpeedrunResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string json = request.RequestUri!.AbsolutePath switch
            {
                var path when path.EndsWith("/categories", StringComparison.Ordinal) => """
                {
                  "data": [
                    { "id": "category-any", "name": "Any%", "type": "per-game" }
                  ]
                }
                """,
                var path when path.Contains("/leaderboards/", StringComparison.Ordinal) => """
                {
                  "data": {
                    "runs": [
                      {
                        "place": 1,
                        "run": {
                          "id": "new-record",
                          "weblink": "https://www.speedrun.com/hollowknight/runs/new-record",
                          "status": { "status": "verified", "verify-date": "2026-08-01T01:00:00Z" },
                          "times": { "primary": "PT31M", "primary_t": 1860 },
                          "players": [{ "rel": "user", "id": "p1" }]
                        }
                      }
                    ],
                    "players": [{ "id": "p1", "names": { "international": "Runner" } }]
                  }
                }
                """,
                _ => """
                {
                  "data": [
                    { "id": "level-1", "name": "Level 1" }
                  ]
                }
                """
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class CountingSpeedrunResponseHandler : SpeedrunResponseHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class ThrowingSpeedrunResponseHandler : HttpMessageHandler
    {
        private int attempts;

        public int Attempts => Volatile.Read(ref attempts);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref attempts);
            // A failure that SpeedrunComClient does not treat as recoverable, so the
            // load task faults and must be contained by the background refresh loop.
            return Task.FromException<HttpResponseMessage>(
                new ObjectDisposedException(nameof(HttpClient)));
        }
    }

    private sealed class PartialSpeedrunResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/categories", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"data\":[{\"id\":\"category-any\",\"name\":\"Any%\"}]}",
                        Encoding.UTF8,
                        "application/json")
                });
            }

            if (request.RequestUri.AbsolutePath.Contains("/leaderboards/", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"data\":[{\"id\":\"recent-run\",\"status\":{\"status\":\"verified\"},\"times\":{\"primary\":\"PT34M\"},\"players\":[{\"rel\":\"guest\",\"name\":\"Recent Runner\"}]}]}",
                    Encoding.UTF8,
                    "application/json")
            });
        }
    }

    private sealed class WaitingQueueExecutor : IDownloadQueueExecutor
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool RequiresGameExit(DownloadQueueItem item) => false;

        public bool IsTransient(Exception exception) => false;

        public async Task TransferAsync(
            DownloadQueueGroup group,
            DownloadQueueItem item,
            IProgress<Crystalfly.Core.Packages.PackageTransferProgress> progress,
            SemaphoreSlim networkGate,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        public Task InstallAsync(
            DownloadQueueGroup group,
            DownloadQueueItem item,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TestDirectory : IDisposable
    {
        private readonly string root = Path.Combine(
            Path.GetTempPath(),
            "Crystalfly.Tests",
            Guid.NewGuid().ToString("N"));

        public string CreateDirectory(params string[] parts)
        {
            var path = parts.Aggregate(root, Path.Combine);
            Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}

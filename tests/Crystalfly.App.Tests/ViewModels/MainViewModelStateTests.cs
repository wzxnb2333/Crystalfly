using Crystalfly.App.Downloads;
using Crystalfly.App.ViewModels;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Loaders;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;
using Crystalfly.Core.Networking;
using Crystalfly.Core.Runtime;
using Crystalfly.Core.Serialization;
using Crystalfly.Steam.Downloads;
using Crystalfly.Steam.Security;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class MainViewModelStateTests : IDisposable
{
    private readonly TestDirectory applicationData = new();

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
        Assert.Equal(GitHubDownloadRoute.Mirror, saved.GitHubDownloadRoute);
        Assert.Equal("practice", saved.CurrentInstanceId);
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
        Directory.CreateDirectory(Path.Combine(instanceRoot, "hollow_knight_Data", "Managed", "Mods"));
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
        Directory.CreateDirectory(Path.Combine(instanceRoot, "hollow_knight_Data", "Managed", "Mods"));
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
        Assert.Single(viewModel.DownloadQueueGroups);
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

        await viewModel.CloneSelectedInstanceCommand.ExecuteAsync(null);

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

        viewModel.SelectInstanceForLaunchCommand.Execute(second);

        Assert.Same(second, viewModel.SelectedInstance);
        Assert.Equal("Launch", viewModel.CurrentPage);

        viewModel.OpenInstanceSettingsCommand.Execute(first);

        Assert.Same(first, viewModel.SelectedInstance);
        Assert.Equal("Manage", viewModel.CurrentPage);
        Assert.Equal("Overview", viewModel.CurrentManageTab);
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
        viewModel.Instances.Add(first);
        viewModel.Instances.Add(second);
        viewModel.SelectedInstance = first;
        viewModel.CurrentPage = "Versions";

        await viewModel.DeleteInstanceCommand.ExecuteAsync(first);

        Assert.Same(first.Record, deleted);
        Assert.NotNull(evaluated);
        Assert.False(evaluated.HasBlockingQueueTasks);
        Assert.True(evaluated.TransactionsHealthy);
        Assert.DoesNotContain(first, viewModel.Instances);
        Assert.Same(second, viewModel.SelectedInstance);
        Assert.Equal("Launch", viewModel.CurrentPage);
    }

    [Fact]
    public void Mod_search_and_status_filter_both_lists()
    {
        var viewModel = CreateViewModel();
        var catalog = Manifest("debugmod", "2.0.0");
        viewModel.AvailableMods.Add(catalog);
        viewModel.AvailableMods.Add(Manifest("benchwarp", "1.0.0"));
        viewModel.InstalledMods.Add(new InstalledModItemViewModel(
            Receipt("debugmod", "1.0.0", enabled: true),
            catalog,
            static () => { }));
        viewModel.InstalledMods.Add(new InstalledModItemViewModel(
            Receipt("local-helper", "1.0.0", enabled: false, isLocal: true),
            null,
            static () => { }));

        viewModel.ModSearchText = "debug";

        Assert.Single(viewModel.VisibleAvailableMods);
        Assert.Single(viewModel.VisibleInstalledMods);

        viewModel.ModSearchText = string.Empty;
        viewModel.SelectedModStatus = ModStatusFilter.Local;

        Assert.Single(viewModel.VisibleInstalledMods);
        Assert.Equal("local-helper", viewModel.VisibleInstalledMods[0].Id);
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
            viewModel.InstalledMods.Add(item);
        }
        foreach (var item in new[] { first, second, third })
        {
            viewModel.VisibleInstalledMods.Add(item);
        }

        viewModel.SelectInstalledMod(first, control: false, shift: false);
        viewModel.SelectInstalledMod(third, control: false, shift: true);

        Assert.True(first.IsSelected);
        Assert.True(second.IsSelected);
        Assert.True(third.IsSelected);
        Assert.False(hidden.IsSelected);

        viewModel.SelectInstalledMod(second, control: true, shift: false);
        Assert.False(second.IsSelected);

        viewModel.ClearInstalledModSelectionCommand.Execute(null);
        Assert.DoesNotContain(viewModel.InstalledMods, item => item.IsSelected);

        viewModel.SelectAllInstalledModsCommand.Execute(null);
        Assert.All(viewModel.InstalledMods, item => Assert.True(item.IsSelected));
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
        viewModel.InstalledMods.Add(library);
        viewModel.InstalledMods.Add(feature);

        var plan = viewModel.CreateModRemovalPlan(bulk: true);

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
        viewModel.InstalledMods.Add(new InstalledModItemViewModel(
            Receipt("library", "1.0.0", enabled: false),
            libraryManifest,
            static () => { }));
        viewModel.InstalledMods.Add(new InstalledModItemViewModel(
            Receipt("feature", "1.0.0", enabled: true) with { Dependencies = ["library"] },
            null,
            static () => { }));

        var plan = viewModel.CreateModDependencyRepairPlan();

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
        viewModel.InstalledMods.Add(Installed("first"));
        viewModel.InstalledMods.Add(new InstalledModItemViewModel(
            Receipt("second", "1.0.0", enabled: true) with { LoaderId = "bepinex-5" },
            null,
            static () => { }));

        Assert.Throws<InvalidOperationException>(() => viewModel.CreateModDependencyRepairPlan());
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
        viewModel.InstalledMods.Add(item);

        await viewModel.UpdateSelectedModsCommand.ExecuteAsync(null);

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
        var receipt = Receipt("debugmod", "1.0.0", enabled: true) with { LoaderId = "modding-api-77" };
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
        for (var attempt = 0; attempt < 100 && viewModel.InstalledMods.Count == 0; attempt++)
        {
            await Task.Delay(10);
        }
        Assert.Single(viewModel.InstalledMods).IsSelected = true;
        var originalReceipt = await File.ReadAllTextAsync(receiptPath);
        await File.WriteAllTextAsync(Path.Combine(stateRoot, "loader.json"), "not-json");

        await viewModel.UpdateSelectedModsCommand.ExecuteAsync(null);

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
        viewModel.InstalledMods.Add(new InstalledModItemViewModel(
            Receipt("debugmod", "1.0.0", enabled: true),
            debugMod,
            static () => { }));

        viewModel.ModSearchText = "debug";
        viewModel.SelectDownloadSectionCommand.Execute("ModMarket");
        viewModel.MarketSearchText = "bench";

        Assert.True(viewModel.IsModMarketDownloadSection);
        Assert.False(viewModel.IsGameVersionsDownloadSection);
        Assert.Single(viewModel.VisibleMarketMods);
        Assert.Equal("benchwarp", viewModel.VisibleMarketMods[0].Id);
        Assert.Single(viewModel.VisibleInstalledMods);

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
        viewModel.Instances.Add(new InstanceItemViewModel(
            Instance("slow", slowRoot), "build-1", "modding-api-77", 0));
        viewModel.Instances.Add(new InstanceItemViewModel(
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
        viewModel.Instances.Add(new InstanceItemViewModel(
            Instance("practice", normalRoot) with { BuildId = "1.5.78.11833", Name = "Practice" },
            "1.5.78.11833",
            "Vanilla",
            0));
        viewModel.Instances.Add(new InstanceItemViewModel(
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
        viewModel.Instances.Add(new InstanceItemViewModel(record, "1.5.78.11833", "Vanilla", 0));
        viewModel.SelectedMarketMod = mod;
        await viewModel.PrepareMarketInstallTargetsCommand.ExecuteAsync(null);

        await viewModel.InstallMarketModCommand.ExecuteAsync(null);

        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal(viewModel.Loc["AddedToDownloadQueue"], Assert.Single(notifications));
        await viewModel.DownloadQueue.WaitForIdleAsync();
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
        viewModel.Instances.Add(new InstanceItemViewModel(
            Instance("broken", instanceRoot) with { BuildId = "1.5.78.11833" },
            "1.5.78.11833",
            "Unknown",
            0));

        await viewModel.PrepareMarketInstallTargetsCommand.ExecuteAsync(null);

        var target = Assert.Single(viewModel.MarketInstallTargets);
        Assert.False(target.IsAvailable);
        Assert.Contains(viewModel.Loc["OperationFailed"], target.StatusText, StringComparison.Ordinal);
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
        viewModel.Instances.Add(new InstanceItemViewModel(
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
        Assert.NotSame(previousLocalization, viewModel.Loc);
        Assert.Same(
            viewModel.LanguageOptions.Single(option => option.Value == english.Value),
            viewModel.SelectedLanguage);
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
        viewModel.InstalledMods.Add(new InstalledModItemViewModel(
            Receipt("hkmod:DebugMod", "1.0.0", enabled: true) with { Name = "DebugMod" },
            manifest,
            static () => { },
            viewModel.ProjectMarketMod(manifest, chinese: false))
        {
            IsSelected = true
        });
        viewModel.SelectedInstalledMod = viewModel.InstalledMods[0];
        viewModel.SelectedLanguage = viewModel.LanguageOptions.Single(option =>
            option.Value == UiLanguage.SimplifiedChinese);

        var projected = Assert.Single(viewModel.InstalledMods);
        Assert.Equal("调试模组", projected.PrimaryName);
        Assert.Equal("DebugMod", projected.SecondaryName);
        Assert.True(projected.IsSelected);
        Assert.Same(projected, viewModel.SelectedInstalledMod);
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

        Assert.StartsWith(viewModel.Loc["OperationFailed"], viewModel.ErrorMessage);
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

        Assert.Contains(viewModel.Instances, instance => instance.Id == record.Id);
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
            viewModel,
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
            viewModel,
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

        var external = Assert.Single(viewModel.InstalledMods);
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
        IsLocal = isLocal
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
        var method = typeof(MainViewModel).GetMethod(
            "QueueDownloadQueueProjection",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(viewModel, [groups]);
    }

    private static void InvokeApplyPendingDownloadQueueProjection(MainViewModel viewModel)
    {
        var method = typeof(MainViewModel).GetMethod(
            "ApplyPendingDownloadQueueProjection",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(viewModel, null);
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

    private static T GetPrivateField<T>(MainViewModel viewModel, string name)
    {
        var field = typeof(MainViewModel).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field.GetValue(viewModel));
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

    private MainViewModel CreateViewModel() => new(applicationData.CreateDirectory("app-data"));

    public void Dispose() => applicationData.Dispose();

    private static string? ReadFileHash(string path) => File.Exists(path)
        ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))
        : null;

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

using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.Core.Catalog;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Loaders;
using Crystalfly.Core.LocalLow;
using Crystalfly.Core.Logs;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;
using Crystalfly.Core.Runtime;
using Crystalfly.Core.Snapshots;
using Crystalfly.Core.Speedrun;
using Crystalfly.Core.Transactions;
using Crystalfly.Steam.Authentication;
using Crystalfly.Steam.Downloads;
using Crystalfly.Steam.Security;
using QRCoder;

namespace Crystalfly.App.ViewModels;

public partial class MainViewModel : ViewModelBase, IAsyncDisposable
{
    private static readonly HttpClient MetadataHttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly HttpClient PackageHttpClient = new() { Timeout = TimeSpan.FromMinutes(30) };
    private readonly CrystalflyPaths paths;
    private readonly string settingsPath;
    private GameCatalog catalog;
    private readonly DpapiRefreshTokenStore tokenStore;
    private readonly SemaphoreSlim settingsSaveLock = new(1, 1);
    private readonly object settingsSaveQueueLock = new();
    private readonly object disposeLock = new();
    private readonly Func<Task>? launchOverride;
    private readonly Func<CancellationToken, Task>? downloadOverride;
    private readonly Func<Task>? disposeSteamOverride;
    private CrystalflySettings settings = new();
    private Task settingsSaveQueue = Task.CompletedTask;
    private Task? disposeTask;
    private SteamAuthenticationSession? steamSession;
    private CancellationTokenSource? downloadCancellation;
    private InstanceRuntimeSession? runtimeSession;
    private Bitmap? qrCodeImage;
    private long detailsLoadGeneration;

    public MainViewModel(string? applicationDataRoot = null)
        : this(applicationDataRoot, null, null, null)
    {
    }

    internal MainViewModel(
        string? applicationDataRoot,
        Func<Task>? launchOverride,
        Func<CancellationToken, Task>? downloadOverride,
        Func<Task>? disposeSteamOverride)
    {
        this.launchOverride = launchOverride;
        this.downloadOverride = downloadOverride;
        this.disposeSteamOverride = disposeSteamOverride;
        paths = applicationDataRoot is null
            ? CrystalflyPaths.Resolve(
                AppContext.BaseDirectory,
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
            : new CrystalflyPaths(Path.GetFullPath(applicationDataRoot), IsPortable: false);
        settingsPath = Path.Combine(paths.ApplicationDataRoot, "settings.json");
        tokenStore = new DpapiRefreshTokenStore(Path.Combine(paths.ApplicationDataRoot, "steam-token.dat"));
        catalog = EmbeddedCatalog.Load();
        Loc = new LocalizationViewModel();
    }

    public LocalizationViewModel Loc { get; private set; }

    public ObservableCollection<InstanceItemViewModel> Instances { get; } = [];

    public ObservableCollection<InstanceItemViewModel> VisibleInstances { get; } = [];

    public ObservableCollection<SpeedrunTemplate> SpeedrunTemplates { get; } = [];

    public ObservableCollection<LoaderManifest> AvailableLoaders { get; } = [];

    public ObservableCollection<ModManifest> AvailableMods { get; } = [];

    public ObservableCollection<ModManifest> VisibleAvailableMods { get; } = [];

    public ObservableCollection<InstalledModItemViewModel> InstalledMods { get; } = [];

    public ObservableCollection<InstalledModItemViewModel> VisibleInstalledMods { get; } = [];

    public ObservableCollection<SettingOption<ModStatusFilter>> ModStatusOptions { get; } = [];

    public ObservableCollection<InstanceLogFile> InstanceLogs { get; } = [];

    public ObservableCollection<NamedSnapshot> Snapshots { get; } = [];

    public ObservableCollection<SettingOption<UiLanguage>> LanguageOptions { get; } = [];

    public ObservableCollection<SettingOption<UiTheme>> ThemeOptions { get; } = [];

    public ObservableCollection<DownloadBuildOption> DownloadBuilds { get; } = [];

    public bool HasInstance => SelectedInstance is not null;

    public bool CanNavigate => !IsBusy && !IsGameRunning;

    public bool CanCloneInstance => CanNavigate && !string.IsNullOrWhiteSpace(CloneInstanceName);

    public bool HasSelectedMods => InstalledMods.Any(mod => mod.IsSelected);

    public int SelectedModCount => InstalledMods.Count(mod => mod.IsSelected);

    public bool CanLaunch => HasInstance && CanNavigate && LaunchPreflight.IsReady;

    public string GameFilesStatus => LaunchPreflight.GameFilesReady ? Loc["Complete"] : Loc["Missing"];

    public string LoaderPreflightStatus => LaunchPreflight.LoaderReady ? Loc["NoConflicts"] : Loc["NeedsRepair"];

    public string ModDependencyStatus => LaunchPreflight.DependenciesReady ? Loc["NoConflicts"] : Loc["DependencyProblem"];

    public string SaveIsolationStatus => LaunchPreflight.SaveIsolationReady ? Loc["Mounted"] : Loc["NeedsAttention"];

    public string LaunchReadinessTitle => !HasInstance
        ? Loc["NoInstance"]
        : LaunchPreflight.IsReady ? Loc["Ready"] : Loc["NeedsAttention"];

    public string LaunchReadinessHint => !HasInstance
        ? Loc["ChooseRoot"]
        : LaunchPreflight.IsReady ? Loc["ReadyHint"] : Loc["LaunchBlocked"];

    public bool IsLaunchPage => CurrentPage == "Launch";

    public bool IsVersionsPage => CurrentPage == "Versions";

    public bool IsManagePage => CurrentPage == "Manage";

    public bool IsSpeedrunPage => CurrentPage == "Speedrun";

    public bool IsDownloadsPage => CurrentPage == "Downloads";

    public bool IsSettingsPage => CurrentPage == "Settings";

    public Bitmap? QrCodeImage
    {
        get => qrCodeImage;
        private set
        {
            var previous = qrCodeImage;
            if (SetProperty(ref qrCodeImage, value))
            {
                previous?.Dispose();
            }
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLaunchPage))]
    [NotifyPropertyChangedFor(nameof(IsVersionsPage))]
    [NotifyPropertyChangedFor(nameof(IsManagePage))]
    [NotifyPropertyChangedFor(nameof(IsSpeedrunPage))]
    [NotifyPropertyChangedFor(nameof(IsDownloadsPage))]
    [NotifyPropertyChangedFor(nameof(IsSettingsPage))]
    public partial string CurrentPage { get; set; } = "Launch";

    [ObservableProperty]
    public partial string CurrentManageTab { get; set; } = "Overview";

    [ObservableProperty]
    public partial string VersionRoot { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ModSearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ModStatusFilter SelectedModStatus { get; set; } = ModStatusFilter.All;

    [ObservableProperty]
    public partial SettingOption<ModStatusFilter>? SelectedModStatusOption { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCloneInstance))]
    public partial string CloneInstanceName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanNavigate))]
    [NotifyPropertyChangedFor(nameof(CanCloneInstance))]
    [NotifyPropertyChangedFor(nameof(CanLaunch))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsSteamLoggedIn { get; set; }

    [ObservableProperty]
    public partial string SteamStatus { get; set; } = "Not signed in";

    [ObservableProperty]
    public partial DownloadBuildOption? SelectedDownloadBuild { get; set; }

    [ObservableProperty]
    public partial bool IsDownloading { get; set; }

    [ObservableProperty]
    public partial double DownloadProgress { get; set; }

    [ObservableProperty]
    public partial string DownloadStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CustomSourcesText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanNavigate))]
    [NotifyPropertyChangedFor(nameof(CanCloneInstance))]
    [NotifyPropertyChangedFor(nameof(CanLaunch))]
    public partial bool IsGameRunning { get; set; }

    [ObservableProperty]
    public partial LoaderState CurrentLoaderState { get; set; }

    [ObservableProperty]
    public partial LoaderManifest? SelectedLoader { get; set; }

    [ObservableProperty]
    public partial ModManifest? SelectedAvailableMod { get; set; }

    [ObservableProperty]
    public partial InstalledModItemViewModel? SelectedInstalledMod { get; set; }

    [ObservableProperty]
    public partial InstanceLogFile? SelectedLogFile { get; set; }

    [ObservableProperty]
    public partial string LogContent { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLaunch))]
    [NotifyPropertyChangedFor(nameof(GameFilesStatus))]
    [NotifyPropertyChangedFor(nameof(LoaderPreflightStatus))]
    [NotifyPropertyChangedFor(nameof(ModDependencyStatus))]
    [NotifyPropertyChangedFor(nameof(SaveIsolationStatus))]
    [NotifyPropertyChangedFor(nameof(LaunchReadinessTitle))]
    [NotifyPropertyChangedFor(nameof(LaunchReadinessHint))]
    public partial LaunchPreflightResult LaunchPreflight { get; set; } = new(false, false, false, false);

    [ObservableProperty]
    public partial NamedSnapshot? SelectedSnapshot { get; set; }

    [ObservableProperty]
    public partial SpeedrunTemplate? SelectedSpeedrunTemplate { get; set; }

    [ObservableProperty]
    public partial int? SelectedLoadNormaliserSeconds { get; set; }

    [ObservableProperty]
    public partial string SnapshotName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SpeedrunEnvironmentName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SpeedrunStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LocalModPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LocalLoaderManifestPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LoaderVerificationStatus { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInstance))]
    [NotifyPropertyChangedFor(nameof(CanLaunch))]
    [NotifyPropertyChangedFor(nameof(LaunchReadinessTitle))]
    [NotifyPropertyChangedFor(nameof(LaunchReadinessHint))]
    public partial InstanceItemViewModel? SelectedInstance { get; set; }

    [ObservableProperty]
    public partial SettingOption<UiLanguage>? SelectedLanguage { get; set; }

    [ObservableProperty]
    public partial SettingOption<UiTheme>? SelectedTheme { get; set; }

    public async Task InitializeAsync()
    {
        settings = await CrystalflySettingsStore.LoadAsync(settingsPath);
        catalog = await LoadCatalogAsync();
        VersionRoot = settings.VersionRoot ?? string.Empty;
        CustomSourcesText = string.Join(
            Environment.NewLine,
            settings.CustomCatalogs.Select(source => $"{source.Namespace}={source.Url}"));
        ApplyLanguage(settings.Language);
        ApplyTheme(settings.Theme);
        RebuildSettingOptions();
        RebuildModStatusOptions();
        foreach (var template in catalog.SpeedrunTemplates)
        {
            SpeedrunTemplates.Add(template);
        }
        SelectedSpeedrunTemplate = SpeedrunTemplates.FirstOrDefault();
        DownloadBuilds.Add(new DownloadBuildOption("public", "Steam public (current)", null));
        foreach (var build in catalog.Builds.OrderByDescending(build => build.DisplayVersion, StringComparer.OrdinalIgnoreCase))
        {
            DownloadBuilds.Add(new DownloadBuildOption(
                build.Id,
                build.DisplayVersion,
                ulong.Parse(build.ManifestId, System.Globalization.CultureInfo.InvariantCulture)));
        }
        SelectedDownloadBuild = DownloadBuilds[0];
        await TryReconnectSteamAsync();

        if (Directory.Exists(VersionRoot))
        {
            await RefreshAsync();
        }
        else
        {
            StatusMessage = Loc["ChooseRoot"];
        }
    }

    [RelayCommand]
    private void SelectPage(string? page)
    {
        if (CanNavigate && !string.IsNullOrWhiteSpace(page))
        {
            CurrentPage = page;
        }
    }

    [RelayCommand]
    private void SelectManageTab(string? tab)
    {
        if (CanNavigate && !string.IsNullOrWhiteSpace(tab))
        {
            CurrentManageTab = tab;
        }
    }

    [RelayCommand]
    private void ManageSelectedInstance()
    {
        if (!CanNavigate)
        {
            return;
        }
        if (SelectedInstance is null)
        {
            ErrorMessage = Loc["NoInstance"];
            return;
        }
        CurrentManageTab = "Overview";
        CurrentPage = "Manage";
    }

    [RelayCommand]
    private async Task ApplyVersionRootAsync()
    {
        if (!Directory.Exists(VersionRoot))
        {
            ErrorMessage = $"{Loc["OperationFailed"]}: {VersionRoot}";
            return;
        }

        settings = settings with { VersionRoot = Path.GetFullPath(VersionRoot) };
        VersionRoot = settings.VersionRoot;
        await QueueSettingsSave();
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (!Directory.Exists(VersionRoot))
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = Loc["StatusChecking"];
        try
        {
            await EnsureTransactionsHealthyAsync();
            var records = await InstanceImportService.DiscoverAsync(VersionRoot, catalog);
            var isolation = new LocalLowIsolationService(
                GetSharedLocalLowPath(),
                paths.GetVersionDataRoot(VersionRoot));
            bool canCompleteActiveSession = runtimeSession is null
                && !IsGameRunning
                && !new SystemHollowKnightProcessProbe().IsRunning();
            await isolation.InitializeBaselinesAsync(
                records.Select(static record => record.Id),
                allowActiveSessionCompletion: canCompleteActiveSession);
            Instances.Clear();
            foreach (var record in records.OrderBy(instance => instance.Name, StringComparer.OrdinalIgnoreCase))
            {
                var build = catalog.Builds.FirstOrDefault(candidate => candidate.Id == record.BuildId);
                var loaderManager = CreateLoaderManager(record);
                var loaderState = await loaderManager.GetStateAsync();
                var loaderReceipt = await loaderManager.GetReceiptAsync();
                var modCount = (await CreateModManager(record).GetInstalledAsync()).Count;
                Instances.Add(new InstanceItemViewModel(
                    record,
                    build?.DisplayVersion ?? Loc["UnknownBuild"],
                    loaderReceipt is null
                        ? loaderState.ToString()
                        : loaderReceipt.IsVerified
                            ? loaderReceipt.PackageId
                            : $"{loaderReceipt.PackageId} · {Loc["Unverified"]}",
                    modCount));
            }

            ApplyInstanceFilter();
            SelectedInstance = Instances.FirstOrDefault(instance => instance.Id == settings.CurrentInstanceId)
                ?? Instances.FirstOrDefault();
            StatusMessage = Loc["StatusReady"];
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or InvalidOperationException)
        {
            ErrorMessage = $"{Loc["OperationFailed"]}: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CloneSelectedInstanceAsync()
    {
        if (SelectedInstance is null)
        {
            ErrorMessage = Loc["NoInstance"];
            return;
        }
        if (string.IsNullOrWhiteSpace(CloneInstanceName))
        {
            ErrorMessage = Loc["CloneNameRequired"];
            return;
        }
        if (IsMutationBlocked())
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (await CreateLoaderManager(SelectedInstance.Record).GetStateAsync() != LoaderState.Vanilla
                || (await CreateModManager(SelectedInstance.Record).GetInstalledAsync()).Count != 0)
            {
                throw new InvalidOperationException(Loc["CloneVanillaOnly"]);
            }

            var clone = await InstanceCloneService.CloneAsync(
                SelectedInstance.RootPath,
                CloneInstanceName.Trim(),
                Guid.NewGuid().ToString("N"));
            CloneInstanceName = string.Empty;
            await RefreshAsync();
            SelectedInstance = Instances.FirstOrDefault(instance => instance.Id == clone.Id);
            StatusMessage = Loc["OperationComplete"];
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or InvalidOperationException
            or UnauthorizedAccessException
            or ArgumentException)
        {
            ErrorMessage = $"{Loc["OperationFailed"]}: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LaunchGameAsync()
    {
        if (launchOverride is not null)
        {
            await launchOverride();
            return;
        }

        ErrorMessage = null;
        if (SelectedInstance is null)
        {
            ErrorMessage = Loc["NoInstance"];
            return;
        }
        if (!CanLaunch)
        {
            ErrorMessage = Loc["LaunchBlocked"];
            return;
        }
        if (Process.GetProcessesByName("hollow_knight").Length > 0)
        {
            ErrorMessage = "Hollow Knight is already running.";
            return;
        }

        var executable = Path.Combine(SelectedInstance.RootPath, "hollow_knight.exe");
        if (!File.Exists(executable))
        {
            ErrorMessage = $"{Loc["OperationFailed"]}: {executable}";
            return;
        }

        var isolation = new LocalLowIsolationService(
            GetSharedLocalLowPath(),
            paths.GetVersionDataRoot(VersionRoot));
        IsGameRunning = true;
        try
        {
            await EnsureTransactionsHealthyAsync();
            if (SelectedInstance.Record.SpeedrunTemplateId is not null)
            {
                await VerifySpeedrunLaunchAsync(SelectedInstance.Record);
            }
            runtimeSession = await InstanceRuntimeSession.StartAsync(isolation, SelectedInstance.Id);
            using var process = Process.Start(new ProcessStartInfo(executable)
            {
                WorkingDirectory = SelectedInstance.RootPath,
                UseShellExecute = true
            }) ?? throw new InvalidOperationException("The game process did not start.");
            await process.WaitForExitAsync();
            var probe = new SystemHollowKnightProcessProbe();
            while (probe.IsRunning())
            {
                await Task.Delay(500);
            }
            await runtimeSession.CompleteAsync();
            runtimeSession = null;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            ErrorMessage = $"{Loc["OperationFailed"]}: {exception.Message}";
            if (runtimeSession is not null && !new SystemHollowKnightProcessProbe().IsRunning())
            {
                try
                {
                    await runtimeSession.CompleteAsync();
                    runtimeSession = null;
                }
                catch (Exception recoveryException) when (recoveryException is IOException or InvalidOperationException)
                {
                    ErrorMessage += $" LocalLow: {recoveryException.Message}";
                }
            }
        }
        finally
        {
            IsGameRunning = false;
        }
    }

    [RelayCommand]
    private async Task SignInWithQrAsync()
    {
        ErrorMessage = null;
        SteamStatus = "Connecting to Steam...";
        if (steamSession is not null)
        {
            await steamSession.DisposeAsync();
        }

        steamSession = new SteamAuthenticationSession(tokenStore);
        steamSession.QrChallengeChanged += OnQrChallengeChanged;
        try
        {
            var credential = await steamSession.ConnectWithQrAsync();
            IsSteamLoggedIn = true;
            SteamStatus = credential.AccountName;
            QrCodeImage = null;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            ErrorMessage = $"Steam: {exception.Message}";
            SteamStatus = "Not signed in";
        }
    }

    private async Task TryReconnectSteamAsync()
    {
        if (!File.Exists(Path.Combine(paths.ApplicationDataRoot, "steam-token.dat")))
        {
            return;
        }
        steamSession = new SteamAuthenticationSession(tokenStore);
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var credential = await steamSession.ConnectWithStoredTokenAsync(timeout.Token);
            IsSteamLoggedIn = true;
            SteamStatus = credential.AccountName;
        }
        catch (Exception exception) when (exception is IOException
            or InvalidOperationException
            or OperationCanceledException)
        {
            await steamSession.DisposeAsync();
            steamSession = null;
            SteamStatus = "Not signed in";
        }
    }

    [RelayCommand]
    private void SignOutSteam()
    {
        steamSession?.SignOut();
        IsSteamLoggedIn = false;
        SteamStatus = "Not signed in";
        QrCodeImage = null;
    }

    [RelayCommand]
    private async Task DownloadBuildAsync()
    {
        if (downloadOverride is not null)
        {
            downloadCancellation = new CancellationTokenSource();
            try
            {
                await downloadOverride(downloadCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                DownloadStatus = "Cancelled";
            }
            finally
            {
                downloadCancellation.Dispose();
                downloadCancellation = null;
            }
            return;
        }

        if (steamSession?.IsLoggedOn != true || SelectedDownloadBuild is null)
        {
            ErrorMessage = "Sign in to Steam and select a build first.";
            return;
        }
        if (!Directory.Exists(VersionRoot))
        {
            ErrorMessage = Loc["ChooseRoot"];
            return;
        }

        downloadCancellation = new CancellationTokenSource();
        IsDownloading = true;
        DownloadProgress = 0;
        ErrorMessage = null;
        var staging = Path.Combine(
            VersionRoot,
            ".crystalfly",
            "downloads",
            $"steam-{Guid.NewGuid():N}");
        try
        {
            using var content = new SteamKitContentDeliveryClient(steamSession.Client);
            var service = new SteamDepotDownloadService(content, progress =>
                Dispatcher.UIThread.Post(() =>
                {
                    DownloadProgress = progress.Fraction;
                    DownloadStatus = progress.CurrentFile;
                }));
            var result = await service.DownloadAsync(
                new SteamDownloadRequest(staging, SelectedDownloadBuild.ManifestId),
                downloadCancellation.Token);
            var build = catalog.Builds.FirstOrDefault(candidate =>
                candidate.ManifestId == result.ManifestId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var name = UniqueInstanceName(build?.DisplayVersion ?? $"public-{result.ManifestId}");
            var destination = InstanceDirectory.ResolveUnderRoot(VersionRoot, name);
            Directory.Move(staging, destination);
            await InstanceSidecar.SaveAsync(new InstanceRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                RootPath = destination,
                BuildId = build?.Id ?? $"steam-public-{result.ManifestId}",
                CreatedAt = DateTimeOffset.UtcNow
            });
            await RefreshAsync();
            DownloadStatus = $"Completed: {name}";
            CurrentPage = "Versions";
        }
        catch (OperationCanceledException)
        {
            DownloadStatus = "Cancelled";
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or InvalidOperationException
            or ArgumentException)
        {
            ErrorMessage = $"Steam: {exception.Message}";
        }
        finally
        {
            IsDownloading = false;
            downloadCancellation.Dispose();
            downloadCancellation = null;
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }
        }
    }

    [RelayCommand]
    private void CancelDownload() => downloadCancellation?.Cancel();

    [RelayCommand]
    private async Task InstallOrSwitchLoaderAsync()
    {
        if (SelectedInstance is null || SelectedLoader is null)
        {
            ErrorMessage = Loc["SelectLoader"];
            return;
        }

        await RunInstanceMutationAsync(async record =>
        {
            var manager = CreateLoaderManager(record);
            var state = await manager.GetStateAsync();
            var receipt = await manager.GetReceiptAsync();
            if (state == LoaderState.Conflict)
            {
                throw new InvalidOperationException(Loc["LoaderConflict"]);
            }
            if (state == LoaderState.Drifted)
            {
                if (!string.Equals(receipt?.PackageId, SelectedLoader.Id, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(Loc["RepairBeforeSwitch"]);
                }
                await manager.RepairFromUriAsync(SelectedLoader);
            }
            else if (state == LoaderState.Vanilla)
            {
                await manager.InstallFromUriAsync(SelectedLoader);
            }
            else if (!string.Equals(receipt?.PackageId, SelectedLoader.Id, StringComparison.OrdinalIgnoreCase))
            {
                await manager.SwitchFromUriAsync(SelectedLoader);
            }
        });
    }

    [RelayCommand]
    private async Task RepairLoaderAsync()
    {
        if (SelectedInstance is null || SelectedLoader is null)
        {
            ErrorMessage = Loc["SelectLoader"];
            return;
        }
        await RunInstanceMutationAsync(record => CreateLoaderManager(record).RepairFromUriAsync(SelectedLoader));
    }

    [RelayCommand]
    private async Task UninstallLoaderAsync()
    {
        if (SelectedInstance is null)
        {
            ErrorMessage = Loc["NoInstance"];
            return;
        }
        await RunInstanceMutationAsync(record => CreateLoaderManager(record).UninstallAsync());
    }

    [RelayCommand]
    private async Task ImportLocalLoaderAsync()
    {
        if (SelectedInstance is null || !File.Exists(LocalLoaderManifestPath))
        {
            ErrorMessage = Loc["LocalLoaderManifestRequired"];
            return;
        }
        await RunInstanceMutationAsync(async record =>
        {
            var package = await LocalLoaderPackageManifest.LoadAsync(
                LocalLoaderManifestPath,
                record.BuildId);
            await CreateLoaderManager(record).InstallLocalFromFileAsync(package);
            LocalLoaderManifestPath = string.Empty;
            LoaderVerificationStatus = Loc["UnverifiedLocalLoader"];
        });
    }

    [RelayCommand]
    private async Task InstallSelectedModAsync()
    {
        if (SelectedInstance is null || SelectedAvailableMod is null)
        {
            ErrorMessage = Loc["SelectMod"];
            return;
        }

        await RunInstanceMutationAsync(async record =>
        {
            var loader = await CreateLoaderManager(record).GetReceiptAsync()
                ?? throw new InvalidOperationException(Loc["LoaderRequired"]);
            var order = ModDependencyResolver.ResolveInstallOrder(catalog.Mods, [SelectedAvailableMod.Id]);
            foreach (var mod in order)
            {
                if (!string.Equals(mod.LoaderId, loader.PackageId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"{Loc["WrongLoader"]}: {mod.Name}");
                }
            }
            await CreateModManager(record).InstallWithDependenciesFromUrisAsync(
                catalog.Mods,
                [SelectedAvailableMod.Id]);
        });
    }

    [RelayCommand]
    private async Task ToggleSelectedModAsync()
    {
        if (SelectedInstance is null || SelectedInstalledMod is null)
        {
            ErrorMessage = Loc["SelectMod"];
            return;
        }
        var modId = SelectedInstalledMod.Id;
        var enabled = !SelectedInstalledMod.Enabled;
        await RunInstanceMutationAsync(record => CreateModManager(record).SetEnabledAsync(modId, enabled));
    }

    [RelayCommand]
    private async Task UpdateSelectedModAsync()
    {
        if (SelectedInstalledMod?.CatalogManifest is not ModManifest manifest
            || !SelectedInstalledMod.HasUpdate)
        {
            ErrorMessage = Loc["NoUpdateAvailable"];
            return;
        }
        await RunInstanceMutationAsync(record => CreateModManager(record).UpdateFromUriAsync(manifest));
    }

    [RelayCommand]
    private async Task UninstallSelectedModAsync()
    {
        if (SelectedInstance is null || SelectedInstalledMod is null)
        {
            ErrorMessage = Loc["SelectMod"];
            return;
        }
        var modId = SelectedInstalledMod.Id;
        await RunInstanceMutationAsync(record => CreateModManager(record).UninstallAsync(modId));
    }

    [RelayCommand]
    private async Task EnableSelectedModsAsync()
    {
        var selected = GetSelectedModReceipts();
        if (selected.Count == 0)
        {
            return;
        }
        var installed = InstalledMods.Select(mod => mod.Receipt).ToArray();
        var selectedIds = selected.Select(mod => mod.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var disabledExternalDependencies = selected
            .SelectMany(mod => mod.Dependencies)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(id => installed.FirstOrDefault(mod => string.Equals(mod.Id, id, StringComparison.OrdinalIgnoreCase)))
            .Where(mod => mod is not null && !mod.Enabled && !selectedIds.Contains(mod.Id))
            .Select(mod => mod!.Name)
            .ToArray();
        if (disabledExternalDependencies.Length > 0)
        {
            ErrorMessage = $"{Loc["DependencyProblem"]}: {string.Join(", ", disabledExternalDependencies)}";
            return;
        }
        var order = InstalledModDependencyGraph.OrderDependentsFirst(installed, selectedIds).Reverse().ToArray();
        await RunInstanceMutationAsync(async record =>
        {
            var manager = CreateModManager(record);
            foreach (var mod in order.Where(mod => !mod.Enabled))
            {
                await manager.SetEnabledAsync(mod.Id, enabled: true);
            }
        });
    }

    [RelayCommand]
    private async Task DisableSelectedModsAsync()
    {
        var selected = GetSelectedModReceipts();
        if (selected.Count == 0)
        {
            return;
        }
        var installed = InstalledMods.Select(mod => mod.Receipt).ToArray();
        var selectedIds = selected.Select(mod => mod.Id).ToArray();
        var blockers = InstalledModDependencyGraph.FindExternalDependents(installed, selectedIds)
            .Where(mod => mod.Enabled)
            .ToArray();
        if (blockers.Length > 0)
        {
            ErrorMessage = $"{Loc["ReverseDependencies"]}: {string.Join(", ", blockers.Select(mod => mod.Name))}";
            return;
        }
        var order = InstalledModDependencyGraph.OrderDependentsFirst(installed, selectedIds);
        await RunInstanceMutationAsync(async record =>
        {
            var manager = CreateModManager(record);
            foreach (var mod in order.Where(mod => mod.Enabled))
            {
                await manager.SetEnabledAsync(mod.Id, enabled: false);
            }
        });
    }

    [RelayCommand]
    private async Task UpdateSelectedModsAsync()
    {
        var updates = InstalledMods.Where(mod => mod.IsSelected && mod.HasUpdate).ToArray();
        if (updates.Length == 0)
        {
            ErrorMessage = Loc["NoUpdateAvailable"];
            return;
        }
        await RunInstanceMutationAsync(async record =>
        {
            var manager = CreateModManager(record);
            foreach (var mod in updates)
            {
                await manager.UpdateFromUriAsync(mod.CatalogManifest!);
            }
        });
    }

    [RelayCommand]
    private async Task UninstallSelectedModsAsync()
    {
        var selected = GetSelectedModReceipts();
        if (selected.Count == 0)
        {
            return;
        }
        var installed = InstalledMods.Select(mod => mod.Receipt).ToArray();
        var selectedIds = selected.Select(mod => mod.Id).ToArray();
        var blockers = InstalledModDependencyGraph.FindExternalDependents(installed, selectedIds);
        if (blockers.Count > 0)
        {
            ErrorMessage = $"{Loc["ReverseDependencies"]}: {string.Join(", ", blockers.Select(mod => mod.Name))}";
            return;
        }
        var order = InstalledModDependencyGraph.OrderDependentsFirst(installed, selectedIds);
        await RunInstanceMutationAsync(async record =>
        {
            var manager = CreateModManager(record);
            foreach (var mod in order)
            {
                await manager.UninstallAsync(mod.Id);
            }
        });
    }

    public IReadOnlyList<string> GetSelectedModExternalDependentNames(bool bulk)
    {
        var selectedIds = bulk
            ? InstalledMods.Where(mod => mod.IsSelected).Select(mod => mod.Id).ToArray()
            : SelectedInstalledMod is null ? [] : [SelectedInstalledMod.Id];
        return InstalledModDependencyGraph.FindExternalDependents(
                InstalledMods.Select(mod => mod.Receipt).ToArray(),
                selectedIds)
            .Select(mod => mod.Name)
            .ToArray();
    }

    public string GetSelectedModNames(bool bulk) => string.Join(
        Environment.NewLine,
        bulk
            ? InstalledMods.Where(mod => mod.IsSelected).Select(mod => mod.Name)
            : SelectedInstalledMod is null ? [] : [SelectedInstalledMod.Name]);

    private IReadOnlyList<InstalledModReceipt> GetSelectedModReceipts() => InstalledMods
        .Where(mod => mod.IsSelected)
        .Select(mod => mod.Receipt)
        .ToArray();

    [RelayCommand]
    private async Task ImportLocalModAsync()
    {
        if (SelectedInstance is null || !File.Exists(LocalModPath))
        {
            ErrorMessage = Loc["LocalModPathRequired"];
            return;
        }
        await RunInstanceMutationAsync(async record =>
        {
            var loader = await CreateLoaderManager(record).GetReceiptAsync()
                ?? throw new InvalidOperationException(Loc["LoaderRequired"]);
            var fileName = Path.GetFileNameWithoutExtension(LocalModPath);
            var id = $"local-{fileName}";
            var manager = CreateModManager(record);
            if (string.Equals(Path.GetExtension(LocalModPath), ".dll", StringComparison.OrdinalIgnoreCase))
            {
                await manager.ImportLocalDllAsync(id, fileName, loader.PackageId, LocalModPath);
            }
            else if (string.Equals(Path.GetExtension(LocalModPath), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                await manager.ImportLocalZipAsync(id, fileName, loader.PackageId, LocalModPath);
            }
            else
            {
                throw new InvalidDataException(Loc["LocalModType"]);
            }
            LocalModPath = string.Empty;
        });
    }

    [RelayCommand]
    private async Task CreateSnapshotAsync()
    {
        if (SelectedInstance is null || string.IsNullOrWhiteSpace(SnapshotName))
        {
            ErrorMessage = Loc["SnapshotNameRequired"];
            return;
        }
        await RunInstanceMutationAsync(async record =>
        {
            await CreateSnapshotService().CreateAsync(record.Id, SnapshotName);
            SnapshotName = string.Empty;
        });
    }

    [RelayCommand]
    private async Task RestoreSnapshotAsync()
    {
        if (SelectedInstance is null || SelectedSnapshot is null)
        {
            ErrorMessage = Loc["SelectSnapshot"];
            return;
        }
        var snapshotId = SelectedSnapshot.Id;
        await RunInstanceMutationAsync(record => CreateSnapshotService().RestoreAsync(record.Id, snapshotId));
    }

    [RelayCommand]
    private async Task CreateSpeedrunEnvironmentAsync()
    {
        if (SelectedSpeedrunTemplate is null || !Directory.Exists(VersionRoot))
        {
            ErrorMessage = Loc["SelectSpeedrunTemplate"];
            return;
        }
        if (IsMutationBlocked())
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        string? createdRoot = null;
        try
        {
            await EnsureTransactionsHealthyAsync();
            var source = await FindVanillaSourceAsync(SelectedSpeedrunTemplate.BuildId)
                ?? throw new InvalidOperationException(Loc["NoVanillaSource"]);
            var name = string.IsNullOrWhiteSpace(SpeedrunEnvironmentName)
                ? UniqueInstanceName($"{SelectedSpeedrunTemplate.Name} Speedrun")
                : SpeedrunEnvironmentName.Trim();
            var clone = await InstanceCloneService.CloneAsync(
                source.RootPath,
                name,
                Guid.NewGuid().ToString("N"));
            createdRoot = clone.RootPath;
            if (SelectedSpeedrunTemplate.RequiredAssetIds.Count > 0)
            {
                await new SpeedrunEnvironmentProvisioner().ProvisionAsync(new SpeedrunProvisioningRequest
                {
                    Catalog = catalog,
                    TemplateId = SelectedSpeedrunTemplate.Id,
                    InstanceRoot = clone.RootPath,
                    TransactionRoot = Path.Combine(paths.GetVersionDataRoot(VersionRoot), "transactions"),
                    PackageCacheRoot = Path.Combine(paths.GetVersionDataRoot(VersionRoot), "packages"),
                    LoadNormaliserSeconds = SelectedLoadNormaliserSeconds,
                    HttpClient = PackageHttpClient
                });
            }
            clone = clone with
            {
                Purpose = SelectedSpeedrunTemplate.IsOfficial
                    ? InstancePurpose.OfficialSpeedrun
                    : InstancePurpose.CustomSpeedrun,
                ProvisioningMode = InstanceProvisioningMode.FullCopy,
                LoaderId = null,
                SpeedrunTemplateId = SelectedSpeedrunTemplate.Id,
                SpeedrunRulesRevision = SelectedSpeedrunTemplate.RulesRevision,
                LoadNormaliserSeconds = SelectedLoadNormaliserSeconds
            };
            await InstanceSidecar.SaveAsync(clone);
            createdRoot = null;
            SpeedrunStatus = SelectedSpeedrunTemplate.IsOfficial
                && catalog.SpeedrunFileManifests.Any(manifest => manifest.Id == SelectedSpeedrunTemplate.FileManifestId)
                    ? Loc["SpeedrunNeedsVerification"]
                    : Loc["SpeedrunUnverified"];
            SpeedrunEnvironmentName = string.Empty;
            await RefreshAsync();
            SelectedInstance = Instances.FirstOrDefault(instance => instance.Id == clone.Id);
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or InvalidOperationException
            or UnauthorizedAccessException
            or ArgumentException)
        {
            bool preserveForRecovery = exception is SpeedrunRecoveryRequiredException;
            if (!preserveForRecovery && createdRoot is not null)
            {
                preserveForRecovery = await SpeedrunEnvironmentProvisioner.RequiresManualRecoveryAsync(
                    Path.Combine(paths.GetVersionDataRoot(VersionRoot), "transactions"),
                    createdRoot);
            }
            if (!preserveForRecovery && createdRoot is not null && Directory.Exists(createdRoot))
            {
                try
                {
                    Directory.Delete(createdRoot, recursive: true);
                }
                catch (Exception cleanupException) when (cleanupException is IOException or UnauthorizedAccessException)
                {
                    ErrorMessage = $"{Loc["OperationFailed"]}: {cleanupException.Message}";
                }
            }
            ErrorMessage = string.IsNullOrWhiteSpace(ErrorMessage)
                ? $"{Loc["OperationFailed"]}: {exception.Message}"
                : $"{Loc["OperationFailed"]}: {exception.Message} {ErrorMessage}";
            if (preserveForRecovery)
            {
                ErrorMessage += $" {Loc["RecoveryNeedsAttention"]}";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveCustomSourcesAsync()
    {
        var definitions = new List<CustomCatalogDefinition>();
        try
        {
            foreach (var line in CustomSourcesText.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var separator = line.IndexOf('=');
                if (separator <= 0
                    || !Uri.TryCreate(line[(separator + 1)..], UriKind.Absolute, out var uri)
                    || uri.Scheme != Uri.UriSchemeHttps)
                {
                    throw new FormatException($"Invalid custom catalog entry: {line}");
                }
                var sourceNamespace = line[..separator].Trim();
                _ = CustomCatalogSource.Namespace(sourceNamespace, new GameCatalog());
                definitions.Add(new CustomCatalogDefinition
                {
                    Namespace = sourceNamespace,
                    Url = uri.AbsoluteUri
                });
            }

            settings = settings with { CustomCatalogs = definitions };
            await QueueSettingsSave();
            catalog = await LoadCatalogAsync();
            if (Directory.Exists(VersionRoot))
            {
                await RefreshAsync();
            }
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            ErrorMessage = exception.Message;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyInstanceFilter();

    partial void OnModSearchTextChanged(string value) => ApplyModFilters();

    partial void OnSelectedModStatusChanged(ModStatusFilter value) => ApplyModFilters();

    partial void OnSelectedModStatusOptionChanged(SettingOption<ModStatusFilter>? value)
    {
        if (value is not null)
        {
            SelectedModStatus = value.Value;
        }
    }

    partial void OnSelectedLogFileChanged(InstanceLogFile? value)
    {
        LogContent = string.Empty;
        if (value is not null)
        {
            _ = LoadLogAsync(value);
        }
    }

    partial void OnSelectedInstanceChanged(InstanceItemViewModel? value)
    {
        long generation = Interlocked.Increment(ref detailsLoadGeneration);
        AvailableLoaders.Clear();
        AvailableMods.Clear();
        VisibleAvailableMods.Clear();
        InstalledMods.Clear();
        VisibleInstalledMods.Clear();
        OnModSelectionChanged();
        InstanceLogs.Clear();
        SelectedLogFile = null;
        LaunchPreflight = new(false, false, false, false);
        if (value is not null)
        {
            foreach (var loader in catalog.Loaders.Where(loader => loader.SupportedBuildIds.Contains(value.Record.BuildId)))
            {
                AvailableLoaders.Add(loader);
            }
            foreach (var mod in catalog.Mods.Where(mod => mod.SupportedBuildIds.Contains(value.Record.BuildId)))
            {
                AvailableMods.Add(mod);
            }
            ApplyModFilters();
            settings = settings with { CurrentInstanceId = value.Id };
            _ = QueueSettingsSave();
            _ = LoadInstanceDetailsAsync(value.Record, generation);
        }
    }

    partial void OnSelectedSpeedrunTemplateChanged(SpeedrunTemplate? value)
    {
        SelectedLoadNormaliserSeconds = value?.RequiresLoadNormaliserSelection == true
            ? value.AllowedLoadNormaliserSeconds.FirstOrDefault()
            : null;
    }

    partial void OnSelectedLanguageChanged(SettingOption<UiLanguage>? value)
    {
        if (value is null || value.Value == settings.Language)
        {
            return;
        }
        settings = settings with { Language = value.Value };
        ApplyLanguage(value.Value);
        RebuildSettingOptions();
        RebuildModStatusOptions();
        NotifyPreflightLabels();
        _ = QueueSettingsSave();
    }

    partial void OnSelectedThemeChanged(SettingOption<UiTheme>? value)
    {
        if (value is null || value.Value == settings.Theme)
        {
            return;
        }
        settings = settings with { Theme = value.Value };
        ApplyTheme(value.Value);
        _ = QueueSettingsSave();
    }

    private void ApplyInstanceFilter()
    {
        VisibleInstances.Clear();
        foreach (var instance in Instances.Where(instance =>
            string.IsNullOrWhiteSpace(SearchText)
            || instance.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || instance.DisplayVersion.Contains(SearchText, StringComparison.OrdinalIgnoreCase)))
        {
            VisibleInstances.Add(instance);
        }
    }

    private void ApplyModFilters()
    {
        VisibleAvailableMods.Clear();
        foreach (var mod in AvailableMods.Where(mod =>
            string.IsNullOrWhiteSpace(ModSearchText)
            || mod.Name.Contains(ModSearchText, StringComparison.OrdinalIgnoreCase)
            || mod.Id.Contains(ModSearchText, StringComparison.OrdinalIgnoreCase)
            || mod.Version.Contains(ModSearchText, StringComparison.OrdinalIgnoreCase)))
        {
            VisibleAvailableMods.Add(mod);
        }

        VisibleInstalledMods.Clear();
        foreach (var mod in InstalledMods.Where(mod => mod.Matches(ModSearchText, SelectedModStatus)))
        {
            VisibleInstalledMods.Add(mod);
        }
    }

    private void RebuildSettingOptions()
    {
        SelectedLanguage = null;
        LanguageOptions.Clear();
        LanguageOptions.Add(new(UiLanguage.FollowSystem, Loc["FollowSystem"]));
        LanguageOptions.Add(new(UiLanguage.SimplifiedChinese, Loc["SimplifiedChinese"]));
        LanguageOptions.Add(new(UiLanguage.English, Loc["English"]));
        ThemeOptions.Clear();
        ThemeOptions.Add(new(UiTheme.System, Loc["System"]));
        ThemeOptions.Add(new(UiTheme.Light, Loc["Light"]));
        ThemeOptions.Add(new(UiTheme.Dark, Loc["Dark"]));
        SelectedLanguage = LanguageOptions.First(option => option.Value == settings.Language);
        SelectedTheme = ThemeOptions.First(option => option.Value == settings.Theme);
    }

    private void ApplyLanguage(UiLanguage language)
    {
        var localization = new LocalizationViewModel();
        localization.Apply(language);
        Loc = localization;
        OnPropertyChanged(nameof(Loc));
    }

    private void RebuildModStatusOptions()
    {
        ModStatusOptions.Clear();
        ModStatusOptions.Add(new(ModStatusFilter.All, Loc["FilterAll"]));
        ModStatusOptions.Add(new(ModStatusFilter.Enabled, Loc["Enabled"]));
        ModStatusOptions.Add(new(ModStatusFilter.Disabled, Loc["Disabled"]));
        ModStatusOptions.Add(new(ModStatusFilter.Local, Loc["Local"]));
        ModStatusOptions.Add(new(ModStatusFilter.Updates, Loc["Updates"]));
        SelectedModStatusOption = ModStatusOptions.First(option => option.Value == SelectedModStatus);
    }

    private void NotifyPreflightLabels()
    {
        OnPropertyChanged(nameof(GameFilesStatus));
        OnPropertyChanged(nameof(LoaderPreflightStatus));
        OnPropertyChanged(nameof(ModDependencyStatus));
        OnPropertyChanged(nameof(SaveIsolationStatus));
        OnPropertyChanged(nameof(LaunchReadinessTitle));
        OnPropertyChanged(nameof(LaunchReadinessHint));
    }

    private void OnModSelectionChanged()
    {
        OnPropertyChanged(nameof(HasSelectedMods));
        OnPropertyChanged(nameof(SelectedModCount));
    }

    private async Task LoadLogAsync(InstanceLogFile logFile)
    {
        try
        {
            string content = await InstanceLogService.ReadTailAsync(logFile.Path);
            if (SelectedLogFile?.Path == logFile.Path)
            {
                LogContent = content;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ErrorMessage = $"{Loc["OperationFailed"]}: {exception.Message}";
        }
    }

    [RelayCommand]
    private async Task RefreshLogsAsync()
    {
        if (SelectedInstance is null)
        {
            return;
        }
        try
        {
            var previousPath = SelectedLogFile?.Path;
            InstanceLogs.Clear();
            foreach (var log in InstanceLogService.Discover(SelectedInstance.RootPath, GetSharedLocalLowPath()))
            {
                InstanceLogs.Add(log);
            }
            SelectedLogFile = InstanceLogs.FirstOrDefault(log =>
                    string.Equals(log.Path, previousPath, StringComparison.OrdinalIgnoreCase))
                ?? InstanceLogs.FirstOrDefault();
            if (SelectedLogFile is not null)
            {
                LogContent = await InstanceLogService.ReadTailAsync(SelectedLogFile.Path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ErrorMessage = $"{Loc["OperationFailed"]}: {exception.Message}";
        }
    }

    private static void ApplyTheme(UiTheme theme)
    {
        if (Application.Current is null)
        {
            return;
        }
        Application.Current.RequestedThemeVariant = theme switch
        {
            UiTheme.Light => ThemeVariant.Light,
            UiTheme.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }

    private async Task SaveSettingsAsync()
    {
        await settingsSaveLock.WaitAsync();
        try
        {
            await CrystalflySettingsStore.SaveAsync(settingsPath, settings);
        }
        catch (Exception exception) when (IsExpectedSettingsException(exception))
        {
            ErrorMessage = $"{Loc["OperationFailed"]}: {exception.Message}";
        }
        finally
        {
            settingsSaveLock.Release();
        }
    }

    private Task QueueSettingsSave()
    {
        lock (settingsSaveQueueLock)
        {
            return settingsSaveQueue = SaveSettingsAfterAsync(settingsSaveQueue);
        }
    }

    private async Task SaveSettingsAfterAsync(Task previousSave)
    {
        await previousSave;
        await SaveSettingsAsync();
    }

    private async Task FlushSettingsSavesAsync()
    {
        while (true)
        {
            Task pendingSave;
            lock (settingsSaveQueueLock)
            {
                pendingSave = settingsSaveQueue;
            }
            await pendingSave;
            lock (settingsSaveQueueLock)
            {
                if (ReferenceEquals(pendingSave, settingsSaveQueue))
                {
                    return;
                }
            }
        }
    }

    private static bool IsExpectedSettingsException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException;

    private async Task LoadInstanceDetailsAsync(InstanceRecord record, long generation)
    {
        try
        {
            var loaderManager = CreateLoaderManager(record);
            var loaderState = await loaderManager.GetStateAsync();
            var loaderReceipt = await loaderManager.GetReceiptAsync();
            var installed = await CreateModManager(record).GetInstalledAsync();
            var snapshots = await CreateSnapshotService().ListAsync(record.Id);
            var logs = InstanceLogService.Discover(record.RootPath, GetSharedLocalLowPath());
            var recoveries = await FileTransaction.RecoverPendingAsync(
                Path.Combine(paths.GetVersionDataRoot(VersionRoot), "transactions"));
            var isolation = new LocalLowIsolationService(
                GetSharedLocalLowPath(),
                paths.GetVersionDataRoot(VersionRoot));
            var preflight = LaunchPreflightEvaluator.Evaluate(
                record.BuildId != "unknown",
                File.Exists(Path.Combine(record.RootPath, "hollow_knight.exe")),
                loaderState,
                installed,
                recoveries.All(recovery => recovery.State != TransactionState.NeedsAttention),
                Directory.Exists(isolation.GetInstanceLocalLowPath(record.Id)),
                new SystemHollowKnightProcessProbe().IsRunning());

            if (generation != Volatile.Read(ref detailsLoadGeneration)
                || SelectedInstance?.Id != record.Id)
            {
                return;
            }

            CurrentLoaderState = loaderState;
            LoaderVerificationStatus = loaderReceipt is null
                ? string.Empty
                : loaderReceipt.IsVerified
                    ? Loc["VerifiedCatalogLoader"]
                    : Loc["UnverifiedLocalLoader"];
            InstalledMods.Clear();
            foreach (var mod in installed)
            {
                var catalogManifest = catalog.Mods.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, mod.Id, StringComparison.OrdinalIgnoreCase));
                InstalledMods.Add(new InstalledModItemViewModel(mod, catalogManifest, OnModSelectionChanged));
            }
            OnModSelectionChanged();
            ApplyModFilters();
            Snapshots.Clear();
            foreach (var snapshot in snapshots)
            {
                Snapshots.Add(snapshot);
            }
            InstanceLogs.Clear();
            foreach (var log in logs)
            {
                InstanceLogs.Add(log);
            }
            SelectedLogFile = InstanceLogs.FirstOrDefault();
            LaunchPreflight = preflight;
        }
        catch (Exception exception) when (IsExpectedInstanceDetailsException(exception))
        {
            ErrorMessage = $"{Loc["OperationFailed"]}: {exception.Message}";
        }
    }

    private static bool IsExpectedInstanceDetailsException(Exception exception) =>
        exception is IOException
            or InvalidDataException
            or UnauthorizedAccessException
            or InvalidOperationException;

    private async Task RefreshAfterFailedMutationAsync(string instanceId)
    {
        try
        {
            await RefreshAsync();
            SelectedInstance = Instances.FirstOrDefault(instance => instance.Id == instanceId);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException)
        {
            ErrorMessage += $" {Loc["RefreshFailed"]}: {exception.Message}";
        }
    }

    private async Task RunInstanceMutationAsync(Func<InstanceRecord, Task> operation)
    {
        if (SelectedInstance is null || IsMutationBlocked())
        {
            return;
        }

        var instanceId = SelectedInstance.Id;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await EnsureTransactionsHealthyAsync();
            await operation(SelectedInstance.Record);
            await RefreshAsync();
            SelectedInstance = Instances.FirstOrDefault(instance => instance.Id == instanceId);
            StatusMessage = Loc["OperationComplete"];
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or InvalidOperationException
            or UnauthorizedAccessException
            or HttpRequestException
            or KeyNotFoundException
            or ArgumentException)
        {
            ErrorMessage = $"{Loc["OperationFailed"]}: {exception.Message}";
            await RefreshAfterFailedMutationAsync(instanceId);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool IsMutationBlocked()
    {
        if (IsBusy)
        {
            return true;
        }
        if (new SystemHollowKnightProcessProbe().IsRunning())
        {
            ErrorMessage = Loc["CloseGameFirst"];
            return true;
        }
        return false;
    }

    private async Task EnsureTransactionsHealthyAsync()
    {
        var recoveries = await FileTransaction.RecoverPendingAsync(
            Path.Combine(paths.GetVersionDataRoot(VersionRoot), "transactions"));
        if (recoveries.Any(recovery => recovery.State == TransactionState.NeedsAttention))
        {
            throw new InvalidOperationException(Loc["RecoveryNeedsAttention"]);
        }
    }

    private async Task<InstanceRecord?> FindVanillaSourceAsync(string buildId)
    {
        foreach (var candidate in Instances.Where(instance => instance.Record.BuildId == buildId))
        {
            if (await CreateLoaderManager(candidate.Record).GetStateAsync() == LoaderState.Vanilla
                && (await CreateModManager(candidate.Record).GetInstalledAsync()).Count == 0)
            {
                return candidate.Record;
            }
        }
        return null;
    }

    private async Task VerifySpeedrunLaunchAsync(InstanceRecord instance)
    {
        var template = catalog.SpeedrunTemplates.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, instance.SpeedrunTemplateId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(Loc["SpeedrunTemplateMissing"]);
        var build = catalog.Builds.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, instance.BuildId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(Loc["SpeedrunBuildMissing"]);
        var fileManifest = catalog.SpeedrunFileManifests.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, template.FileManifestId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(Loc["SpeedrunManifestMissing"]);
        var result = await new SpeedrunEnvironmentVerifier().VerifyAndWriteReportAsync(
            new SpeedrunVerificationRequest
            {
                Instance = instance,
                Template = template,
                TemplateSource = SpeedrunTemplateSource.OfficialCatalog,
                ExpectedBuild = build,
                CurrentRulesRevision = template.RulesRevision,
                FileManifest = fileManifest,
                LoadNormaliserSeconds = instance.LoadNormaliserSeconds,
                ReportsDirectory = Path.Combine(GetInstanceStateRoot(instance.Id), "speedrun-reports")
            });
        SpeedrunStatus = template.IsOfficial
            ? result.Report.IsOfficiallyVerified
                ? Loc["SpeedrunVerified"]
                : Loc["SpeedrunVerificationFailed"]
            : Loc["SpeedrunUnverifiedReport"];
        StatusMessage = $"{SpeedrunStatus} {result.ReportPath}";
        if (template.IsOfficial && !result.Report.IsOfficiallyVerified)
        {
            throw new InvalidOperationException(
                $"{Loc["SpeedrunVerificationFailed"]} {result.ReportPath}");
        }
    }

    private void OnQrChallengeChanged(object? sender, QrChallengeEventArgs eventArgs)
    {
        var bytes = PngByteQRCodeHelper.GetQRCode(
            eventArgs.ChallengeUrl,
            QRCodeGenerator.ECCLevel.Q,
            6);
        Dispatcher.UIThread.Post(() =>
        {
            using var stream = new MemoryStream(bytes, writable: false);
            QrCodeImage = new Bitmap(stream);
            SteamStatus = "Scan with the Steam mobile app";
        });
    }

    private async Task<GameCatalog> LoadCatalogAsync()
    {
        var result = await CatalogProvider.LoadAsync(
            new Uri("https://raw.githubusercontent.com/wzxnb2333/Crystalfly/main/catalog/catalog.v1.json"),
            Path.Combine(paths.ApplicationDataRoot, "catalog", "catalog.v1.json"),
            MetadataHttpClient);
        try
        {
            result = CatalogMerger.Merge(
                result,
                null,
                null,
                [await OfficialCatalogSource.LoadAsync(MetadataHttpClient)]);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidDataException or System.Xml.XmlException)
        {
        }

        var customCatalogs = new List<GameCatalog>();
        foreach (var definition in settings.CustomCatalogs)
        {
            try
            {
                customCatalogs.Add(await CustomCatalogSource.LoadAsync(
                    definition.Namespace,
                    new Uri(definition.Url),
                    MetadataHttpClient));
            }
            catch (Exception exception) when (exception is HttpRequestException
                or InvalidDataException
                or System.Text.Json.JsonException
                or UriFormatException
                or ArgumentException)
            {
            }
        }
        return CatalogMerger.Merge(result, null, null, customCatalogs);
    }

    private string UniqueInstanceName(string version)
    {
        var baseName = $"Hollow Knight {version}";
        var name = baseName;
        for (var suffix = 2; ; suffix++)
        {
            var destination = InstanceDirectory.ResolveUnderRoot(VersionRoot, name);
            if (!Directory.Exists(destination) && !File.Exists(destination))
            {
                return name;
            }

            var suffixText = $" ({suffix})";
            name = $"{baseName[..Math.Min(baseName.Length, 255 - suffixText.Length)]}{suffixText}";
        }
    }

    private LoaderManager CreateLoaderManager(InstanceRecord record)
    {
        var stateRoot = GetInstanceStateRoot(record.Id);
        return new LoaderManager(
            record.RootPath,
            Path.Combine(paths.GetVersionDataRoot(VersionRoot), "transactions"),
            Path.Combine(stateRoot, "loader.json"),
            Path.Combine(paths.GetVersionDataRoot(VersionRoot), "packages"),
            PackageHttpClient);
    }

    private ModManager CreateModManager(InstanceRecord record) => new(
        record.RootPath,
        Path.Combine(paths.GetVersionDataRoot(VersionRoot), "transactions"),
        Path.Combine(GetInstanceStateRoot(record.Id), "mods"),
        Path.Combine(paths.GetVersionDataRoot(VersionRoot), "packages"),
        PackageHttpClient);

    private NamedSnapshotService CreateSnapshotService() => new(
        paths.GetVersionDataRoot(VersionRoot));

    private string GetInstanceStateRoot(string instanceId) =>
        Path.Combine(paths.GetVersionDataRoot(VersionRoot), "instances", instanceId);

    private static string GetSharedLocalLowPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "..",
        "LocalLow",
        "Team Cherry",
        "Hollow Knight");

    public ValueTask DisposeAsync()
    {
        lock (disposeLock)
        {
            return new ValueTask(disposeTask ??= DisposeCoreAsync());
        }
    }

    private async Task DisposeCoreAsync()
    {
        downloadCancellation?.Cancel();
        try
        {
            try
            {
                await Task.WhenAll(
                    LaunchGameCommand.ExecutionTask ?? Task.CompletedTask,
                    DownloadBuildCommand.ExecutionTask ?? Task.CompletedTask);
            }
            finally
            {
                await FlushSettingsSavesAsync();
            }
        }
        finally
        {
            try
            {
                if (disposeSteamOverride is not null)
                {
                    await disposeSteamOverride();
                }
                else if (steamSession is not null)
                {
                    await steamSession.DisposeAsync();
                }
            }
            finally
            {
                QrCodeImage = null;
            }
        }
    }
}

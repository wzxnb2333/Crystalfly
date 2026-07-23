using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.App.Downloads;
using Crystalfly.App.Updates;
using Crystalfly.App.Runtime;
using Crystalfly.App.ViewModels.Dialogs;
using Crystalfly.Core.Catalog;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Loaders;
using Crystalfly.Core.LocalLow;
using Crystalfly.Core.Networking;
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
using Semi.Avalonia;
using Ursa.Themes.Semi;

namespace Crystalfly.App.ViewModels;

public partial class MainViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly HttpClient metadataHttpClient;
    private readonly HttpClient directMetadataHttpClient;
    private readonly HttpClient packageHttpClient;
    private readonly NetworkPolicy networkPolicy;
    private readonly CrystalflyPaths paths;
    private readonly string settingsPath;
    private GameCatalog catalog;
    private ModTranslationCatalog modTranslations;
    private ModActivityCatalog modActivityCatalog;
    private readonly DpapiRefreshTokenStore tokenStore;
    private readonly SemaphoreSlim settingsSaveLock = new(1, 1);
    private readonly SemaphoreSlim steamConnectionGate = new(1, 1);
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private readonly object settingsSaveQueueLock = new();
    private readonly object disposeLock = new();
    private readonly object externalProtocolCommandSync = new();
    private readonly Func<Task>? launchOverride;
    private readonly Func<CancellationToken, Task>? downloadOverride;
    private readonly Func<Task>? disposeSteamOverride;
    private readonly Func<CancellationToken, Task<RefreshTokenCredential>>? qrSignInOverride;
    private readonly Func<bool>? steamLoggedOnOverride;
    private readonly Func<CancellationToken, Task<GitHubRouteLatencyTestResult>>? githubLatencyTestOverride;
    private readonly Func<ModManifest, CancellationToken, Task<ModContentLoadResult>>? modContentLoadOverride;
    private readonly Func<
        CrystalflySettings,
        bool,
        CancellationToken,
        Task<ApplicationUpdateCheckResult>>? applicationUpdateCheckOverride;
    private readonly Func<
        InstanceRecord,
        Func<CancellationToken, ValueTask<InstanceDeletionConditions>>,
        CancellationToken,
        Task<InstanceDeletionResult>>? instanceDeletionOverride;
    private readonly GitHubRouteLatencyService githubLatencyService;
    private readonly IProtocolRegistrationService protocolRegistrationService;
    private CrystalflySettings settings = new();
    private Task settingsSaveQueue = Task.CompletedTask;
    private Task steamOfflineTransitionTask = Task.CompletedTask;
    private Task? initializationTask;
    private Task? disposeTask;
    private Task externalProtocolCommandTask = Task.CompletedTask;
    private SteamAuthenticationSession? steamSession;
    private CancellationTokenSource? steamSignInCancellation;
    private CancellationTokenSource? downloadCancellation;
    private InstanceRuntimeSession? runtimeSession;
    private Bitmap? qrCodeImage;
    private long detailsLoadGeneration;
    private CancellationTokenSource? detailsLoadCancellation;
    private Task detailsLoadTask = Task.CompletedTask;
    private long selectedModContentLoadGeneration;
    private CancellationTokenSource? selectedModContentLoadCancellation;
    private Task selectedModContentLoadTask = Task.CompletedTask;
    private OfficialCatalogLoadResult? officialCatalogResult;
    private CustomModLinksLoadResult? customModLinksResult;
    private string? customModLinksError;
    private ModTranslationLoadResult? modTranslationResult;
    private ModActivityLoadResult? modActivityResult;
    private MarketModItemViewModel? selectedMarketModDisplay;
    private string? installedModSelectionAnchorId;
    private LoaderInspection currentLoaderInspection = new()
    {
        State = LoaderState.Vanilla,
        Ownership = LoaderOwnership.None
    };

    public MainViewModel(string? applicationDataRoot = null)
        : this(applicationDataRoot, null, null, null)
    {
    }

    internal MainViewModel(
        string? applicationDataRoot,
        Func<Task>? launchOverride,
        Func<CancellationToken, Task>? downloadOverride,
        Func<Task>? disposeSteamOverride,
        Func<CancellationToken, Task<RefreshTokenCredential>>? qrSignInOverride = null,
        DownloadQueueService? downloadQueueOverride = null,
        Func<bool>? steamLoggedOnOverride = null,
        Func<CancellationToken, Task<GitHubRouteLatencyTestResult>>? githubLatencyTestOverride = null,
        Func<
            InstanceRecord,
            Func<CancellationToken, ValueTask<InstanceDeletionConditions>>,
            CancellationToken,
            Task<InstanceDeletionResult>>? instanceDeletionOverride = null,
        Func<ModManifest, CancellationToken, Task<ModContentLoadResult>>? modContentLoadOverride = null,
        Func<
            CrystalflySettings,
            bool,
            CancellationToken,
            Task<ApplicationUpdateCheckResult>>? applicationUpdateCheckOverride = null,
        IProtocolRegistrationService? protocolRegistrationService = null)
    {
        this.launchOverride = launchOverride;
        this.downloadOverride = downloadOverride;
        this.disposeSteamOverride = disposeSteamOverride;
        this.qrSignInOverride = qrSignInOverride;
        this.steamLoggedOnOverride = steamLoggedOnOverride;
        this.githubLatencyTestOverride = githubLatencyTestOverride;
        this.modContentLoadOverride = modContentLoadOverride;
        this.instanceDeletionOverride = instanceDeletionOverride;
        this.applicationUpdateCheckOverride = applicationUpdateCheckOverride;
        this.protocolRegistrationService = protocolRegistrationService
            ?? new WindowsProtocolRegistrationService();
        paths = applicationDataRoot is null
            ? CrystalflyPaths.Resolve(
                AppContext.BaseDirectory,
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
            : new CrystalflyPaths(Path.GetFullPath(applicationDataRoot), IsPortable: false);
        settingsPath = Path.Combine(paths.ApplicationDataRoot, "settings.json");
        tokenStore = new DpapiRefreshTokenStore(Path.Combine(paths.ApplicationDataRoot, "steam-token.dat"));
        catalog = EmbeddedCatalog.Load();
        modTranslations = EmbeddedModTranslationCatalog.Load();
        modActivityCatalog = EmbeddedModActivityCatalog.Load();
        networkPolicy = new NetworkPolicy();
        metadataHttpClient = new HttpClient(new GitHubDownloadRouteHandler(
            () => settings.GitHubDownloadRoute,
            networkPolicy,
            new HttpClientHandler())) { Timeout = TimeSpan.FromSeconds(15) };
        directMetadataHttpClient = new HttpClient(new NetworkPolicyHandler(
            networkPolicy,
            new HttpClientHandler())) { Timeout = TimeSpan.FromSeconds(15) };
        packageHttpClient = new HttpClient(new GitHubDownloadRouteHandler(
            () => settings.GitHubDownloadRoute,
            networkPolicy,
            new HttpClientHandler())) { Timeout = TimeSpan.FromMinutes(30) };
        githubLatencyService = new GitHubRouteLatencyService(networkPolicy, new HttpClientHandler());
        Loc = new LocalizationViewModel();
        downloadQueue = downloadQueueOverride ?? CreateDownloadQueue();
        downloadQueue.QueueChanged += OnDownloadQueueChanged;
    }

    private bool IsSteamSessionLoggedOn() =>
        steamLoggedOnOverride?.Invoke() ?? steamSession?.IsLoggedOn == true;

    public LocalizationViewModel Loc { get; private set; }

    public event Action<string>? ToastRequested;

    public ObservableCollection<InstanceItemViewModel> Instances { get; } = [];

    public ObservableCollection<InstanceItemViewModel> VisibleInstances { get; } = [];

    public ObservableCollection<SpeedrunTemplate> SpeedrunTemplates { get; } = [];

    public ObservableCollection<LoaderManifest> AvailableLoaders { get; } = [];

    public ObservableCollection<ModManifest> AvailableMods { get; } = [];

    public ObservableCollection<ModManifest> VisibleAvailableMods { get; } = [];

    public ObservableCollection<ModManifest> MarketMods { get; } = [];

    public ObservableCollection<ModManifest> VisibleMarketMods { get; } = [];

    public ObservableCollection<MarketModItemViewModel> MarketDisplayMods { get; } = [];

    public ObservableCollection<MarketModItemViewModel> VisibleMarketDisplayMods { get; } = [];

    public ObservableCollection<SettingOption<string>> MarketBuildOptions { get; } = [];

    public ObservableCollection<SettingOption<string>> MarketLoaderOptions { get; } = [];

    public ObservableCollection<SettingOption<string>> MarketSourceOptions { get; } = [];

    public ObservableCollection<SettingOption<string>> MarketTagOptions { get; } = [];

    public ObservableCollection<SettingOption<MarketActivityFilter>> MarketActivityOptions { get; } = [];

    public ObservableCollection<MarketInstallTargetViewModel> MarketInstallTargets { get; } = [];

    public ObservableCollection<InstalledModItemViewModel> InstalledMods { get; } = [];

    public ObservableCollection<InstalledModItemViewModel> VisibleInstalledMods { get; } = [];

    public ObservableCollection<string> UnusedDependencySuggestions { get; } = [];

    public ObservableCollection<SettingOption<ModStatusFilter>> ModStatusOptions { get; } = [];

    public ObservableCollection<InstanceLogFile> InstanceLogs { get; } = [];

    public ObservableCollection<NamedSnapshot> Snapshots { get; } = [];

    public ObservableCollection<SettingOption<UiLanguage>> LanguageOptions { get; } = [];

    public ObservableCollection<SettingOption<UiTheme>> ThemeOptions { get; } = [];

    public ObservableCollection<SettingOption<GitHubDownloadRoute>> GitHubRouteOptions { get; } = [];

    public ObservableCollection<SettingOption<string>> CustomModLinksBuildOptions { get; } = [];

    public ObservableCollection<SettingOption<string>> CustomModLinksLoaderOptions { get; } = [];

    public ObservableCollection<DownloadBuildOption> DownloadBuilds { get; } = [];

    public bool HasInstance => SelectedInstance is not null;

    public bool CanNavigate => !IsBusy && !IsGameRunning && !IsExternalCommandRunning;

    public bool CanCloneInstance => CanNavigate && !string.IsNullOrWhiteSpace(CloneInstanceName);

    public bool HasSelectedMods => InstalledMods.Any(mod => mod.IsSelected);

    public int SelectedModCount => InstalledMods.Count(mod => mod.IsSelected);

    public bool HasModDependencyProblems => InstalledMods.Count > 0 && !LaunchPreflight.DependenciesReady;

    public bool HasUnusedDependencySuggestions => UnusedDependencySuggestions.Count > 0;

    public string UnusedDependencySummary => string.Format(
        CultureInfo.CurrentUICulture,
        Loc["UnusedDependencyCountFormat"],
        UnusedDependencySuggestions.Count);

    public bool HasLaunchIssues => LaunchPreflight.Issues.Count > 0;

    public int LaunchIssueCount => LaunchPreflight.Issues.Count;

    public string LaunchIssueCountText => string.Format(
        CultureInfo.CurrentUICulture,
        Loc["LaunchIssueCountFormat"],
        LaunchIssueCount);

    public bool CanLaunch => HasInstance && CanNavigate && LaunchPreflight.IsReady;

    public bool CanAttemptLaunch => HasInstance
        && CanNavigate
        && !IsLoadingInstanceDetails
        && LaunchPreflight.CanAttemptLaunch;

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

    public string OfficialModCatalogStatus => settings.CustomModLinks is not null
        ? customModLinksResult?.Status switch
        {
            CustomModLinksLoadStatus.Remote => Loc["CustomModLinksRemote"],
            CustomModLinksLoadStatus.Cached => Loc["CustomModLinksCached"],
            _ => Loc["CustomModLinksFailed"]
        }
        : officialCatalogResult?.Status switch
    {
        OfficialCatalogLoadStatus.Remote => Loc["CatalogRemote"],
        OfficialCatalogLoadStatus.Cached => Loc["CatalogCached"],
        _ => Loc["CatalogFailed"]
    };

    public string OfficialModCatalogSummary => settings.CustomModLinks is not null
        ? customModLinksResult is null
            ? Loc["UnverifiedSource"]
            : $"{settings.CustomModLinks.LoaderId} · {customModLinksResult.Catalog.Mods.Count} Mods · {Loc["UnverifiedSource"]}"
        : officialCatalogResult is null
            ? string.Empty
            : $"API v{officialCatalogResult.ApiVersion ?? "?"} · {officialCatalogResult.ModCount} Mods";

    public string OfficialModCatalogError => settings.CustomModLinks is not null
        ? customModLinksError ?? customModLinksResult?.Reason ?? string.Empty
        : officialCatalogResult?.Reason ?? string.Empty;

    public bool IsProtocolRegistered => protocolRegistrationService.IsRegistered(
        Path.Combine(AppContext.BaseDirectory, "Crystalfly.App.exe"));

    public string ProtocolRegistrationStatus => IsProtocolRegistered
        ? Loc["ProtocolRegistered"]
        : Loc["ProtocolNotRegistered"];

    public bool IsLaunchPage => CurrentPage == "Launch";

    public bool IsVersionsPage => CurrentPage == "Versions";

    public bool IsManagePage => CurrentPage == "Manage";

    public bool IsSpeedrunPage => CurrentPage == "Speedrun";

    public bool IsDownloadsPage => CurrentPage == "Downloads";

    public bool IsSettingsPage => CurrentPage == "Settings";

    public bool IsGameVersionsDownloadSection => CurrentDownloadSection == "GameVersions";

    public bool IsModMarketDownloadSection => CurrentDownloadSection == "ModMarket";

    public bool IsMarketList => SelectedMarketMod is null;

    public bool IsMarketDetail => SelectedMarketMod is not null;

    public MarketModItemViewModel? SelectedMarketModDisplay => selectedMarketModDisplay;

    public bool HasSelectedModReadme => !string.IsNullOrWhiteSpace(SelectedModReadmeMarkdown);

    public bool HasSelectedModReleaseNotes => !string.IsNullOrWhiteSpace(SelectedModReleaseNotesMarkdown);

    public bool HasSelectedModContentError => !string.IsNullOrWhiteSpace(SelectedModContentError);

    public bool HasSelectedMarketModInstallation => SelectedMarketInstalledMod is not null;

    public bool CanReinstallSelectedMarketMod => SelectedMarketInstalledMod?.CanReinstall == true;

    public bool CanAccessSelectedModGlobalSettings => SelectedMarketInstalledMod is not null
        && SelectedMarketMod is { SourceName: "HK ModLinks" } manifest
        && manifest.Id.StartsWith("hkmod:", StringComparison.OrdinalIgnoreCase);

    public bool HasSelectedModGlobalSettings => SelectedModGlobalSettingsFile is not null;

    public string SelectedModContentStatusText => SelectedModContentStatus switch
    {
        ModContentLoadStatus.Remote => Loc["ContentRemote"],
        ModContentLoadStatus.Cached => Loc["ContentCached"],
        _ => Loc["ContentUnavailable"]
    };

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
    public partial GameConfigViewModel? GameConfig { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGameVersionsDownloadSection))]
    [NotifyPropertyChangedFor(nameof(IsModMarketDownloadSection))]
    [NotifyPropertyChangedFor(nameof(IsDownloadQueueSection))]
    public partial string CurrentDownloadSection { get; set; } = "GameVersions";

    [ObservableProperty]
    public partial string VersionRoot { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ModSearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MarketSearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial SettingOption<string>? SelectedMarketBuildOption { get; set; }

    [ObservableProperty]
    public partial SettingOption<string>? SelectedMarketLoaderOption { get; set; }

    [ObservableProperty]
    public partial SettingOption<string>? SelectedMarketSourceOption { get; set; }

    [ObservableProperty]
    public partial SettingOption<string>? SelectedMarketTagOption { get; set; }

    [ObservableProperty]
    public partial SettingOption<MarketActivityFilter>? SelectedMarketActivityOption { get; set; }

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
    [NotifyPropertyChangedFor(nameof(CanAttemptLaunch))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanNavigate))]
    [NotifyPropertyChangedFor(nameof(CanCloneInstance))]
    [NotifyPropertyChangedFor(nameof(CanLaunch))]
    [NotifyPropertyChangedFor(nameof(CanAttemptLaunch))]
    public partial bool IsExternalCommandRunning { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAttemptLaunch))]
    public partial bool IsLoadingInstanceDetails { get; set; }

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
    [NotifyPropertyChangedFor(nameof(CanAttemptLaunch))]
    public partial bool IsGameRunning { get; set; }

    [ObservableProperty]
    public partial LoaderState CurrentLoaderState { get; set; }

    [ObservableProperty]
    public partial LoaderManifest? SelectedLoader { get; set; }

    [ObservableProperty]
    public partial ModManifest? SelectedAvailableMod { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMarketList))]
    [NotifyPropertyChangedFor(nameof(IsMarketDetail))]
    public partial ModManifest? SelectedMarketMod { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedModReadme))]
    public partial string SelectedModReadmeMarkdown { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedModReleaseNotes))]
    public partial string SelectedModReleaseNotesMarkdown { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedModContentError))]
    public partial string SelectedModContentError { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedModContentStatusText))]
    public partial ModContentLoadStatus SelectedModContentStatus { get; set; } = ModContentLoadStatus.Unavailable;

    [ObservableProperty]
    public partial bool IsLoadingSelectedModContent { get; set; }

    [ObservableProperty]
    public partial MarketInstallTargetViewModel? SelectedMarketInstallTarget { get; set; }

    [ObservableProperty]
    public partial InstalledModItemViewModel? SelectedInstalledMod { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedMarketModInstallation))]
    [NotifyPropertyChangedFor(nameof(CanReinstallSelectedMarketMod))]
    [NotifyPropertyChangedFor(nameof(CanAccessSelectedModGlobalSettings))]
    public partial InstalledModItemViewModel? SelectedMarketInstalledMod { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedModGlobalSettings))]
    public partial GlobalModSettingsFile? SelectedModGlobalSettingsFile { get; set; }

    [ObservableProperty]
    public partial InstanceLogFile? SelectedLogFile { get; set; }

    [ObservableProperty]
    public partial string LogContent { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLaunch))]
    [NotifyPropertyChangedFor(nameof(CanAttemptLaunch))]
    [NotifyPropertyChangedFor(nameof(GameFilesStatus))]
    [NotifyPropertyChangedFor(nameof(LoaderPreflightStatus))]
    [NotifyPropertyChangedFor(nameof(ModDependencyStatus))]
    [NotifyPropertyChangedFor(nameof(HasModDependencyProblems))]
    [NotifyPropertyChangedFor(nameof(HasLaunchIssues))]
    [NotifyPropertyChangedFor(nameof(LaunchIssueCount))]
    [NotifyPropertyChangedFor(nameof(LaunchIssueCountText))]
    [NotifyPropertyChangedFor(nameof(SaveIsolationStatus))]
    [NotifyPropertyChangedFor(nameof(LaunchReadinessTitle))]
    [NotifyPropertyChangedFor(nameof(LaunchReadinessHint))]
    public partial LaunchPreflightResult LaunchPreflight { get; set; } = new(false, false, false, false);

    [ObservableProperty]
    public partial NamedSnapshot? SelectedSnapshot { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingSave))]
    public partial SaveEditorViewModel? SaveEditor { get; set; }

    public bool IsEditingSave => SaveEditor is not null;

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
    [NotifyPropertyChangedFor(nameof(CanAttemptLaunch))]
    [NotifyPropertyChangedFor(nameof(LaunchReadinessTitle))]
    [NotifyPropertyChangedFor(nameof(LaunchReadinessHint))]
    public partial InstanceItemViewModel? SelectedInstance { get; set; }

    [ObservableProperty]
    public partial SettingOption<UiLanguage>? SelectedLanguage { get; set; }

    [ObservableProperty]
    public partial SettingOption<UiTheme>? SelectedTheme { get; set; }
    [ObservableProperty]
    public partial SettingOption<GitHubDownloadRoute>? SelectedGitHubRoute { get; set; }

    [ObservableProperty]
    public partial bool IsTestingGitHubLatency { get; set; }

    [ObservableProperty]
    public partial string GitHubDirectLatency { get; set; } = "-";

    [ObservableProperty]
    public partial string GitHubMirrorLatency { get; set; } = "-";

    [ObservableProperty]
    public partial bool IsOfflineMode { get; set; }

    [ObservableProperty]
    public partial string CustomModLinksUrl { get; set; } = string.Empty;

    [ObservableProperty]
    public partial SettingOption<string>? SelectedCustomModLinksBuild { get; set; }

    [ObservableProperty]
    public partial SettingOption<string>? SelectedCustomModLinksLoader { get; set; }


    public Task InitializeAsync()
    {
        lock (disposeLock)
        {
            return disposeTask is not null
                ? Task.CompletedTask
                : initializationTask ??= InitializeCoreAsync();
        }
    }

    private async Task InitializeCoreAsync()
    {
        ApplicationUpdateLauncher.CleanupExpiredAssets(
            Path.Combine(paths.ApplicationDataRoot, "updates"),
            DateTimeOffset.UtcNow,
            TimeSpan.FromDays(7));
        settings = await CrystalflySettingsStore.LoadAsync(settingsPath);
        IsOfflineMode = settings.OfflineMode;
        ApplyLanguage(settings.Language);
        ApplyTheme(settings.Theme);
        InitializeApplicationUpdateSettings();
        catalog = await LoadCatalogAsync(lifetimeCancellation.Token);
        VersionRoot = settings.VersionRoot ?? string.Empty;
        CustomSourcesText = string.Join(
            Environment.NewLine,
            settings.CustomCatalogs.Select(source => $"{source.Namespace}={source.Url}"));
        CustomModLinksUrl = settings.CustomModLinks?.Url ?? string.Empty;
        RebuildSettingOptions();
        RebuildCustomModLinksOptions();
        RebuildModStatusOptions();
        RebuildMarketCatalog();
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
        if (!IsOfflineMode)
        {
            await TryReconnectSteamAsync();
        }

        if (Directory.Exists(VersionRoot))
        {
            await RefreshAsync();
        }
        else
        {
            StatusMessage = Loc["ChooseRoot"];
        }
        await InitializeDownloadQueueAsync();
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

    private async Task LoadGameConfigAsync()
    {
        var selected = SelectedInstance;
        if (selected is null || IsGameRunning)
        {
            GameConfig = null;
            return;
        }

        var configPath = Path.Combine(
            GetInstanceStateRoot(selected.Id),
            "local-low",
            AppConfigService.FileName);
        if (GameConfig is not null
            && string.Equals(GameConfig.ConfigPath, configPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var viewModel = new GameConfigViewModel(configPath);
        viewModel.Saved += OnGameConfigSaved;
        GameConfig = viewModel;
        try
        {
            await viewModel.LoadAsync(lifetimeCancellation.Token);
            if (SelectedInstance?.Id != selected.Id
                || CurrentManageTab != "Config"
                || IsGameRunning)
            {
                if (ReferenceEquals(GameConfig, viewModel))
                {
                    GameConfig = null;
                }
            }
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
            if (ReferenceEquals(GameConfig, viewModel))
            {
                GameConfig = null;
            }
        }
        catch (Exception exception) when (
            exception is IOException
                or InvalidDataException
                or UnauthorizedAccessException)
        {
            if (ReferenceEquals(GameConfig, viewModel))
            {
                GameConfig = null;
                ErrorMessage = $"{Loc["OperationFailed"]}: {exception.Message}";
            }
        }
    }

    private void OnGameConfigSaved() =>
        ToastRequested?.Invoke(Loc["ConfigSaved"]);

    [RelayCommand]
    private void SelectDownloadSection(string? section)
    {
        if (IsBusy || section is not ("GameVersions" or "ModMarket" or "DownloadQueue"))
        {
            return;
        }
        CurrentDownloadSection = section;
        if (section == "GameVersions")
        {
            SelectedMarketMod = null;
        }
    }

    [RelayCommand]
    private void OpenMarketMod(ModManifest? mod)
    {
        if (CanNavigate && mod is not null)
        {
            SelectedMarketMod = mod;
        }
    }

    [RelayCommand]
    private void OpenModMarketForSelectedInstance()
    {
        CurrentPage = "Downloads";
        CurrentDownloadSection = "ModMarket";
        SelectedMarketMod = null;
        if (SelectedInstance is null)
        {
            return;
        }
        SelectedMarketBuildOption = MarketBuildOptions.FirstOrDefault(option =>
            string.Equals(option.Value, SelectedInstance.Record.BuildId, StringComparison.OrdinalIgnoreCase));
        var loaderId = currentLoaderInspection.State is LoaderState.ModdingApi or LoaderState.BepInEx
            ? currentLoaderInspection.PackageId
            : null;
        SelectedMarketLoaderOption = string.IsNullOrWhiteSpace(loaderId)
            ? null
            : MarketLoaderOptions.FirstOrDefault(option =>
                string.Equals(option.Value, loaderId, StringComparison.OrdinalIgnoreCase));
    }

    [RelayCommand]
    private void OpenInstalledModInfo(InstalledModItemViewModel? item)
    {
        if (item?.CatalogManifest is not ModManifest manifest)
        {
            return;
        }
        SelectedInstalledMod = item;
        OpenModMarketForSelectedInstance();
        SelectedMarketMod = manifest;
    }

    [RelayCommand]
    private void BackToMarket() => SelectedMarketMod = null;

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task TestGitHubLatencyAsync(CancellationToken cancellationToken)
    {
        IsTestingGitHubLatency = true;
        GitHubDirectLatency = Loc["LatencyTesting"];
        GitHubMirrorLatency = Loc["LatencyTesting"];
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            lifetimeCancellation.Token);
        try
        {
            var result = githubLatencyTestOverride is null
                ? await githubLatencyService.TestAsync(linkedCancellation.Token)
                : await githubLatencyTestOverride(linkedCancellation.Token);
            GitHubDirectLatency = FormatGitHubLatency(result.Direct);
            GitHubMirrorLatency = FormatGitHubLatency(result.Mirror);
        }
        catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
        {
            GitHubDirectLatency = Loc["LatencyCanceled"];
            GitHubMirrorLatency = Loc["LatencyCanceled"];
        }
        finally
        {
            IsTestingGitHubLatency = false;
        }
    }

    private string FormatGitHubLatency(GitHubRouteLatencyResult result) => result.Status switch
    {
        GitHubRouteLatencyStatus.Success when result.Latency is { } latency =>
            $"{Math.Max(0, Math.Round(latency.TotalMilliseconds))} ms",
        GitHubRouteLatencyStatus.Timeout => Loc["LatencyTimeout"],
        _ => Loc["LatencyUnavailable"]
    };

    [RelayCommand]
    private async Task PrepareMarketInstallTargetsAsync()
    {
        var mod = SelectedMarketMod;
        MarketInstallTargets.Clear();
        SelectedMarketInstallTarget = null;
        if (mod is null)
        {
            return;
        }

        var targets = new List<MarketInstallTargetViewModel>();
        foreach (var instance in Instances)
        {
            MarketInstallTargetViewModel target;
            try
            {
                var service = CreateModInstallService(instance.Record);
                var evaluation = await service.EvaluateAsync(
                    mod.Id,
                    lifetimeCancellation.Token);
                var requiredLoader = catalog.Loaders.FirstOrDefault(loader =>
                    string.Equals(loader.Id, evaluation.RequiredLoaderId, StringComparison.OrdinalIgnoreCase)
                    && loader.SupportedBuildIds.Contains(instance.Record.BuildId, StringComparer.OrdinalIgnoreCase));
                target = new MarketInstallTargetViewModel(
                    instance,
                    instance.DisplayVersion,
                    FormatLoaderDisplay(evaluation.Loader),
                    FormatMarketInstallStatus(instance.Record, evaluation, requiredLoader),
                    evaluation.Status != ModInstallReadiness.Blocked,
                    evaluation.Status == ModInstallReadiness.RequiresLoader);
            }
            catch (Exception exception) when (exception is IOException
                or InvalidDataException
                or KeyNotFoundException
                or UnauthorizedAccessException
                or System.Text.Json.JsonException)
            {
                target = new MarketInstallTargetViewModel(
                    instance,
                    instance.DisplayVersion,
                    instance.LoaderDisplay,
                    $"{Loc["OperationFailed"]}: {exception.Message}",
                    IsAvailable: false,
                    RequiresLoader: false);
            }
            targets.Add(target);
        }

        if (!ReferenceEquals(SelectedMarketMod, mod))
        {
            return;
        }
        foreach (var target in targets)
        {
            MarketInstallTargets.Add(target);
            SelectedMarketInstallTarget ??= target.IsAvailable ? target : null;
        }
    }

    [RelayCommand]
    private async Task InstallMarketModAsync()
    {
        await EnqueueSelectedMarketModAsync();
    }

    private string FormatLoaderDisplay(LoaderInspection loader)
    {
        if (loader.State == LoaderState.Vanilla)
        {
            return Loc["Vanilla"];
        }

        var display = loader.PackageId ?? loader.State.ToString();
        return loader.Ownership == LoaderOwnership.External
            ? $"{display} · {Loc["ExternalLoader"]}"
            : display;
    }

    private string FormatMarketInstallStatus(
        InstanceRecord instance,
        ModInstallEvaluation evaluation,
        LoaderManifest? requiredLoader)
    {
        if (evaluation.Status == ModInstallReadiness.Ready)
        {
            return Loc["Ready"];
        }
        if (evaluation.Status == ModInstallReadiness.RequiresLoader)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                Loc["MarketWillInstallLoader"],
                requiredLoader?.Name ?? evaluation.RequiredLoaderId);
        }
        if (instance.Purpose == InstancePurpose.OfficialSpeedrun)
        {
            return Loc["OfficialSpeedrunModBlocked"];
        }
        if (string.Equals(instance.BuildId, "unknown", StringComparison.OrdinalIgnoreCase))
        {
            return Loc["UnknownBuild"];
        }
        return evaluation.Loader.State switch
        {
            LoaderState.Conflict => Loc["LoaderConflict"],
            LoaderState.Drifted => Loc["MarketDriftedBlocked"],
            LoaderState.ModdingApi or LoaderState.BepInEx => Loc["MarketWrongLoaderBlocked"],
            _ => Loc["MarketIncompatibleBlocked"]
        };
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
    private void SelectInstanceForLaunch(InstanceItemViewModel? instance)
    {
        if (!CanNavigate || instance is null)
        {
            return;
        }
        SelectedInstance = instance;
        CurrentPage = "Launch";
    }

    [RelayCommand]
    private void OpenInstanceSettings(InstanceItemViewModel? instance)
    {
        if (!CanNavigate || instance is null)
        {
            return;
        }
        SelectedInstance = instance;
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
            var discovered = new List<(
                InstanceRecord Record,
                LoaderState LoaderState,
                InstalledPackageReceipt? LoaderReceipt,
                int ModCount)>();
            await instanceOperationCoordinator.RunAsync(
                "transactions",
                async cancellationToken =>
                {
                    var deletionRecoveries = await new InstanceDeletionService(VersionRoot)
                        .RecoverPendingAsync(cancellationToken);
                    if (deletionRecoveries.Any(recovery => !recovery.Completed))
                    {
                        throw new InvalidOperationException(Loc["DeleteRecoveryNeedsAttention"]);
                    }
                    await EnsureTransactionsHealthyAsync(cancellationToken);
                    var records = await InstanceImportService.DiscoverAsync(
                        VersionRoot,
                        catalog,
                        cancellationToken);
                    var isolation = new LocalLowIsolationService(
                        GetSharedLocalLowPath(),
                        paths.GetVersionDataRoot(VersionRoot));
                    bool canCompleteActiveSession = runtimeSession is null
                        && !IsGameRunning
                        && !new SystemHollowKnightProcessProbe().IsRunning();
                    await isolation.InitializeBaselinesAsync(
                        records.Select(static record => record.Id),
                        allowActiveSessionCompletion: canCompleteActiveSession,
                        cancellationToken);
                    foreach (var record in records.OrderBy(
                        instance => instance.Name,
                        StringComparer.OrdinalIgnoreCase))
                    {
                        var loaderManager = CreateLoaderManager(record);
                        var loaderInspection = await loaderManager.InspectAsync(cancellationToken);
                        var loaderState = loaderInspection.State;
                        var loaderReceipt = await loaderManager.GetReceiptAsync(cancellationToken);
                        var modCount = (await CreateModManager(record).DiscoverAsync(
                            loaderInspection.PackageId ?? loaderState.ToString(),
                            cancellationToken)).Mods.Count;
                        discovered.Add((record, loaderState, loaderReceipt, modCount));
                    }
                },
                lifetimeCancellation.Token);
            Instances.Clear();
            foreach (var item in discovered)
            {
                var build = catalog.Builds.FirstOrDefault(candidate => candidate.Id == item.Record.BuildId);
                Instances.Add(new InstanceItemViewModel(
                    item.Record,
                    build?.DisplayVersion ?? Loc["UnknownBuild"],
                    item.LoaderReceipt is null
                        ? item.LoaderState.ToString()
                        : item.LoaderReceipt.IsVerified
                            ? item.LoaderReceipt.PackageId
                            : $"{item.LoaderReceipt.PackageId} · {Loc["Unverified"]}",
                    item.ModCount));
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
            var source = SelectedInstance.Record;
            InstanceRecord? clone = null;
            await instanceOperationCoordinator.RunAsync(
                source.Id,
                async _ =>
                {
                    if (new SystemHollowKnightProcessProbe().IsRunning())
                    {
                        throw new InvalidOperationException(Loc["CloseGameFirst"]);
                    }
                    if (await CreateLoaderManager(source).GetStateAsync() != LoaderState.Vanilla
                        || (await CreateModManager(source).GetInstalledAsync()).Count != 0)
                    {
                        throw new InvalidOperationException(Loc["CloneVanillaOnly"]);
                    }
                    clone = await InstanceCloneService.CloneAsync(
                        source.RootPath,
                        CloneInstanceName.Trim(),
                        Guid.NewGuid().ToString("N"));
                },
                lifetimeCancellation.Token);
            var createdClone = clone
                ?? throw new InvalidOperationException("The instance clone was not created.");
            CloneInstanceName = string.Empty;
            await RefreshAsync();
            var selectedClone = Instances.FirstOrDefault(instance => instance.Id == createdClone.Id);
            if (selectedClone is not null)
            {
                SelectedInstance = selectedClone;
                CurrentPage = "Launch";
            }
            NotifyOperationCompleted();
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
    private async Task DeleteInstanceAsync(InstanceItemViewModel? instance)
    {
        if (instance is null || IsMutationBlocked())
        {
            return;
        }

        var originalIndex = Instances.IndexOf(instance);
        var nextId = Instances
            .Where(candidate => !string.Equals(candidate.Id, instance.Id, StringComparison.Ordinal))
            .ElementAtOrDefault(Math.Min(Math.Max(originalIndex, 0), Math.Max(Instances.Count - 2, 0)))
            ?.Id;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            InstanceDeletionResult? result = null;
            await instanceOperationCoordinator.RunAsync(
                instance.Id,
                async cancellationToken =>
                {
                    Func<CancellationToken, ValueTask<InstanceDeletionConditions>> evaluateConditions =
                        token => EvaluateInstanceDeletionConditionsAsync(instance.Id, token);
                    result = instanceDeletionOverride is null
                        ? await new InstanceDeletionService(VersionRoot).DeleteAsync(
                            instance.Record,
                            evaluateConditions,
                            cancellationToken)
                        : await instanceDeletionOverride(
                            instance.Record,
                            evaluateConditions,
                            cancellationToken);
                },
                lifetimeCancellation.Token);

            Instances.Remove(instance);
            VisibleInstances.Remove(instance);
            if (SelectedInstance?.Id == instance.Id)
            {
                SelectedInstance = nextId is null
                    ? Instances.FirstOrDefault()
                    : Instances.FirstOrDefault(candidate => candidate.Id == nextId)
                        ?? Instances.FirstOrDefault();
            }
            settings = settings with { CurrentInstanceId = SelectedInstance?.Id };
            await QueueSettingsSave();
            CurrentPage = "Launch";
            if (result is { CleanupCompleted: false })
            {
                StatusMessage = Loc["DeleteCleanupPending"];
                ToastRequested?.Invoke(StatusMessage);
            }
            else
            {
                NotifyOperationCompleted();
            }
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or InvalidOperationException
            or UnauthorizedAccessException
            or ArgumentException
            or System.Text.Json.JsonException)
        {
            ErrorMessage = $"{Loc["OperationFailed"]}: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async ValueTask<InstanceDeletionConditions> EvaluateInstanceDeletionConditionsAsync(
        string instanceId,
        CancellationToken cancellationToken)
    {
        var transactionsHealthy = true;
        try
        {
            await EnsureTransactionsHealthyAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            transactionsHealthy = false;
        }
        return new InstanceDeletionConditions
        {
            HasBlockingQueueTasks = downloadQueue.Groups.Any(group =>
                string.Equals(
                    group.TargetInstanceId,
                    instanceId,
                    StringComparison.OrdinalIgnoreCase)
                && group.State is DownloadQueueGroupState.Pending
                    or DownloadQueueGroupState.Running
                    or DownloadQueueGroupState.Failed),
            TransactionsHealthy = transactionsHealthy
        };
    }

    [RelayCommand]
    private Task LaunchGameAsync() => LaunchGameCoreAsync(force: false);

    [RelayCommand]
    private Task ForceLaunchGameAsync() => LaunchGameCoreAsync(force: true);

    private async Task LaunchGameCoreAsync(bool force)
    {
        if (launchOverride is not null && SelectedInstance is null)
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
        await ReloadSelectedInstanceDetailsAsync();
        if (force ? !LaunchPreflight.CanForceLaunch : !LaunchPreflight.CanLaunchNormally)
        {
            ErrorMessage = Loc["LaunchBlocked"];
            return;
        }
        if (launchOverride is not null)
        {
            await launchOverride();
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

        var record = SelectedInstance.Record;
        var isolation = new LocalLowIsolationService(
            GetSharedLocalLowPath(),
            paths.GetVersionDataRoot(VersionRoot));
        IsGameRunning = true;
        Process? process = null;
        try
        {
            await instanceOperationCoordinator.RunAsync(
                record.Id,
                async _ =>
                {
                    if (new SystemHollowKnightProcessProbe().IsRunning())
                    {
                        throw new InvalidOperationException("Hollow Knight is already running.");
                    }
                    await EnsureTransactionsHealthyAsync();
                    if (record.SpeedrunTemplateId is not null)
                    {
                        await VerifySpeedrunLaunchAsync(record);
                    }
                    runtimeSession = await InstanceRuntimeSession.StartAsync(isolation, record.Id);
                    process = Process.Start(new ProcessStartInfo(executable)
                    {
                        WorkingDirectory = record.RootPath,
                        UseShellExecute = true
                    }) ?? throw new InvalidOperationException("The game process did not start.");
                },
                lifetimeCancellation.Token);
            using (var startedProcess = process
                   ?? throw new InvalidOperationException("The game process did not start."))
            {
                await startedProcess.WaitForExitAsync();
            }
            var probe = new SystemHollowKnightProcessProbe();
            while (probe.IsRunning())
            {
                await Task.Delay(500);
            }
            var completedSession = runtimeSession
                ?? throw new InvalidOperationException("The instance runtime session was not created.");
            await completedSession.CompleteAsync();
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

    private async Task ReloadSelectedInstanceDetailsAsync()
    {
        var selected = SelectedInstance;
        if (selected is null || !Directory.Exists(VersionRoot))
        {
            return;
        }
        long generation = Interlocked.Increment(ref detailsLoadGeneration);
        var previousCancellation = Interlocked.Exchange(ref detailsLoadCancellation, null);
        previousCancellation?.Cancel();
        previousCancellation?.Dispose();
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetimeCancellation.Token);
        detailsLoadCancellation = cancellation;
        IsLoadingInstanceDetails = true;
        detailsLoadTask = LoadInstanceDetailsAsync(selected.Record, generation, cancellation.Token);
        await detailsLoadTask;
    }

    [RelayCommand]
    private async Task SignInWithQrAsync()
    {
        if (lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }
        if (IsOfflineMode)
        {
            IsSteamLoggedIn = false;
            SteamStatus = Loc["OfflineMode"];
            ErrorMessage = Loc["OfflineModeHint"];
            return;
        }

        var signInCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            lifetimeCancellation.Token);
        if (Interlocked.CompareExchange(ref steamSignInCancellation, signInCancellation, null) is not null)
        {
            signInCancellation.Dispose();
            return;
        }

        ErrorMessage = null;
        SteamStatus = "Connecting to Steam...";
        IsSteamLoggedIn = false;
        var gateTaken = false;
        try
        {
            await steamConnectionGate.WaitAsync(signInCancellation.Token);
            gateTaken = true;
            await DisposeCurrentSteamSessionAsync();
            RefreshTokenCredential credential;
            if (qrSignInOverride is not null)
            {
                credential = await qrSignInOverride(signInCancellation.Token);
            }
            else
            {
                steamSession = new SteamAuthenticationSession(tokenStore);
                steamSession.QrChallengeChanged += OnQrChallengeChanged;
                credential = await steamSession.ConnectWithQrAsync(signInCancellation.Token);
            }
            IsSteamLoggedIn = true;
            SteamStatus = credential.AccountName;
            QrCodeImage = null;
        }
        catch (Exception exception)
        {
            ErrorMessage = IsOfflineMode ? null : $"Steam: {exception.Message}";
            IsSteamLoggedIn = false;
            SteamStatus = IsOfflineMode ? Loc["OfflineMode"] : "Not signed in";
            QrCodeImage = null;
            if (gateTaken)
            {
                try
                {
                    await DisposeCurrentSteamSessionAsync();
                }
                catch (Exception cleanupException)
                {
                    ErrorMessage += $" Cleanup: {cleanupException.Message}";
                }
            }
        }
        finally
        {
            if (gateTaken)
            {
                steamConnectionGate.Release();
            }
            if (ReferenceEquals(
                Interlocked.CompareExchange(ref steamSignInCancellation, null, signInCancellation),
                signInCancellation))
            {
                signInCancellation.Dispose();
            }
        }
    }

    private async Task TryReconnectSteamAsync()
    {
        if (IsOfflineMode
            || !File.Exists(Path.Combine(paths.ApplicationDataRoot, "steam-token.dat")))
        {
            return;
        }
        var gateTaken = false;
        try
        {
            await steamConnectionGate.WaitAsync(lifetimeCancellation.Token);
            gateTaken = true;
            steamSession = new SteamAuthenticationSession(tokenStore);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetimeCancellation.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            var credential = await steamSession.ConnectWithStoredTokenAsync(timeout.Token);
            IsSteamLoggedIn = true;
            SteamStatus = credential.AccountName;
        }
        catch (Exception)
        {
            if (gateTaken)
            {
                try
                {
                    await DisposeCurrentSteamSessionAsync();
                }
                catch (Exception)
                {
                }
            }
            SteamStatus = "Not signed in";
        }
        finally
        {
            if (gateTaken)
            {
                steamConnectionGate.Release();
            }
        }
    }

    [RelayCommand]
    private void SignOutSteam()
    {
        try
        {
            steamSession?.SignOut();
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Steam: {exception.Message}";
        }
        finally
        {
            IsSteamLoggedIn = false;
            SteamStatus = "Not signed in";
            QrCodeImage = null;
        }
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
            catch (Exception exception)
            {
                ErrorMessage = $"Steam: {exception.Message}";
                DownloadStatus = "Failed";
            }
            finally
            {
                downloadCancellation.Dispose();
                downloadCancellation = null;
            }
            return;
        }

        try
        {
            ErrorMessage = null;
            await EnqueueSteamBuildAsync();
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Steam: {exception.Message}";
            DownloadStatus = Loc["QueueStateFailed"];
        }
    }

    internal static string FormatDownloadStatus(SteamDownloadProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} · {1} / {2} · {3:0}%\n{4}",
            FormatByteAmount(progress.BytesPerSecond, perSecond: true),
            FormatByteAmount(progress.CompletedBytes, perSecond: false),
            FormatByteAmount(progress.TotalBytes, perSecond: false),
            progress.Fraction * 100,
            progress.CurrentFile);
    }

    private static string FormatByteAmount(double bytes, bool perSecond)
    {
        const double Kilobyte = 1024;
        const double Megabyte = Kilobyte * 1024;
        const double Gigabyte = Megabyte * 1024;
        double value;
        string unit;
        if (bytes >= Gigabyte)
        {
            value = bytes / Gigabyte;
            unit = "GB";
        }
        else if (bytes >= Megabyte)
        {
            value = bytes / Megabyte;
            unit = "MB";
        }
        else if (bytes >= Kilobyte)
        {
            value = bytes / Kilobyte;
            unit = "KB";
        }
        else
        {
            value = bytes;
            unit = "B";
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:0.#} {1}{2}",
            value,
            unit,
            perSecond ? "/s" : string.Empty);
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
            if (state == LoaderState.BepInEx && receipt is null)
            {
                throw new InvalidOperationException(Loc["ExternalLoaderBlocked"]);
            }
            if (state == LoaderState.Drifted)
            {
                if (receipt is null)
                {
                    throw new InvalidOperationException(Loc["ExternalLoaderBlocked"]);
                }
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
                if ((await CreateModManager(record).GetInstalledAsync()).Count != 0)
                {
                    throw new InvalidOperationException(Loc["LoaderSwitchBlockedByMods"]);
                }
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
        await RunInstanceMutationAsync(record => enabled
            ? CreateModManager(record).SetEnabledAsync(modId, enabled: true)
            : CreateModManager(record).DisableIgnoringDependentsAsync(modId));
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
        await RunInstanceMutationAsync(record => CreateModInstallService(record).UpdateAsync(manifest.Id));
    }

    [RelayCommand]
    private async Task UninstallSelectedModAsync()
    {
        if (SelectedInstance is null || SelectedInstalledMod is null)
        {
            ErrorMessage = Loc["SelectMod"];
            return;
        }
        if (!SelectedInstalledMod.CanUninstall)
        {
            ErrorMessage = SelectedInstalledMod.IsPinned
                ? Loc["UnpinBeforeUninstall"]
                : Loc["ExternalModReadOnly"];
            return;
        }
        var modId = SelectedInstalledMod.Id;
        IReadOnlyList<InstalledModReceipt> unused = [];
        await RunInstanceMutationAsync(async record =>
        {
            var manager = CreateModManager(record);
            var before = await manager.GetInstalledAsync();
            var removed = before.Single(mod => string.Equals(mod.Id, modId, StringComparison.OrdinalIgnoreCase));
            await manager.UninstallIgnoringDependentsAsync(modId);
            var remaining = await manager.GetInstalledAsync();
            unused = InstalledModDependencyGraph.FindUnusedDependencies([removed], remaining);
        });
        SetUnusedDependencySuggestions(unused);
    }

    [RelayCommand]
    private async Task TakeOverSelectedModAsync()
    {
        if (SelectedInstalledMod is not { CanTakeOver: true } selected)
        {
            return;
        }
        await RunInstanceMutationAsync(record =>
            CreateModManager(record).TakeOverAsync(selected.Id, selected.Discovery));
    }

    [RelayCommand]
    private async Task ToggleSelectedModPinnedAsync()
    {
        if (SelectedInstalledMod is not { CanPin: true, Receipt: { } receipt })
        {
            return;
        }
        await RunInstanceMutationAsync(record =>
            CreateModManager(record).SetPinnedAsync(receipt.Id, !receipt.Pinned));
    }

    [RelayCommand]
    private async Task RepairSelectedModAsync()
    {
        if (SelectedInstalledMod is not { CanRepair: true, CatalogManifest: { } manifest } selected)
        {
            return;
        }
        await RunInstanceMutationAsync(async record =>
        {
            var manager = CreateModManager(record);
            var plan = await manager.GetRepairPlanAsync(selected.Id, catalog.Mods);
            switch (plan.Action)
            {
                case ModRepairAction.Repair when plan.Manifest is not null:
                    await manager.RepairFromUriAsync(plan.Manifest);
                    break;
                case ModRepairAction.Update when plan.Manifest is not null:
                    await CreateModInstallService(record).UpdateAsync(plan.Manifest.Id);
                    break;
                default:
                    throw new InvalidOperationException(plan.Reason);
            }
        });
    }

    [RelayCommand]
    private async Task RepairSelectedMarketModAsync()
    {
        if (SelectedMarketInstalledMod is not { CanReinstall: true, CatalogManifest: { } manifest })
        {
            return;
        }

        await RunInstanceMutationAsync(record =>
            CreateModManager(record).RepairFromUriAsync(manifest));
    }

    [RelayCommand]
    private void OpenSelectedModLogs()
    {
        if (SelectedInstance is null || SelectedMarketInstalledMod is null)
        {
            return;
        }
        CurrentPage = "Manage";
        CurrentManageTab = "Logs";
    }

    [RelayCommand]
    private async Task DeleteSelectedModGlobalSettingsAsync()
    {
        if (SelectedInstance is null
            || SelectedMarketMod is not { } manifest
            || !CanAccessSelectedModGlobalSettings)
        {
            return;
        }

        var instance = SelectedInstance.Record;
        try
        {
            await instanceOperationCoordinator.RunAsync(instance.Id, async cancellationToken =>
            {
                var deleted = await CreateGlobalModSettingsService().DeleteAsync(
                    instance.Id,
                    [manifest],
                    cancellationToken);
                if (deleted == 0)
                {
                    throw new FileNotFoundException("The selected Mod does not have global settings.");
                }
            }, lifetimeCancellation.Token);
            UpdateSelectedMarketInstallationState();
            NotifyOperationCompleted();
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or UnauthorizedAccessException
            or InvalidOperationException)
        {
            ErrorMessage = $"{Loc["OperationFailed"]}: {exception.Message}";
        }
    }

    internal string? ResolveSelectedModGlobalSettingsPath()
    {
        if (SelectedInstance is null
            || SelectedMarketMod is not { } manifest
            || !CanAccessSelectedModGlobalSettings)
        {
            return null;
        }
        return CreateGlobalModSettingsService().ResolveFile(SelectedInstance.Id, manifest).FilePath;
    }

    [RelayCommand]
    private async Task AcceptSelectedLocalModFilesAsync()
    {
        if (SelectedInstalledMod is not { CanAcceptCurrent: true } selected)
        {
            return;
        }
        await RunInstanceMutationAsync(record =>
            CreateModManager(record).AcceptCurrentLocalFilesAsync(selected.Id));
    }

    [RelayCommand]
    private async Task ReimportSelectedLocalModAsync()
    {
        if (SelectedInstalledMod is not { CanReimport: true } selected || !File.Exists(LocalModPath))
        {
            ErrorMessage = Loc["LocalModPathRequired"];
            return;
        }
        await RunInstanceMutationAsync(record =>
        {
            var manager = CreateModManager(record);
            return string.Equals(Path.GetExtension(LocalModPath), ".dll", StringComparison.OrdinalIgnoreCase)
                ? manager.ReimportLocalDllAsync(selected.Id, LocalModPath)
                : manager.ReimportLocalZipAsync(selected.Id, LocalModPath);
        });
    }

    [RelayCommand]
    private async Task AcknowledgeLaunchWarningsAsync()
    {
        if (SelectedInstance is null)
        {
            return;
        }
        var acknowledgements = settings.ModHealthAcknowledgements.ToList();
        var knownFingerprints = acknowledgements
            .Select(item => item.Fingerprint)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var issue in LaunchPreflight.Issues.Where(issue =>
                     issue.Severity == LaunchIssueSeverity.Warning
                     && !issue.IsAcknowledged
                     && !string.IsNullOrWhiteSpace(issue.SubjectModId)))
        {
            var acknowledgement = ModHealthAcknowledgement.Create(SelectedInstance.Id, issue);
            if (knownFingerprints.Add(acknowledgement.Fingerprint))
            {
                acknowledgements.Add(acknowledgement);
            }
        }
        settings = settings with { ModHealthAcknowledgements = acknowledgements };
        LaunchPreflight = LaunchPreflight with
        {
            Issues = LaunchPreflight.Issues.Select(issue =>
                issue.Severity == LaunchIssueSeverity.Warning
                    ? issue with { IsAcknowledged = true }
                    : issue).ToArray()
        };
        await QueueSettingsSave();
    }

    [RelayCommand]
    private async Task EnableSelectedModsAsync()
    {
        var selected = GetSelectedModReceipts();
        if (selected.Count == 0)
        {
            return;
        }
        var installed = GetManagedModReceipts();
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
        var installed = GetManagedModReceipts();
        var selectedIds = selected.Select(mod => mod.Id).ToArray();
        var order = InstalledModDependencyGraph.OrderDependentsFirst(installed, selectedIds);
        await RunInstanceMutationAsync(async record =>
        {
            var manager = CreateModManager(record);
            foreach (var mod in order.Where(mod => mod.Enabled))
            {
                await manager.DisableIgnoringDependentsAsync(mod.Id);
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
            foreach (var mod in updates)
            {
                await CreateModInstallService(record).UpdateAsync(mod.CatalogManifest!.Id);
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
        var selectedIds = selected.Select(mod => mod.Id).ToArray();
        ModBatchUninstallResult? result = null;
        await RunInstanceMutationAsync(async record =>
        {
            result = await CreateModManager(record).UninstallBatchAsync(selectedIds);
        });
        SetUnusedDependencySuggestions(result?.UnusedDependencies ?? []);
    }

    private void SetUnusedDependencySuggestions(IReadOnlyList<InstalledModReceipt> suggestions)
    {
        UnusedDependencySuggestions.Clear();
        foreach (var suggestion in suggestions)
        {
            UnusedDependencySuggestions.Add(suggestion.Name);
        }
        OnPropertyChanged(nameof(HasUnusedDependencySuggestions));
        OnPropertyChanged(nameof(UnusedDependencySummary));
    }


    public ModRemovalImpactPlan CreateModRemovalPlan(bool bulk)
    {
        var selectedIds = bulk
            ? InstalledMods.Where(mod => mod.IsSelected).Select(mod => mod.Id).ToArray()
            : SelectedInstalledMod is null ? [] : [SelectedInstalledMod.Id];
        if (selectedIds.Length == 0)
        {
            throw new InvalidOperationException(Loc["SelectMod"]);
        }
        return InstalledModDependencyGraph.CreateRemovalPlan(
            GetManagedModReceipts(),
            selectedIds);
    }

    public ModDependencyRepairPlan CreateModDependencyRepairPlan()
    {
        if (SelectedInstance is null)
        {
            throw new InvalidOperationException(Loc["NoInstance"]);
        }
        var installed = GetManagedModReceipts();
        var loaderIds = installed
            .Select(mod => mod.LoaderId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (loaderIds.Length != 1)
        {
            throw new InvalidOperationException(Loc["WrongLoader"]);
        }
        return ModDependencyRepairPlanner.CreatePlan(
            installed,
            catalog.Mods,
            SelectedInstance.Record.BuildId,
            loaderIds[0]);
    }

    [RelayCommand]
    private async Task RepairModDependenciesAsync()
    {
        var plan = CreateModDependencyRepairPlan();
        if (plan.Items.All(item => item.Action == ModDependencyRepairAction.Unresolved))
        {
            ErrorMessage = Loc["CannotRepair"];
            return;
        }
        await EnqueueModDependencyRepairAsync(plan);
    }
    public IReadOnlyList<string> GetSelectedModExternalDependentNames(bool bulk)
    {
        var selectedIds = bulk
            ? InstalledMods.Where(mod => mod.IsSelected).Select(mod => mod.Id).ToArray()
            : SelectedInstalledMod is null ? [] : [SelectedInstalledMod.Id];
        return InstalledModDependencyGraph.FindExternalDependents(
                GetManagedModReceipts(),
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
        .Where(mod => mod.IsSelected && mod.Receipt is not null)
        .Select(mod => mod.Receipt)
        .OfType<InstalledModReceipt>()
        .ToArray();

    private IReadOnlyList<InstalledModReceipt> GetManagedModReceipts() => InstalledMods
        .Select(mod => mod.Receipt)
        .OfType<InstalledModReceipt>()
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
    private async Task EditSaveAsync(string? snapshotId)
    {
        var selected = SelectedInstance;
        if (selected is null || IsGameRunning)
        {
            return;
        }

        string? targetSnapshotId;
        string sourceLabel;
        if (string.IsNullOrWhiteSpace(snapshotId)
            || string.Equals(snapshotId, "current", StringComparison.OrdinalIgnoreCase))
        {
            targetSnapshotId = null;
            sourceLabel = selected.Name;
        }
        else
        {
            targetSnapshotId = snapshotId;
            sourceLabel = Snapshots.FirstOrDefault(snapshot => snapshot.Id == snapshotId)?.Name
                ?? snapshotId;
        }

        var editor = new SaveEditorViewModel(
            CreateSnapshotService(),
            selected.Id,
            targetSnapshotId,
            sourceLabel);
        SaveEditor = editor;
        try
        {
            await editor.InitializeAsync(lifetimeCancellation.Token);
            if (SelectedInstance?.Id != selected.Id || IsGameRunning)
            {
                if (ReferenceEquals(SaveEditor, editor))
                {
                    SaveEditor = null;
                }
            }
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
            if (ReferenceEquals(SaveEditor, editor))
            {
                SaveEditor = null;
            }
        }
        catch (Exception exception) when (
            exception is IOException
                or InvalidDataException
                or UnauthorizedAccessException
                or CryptographicException)
        {
            if (ReferenceEquals(SaveEditor, editor))
            {
                SaveEditor = null;
                ErrorMessage = $"{Loc["OperationFailed"]}: {exception.Message}";
            }
        }
    }

    [RelayCommand]
    private void ExitSaveEditor() => SaveEditor = null;

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
        InstanceRecord? createdInstance = null;
        try
        {
            await instanceOperationCoordinator.RunAsync(
                "transactions",
                async _ =>
                {
                    if (new SystemHollowKnightProcessProbe().IsRunning())
                    {
                        throw new InvalidOperationException(Loc["CloseGameFirst"]);
                    }
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
                            HttpClient = packageHttpClient
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
                    createdInstance = clone;
                    createdRoot = null;
                },
                lifetimeCancellation.Token);
            var clone = createdInstance
                ?? throw new InvalidOperationException("The speedrun instance was not created.");
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
            RebuildMarketCatalog();
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

    [RelayCommand]
    private async Task SaveCustomModLinksAsync()
    {
        try
        {
            CustomModLinksDefinition? definition = null;
            if (!string.IsNullOrWhiteSpace(CustomModLinksUrl))
            {
                if (!Uri.TryCreate(CustomModLinksUrl.Trim(), UriKind.Absolute, out var uri)
                    || uri.Scheme != Uri.UriSchemeHttps
                    || SelectedCustomModLinksBuild is null
                    || SelectedCustomModLinksLoader is null)
                {
                    throw new FormatException(Loc["CustomModLinksInvalid"]);
                }
                definition = new CustomModLinksDefinition
                {
                    Url = uri.AbsoluteUri,
                    BuildId = SelectedCustomModLinksBuild.Value,
                    LoaderId = SelectedCustomModLinksLoader.Value
                };
            }

            settings = settings with { CustomModLinks = definition };
            CustomModLinksUrl = definition?.Url ?? string.Empty;
            await QueueSettingsSave();
            catalog = await LoadCatalogAsync(lifetimeCancellation.Token);
            RebuildCustomModLinksOptions();
            RebuildMarketCatalog();
            if (Directory.Exists(VersionRoot))
            {
                await RefreshAsync();
            }
            NotifyOperationCompleted();
        }
        catch (Exception exception) when (exception is FormatException
            or ArgumentException
            or IOException
            or InvalidDataException)
        {
            ErrorMessage = $"{Loc["OperationFailed"]}: {exception.Message}";
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyInstanceFilter();

    partial void OnModSearchTextChanged(string value) => ApplyModFilters();

    partial void OnMarketSearchTextChanged(string value) => ApplyMarketFilters();

    partial void OnSelectedMarketBuildOptionChanged(SettingOption<string>? value) => ApplyMarketFilters();

    partial void OnSelectedMarketLoaderOptionChanged(SettingOption<string>? value) => ApplyMarketFilters();

    partial void OnSelectedMarketSourceOptionChanged(SettingOption<string>? value) => ApplyMarketFilters();

    partial void OnSelectedMarketTagOptionChanged(SettingOption<string>? value) => ApplyMarketFilters();

    partial void OnSelectedMarketActivityOptionChanged(SettingOption<MarketActivityFilter>? value) =>
        ApplyMarketFilters();

    partial void OnSelectedModStatusChanged(ModStatusFilter value) => ApplyModFilters();

    partial void OnIsOfflineModeChanged(bool value)
    {
        networkPolicy.SetOffline(value);
        if (value)
        {
            Volatile.Read(ref steamSignInCancellation)?.Cancel();
            steamOfflineTransitionTask = DisconnectSteamForOfflineAsync();
        }
        if (value == settings.OfflineMode)
        {
            return;
        }
        settings = settings with { OfflineMode = value };
        _ = QueueSettingsSave();
    }

    private async Task DisconnectSteamForOfflineAsync()
    {
        var gateTaken = false;
        try
        {
            await steamConnectionGate.WaitAsync(lifetimeCancellation.Token);
            gateTaken = true;
            await DisposeCurrentSteamSessionAsync();
            IsSteamLoggedIn = false;
            SteamStatus = Loc["OfflineMode"];
            QrCodeImage = null;
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Steam: {exception.Message}";
        }
        finally
        {
            if (gateTaken)
            {
                steamConnectionGate.Release();
            }
        }
    }

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
        var previousLoadCancellation = Interlocked.Exchange(ref detailsLoadCancellation, null);
        previousLoadCancellation?.Cancel();
        previousLoadCancellation?.Dispose();
        GameConfig = null;
        SaveEditor = null;
        IsLoadingInstanceDetails = value is not null;
        AvailableLoaders.Clear();
        SelectedLoader = null;
        AvailableMods.Clear();
        VisibleAvailableMods.Clear();
        InstalledMods.Clear();
        VisibleInstalledMods.Clear();
        Snapshots.Clear();
        ModPresets.Clear();
        SelectedPreset = null;
        HasPresetRestorePoint = false;
        UpdateSelectedMarketInstallationState();
        currentLoaderInspection = new LoaderInspection
        {
            State = LoaderState.Vanilla,
            Ownership = LoaderOwnership.None
        };
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
            if (AvailableLoaders.Count == 1)
            {
                SelectedLoader = AvailableLoaders[0];
            }
            foreach (var mod in catalog.Mods.Where(mod => mod.SupportedBuildIds.Contains(value.Record.BuildId)))
            {
                AvailableMods.Add(mod);
            }
            ApplyModFilters();
            settings = settings with { CurrentInstanceId = value.Id };
            _ = QueueSettingsSave();
            if (!Directory.Exists(VersionRoot))
            {
                IsLoadingInstanceDetails = false;
                return;
            }
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetimeCancellation.Token);
            detailsLoadCancellation = cancellation;
            detailsLoadTask = LoadInstanceDetailsAsync(value.Record, generation, cancellation.Token);
            if (CurrentManageTab == "Config")
            {
                _ = LoadGameConfigAsync();
            }
        }
    }

    partial void OnCurrentManageTabChanged(string value)
    {
        if (value == "Config")
        {
            _ = LoadGameConfigAsync();
        }
        else
        {
            GameConfig = null;
        }
    }

    partial void OnIsGameRunningChanged(bool value)
    {
        if (value)
        {
            GameConfig = null;
            SaveEditor = null;
        }
        else if (CurrentManageTab == "Config")
        {
            _ = LoadGameConfigAsync();
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
        RebuildCustomModLinksOptions();
        RebuildModStatusOptions();
        RebuildMarketCatalog();
        RebuildInstalledModCatalogProjection();
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
    partial void OnSelectedGitHubRouteChanged(SettingOption<GitHubDownloadRoute>? value)
    {
        if (value is null || value.Value == settings.GitHubDownloadRoute)
        {
            return;
        }
        settings = settings with { GitHubDownloadRoute = value.Value };
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

    private void ApplyMarketFilters()
    {
        VisibleMarketMods.Clear();
        VisibleMarketDisplayMods.Clear();
        foreach (var item in MarketDisplayMods.Where(item =>
            item.MatchesSearch(MarketSearchText)
            && (string.IsNullOrEmpty(SelectedMarketBuildOption?.Value)
                || item.SupportedBuildIds.Contains(SelectedMarketBuildOption.Value, StringComparer.OrdinalIgnoreCase))
            && (string.IsNullOrEmpty(SelectedMarketLoaderOption?.Value)
                || string.Equals(item.LoaderId, SelectedMarketLoaderOption.Value, StringComparison.OrdinalIgnoreCase))
            && (string.IsNullOrEmpty(SelectedMarketSourceOption?.Value)
                || string.Equals(item.SourceName, SelectedMarketSourceOption.Value, StringComparison.OrdinalIgnoreCase))
            && (string.IsNullOrEmpty(SelectedMarketTagOption?.Value)
                || item.CanonicalTags.Contains(SelectedMarketTagOption.Value, StringComparer.OrdinalIgnoreCase))
            && SelectedMarketActivityOption?.Value switch
            {
                MarketActivityFilter.RecentlyAdded => item.IsRecentlyAdded,
                MarketActivityFilter.RecentlyUpdated => item.IsRecentlyUpdated,
                _ => true
            }))
        {
            VisibleMarketDisplayMods.Add(item);
            VisibleMarketMods.Add(item.Manifest);
        }
    }

    private void RebuildMarketCatalog()
    {
        var selectedModId = SelectedMarketMod?.Id;
        var selectedBuild = SelectedMarketBuildOption?.Value;
        var selectedLoader = SelectedMarketLoaderOption?.Value;
        var selectedSource = SelectedMarketSourceOption?.Value;
        var selectedTag = SelectedMarketTagOption?.Value;
        var selectedActivity = SelectedMarketActivityOption?.Value ?? MarketActivityFilter.All;
        var chinese = Loc.Culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        MarketMods.Clear();
        MarketDisplayMods.Clear();
        foreach (var mod in catalog.Mods)
        {
            MarketMods.Add(mod);
            MarketDisplayMods.Add(ProjectMarketMod(mod, chinese));
        }
        var ordered = MarketDisplayMods
            .OrderBy(item => item.PrimaryName, StringComparer.Create(Loc.Culture, ignoreCase: true))
            .ToArray();
        MarketDisplayMods.Clear();
        foreach (var item in ordered)
        {
            MarketDisplayMods.Add(item);
        }
        RebuildMarketOptions(
            MarketBuildOptions,
            MarketDisplayMods.SelectMany(mod => mod.SupportedBuildIds),
            value => catalog.Builds.FirstOrDefault(build =>
                string.Equals(build.Id, value, StringComparison.OrdinalIgnoreCase))?.DisplayVersion ?? value,
            selectedBuild);
        RebuildMarketOptions(
            MarketLoaderOptions,
            MarketDisplayMods.Select(mod => mod.LoaderId),
            value => catalog.Loaders.FirstOrDefault(loader =>
                string.Equals(loader.Id, value, StringComparison.OrdinalIgnoreCase)) is { } loader
                    ? $"{loader.Name} {loader.Version}"
                    : value,
            selectedLoader);
        RebuildMarketOptions(
            MarketSourceOptions,
            MarketDisplayMods.Select(mod => mod.SourceName).OfType<string>(),
            selectedValue: selectedSource);
        RebuildMarketOptions(
            MarketTagOptions,
            MarketDisplayMods.SelectMany(mod => mod.CanonicalTags),
            DisplayMarketTag,
            selectedTag);
        RebuildMarketActivityOptions(selectedActivity);
        ApplyMarketFilters();
        selectedMarketModDisplay = selectedModId is null
            ? null
            : MarketDisplayMods.FirstOrDefault(item =>
                string.Equals(item.Id, selectedModId, StringComparison.OrdinalIgnoreCase));
        if (selectedModId is not null)
        {
            SelectedMarketMod = MarketMods.FirstOrDefault(mod =>
                string.Equals(mod.Id, selectedModId, StringComparison.OrdinalIgnoreCase));
        }
        OnPropertyChanged(nameof(SelectedMarketModDisplay));
    }

    private void RebuildInstalledModCatalogProjection()
    {
        if (InstalledMods.Count == 0)
        {
            UpdateSelectedMarketInstallationState();
            return;
        }

        var selectedIds = InstalledMods
            .Where(mod => mod.IsSelected)
            .Select(mod => mod.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var focusedId = SelectedInstalledMod?.Id;
        var items = InstalledMods.ToArray();
        InstalledMods.Clear();
        foreach (var item in items)
        {
            var receipt = item.Receipt;
            var manifest = catalog.Mods.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, item.Id, StringComparison.OrdinalIgnoreCase));
            InstalledMods.Add(new InstalledModItemViewModel(
                item.Discovery,
                receipt,
                item.HealthReport,
                manifest,
                OnModSelectionChanged,
                manifest is null ? null : ProjectMarketMod(manifest),
                ModOwnershipDisplay(item.Ownership),
                ModHealthDisplay(item.HealthStatus))
            {
                IsSelected = selectedIds.Contains(item.Id)
            });
        }
        UpdateSelectedMarketInstallationState();
        SelectedInstalledMod = focusedId is null
            ? null
            : InstalledMods.FirstOrDefault(mod =>
                string.Equals(mod.Id, focusedId, StringComparison.OrdinalIgnoreCase));
        ApplyModFilters();
        OnModSelectionChanged();
    }

    private void RebuildMarketOptions(
        ObservableCollection<SettingOption<string>> options,
        IEnumerable<string> values,
        Func<string, string>? displayName = null,
        string? selectedValue = null)
    {
        options.Clear();
        options.Add(new(string.Empty, Loc["FilterAll"]));
        foreach (var value in values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            options.Add(new(value, displayName?.Invoke(value) ?? value));
        }
        var selected = options.FirstOrDefault(option =>
            string.Equals(option.Value, selectedValue, StringComparison.OrdinalIgnoreCase)) ?? options[0];
        if (ReferenceEquals(options, MarketBuildOptions)) SelectedMarketBuildOption = selected;
        else if (ReferenceEquals(options, MarketLoaderOptions)) SelectedMarketLoaderOption = selected;
        else if (ReferenceEquals(options, MarketSourceOptions)) SelectedMarketSourceOption = selected;
        else SelectedMarketTagOption = selected;
    }

    private void RebuildMarketActivityOptions(MarketActivityFilter selected)
    {
        MarketActivityOptions.Clear();
        MarketActivityOptions.Add(new(MarketActivityFilter.All, Loc["ActivityAll"]));
        MarketActivityOptions.Add(new(MarketActivityFilter.RecentlyAdded, Loc["RecentlyAdded"]));
        MarketActivityOptions.Add(new(MarketActivityFilter.RecentlyUpdated, Loc["RecentlyUpdated"]));
        SelectedMarketActivityOption = MarketActivityOptions.First(option => option.Value == selected);
    }

    internal MarketModItemViewModel ProjectMarketMod(ModManifest manifest, bool? chinese = null) =>
        new(
            manifest,
            modTranslations.Mods.FirstOrDefault(translation =>
                string.Equals(translation.Id, manifest.Id, StringComparison.OrdinalIgnoreCase)),
            modTranslations.TagNames,
            chinese ?? Loc.Culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase),
            modActivityCatalog.Entries.FirstOrDefault(entry =>
                string.Equals(entry.Id, manifest.Id, StringComparison.OrdinalIgnoreCase)),
            modActivityCatalog.GeneratedAt.AddDays(-30));

    internal MarketModItemViewModel? ProjectMarketMod(string id) =>
        catalog.Mods.FirstOrDefault(manifest =>
            string.Equals(manifest.Id, id, StringComparison.OrdinalIgnoreCase)) is { } manifest
            ? ProjectMarketMod(manifest)
            : null;

    private string DisplayMarketTag(string value) =>
        Loc.Culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            && modTranslations.TagNames.TryGetValue(value, out var translated)
                ? translated
                : value;

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
        GitHubRouteOptions.Clear();
        GitHubRouteOptions.Add(new(GitHubDownloadRoute.Direct, Loc["GitHubDirect"]));
        GitHubRouteOptions.Add(new(GitHubDownloadRoute.Mirror, Loc["GitHubMirror"]));
        SelectedGitHubRoute = GitHubRouteOptions.First(option => option.Value == settings.GitHubDownloadRoute);
        RebuildPresetModeOptions();
    }

    private void RebuildCustomModLinksOptions()
    {
        var selectedBuild = settings.CustomModLinks?.BuildId;
        var selectedLoader = settings.CustomModLinks?.LoaderId;
        CustomModLinksBuildOptions.Clear();
        foreach (var build in catalog.Builds.OrderBy(build => build.DisplayVersion, StringComparer.OrdinalIgnoreCase))
        {
            CustomModLinksBuildOptions.Add(new(build.Id, build.DisplayVersion));
        }
        CustomModLinksLoaderOptions.Clear();
        foreach (var loader in catalog.Loaders
                     .Where(loader => loader.Id.StartsWith("modding-api-", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(loader => loader.Name, StringComparer.OrdinalIgnoreCase))
        {
            CustomModLinksLoaderOptions.Add(new(loader.Id, $"{loader.Name} {loader.Version}"));
        }
        SelectedCustomModLinksBuild = CustomModLinksBuildOptions.FirstOrDefault(option =>
            string.Equals(option.Value, selectedBuild, StringComparison.OrdinalIgnoreCase));
        SelectedCustomModLinksLoader = CustomModLinksLoaderOptions.FirstOrDefault(option =>
            string.Equals(option.Value, selectedLoader, StringComparison.OrdinalIgnoreCase));
    }

    private void ApplyLanguage(UiLanguage language)
    {
        var localization = new LocalizationViewModel();
        localization.Apply(language);
        Loc = localization;
        OnPropertyChanged(nameof(Loc));
        OnPropertyChanged(nameof(SelectedModContentStatusText));
        OnPropertyChanged(nameof(IsProtocolRegistered));
        OnPropertyChanged(nameof(ProtocolRegistrationStatus));
        RefreshApplicationUpdateText();
        NotifyOfficialCatalogLabels();
        if (DownloadQueueGroups.Count > 0)
        {
            QueueDownloadQueueProjection(downloadQueue.Groups);
        }

        if (Application.Current is { } application)
        {
            SemiTheme.OverrideLocaleResources(application, localization.Culture);
            UrsaSemiTheme.OverrideLocaleResources(application, localization.Culture);
        }
    }

    partial void OnSelectedMarketModChanged(ModManifest? value)
    {
        selectedMarketModDisplay = value is null
            ? null
            : ProjectMarketMod(value);
        OnPropertyChanged(nameof(SelectedMarketModDisplay));
        UpdateSelectedMarketInstallationState();
        BeginSelectedModContentLoad(value);
    }

    private void UpdateSelectedMarketInstallationState()
    {
        SelectedMarketInstalledMod = SelectedMarketMod is null
            ? null
            : InstalledMods.FirstOrDefault(item =>
                string.Equals(item.Id, SelectedMarketMod.Id, StringComparison.OrdinalIgnoreCase));
        SelectedModGlobalSettingsFile = null;
        if (SelectedInstance is null
            || SelectedMarketMod is not { } manifest
            || SelectedMarketInstalledMod is null
            || !CanAccessSelectedModGlobalSettings)
        {
            return;
        }

        try
        {
            SelectedModGlobalSettingsFile = CreateGlobalModSettingsService()
                .ListFiles(SelectedInstance.Id, [manifest])
                .FirstOrDefault();
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or UnauthorizedAccessException
            or ArgumentException)
        {
            SelectedModContentError = exception.Message;
        }
    }

    private void BeginSelectedModContentLoad(ModManifest? manifest)
    {
        var generation = Interlocked.Increment(ref selectedModContentLoadGeneration);
        var previous = Interlocked.Exchange(ref selectedModContentLoadCancellation, null);
        previous?.Cancel();
        previous?.Dispose();

        SelectedModReadmeMarkdown = string.Empty;
        SelectedModReleaseNotesMarkdown = string.Empty;
        SelectedModContentError = string.Empty;
        SelectedModContentStatus = ModContentLoadStatus.Unavailable;
        IsLoadingSelectedModContent = manifest is not null;
        if (manifest is null)
        {
            selectedModContentLoadTask = Task.CompletedTask;
            return;
        }

        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetimeCancellation.Token);
        selectedModContentLoadCancellation = cancellation;
        selectedModContentLoadTask = LoadSelectedModContentAsync(
            manifest,
            generation,
            cancellation.Token);
    }

    private async Task LoadSelectedModContentAsync(
        ModManifest manifest,
        long generation,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = modContentLoadOverride is null
                ? await new ModContentSource(
                    metadataHttpClient,
                    Path.Combine(paths.ApplicationDataRoot, "content", "mods"))
                    .LoadAsync(manifest, cancellationToken)
                : await modContentLoadOverride(manifest, cancellationToken);
            if (generation != Volatile.Read(ref selectedModContentLoadGeneration)
                || !ReferenceEquals(SelectedMarketMod, manifest))
            {
                return;
            }

            SelectedModContentStatus = result.Status;
            SelectedModReadmeMarkdown = result.Document?.ReadmeMarkdown ?? string.Empty;
            SelectedModReleaseNotesMarkdown = result.Document?.ReleaseNotesMarkdown ?? string.Empty;
            SelectedModContentError = result.Reason ?? string.Empty;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is HttpRequestException
            or IOException
            or InvalidDataException
            or UnauthorizedAccessException
            or System.Text.Json.JsonException)
        {
            if (generation == Volatile.Read(ref selectedModContentLoadGeneration))
            {
                SelectedModContentStatus = ModContentLoadStatus.Unavailable;
                SelectedModContentError = exception.Message;
            }
        }
        finally
        {
            if (generation == Volatile.Read(ref selectedModContentLoadGeneration))
            {
                IsLoadingSelectedModContent = false;
            }
        }
    }

    private void NotifyOperationCompleted()
    {
        StatusMessage = Loc["OperationComplete"];
        ToastRequested?.Invoke(StatusMessage);
    }

    private void NotifyOfficialCatalogLabels()
    {
        OnPropertyChanged(nameof(OfficialModCatalogStatus));
        OnPropertyChanged(nameof(OfficialModCatalogSummary));
        OnPropertyChanged(nameof(OfficialModCatalogError));
    }

    private void RebuildModStatusOptions()
    {
        ModStatusOptions.Clear();
        ModStatusOptions.Add(new(ModStatusFilter.All, Loc["FilterAll"]));
        ModStatusOptions.Add(new(ModStatusFilter.Enabled, Loc["Enabled"]));
        ModStatusOptions.Add(new(ModStatusFilter.Disabled, Loc["Disabled"]));
        ModStatusOptions.Add(new(ModStatusFilter.Local, Loc["Local"]));
        ModStatusOptions.Add(new(ModStatusFilter.Updates, Loc["Updates"]));
        ModStatusOptions.Add(new(ModStatusFilter.External, Loc["External"]));
        ModStatusOptions.Add(new(ModStatusFilter.Pinned, Loc["Pinned"]));
        ModStatusOptions.Add(new(ModStatusFilter.NeedsAttention, Loc["NeedsAttentionFilter"]));
        SelectedModStatusOption = ModStatusOptions.First(option => option.Value == SelectedModStatus);
    }

    private string ModOwnershipDisplay(ModOwnership ownership) => ownership switch
    {
        ModOwnership.Managed => Loc["Installed"],
        ModOwnership.External => Loc["External"],
        ModOwnership.LocalTakenOver => Loc["Local"],
        _ => ownership.ToString()
    };

    private string ModHealthDisplay(ModHealthStatus status) => Loc[status switch
    {
        ModHealthStatus.Healthy => "ModHealthHealthy",
        ModHealthStatus.CriticalFileMissing => "ModHealthCriticalFileMissing",
        ModHealthStatus.ModifiedFile => "ModHealthModifiedFile",
        ModHealthStatus.ExtraFile => "ModHealthExtraFile",
        ModHealthStatus.UnmanagedExternal => "ModHealthUnmanagedExternal",
        ModHealthStatus.Indeterminate => "ModHealthIndeterminate",
        _ => "ModHealthIndeterminate"
    }];

    private void NotifyPreflightLabels()
    {
        OnPropertyChanged(nameof(GameFilesStatus));
        OnPropertyChanged(nameof(LoaderPreflightStatus));
        OnPropertyChanged(nameof(ModDependencyStatus));
        OnPropertyChanged(nameof(SaveIsolationStatus));
        OnPropertyChanged(nameof(LaunchReadinessTitle));
        OnPropertyChanged(nameof(LaunchReadinessHint));
        OnPropertyChanged(nameof(LaunchIssueCountText));
    }

    public IReadOnlyList<LaunchIssueItemViewModel> CreateLaunchIssueItems() =>
        LaunchPreflight.Issues.Select(issue => new LaunchIssueItemViewModel(
            Loc[LaunchIssueLocalizationKey(issue.Code)],
            issue.Arguments.Count == 0 ? string.Empty : string.Join(" · ", issue.Arguments),
            issue.Severity)).ToArray();

    private static string LaunchIssueLocalizationKey(LaunchIssueCode code) => code switch
    {
        LaunchIssueCode.ExecutableMissing => "LaunchIssueExecutableMissing",
        LaunchIssueCode.GameAlreadyRunning => "LaunchIssueGameAlreadyRunning",
        LaunchIssueCode.LoaderConflict => "LaunchIssueLoaderConflict",
        LaunchIssueCode.LoaderDrifted => "LaunchIssueLoaderDrifted",
        LaunchIssueCode.UnsupportedBuildLoaderCombination => "LaunchIssueUnknownBuild",
        LaunchIssueCode.TransactionUnhealthy => "LaunchIssueTransactionNeedsAttention",
        LaunchIssueCode.LocalLowNotReady => "LaunchIssueLocalLowNotReady",
        LaunchIssueCode.MissingDependency => "LaunchIssueMissingDependency",
        LaunchIssueCode.DisabledDependency => "LaunchIssueDisabledDependency",
        LaunchIssueCode.ModCriticalFileMissing => "LaunchIssueModCriticalFileMissing",
        LaunchIssueCode.ModModifiedFile => "LaunchIssueModModifiedFile",
        LaunchIssueCode.ModExtraFile => "LaunchIssueModExtraFile",
        LaunchIssueCode.UnmanagedExternalMod => "LaunchIssueExternalMod",
        LaunchIssueCode.ModHealthIndeterminate => "LaunchIssueModIndeterminate",
        _ => "LaunchIssues"
    };

    private void OnModSelectionChanged()
    {
        OnPropertyChanged(nameof(HasSelectedMods));
        OnPropertyChanged(nameof(SelectedModCount));
    }

    internal void SelectInstalledMod(
        InstalledModItemViewModel item,
        bool control,
        bool shift)
    {
        ArgumentNullException.ThrowIfNull(item);
        SelectedInstalledMod = item;

        if (shift && installedModSelectionAnchorId is not null)
        {
            var anchorIndex = VisibleInstalledMods
                .Select((candidate, index) => (candidate, index))
                .FirstOrDefault(pair => string.Equals(
                    pair.candidate.Id,
                    installedModSelectionAnchorId,
                    StringComparison.OrdinalIgnoreCase)).index;
            var itemIndex = VisibleInstalledMods.IndexOf(item);
            var hasVisibleAnchor = anchorIndex >= 0
                && anchorIndex < VisibleInstalledMods.Count
                && string.Equals(
                    VisibleInstalledMods[anchorIndex].Id,
                    installedModSelectionAnchorId,
                    StringComparison.OrdinalIgnoreCase);
            if (hasVisibleAnchor && itemIndex >= 0)
            {
                if (!control)
                {
                    SetInstalledModSelection(InstalledMods, selected: false);
                }
                var first = Math.Min(anchorIndex, itemIndex);
                var last = Math.Max(anchorIndex, itemIndex);
                SetInstalledModSelection(
                    VisibleInstalledMods.Skip(first).Take(last - first + 1),
                    selected: true);
                return;
            }
        }

        if (control)
        {
            item.IsSelected = !item.IsSelected;
        }
        else
        {
            SetInstalledModSelection(InstalledMods, selected: false);
            item.IsSelected = true;
        }
        installedModSelectionAnchorId = item.Id;
    }

    [RelayCommand]
    private void SelectAllInstalledMods()
    {
        SetInstalledModSelection(InstalledMods, selected: true);
        installedModSelectionAnchorId = InstalledMods.FirstOrDefault()?.Id;
    }

    [RelayCommand]
    private void ClearInstalledModSelection()
    {
        SetInstalledModSelection(InstalledMods, selected: false);
        SelectedInstalledMod = null;
        installedModSelectionAnchorId = null;
    }

    private static void SetInstalledModSelection(
        IEnumerable<InstalledModItemViewModel> mods,
        bool selected)
    {
        foreach (var mod in mods)
        {
            mod.IsSelected = selected;
        }
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

    private async Task LoadInstanceDetailsAsync(
        InstanceRecord record,
        long generation,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (generation != Volatile.Read(ref detailsLoadGeneration)
                || SelectedInstance?.Id != record.Id)
            {
                return;
            }
            var loaderManager = CreateLoaderManager(record);
            var loaderState = LoaderState.Vanilla;
            var loaderInspection = new LoaderInspection
            {
                State = LoaderState.Vanilla,
                Ownership = LoaderOwnership.None
            };
            InstalledPackageReceipt? loaderReceipt = null;
            IReadOnlyList<InstalledModReceipt> installed = [];
            ModDiscoveryResult discovery = new();
            IReadOnlyList<ModHealthReport> modHealthReports = [];
            IReadOnlyList<TransactionJournal> recoveries = [];
            var stateLoaded = false;
            await instanceOperationCoordinator.RunAsync(record.Id, async cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (generation != Volatile.Read(ref detailsLoadGeneration)
                    || SelectedInstance?.Id != record.Id)
                {
                    return;
                }
                recoveries = await FileTransaction.RecoverPendingAsync(
                    Path.Combine(paths.GetVersionDataRoot(VersionRoot), "transactions"),
                    cancellationToken);
                loaderInspection = await loaderManager.InspectAsync(cancellationToken);
                loaderState = loaderInspection.State;
                loaderReceipt = await loaderManager.GetReceiptAsync(cancellationToken);
                var modManager = CreateModManager(record);
                discovery = await modManager.DiscoverAsync(
                    loaderInspection.PackageId ?? loaderState.ToString(),
                    cancellationToken);
                installed = discovery.InstalledReceipts;
                var healthService = new ModHealthService(record.RootPath);
                var reports = new List<ModHealthReport>(discovery.Mods.Count);
                foreach (var mod in discovery.Mods)
                {
                    var receipt = installed.FirstOrDefault(candidate =>
                        string.Equals(candidate.Id, mod.Id, StringComparison.OrdinalIgnoreCase));
                    reports.Add(receipt is null
                        ? await healthService.AssessExternalAsync(mod, cancellationToken)
                        : await healthService.AssessAsync(receipt, installed, cancellationToken));
                }
                modHealthReports = reports;
                stateLoaded = true;
            }, cancellationToken);
            if (!stateLoaded
                || generation != Volatile.Read(ref detailsLoadGeneration)
                || SelectedInstance?.Id != record.Id)
            {
                return;
            }
            var snapshots = await CreateSnapshotService().ListAsync(record.Id, cancellationToken);
            var presetService = CreateModPresetService(record);
            var presets = await presetService.GetAllAsync(cancellationToken);
            var hasPresetRestorePoint = await presetService.HasRestorePointAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var logs = InstanceLogService.Discover(record.RootPath, GetSharedLocalLowPath());
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
                record.Id,
                modHealthReports,
                settings.ModHealthAcknowledgements,
                new SystemHollowKnightProcessProbe().IsRunning());

            if (generation != Volatile.Read(ref detailsLoadGeneration)
                || SelectedInstance?.Id != record.Id)
            {
                return;
            }

            CurrentLoaderState = loaderState;
            currentLoaderInspection = loaderInspection;
            LoaderVerificationStatus = loaderReceipt is null
                ? loaderState switch
                {
                    LoaderState.BepInEx or LoaderState.Drifted => Loc["ExternalLoaderBlocked"],
                    LoaderState.Conflict => Loc["LoaderConflict"],
                    _ => string.Empty
                }
                : loaderReceipt.IsVerified
                    ? Loc["VerifiedCatalogLoader"]
                    : Loc["UnverifiedLocalLoader"];
            InstalledMods.Clear();
            foreach (var mod in discovery.Mods)
            {
                var receipt = installed.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, mod.Id, StringComparison.OrdinalIgnoreCase));
                var healthReport = modHealthReports.First(report =>
                    string.Equals(report.ModId, mod.Id, StringComparison.OrdinalIgnoreCase));
                var catalogManifest = catalog.Mods.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, mod.Id, StringComparison.OrdinalIgnoreCase));
                InstalledMods.Add(new InstalledModItemViewModel(
                    mod,
                    receipt,
                    healthReport,
                    catalogManifest,
                    OnModSelectionChanged,
                    catalogManifest is null ? null : ProjectMarketMod(catalogManifest),
                    ModOwnershipDisplay(mod.Ownership),
                    ModHealthDisplay(healthReport.Status)));
            }
            UpdateSelectedMarketInstallationState();
            OnModSelectionChanged();
            ApplyModFilters();
            Snapshots.Clear();
            foreach (var snapshot in snapshots)
            {
                Snapshots.Add(snapshot);
            }
            var selectedPresetId = SelectedPreset?.Id;
            ModPresets.Clear();
            foreach (var preset in presets)
            {
                ModPresets.Add(preset);
            }
            SelectedPreset = selectedPresetId is null
                ? ModPresets.FirstOrDefault()
                : ModPresets.FirstOrDefault(preset => preset.Id == selectedPresetId)
                    ?? ModPresets.FirstOrDefault();
            HasPresetRestorePoint = hasPresetRestorePoint;
            InstanceLogs.Clear();
            foreach (var log in logs)
            {
                InstanceLogs.Add(log);
            }
            SelectedLogFile = InstanceLogs.FirstOrDefault();
            LaunchPreflight = preflight;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (IsExpectedInstanceDetailsException(exception))
        {
            ErrorMessage = $"{Loc["OperationFailed"]}: {exception.Message}";
        }
        finally
        {
            if (generation == Volatile.Read(ref detailsLoadGeneration)
                && SelectedInstance?.Id == record.Id)
            {
                IsLoadingInstanceDetails = false;
            }
        }
    }

    private static bool IsExpectedInstanceDetailsException(Exception exception) =>
        exception is IOException
            or InvalidDataException
            or UnauthorizedAccessException
            or InvalidOperationException;

    private async Task RefreshAfterFailedMutationAsync(string instanceId, string operationError)
    {
        try
        {
            await RefreshAsync();
            SelectedInstance = Instances.FirstOrDefault(instance => instance.Id == instanceId);
            ErrorMessage = operationError;
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or InvalidOperationException
            or System.Text.Json.JsonException)
        {
            ErrorMessage = $"{operationError} {Loc["RefreshFailed"]}: {exception.Message}";
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
            var record = SelectedInstance.Record;
            await instanceOperationCoordinator.RunAsync(
                record.Id,
                async _ =>
                {
                    if (new SystemHollowKnightProcessProbe().IsRunning())
                    {
                        throw new InvalidOperationException(Loc["CloseGameFirst"]);
                    }
                    await EnsureTransactionsHealthyAsync();
                    await operation(record);
                },
                lifetimeCancellation.Token);
            await RefreshAsync();
            SelectedInstance = Instances.FirstOrDefault(instance => instance.Id == instanceId);
            NotifyOperationCompleted();
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or InvalidOperationException
            or UnauthorizedAccessException
            or HttpRequestException
            or KeyNotFoundException
            or ArgumentException
            or System.Text.Json.JsonException)
        {
            var operationError = $"{Loc["OperationFailed"]}: {exception.Message}";
            await RefreshAfterFailedMutationAsync(instanceId, operationError);
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

    private async Task EnsureTransactionsHealthyAsync(CancellationToken cancellationToken = default)
    {
        var recoveries = await FileTransaction.RecoverPendingAsync(
            Path.Combine(paths.GetVersionDataRoot(VersionRoot), "transactions"),
            cancellationToken);
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
        if (!ReferenceEquals(sender, steamSession)
            || Volatile.Read(ref steamSignInCancellation) is null
            || IsSteamLoggedIn)
        {
            return;
        }

        byte[] bytes;
        try
        {
            bytes = PngByteQRCodeHelper.GetQRCode(
                eventArgs.ChallengeUrl,
                QRCodeGenerator.ECCLevel.Q,
                6);
        }
        catch (Exception exception)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (ReferenceEquals(sender, steamSession)
                    && Volatile.Read(ref steamSignInCancellation) is not null
                    && !IsSteamLoggedIn)
                {
                    ErrorMessage = $"Steam QR: {exception.Message}";
                }
            });
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(sender, steamSession)
                || Volatile.Read(ref steamSignInCancellation) is null
                || IsSteamLoggedIn)
            {
                return;
            }

            Bitmap? image = null;
            try
            {
                using var stream = new MemoryStream(bytes, writable: false);
                image = new Bitmap(stream);
                QrCodeImage = image;
                image = null;
                SteamStatus = "Scan with the Steam mobile app";
            }
            catch (Exception exception)
            {
                image?.Dispose();
                ErrorMessage = $"Steam QR: {exception.Message}";
            }
        });
    }

    private async Task<GameCatalog> LoadCatalogAsync(CancellationToken cancellationToken = default)
    {
        var result = await CatalogProvider.LoadAsync(
            new Uri("https://raw.githubusercontent.com/wzxnb2333/Crystalfly/main/catalog/catalog.v1.json"),
            Path.Combine(paths.ApplicationDataRoot, "catalog", "catalog.v1.json"),
            metadataHttpClient,
            cancellationToken: cancellationToken);
        officialCatalogResult = null;
        customModLinksResult = null;
        customModLinksError = null;
        if (settings.CustomModLinks is { } customModLinks)
        {
            try
            {
                customModLinksResult = await CustomModLinksSource.LoadAsync(
                    customModLinks,
                    directMetadataHttpClient,
                    Path.Combine(paths.ApplicationDataRoot, "catalog", "custom-modlinks.json"),
                    cancellationToken);
                result = CatalogMerger.Merge(result, null, null, [customModLinksResult.Catalog]);
            }
            catch (Exception exception) when (exception is HttpRequestException
                or IOException
                or InvalidDataException
                or UnauthorizedAccessException
                or ArgumentException)
            {
                customModLinksError = exception.Message;
            }
        }
        else
        {
            officialCatalogResult = await OfficialCatalogSource.LoadAsync(
                metadataHttpClient,
                Path.Combine(paths.ApplicationDataRoot, "catalog", "hk-modlinks.v77.json"),
                cancellationToken);
            if (officialCatalogResult.Status != OfficialCatalogLoadStatus.Failed)
            {
                result = CatalogMerger.Merge(result, null, null, [officialCatalogResult.Catalog]);
            }
        }
        modTranslationResult = await ModTranslationSource.LoadAsync(
            metadataHttpClient,
            Path.Combine(paths.ApplicationDataRoot, "catalog", "mod-translations.zh-CN.v1.json"),
            cancellationToken);
        modTranslations = modTranslationResult.Catalog;
        modActivityResult = await ModActivitySource.LoadAsync(
            metadataHttpClient,
            Path.Combine(paths.ApplicationDataRoot, "catalog", "mod-activity.v1.json"),
            cancellationToken);
        modActivityCatalog = modActivityResult.Catalog;
        ApplicationLog.Write(
            Path.Combine(paths.ApplicationDataRoot, "logs", "crystalfly.log"),
            "mod-translation-catalog",
            $"source={modTranslationResult.Status} count={modTranslationResult.ModCount}"
                + (string.IsNullOrWhiteSpace(modTranslationResult.Reason)
                    ? string.Empty
                    : $" reason={modTranslationResult.Reason}"));
        ApplicationLog.Write(
            Path.Combine(paths.ApplicationDataRoot, "logs", "crystalfly.log"),
            "mod-activity-catalog",
            $"source={modActivityResult.Status} count={modActivityResult.Catalog.Entries.Count}"
                + (string.IsNullOrWhiteSpace(modActivityResult.Reason)
                    ? string.Empty
                    : $" reason={modActivityResult.Reason}"));
        OnPropertyChanged(nameof(OfficialModCatalogStatus));
        OnPropertyChanged(nameof(OfficialModCatalogSummary));
        OnPropertyChanged(nameof(OfficialModCatalogError));

        var customCatalogs = new List<GameCatalog>();
        var customCatalogErrors = new List<string>();
        foreach (var definition in settings.CustomCatalogs)
        {
            try
            {
                var customResult = await CustomCatalogSource.LoadWithCacheAsync(
                    definition.Namespace,
                    new Uri(definition.Url),
                    GetCustomCatalogCachePath(definition),
                    directMetadataHttpClient,
                    cancellationToken);
                customCatalogs.Add(customResult.Catalog);
                if (customResult.Status == CustomCatalogLoadStatus.Cached)
                {
                    customCatalogErrors.Add(
                        $"{definition.Namespace}: cached catalog used ({customResult.Reason})");
                }
            }
            catch (Exception exception) when (exception is HttpRequestException
                or InvalidDataException
                or System.Text.Json.JsonException
                or UriFormatException
                or ArgumentException
                || exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
            {
                customCatalogErrors.Add($"{definition.Namespace}: {exception.Message}");
            }
        }
        var customMerge = CatalogProvider.MergeCustomCatalogs(result, customCatalogs);
        customCatalogErrors.AddRange(customMerge.RejectedReasons);
        if (customCatalogErrors.Count > 0)
        {
            ErrorMessage = string.Join(Environment.NewLine, customCatalogErrors);
        }
        return customMerge.Catalog;
    }

    private string GetCustomCatalogCachePath(CustomCatalogDefinition definition)
    {
        var identity = $"{definition.Namespace}\n{definition.Url}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))
            .ToLowerInvariant();
        return Path.Combine(
            paths.ApplicationDataRoot,
            "catalog",
            "custom",
            $"{hash}.json");
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

    private async Task DisposeCurrentSteamSessionAsync()
    {
        var session = Interlocked.Exchange(ref steamSession, null);
        if (session is null)
        {
            return;
        }

        session.QrChallengeChanged -= OnQrChallengeChanged;
        await session.DisposeAsync();
    }

    private LoaderManager CreateLoaderManager(InstanceRecord record)
    {
        var stateRoot = GetInstanceStateRoot(record.Id);
        return new LoaderManager(
            record.RootPath,
            Path.Combine(paths.GetVersionDataRoot(VersionRoot), "transactions"),
            Path.Combine(stateRoot, "loader.json"),
            Path.Combine(paths.GetVersionDataRoot(VersionRoot), "packages"),
            packageHttpClient);
    }

    private ModManager CreateModManager(InstanceRecord record) => new(
        record.RootPath,
        Path.Combine(paths.GetVersionDataRoot(VersionRoot), "transactions"),
        Path.Combine(GetInstanceStateRoot(record.Id), "mods"),
        Path.Combine(paths.GetVersionDataRoot(VersionRoot), "packages"),
        packageHttpClient);

    private ModInstallService CreateModInstallService(InstanceRecord record) => new(
        record,
        catalog.Mods,
        catalog.Loaders,
        CreateLoaderManager(record),
        CreateModManager(record));

    private NamedSnapshotService CreateSnapshotService() => new(
        paths.GetVersionDataRoot(VersionRoot));

    private GlobalModSettingsService CreateGlobalModSettingsService() => new(
        new LocalLowIsolationService(
            GetSharedLocalLowPath(),
            paths.GetVersionDataRoot(VersionRoot)));

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
        lifetimeCancellation.Cancel();
        Task pendingExternalProtocolCommand;
        lock (externalProtocolCommandSync)
        {
            pendingExternalProtocolCommand = externalProtocolCommandTask;
        }
        var detailsCancellation = Interlocked.Exchange(ref detailsLoadCancellation, null);
        detailsCancellation?.Cancel();
        var pendingDetailsLoad = detailsLoadTask;
        var contentCancellation = Interlocked.Exchange(ref selectedModContentLoadCancellation, null);
        contentCancellation?.Cancel();
        var pendingContentLoad = selectedModContentLoadTask;
        var signInCancellation = Interlocked.Exchange(ref steamSignInCancellation, null);
        signInCancellation?.Cancel();
        downloadCancellation?.Cancel();
        try
        {
            try
            {
                await Task.WhenAll(
                    initializationTask ?? Task.CompletedTask,
                    LaunchGameCommand.ExecutionTask ?? Task.CompletedTask,
                    ForceLaunchGameCommand.ExecutionTask ?? Task.CompletedTask,
                    SignInWithQrCommand.ExecutionTask ?? Task.CompletedTask,
                    DownloadBuildCommand.ExecutionTask ?? Task.CompletedTask,
                    PrepareMarketInstallTargetsCommand.ExecutionTask ?? Task.CompletedTask,
                    InstallMarketModCommand.ExecutionTask ?? Task.CompletedTask,
                    TestGitHubLatencyCommand.ExecutionTask ?? Task.CompletedTask,
                    pendingExternalProtocolCommand,
                    steamOfflineTransitionTask,
                    pendingDetailsLoad,
                    pendingContentLoad);
            }
            catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
            {
            }
            finally
            {
                try
                {
                    await FlushSettingsSavesAsync();
                }
                finally
                {
                    try
                    {
                        await downloadQueue.DisposeAsync();
                    }
                    finally
                    {
                        downloadQueue.QueueChanged -= OnDownloadQueueChanged;
                    }
                }
            }
        }
        finally
        {
            detailsCancellation?.Dispose();
            contentCancellation?.Dispose();
            signInCancellation?.Dispose();
            try
            {
                if (disposeSteamOverride is not null)
                {
                    await disposeSteamOverride();
                }
                else if (steamSession is not null)
                {
                    await DisposeCurrentSteamSessionAsync();
                }
            }
            finally
            {
                QrCodeImage = null;
                steamConnectionGate.Dispose();
                metadataHttpClient.Dispose();
                directMetadataHttpClient.Dispose();
                packageHttpClient.Dispose();
                githubLatencyService.Dispose();
                networkPolicy.Dispose();
                lifetimeCancellation.Dispose();
            }
        }
    }
}

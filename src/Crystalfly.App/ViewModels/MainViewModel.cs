using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.App.Downloads;
using Crystalfly.App.Services;
using Crystalfly.App.Theming;
using Crystalfly.App.Updates;
using Crystalfly.App.Runtime;
using Crystalfly.App.ViewModels.DependencyGraph;
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
    private readonly SystemProxyService systemProxy;
    private readonly CrystalflyPaths paths;
    private readonly string settingsPath;
    private GameCatalog catalog;
    private ModTranslationCatalog modTranslations;
    private ModActivityCatalog modActivityCatalog;
    private readonly DpapiRefreshTokenStore tokenStore;
    private readonly DpapiCredentialStore credentialStore;
    private Func<string, string, bool, Task<string?>>? guardCodePrompt;
    private Func<Task<bool?>>? deviceConfirmationPrompt;
    private Func<string, string, string, Task<bool>>? externalContentConfirmPrompt;
    private Func<IReadOnlyList<ModManifest>, string, Task<ModManifest?>>? catalogMatchPrompt;
    private string? adoptPromptedForInstance;
    private readonly SemaphoreSlim settingsSaveLock = new(1, 1);
    private readonly SemaphoreSlim steamConnectionGate = new(1, 1);
    private readonly SemaphoreSlim runtimePatchesConfigurationSaveLock = new(1, 1);
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private readonly object settingsSaveQueueLock = new();
    private readonly object steamReconnectQueueLock = new();
    private readonly object disposeLock = new();
    private readonly Func<Task>? launchOverride;
    private readonly Func<CancellationToken, Task>? downloadOverride;
    private readonly Func<Task>? disposeSteamOverride;
    private readonly Func<CancellationToken, Task<RefreshTokenCredential>>? qrSignInOverride;
    private readonly Func<string, string, CancellationToken, Task<RefreshTokenCredential>>? passwordSignInOverride;
    private readonly Func<bool>? steamLoggedOnOverride;
    private readonly Func<bool> isGameProcessRunning;
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
    private readonly GitHubRoutePreference githubRoutePreference;
    private readonly IProtocolRegistrationService protocolRegistrationService;
    private readonly bool autoRequestGameDirectoryDiscovery;
    private IReadOnlyList<(InstanceRecord Record, LoaderState LoaderState, InstalledPackageReceipt? LoaderReceipt, int ModCount)>? lastInstanceProjection;
    private string? speedrunReminderLocalizationKey;
    private string? speedrunReminderSuffix;
    private bool speedrunReminderVisible = true;
    private string? speedrunReminderTrackedStatus;
    private string? loaderVerificationLocalizationKey;
    private string? loaderVerificationTrackedStatus;
    private bool suppressInstanceDetailsReload;
    private PresetApplyPlan? lastPresetApplyPlan;
    private CrystalflySettings settings = new();
    private Task settingsSaveQueue = Task.CompletedTask;
    private bool settingsSavesClosed;
    private Task steamOfflineTransitionTask = Task.CompletedTask;
    private Task? initializationTask;
    private Task? disposeTask;
    private readonly ProtocolService protocolService;
    private SteamAuthenticationSession? steamSession;
    private CancellationTokenSource? steamSignInCancellation;
    private int steamQrChallengeReceived;
    private int steamQrRestartPending;
    private CancellationTokenSource? downloadCancellation;
    private InstanceRuntimeSession? runtimeSession;
    private Bitmap? qrCodeImage;
    private long detailsLoadGeneration;
    private CancellationTokenSource? detailsLoadCancellation;
    private Task detailsLoadTask = Task.CompletedTask;
    private Task catalogRefreshTask = Task.CompletedTask;
    private Task steamReconnectTask = Task.CompletedTask;
    private long selectedModContentLoadGeneration;
    private bool loadingRuntimePatchesConfiguration;
    private CancellationTokenSource? selectedModContentLoadCancellation;
    private Task selectedModContentLoadTask = Task.CompletedTask;
    private OfficialCatalogLoadResult? officialCatalogResult;
    private CustomModLinksLoadResult? customModLinksResult;
    private string? customModLinksError;
    private ModTranslationLoadResult? modTranslationResult;
    private ModActivityLoadResult? modActivityResult;
    private MarketModItemViewModel? selectedMarketModDisplay;
    private Func<CancellationToken, Task<GameCatalog>> catalogLoader;
    private Func<Task> steamReconnect;
    private Func<
        string,
        GameCatalog,
        CancellationToken,
        Task<IReadOnlyList<InstanceRecord>>> instanceDiscovery;
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
        Func<Task>? launchOverride = null,
        Func<CancellationToken, Task>? downloadOverride = null,
        Func<Task>? disposeSteamOverride = null,
        Func<CancellationToken, Task<RefreshTokenCredential>>? qrSignInOverride = null,
        Func<string, string, CancellationToken, Task<RefreshTokenCredential>>? passwordSignInOverride = null,
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
        IProtocolRegistrationService? protocolRegistrationService = null,
        Func<bool>? gameProcessRunningOverride = null,
        SpeedrunComClient? speedrunComClientOverride = null,
        SystemProxyService? systemProxyOverride = null)
    {
        this.launchOverride = launchOverride;
        this.downloadOverride = downloadOverride;
        this.disposeSteamOverride = disposeSteamOverride;
        this.qrSignInOverride = qrSignInOverride;
        this.passwordSignInOverride = passwordSignInOverride;
        this.steamLoggedOnOverride = steamLoggedOnOverride;
        isGameProcessRunning = gameProcessRunningOverride
            ?? (static () => new SystemHollowKnightProcessProbe().IsRunning());
        this.githubLatencyTestOverride = githubLatencyTestOverride;
        this.modContentLoadOverride = modContentLoadOverride;
        this.instanceDeletionOverride = instanceDeletionOverride;
        this.applicationUpdateCheckOverride = applicationUpdateCheckOverride;
        this.protocolRegistrationService = protocolRegistrationService
            ?? new WindowsProtocolRegistrationService();
        autoRequestGameDirectoryDiscovery = applicationDataRoot is null;
        paths = applicationDataRoot is null
            ? CrystalflyPaths.Resolve(
                AppContext.BaseDirectory,
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
            : new CrystalflyPaths(Path.GetFullPath(applicationDataRoot), IsPortable: false);
        settingsPath = Path.Combine(paths.ApplicationDataRoot, "settings.json");
        tokenStore = new DpapiRefreshTokenStore(Path.Combine(paths.ApplicationDataRoot, "steam-token.dat"));
        credentialStore = new DpapiCredentialStore(Path.Combine(paths.ApplicationDataRoot, "steam-credentials.dat"));
        catalog = EmbeddedCatalog.Load();
        catalogLoader = LoadCatalogAsync;
        steamReconnect = TryReconnectSteamAsync;
        instanceDiscovery = InstanceImportService.DiscoverAsync;
        modTranslations = EmbeddedModTranslationCatalog.Load();
        modActivityCatalog = EmbeddedModActivityCatalog.Load();
        networkPolicy = new NetworkPolicy();
        systemProxy = systemProxyOverride ?? new SystemProxyService();
        githubRoutePreference = new GitHubRoutePreference();
        metadataHttpClient = new HttpClient(new GitHubDownloadRouteHandler(
            () => settings.GitHubDownloadRoute,
            networkPolicy,
            CreateSystemProxyHandler(),
            githubRoutePreference)) { Timeout = TimeSpan.FromSeconds(15) };
        directMetadataHttpClient = new HttpClient(new NetworkPolicyHandler(
            networkPolicy,
            CreateSystemProxyHandler())) { Timeout = TimeSpan.FromSeconds(15) };
        speedrunComClient = speedrunComClientOverride ?? new SpeedrunComClient(
            directMetadataHttpClient,
            Path.Combine(paths.ApplicationDataRoot, "speedrun-cache"),
            networkPolicy);
        packageHttpClient = new HttpClient(new GitHubDownloadRouteHandler(
            () => settings.GitHubDownloadRoute,
            networkPolicy,
            CreateSystemProxyHandler(),
            githubRoutePreference)) { Timeout = TimeSpan.FromMinutes(30) };
        githubLatencyService = new GitHubRouteLatencyService(
            networkPolicy,
            CreateSystemProxyHandler(),
            null,
            null,
            githubRoutePreference);
        Loc = new LocalizationViewModel();
        SteamNetworkStatus = FormatSteamNetworkStatus(systemProxy.Current);
        systemProxy.Changed += OnSystemProxyChanged;
        var downloadQueue = downloadQueueOverride ?? CreateDownloadQueue();
        Instances = new InstancesViewModel(new InstancesDependencies(
            Loc: () => Loc,
            GetSettings: () => settings,
            SetSettings: value => settings = value,
            QueueSettingsSave: () => _ = QueueSettingsSave(),
            RefreshInstances: () => RefreshAsync(),
            RefreshInstancesQuietly: () => RefreshInstancesAsync(showBusy: false),
            GetCanNavigate: () => CanNavigate,
            GetIsBusy: () => IsBusy,
            SetIsBusy: value => IsBusy = value,
            IsMutationBlocked: IsMutationBlocked,
            RunCoordinated: (id, operation, token) => instanceOperationCoordinator.RunAsync(id, operation, token),
            EvaluateDeletionConditions: EvaluateInstanceDeletionConditionsAsync,
            InstanceDeletionOverride: instanceDeletionOverride,
            IsVanillaInstanceAsync: async record =>
                await CreateLoaderManager(record).GetStateAsync() == LoaderState.Vanilla
                && (await CreateModManager(record).GetInstalledAsync()).Count == 0,
            GetVersionDataRoot: paths.GetVersionDataRoot,
            GetVersionRoot: () => VersionRoot,
            SetVersionRoot: value => VersionRoot = value,
            GetCatalog: () => catalog,
            GetSelectedInstance: () => SelectedInstance,
            SetSelectedInstance: value => SelectedInstance = value,
            GetSelectedSpeedrunInstance: () => SelectedSpeedrunInstance,
            SetSelectedSpeedrunInstance: value => SelectedSpeedrunInstance = value,
            SetErrorMessage: message => ErrorMessage = message,
            SetStatusMessage: message => StatusMessage = message,
            SetCurrentPage: value => CurrentPage = value,
            SetCurrentManageTab: value => CurrentManageTab = value,
            GetCloneInstanceName: () => CloneInstanceName,
            SetCloneInstanceName: value => CloneInstanceName = value,
            DiscoverInstances: (root, catalog, token) => instanceDiscovery(root, catalog, token),
            GetSearchText: () => SearchText,
            HasBlockingDownloadForPath: path => downloadQueue.Groups.Any(group =>
                string.Equals(
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(group.TargetInstanceRoot)),
                    path,
                    StringComparison.OrdinalIgnoreCase)
                && group.State is DownloadQueueGroupState.Pending
                    or DownloadQueueGroupState.Running
                    or DownloadQueueGroupState.Failed
                    or DownloadQueueGroupState.WaitingForNetwork),
            NotifyOperationCompleted: NotifyOperationCompleted,
            AutoRequestGameDirectoryDiscovery: autoRequestGameDirectoryDiscovery,
            LifetimeCancellation: lifetimeCancellation.Token));
        Instances.PropertyChanged += (_, eventArgs) =>
        {
            OnPropertyChanged(eventArgs.PropertyName);
            NotifyOnboardingStateChanged();
        };
        Instances.ToastRequested += message => NotifyToast(message);
        Settings = new SettingsViewModel(new SettingsDependencies(
            Loc: () => Loc,
            GetSettings: () => settings,
            SetSettings: value => settings = value,
            QueueSettingsSave: () => _ = QueueSettingsSave(),
            SaveSettingsImmediately: SaveSettingsWithLockAsync,
            FlushSettingsSavesAsync: FlushSettingsSavesAsync,
            ApplyLanguage: ApplyLanguage,
            ApplyTheme: ApplyTheme,
            GetCatalog: () => catalog,
            LoadCatalog: async cancellationToken =>
            {
                catalog = await LoadCatalogAsync(cancellationToken);
                return catalog;
            },
            RefreshAfterCatalogChange: async () =>
            {
                RebuildMarketCatalog();
                if (Directory.Exists(VersionRoot))
                {
                    await RefreshAsync();
                }
            },
            RebuildModStatusOptions: () => ModManagement!.RebuildStatusOptions(),
            RebuildMarketCatalog: RebuildMarketCatalog,
            RebuildInstalledModCatalogProjection: () => ModManagement!.RebuildCatalogProjection(),
            RefreshGameDirectoryLabels: () => Instances.RefreshGameDirectoryLabels(),
            NotifyPreflightLabels: NotifyPreflightLabels,
            RebuildPresetModeOptions: RebuildPresetModeOptions,
            NotifyOperationCompleted: NotifyOperationCompleted,
            TestGitHubLatency: githubLatencyTestOverride is null
                ? cancellationToken => githubLatencyService.TestAsync(
                    [
                        GitHubDownloadRoute.Direct,
                        GitHubDownloadRoute.GhProxyOrg,
                        GitHubDownloadRoute.Mirror,
                        GitHubDownloadRoute.GhProxyNet,
                        GitHubDownloadRoute.GhFastTop
                    ],
                    cancellationToken)
                : githubLatencyTestOverride,
            GetCanNavigate: () => CanNavigate,
            GetSelectedInstance: () => SelectedInstance,
            GetInstanceStateRoot: GetInstanceStateRoot,
            GetVersionRoot: () => VersionRoot,
            ApplicationDataRoot: paths.ApplicationDataRoot,
            SetErrorMessage: message => ErrorMessage = message,
            LifetimeCancellation: lifetimeCancellation.Token));
        Settings.PropertyChanged += (_, eventArgs) => OnPropertyChanged(eventArgs.PropertyName);
        Settings.ToastRequested += message => NotifyToast(message);
        protocolService = new ProtocolService(
            () => Loc,
            () => SelectedInstance,
            () => Instances.Instances,
            () => catalog,
            () => IsBusy,
            () => IsGameRunning,
            () => DownloadCenter!.HasUnfinishedDownloads,
            lifetimeCancellation.Token);
        ModManagement = new ModManagementViewModel(new ModManagementDependencies(
            () => catalog,
            () => Loc,
            message => NotifyToast(message),
            lifetimeCancellation.Token,
            operation => RunInstanceMutationAsync(operation),
            CreateModManager,
            CreateLoaderManager,
            CreateModInstallService,
            ProjectMarketMod,
            ProjectMarketMod,
            ModOwnershipDisplay,
            ModHealthDisplay,
            message => ErrorMessage = message,
            () => SelectedInstance?.Record,
            EnqueueModDependencyRepairAsync));
        DependencyGraph = new DependencyGraphViewModel(new DependencyGraphDependencies(
            () => Loc,
            ProjectMarketMod,
            GetInstalledModGraphLayoutPath,
            () => SelectedInstance?.Id,
            message => ErrorMessage = message));
        ModManagement.PropertyChanged += OnModManagementPropertyChanged;
        ModManagement.InstalledModsRefreshed += OnInstalledModsRefreshed;
        ModManagement.GraphVisibilityChanged += OnInstalledModGraphVisibilityChanged;
        DependencyGraph.NodeSelectedRequested += ModManagement.SelectModFromGraph;
        DependencyGraph.NodeToggleRequested += id => _ = ModManagement.ToggleInstalledModFromGraphAsync(id);
        DependencyGraph.NodeDeleteRequested += RequestInstalledModRemovalFromGraph;        DownloadCenter = new DownloadCenterViewModel(new DownloadCenterDependencies(
            downloadQueue,
            Loc,
            message => NotifyToast(message),
            message => ErrorMessage = message,
            () => IsBusy,
            () => VersionRoot,
            () => SelectedInstance?.Id,
            id => SelectedInstance = Instances.Instances.FirstOrDefault(instance => instance.Id == id)
                ?? SelectedInstance,
            RefreshAsync,
            lifetimeCancellation.Token));
    }

    private bool IsSteamSessionLoggedOn() =>
        steamLoggedOnOverride?.Invoke() ?? steamSession?.IsLoggedOn == true;

    private void OnModManagementPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        NotifyOnboardingStateChanged();
        if (eventArgs.PropertyName == nameof(ModManagementViewModel.SelectedInstalledMod)
            && ModManagement.IsInstalledModGraphVisible)
        {
            DependencyGraph.SelectNode(ModManagement.SelectedInstalledMod?.Id);
        }
    }

    private void OnInstalledModsRefreshed()
    {
        if (ModManagement.IsInstalledModGraphVisible)
        {
            DependencyGraph.Rebuild(
                ModManagement.InstalledMods,
                ModManagement.SelectedInstalledMod?.Id,
                SelectedInstance?.Id);
        }
    }

    private void OnInstalledModGraphVisibilityChanged()
    {
        if (ModManagement.IsInstalledModGraphVisible)
        {
            OnInstalledModsRefreshed();
        }
    }

    public LocalizationViewModel Loc { get; private set; }

    public SettingsViewModel Settings { get; }

    public ModManagementViewModel ModManagement { get; }

    public DependencyGraphViewModel DependencyGraph { get; }

    public DownloadCenterViewModel DownloadCenter { get; }

    public event Action? GraphModRemovalRequested;

    public InstancesViewModel Instances { get; }

    public ObservableCollection<SpeedrunTemplate> SpeedrunTemplates { get; } = [];

    public ObservableCollection<InstanceItemViewModel> SpeedrunSourceInstances { get; } = [];

    public ObservableCollection<LoaderManifest> AvailableLoaders { get; } = [];

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

    public ObservableCollection<InstanceLogFile> InstanceLogs { get; } = [];

    public ObservableCollection<NamedSnapshot> Snapshots { get; } = [];

    public ObservableCollection<DownloadBuildOption> DownloadBuilds { get; } = [];

    public ObservableCollection<DownloadBuildOption> VisibleDownloadBuilds { get; } = [];

    public bool HasInstance => SelectedInstance is not null;

    public bool IsOnboardingCompleted => settings.OnboardingCompleted;

    public bool ShouldShowLaunchOnboarding => !IsOnboardingCompleted;

    public IReadOnlyList<OnboardingTaskItemViewModel> OnboardingTasks => BuildOnboardingTasks();

    public OnboardingTaskItemViewModel? OnboardingNextTask =>
        OnboardingTasks.FirstOrDefault(task => !task.IsDone) ?? OnboardingTasks.LastOrDefault();

    public string OnboardingNextTaskTitle => OnboardingNextTask?.Title ?? Loc["OnboardingSection"];

    public string OnboardingNextTaskDescription =>
        OnboardingNextTask?.Description ?? Loc["OnboardingSectionHint"];

    public string OnboardingProgressText
    {
        get
        {
            var tasks = OnboardingTasks;
            return string.Format(
                CultureInfo.CurrentUICulture,
                Loc["OnboardingProgressFormat"],
                tasks.Count(task => task.IsDone),
                tasks.Count);
        }
    }

    public bool HasOnboardingAction => OnboardingNextTask?.HasAction == true;

    public string OnboardingNextActionText =>
        OnboardingNextTask?.ActionText ?? Loc["OnboardingReopen"];

    public bool CanNavigate => !IsBusy && !IsGameRunning && !IsExternalCommandRunning;

    public bool CanCloneInstance => CanNavigate && !string.IsNullOrWhiteSpace(CloneInstanceName);

    public bool HasModDependencyProblems => ModManagement.InstalledMods.Count > 0 && !LaunchPreflight.DependenciesReady;

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

    public bool HasSelectedSpeedrunInstance => SelectedSpeedrunInstance is not null;

    public bool IsSelectedSpeedrunLegacy => SelectedSpeedrunInstance?.Record.SpeedrunTemplateId is { } templateId
        && RuntimePatchesPolicy.IsLegacyTemplate(templateId);

    public bool IsSelectedSpeedrunCurrent => SelectedSpeedrunInstance?.Record.SpeedrunTemplateId is { } templateId
        && RuntimePatchesPolicy.IsCurrentTemplate(templateId);

    public string SelectedSpeedrunTechnicalStatus => SelectedSpeedrunInstance is null
        ? Loc["SpeedrunNoInstance"]
        : IsSelectedSpeedrunLegacy
            ? Loc["SpeedrunTemplateExpired"]
            : IsSelectedSpeedrunCurrent
                ? Loc["SpeedrunTechnicalReady"]
                : Loc["SpeedrunNeedsRebuild"];

    public RuntimePatchesFeature SelectedSpeedrunSupportedFeatures =>
        SelectedSpeedrunInstance is null
            ? RuntimePatchesFeature.None
            : RuntimePatchesPolicy.GetSupportedFeatures(SelectedSpeedrunInstance.Record.BuildId);

    public bool IsScreenShakeModifierAvailable =>
        SelectedSpeedrunSupportedFeatures.HasFlag(RuntimePatchesFeature.ScreenShakeModifier);

    public bool IsMiniSaveStatesAvailable =>
        SelectedSpeedrunSupportedFeatures.HasFlag(RuntimePatchesFeature.MiniSaveStates);

    public bool IsFasterIntroSkipAvailable =>
        SelectedSpeedrunSupportedFeatures.HasFlag(RuntimePatchesFeature.FasterIntroSkip);

    public bool IsTextMasherAvailable =>
        SelectedSpeedrunSupportedFeatures.HasFlag(RuntimePatchesFeature.TextMasher);

    public bool IsRuntimePatchesConfigurationEditable =>
        IsSelectedSpeedrunCurrent && !IsGameRunning && !IsBusy;

    public bool IsDownloadsPage => CurrentPage == "Downloads";

    public bool IsSettingsPage => CurrentPage == "Settings";

    public bool IsGameVersionsDownloadSection => CurrentDownloadSection == "GameVersions";

    public bool IsModMarketDownloadSection => CurrentDownloadSection == "ModMarket";

    public bool IsDownloadQueueSection => CurrentDownloadSection == "DownloadQueue";

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
    [NotifyPropertyChangedFor(nameof(IsRuntimePatchesConfigurationEditable))]
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
    public partial string SteamNetworkStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SteamUsername { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SteamPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsPasswordLoginVisible { get; set; }

    public Func<string, string, bool, Task<string?>>? GuardCodePrompt
    {
        get => guardCodePrompt;
        set => guardCodePrompt = value;
    }

    public Func<Task<bool?>>? DeviceConfirmationPrompt
    {
        get => deviceConfirmationPrompt;
        set => deviceConfirmationPrompt = value;
    }

    public Func<string, string, string, Task<bool>>? ExternalContentConfirmPrompt
    {
        get => externalContentConfirmPrompt;
        set => externalContentConfirmPrompt = value;
    }

    public Func<IReadOnlyList<ModManifest>, string, Task<ModManifest?>>? CatalogMatchPrompt
    {
        get => catalogMatchPrompt;
        set => catalogMatchPrompt = value;
    }

    public event Action? OnboardingRequested;

    [ObservableProperty]
    public partial DownloadBuildOption? SelectedDownloadBuild { get; set; }

    [ObservableProperty]
    public partial bool IsDownloading { get; set; }

    [ObservableProperty]
    public partial double DownloadProgress { get; set; }

    [ObservableProperty]
    public partial string DownloadStatus { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanNavigate))]
    [NotifyPropertyChangedFor(nameof(CanCloneInstance))]
    [NotifyPropertyChangedFor(nameof(CanLaunch))]
    [NotifyPropertyChangedFor(nameof(CanAttemptLaunch))]
    [NotifyPropertyChangedFor(nameof(IsRuntimePatchesConfigurationEditable))]
    public partial bool IsGameRunning { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanOpenModFolder))]
    [NotifyPropertyChangedFor(nameof(OnboardingTasks))]
    [NotifyPropertyChangedFor(nameof(OnboardingNextTask))]
    [NotifyPropertyChangedFor(nameof(OnboardingNextTaskTitle))]
    [NotifyPropertyChangedFor(nameof(OnboardingNextTaskDescription))]
    [NotifyPropertyChangedFor(nameof(OnboardingProgressText))]
    [NotifyPropertyChangedFor(nameof(HasOnboardingAction))]
    [NotifyPropertyChangedFor(nameof(OnboardingNextActionText))]
    public partial LoaderState CurrentLoaderState { get; set; }

    public bool CanOpenModFolder => CurrentLoaderState is LoaderState.ModdingApi or LoaderState.BepInEx;

    [ObservableProperty]
    public partial LoaderManifest? SelectedLoader { get; set; }

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
    [NotifyPropertyChangedFor(nameof(OnboardingTasks))]
    [NotifyPropertyChangedFor(nameof(OnboardingNextTask))]
    [NotifyPropertyChangedFor(nameof(OnboardingNextTaskTitle))]
    [NotifyPropertyChangedFor(nameof(OnboardingNextTaskDescription))]
    [NotifyPropertyChangedFor(nameof(OnboardingProgressText))]
    [NotifyPropertyChangedFor(nameof(HasOnboardingAction))]
    [NotifyPropertyChangedFor(nameof(OnboardingNextActionText))]
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
    public partial InstanceItemViewModel? SelectedSpeedrunSourceInstance { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedSpeedrunInstance))]
    [NotifyPropertyChangedFor(nameof(IsSelectedSpeedrunLegacy))]
    [NotifyPropertyChangedFor(nameof(IsSelectedSpeedrunCurrent))]
    [NotifyPropertyChangedFor(nameof(SelectedSpeedrunTechnicalStatus))]
    [NotifyPropertyChangedFor(nameof(SelectedSpeedrunSupportedFeatures))]
    [NotifyPropertyChangedFor(nameof(IsScreenShakeModifierAvailable))]
    [NotifyPropertyChangedFor(nameof(IsMiniSaveStatesAvailable))]
    [NotifyPropertyChangedFor(nameof(IsFasterIntroSkipAvailable))]
    [NotifyPropertyChangedFor(nameof(IsTextMasherAvailable))]
    [NotifyPropertyChangedFor(nameof(IsRuntimePatchesConfigurationEditable))]
    public partial InstanceItemViewModel? SelectedSpeedrunInstance { get; set; }

    [ObservableProperty]
    public partial bool RuntimePatchesScreenShakeModifier { get; set; }

    [ObservableProperty]
    public partial bool RuntimePatchesMiniSaveStates { get; set; }

    [ObservableProperty]
    public partial bool RuntimePatchesFasterIntroSkip { get; set; }

    [ObservableProperty]
    public partial bool RuntimePatchesTextMasher { get; set; }

    [ObservableProperty]
    public partial int? SelectedLoadNormaliserSeconds { get; set; }

    [ObservableProperty]
    public partial string SnapshotName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SpeedrunEnvironmentName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SpeedrunStatus { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSpeedrunReminder))]
    public partial string SpeedrunReminderText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSpeedrunReport))]
    public partial string? SpeedrunReportPath { get; set; }

    [ObservableProperty]
    public partial bool SpeedrunReminderIsError { get; set; }

    public bool HasSpeedrunReminder => !string.IsNullOrWhiteSpace(SpeedrunReminderText);

    public bool HasSpeedrunReport => !string.IsNullOrWhiteSpace(SpeedrunReportPath);

    [ObservableProperty]
    public partial string LocalLoaderManifestPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LoaderVerificationStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool CanAdoptExternalContent { get; set; }

    [ObservableProperty]
    public partial int ExternalModAdoptCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInstance))]
    [NotifyPropertyChangedFor(nameof(CanLaunch))]
    [NotifyPropertyChangedFor(nameof(CanAttemptLaunch))]
    [NotifyPropertyChangedFor(nameof(LaunchReadinessTitle))]
    [NotifyPropertyChangedFor(nameof(LaunchReadinessHint))]
    [NotifyPropertyChangedFor(nameof(SupportsAccessibility))]
    [NotifyPropertyChangedFor(nameof(OnboardingTasks))]
    [NotifyPropertyChangedFor(nameof(OnboardingNextTask))]
    [NotifyPropertyChangedFor(nameof(OnboardingNextTaskTitle))]
    [NotifyPropertyChangedFor(nameof(OnboardingNextTaskDescription))]
    [NotifyPropertyChangedFor(nameof(OnboardingProgressText))]
    [NotifyPropertyChangedFor(nameof(HasOnboardingAction))]
    [NotifyPropertyChangedFor(nameof(OnboardingNextActionText))]
    public partial InstanceItemViewModel? SelectedInstance { get; set; }

    public bool SupportsAccessibility =>
        SelectedInstance is not null
        && !SelectedInstance.Record.BuildId.StartsWith("1.2.", StringComparison.OrdinalIgnoreCase)
        && !SelectedInstance.Record.BuildId.StartsWith("1.4.", StringComparison.OrdinalIgnoreCase);

    [ObservableProperty]
    public partial bool IsOfflineMode { get; set; }

    public ObservableCollection<SettingOption<UiLanguage>> LanguageOptions => Settings.LanguageOptions;

    public ObservableCollection<AccentColorOptionViewModel> AccentColorOptions => Settings.AccentColorOptions;

    public SettingOption<UiLanguage>? SelectedLanguage
    {
        get => Settings.SelectedLanguage;
        set => Settings.SelectedLanguage = value;
    }

    public SettingOption<UiTheme>? SelectedTheme
    {
        get => Settings.SelectedTheme;
        set => Settings.SelectedTheme = value;
    }

    public SettingOption<UiMotionPreference>? SelectedMotionPreference
    {
        get => Settings.SelectedMotionPreference;
        set => Settings.SelectedMotionPreference = value;
    }

    public SettingOption<GitHubDownloadRoute>? SelectedGitHubRoute
    {
        get => Settings.SelectedGitHubRoute;
        set => Settings.SelectedGitHubRoute = value;
    }

    public bool IsTestingGitHubLatency => Settings.IsTestingGitHubLatency;

    public string GitHubDirectLatency => Settings.GitHubDirectLatency;

    public string GitHubMirrorLatency => Settings.GitHubMirrorLatency;

    public string CustomSourcesText
    {
        get => Settings.CustomSourcesText;
        set => Settings.CustomSourcesText = value;
    }

    public string CustomModLinksUrl
    {
        get => Settings.CustomModLinksUrl;
        set => Settings.CustomModLinksUrl = value;
    }

    public UiMotionPreference EffectiveMotionPreference => Settings.EffectiveMotionPreference;

    public IAsyncRelayCommand TestGitHubLatencyCommand => Settings.TestGitHubLatencyCommand;

    public ObservableCollection<SettingOption<BackgroundEditScope>> BackgroundScopeOptions => Settings.BackgroundScopeOptions;

    public SettingOption<BackgroundEditScope>? SelectedBackgroundScope
    {
        get => Settings.SelectedBackgroundScope;
        set => Settings.SelectedBackgroundScope = value;
    }

    public bool HasActiveBackgroundImage => Settings.HasActiveBackgroundImage;

    public double ActiveBackgroundOpacity => Settings.ActiveBackgroundOpacity;

    public double BackgroundOpacityPercent
    {
        get => Settings.BackgroundOpacityPercent;
        set => Settings.BackgroundOpacityPercent = value;
    }

    public bool HasInstanceBackgroundOverride => Settings.HasInstanceBackgroundOverride;

    public bool CanEditInstanceBackground => Settings.CanEditInstanceBackground;

    public bool CanChangeBackgroundOpacity => Settings.CanChangeBackgroundOpacity;

    public string BackgroundScopeStatus => Settings.BackgroundScopeStatus;

    public string AccentColor => Settings.AccentColor;

    public string CurrentSettingsSection
    {
        get => Settings.CurrentSettingsSection;
        set => Settings.CurrentSettingsSection = value;
    }

    internal void PreviewAccentColor(string accentColor) => Settings.PreviewAccentColor(accentColor);

    internal void SetAccentColor(string accentColor) => Settings.SetAccentColor(accentColor);

    internal void RestoreAccentColor() => Settings.RestoreAccentColor();

    internal Task SetBackgroundImageAsync(
        string sourcePath,
        CancellationToken cancellationToken = default) =>
        Settings.SetBackgroundImageAsync(sourcePath, cancellationToken);

    internal Task RemoveBackgroundImageAsync(CancellationToken cancellationToken = default) =>
        Settings.RemoveBackgroundImageAsync(cancellationToken);

    internal Task RefreshBackgroundAppearanceAsync(CancellationToken cancellationToken = default) =>
        Settings.RefreshBackgroundAppearanceAsync(cancellationToken);


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
        OnPropertyChanged(nameof(EffectiveMotionPreference));
        SteamUsernameCredential? storedCredential = await credentialStore.LoadAsync(lifetimeCancellation.Token);
        if (storedCredential is not null)
        {
            SteamUsername = storedCredential.Username;
            SteamPassword = storedCredential.Password;
        }
        Settings.InitializeBackgroundState(settings.BackgroundImage);
        IsOfflineMode = settings.OfflineMode;
        ApplyLanguage(settings.Language);
        ApplyTheme(settings.Theme, settings.AccentColor);
        Settings.RebuildBackgroundScopeOptions();
        await Settings.RefreshBackgroundAppearanceAsync(lifetimeCancellation.Token);
        // The download queue restore and the remote catalog load only depend on
        // the loaded settings, so they start in parallel with game-directory
        // setup and the instance scan instead of serially after them. The
        // catalog refresh task still awaits the first instance scan before it
        // re-scans with the loaded catalog, so the scan ordering is unchanged.
        var downloadQueueTask = InitializeDownloadQueueAsync();
        var catalogLoadTask = catalogLoader(lifetimeCancellation.Token);
        InitializeApplicationUpdateSettings();
        VersionRoot = settings.VersionRoot ?? string.Empty;
        await Instances.InitializeGameDirectoriesAsync();
        Settings.CustomSourcesText = string.Join(
            Environment.NewLine,
            settings.CustomCatalogs.Select(source => $"{source.Namespace}={source.Url}"));
        Settings.CustomModLinksUrl = settings.CustomModLinks?.Url ?? string.Empty;
        RebuildSettingOptions();
        if (settings.GitHubDownloadRoute == GitHubDownloadRoute.Auto)
        {
            _ = Settings.TestGitHubLatencyCommand.ExecuteAsync(null);
        }
        RebuildCustomModLinksOptions();
        ModManagement.RebuildStatusOptions();
        RebuildMarketCatalog();
        PopulateSpeedrunTemplates();
        PopulateDownloadBuilds();

        steamReconnectTask = IsOfflineMode
            ? Task.CompletedTask
            : steamReconnect();

        var refreshTask = Directory.Exists(VersionRoot)
            ? RefreshAsync()
            : Task.CompletedTask;
        catalogRefreshTask = RefreshCatalogInBackgroundAsync(refreshTask, catalogLoadTask);
        if (!Directory.Exists(VersionRoot))
        {
            StatusMessage = Loc["ChooseRoot"];
        }
        await Task.WhenAll(refreshTask, downloadQueueTask);
        _ = StartSpeedrunActivityRefreshLoop();
        await Instances.CompleteGameDirectoryInitializationAsync();
        if (!settings.OnboardingCompleted)
        {
            OnboardingRequested?.Invoke();
        }
    }

    public void CompleteOnboarding()
    {
        if (settings.OnboardingCompleted)
        {
            return;
        }
        settings = settings with { OnboardingCompleted = true };
        _ = QueueSettingsSave();
        NotifyOnboardingStateChanged();
    }

    [RelayCommand]
    private void CompleteOnboardingFromUi() => CompleteOnboarding();

    [RelayCommand]
    private void RunOnboardingNextAction()
    {
        if (OnboardingNextTask is { ActionKey: { Length: > 0 } actionKey })
        {
            RunOnboardingAction(actionKey);
        }
    }

    public void RunOnboardingAction(string action)
    {
        switch (action)
        {
            case "Versions":
                CurrentPage = "Versions";
                break;
            case "ManageOverview":
                CurrentManageTab = "Overview";
                CurrentPage = "Manage";
                break;
            case "ManageLoader":
                CurrentManageTab = "Loader";
                CurrentPage = "Manage";
                break;
            case "ModMarket":
                OpenModMarketForSelectedInstance();
                break;
            case "ManageMods":
                CurrentManageTab = "Mods";
                CurrentPage = "Manage";
                break;
            case "SettingsNetwork":
                CurrentSettingsSection = "Network";
                CurrentPage = "Settings";
                break;
            case "Speedrun":
                CurrentPage = "Speedrun";
                break;
            case "Launch":
                CurrentPage = "Launch";
                break;
        }
    }

    private IReadOnlyList<OnboardingTaskItemViewModel> BuildOnboardingTasks()
    {
        var hasDirectory = Instances.GameDirectories.Count > 0 || Directory.Exists(VersionRoot);
        var hasInstance = SelectedInstance is not null;
        var hasLoader = hasInstance && CurrentLoaderState is not LoaderState.Vanilla;
        var hasMods = ModManagement.InstalledMods.Count > 0;
        var hasModIssue = CanAdoptExternalContent || HasModDependencyProblems;
        var launchCheckComplete = hasInstance && LaunchPreflight.IsReady;
        var canLaunch = hasInstance && LaunchPreflight.CanLaunchNormally;

        return
        [
            CreateOnboardingTask("GameDirectory", "Versions", hasDirectory, false),
            CreateOnboardingTask("SelectInstance", "Versions", hasInstance, hasDirectory),
            CreateOnboardingTask("InstanceIsolation", "ManageOverview", hasInstance, hasInstance),
            CreateOnboardingTask("Loader", "ManageLoader", hasLoader, hasInstance),
            CreateOnboardingTask("LaunchIssues", "Launch", launchCheckComplete, hasInstance, HasLaunchIssues),
            CreateOnboardingTask("ModMarket", "ModMarket", hasMods, hasLoader),
            CreateOnboardingTask("InstalledMods", "ManageMods", hasMods && !hasModIssue, hasMods, hasModIssue),
            CreateOnboardingTask("LaunchGame", "Launch", canLaunch, hasMods || hasLoader),
            CreateOnboardingTask("Explore", "Speedrun", IsSpeedrunPage, canLaunch)
        ];
    }

    private OnboardingTaskItemViewModel CreateOnboardingTask(
        string id,
        string action,
        bool isDone,
        bool isUnlocked,
        bool attention = false)
    {
        var state = isDone
            ? OnboardingTaskState.Done
            : attention
                ? OnboardingTaskState.Attention
                : isUnlocked
                    ? OnboardingTaskState.Current
                    : OnboardingTaskState.Locked;
        return new OnboardingTaskItemViewModel(
            id,
            Loc[$"OnboardingTask{id}Title"],
            Loc[$"OnboardingTask{id}Description"],
            Loc[$"OnboardingTaskState{state}"],
            Loc[$"OnboardingTask{id}Action"],
            action,
            state);
    }

    private void NotifyOnboardingStateChanged()
    {
        OnPropertyChanged(nameof(IsOnboardingCompleted));
        OnPropertyChanged(nameof(ShouldShowLaunchOnboarding));
        OnPropertyChanged(nameof(OnboardingTasks));
        OnPropertyChanged(nameof(OnboardingNextTask));
        OnPropertyChanged(nameof(OnboardingNextTaskTitle));
        OnPropertyChanged(nameof(OnboardingNextTaskDescription));
        OnPropertyChanged(nameof(OnboardingProgressText));
        OnPropertyChanged(nameof(HasOnboardingAction));
        OnPropertyChanged(nameof(OnboardingNextActionText));
    }

    [RelayCommand]
    private void ShowOnboarding()
    {
        OnboardingRequested?.Invoke();
    }

    [ObservableProperty]
    private string downloadBuildSearchText = string.Empty;

    private void PopulateDownloadBuilds()
    {
        var previousBuildId = SelectedDownloadBuild?.BuildId;
        DownloadBuilds.Clear();
        DownloadBuilds.Add(new DownloadBuildOption("public", Loc["LatestBuild"], null));
        var publicBuildId = catalog.Channels
            .FirstOrDefault(channel => channel.Name == "public")?.BuildId;
        foreach (var build in catalog.Builds
                     .Where(build => !string.Equals(
                         build.Id,
                         publicBuildId,
                         StringComparison.OrdinalIgnoreCase))
                     .OrderByDescending(
                         build => build.DisplayVersion,
                         StringComparer.OrdinalIgnoreCase))
        {
            DownloadBuilds.Add(new DownloadBuildOption(
                build.Id,
                build.DisplayVersion,
                ulong.Parse(build.ManifestId, CultureInfo.InvariantCulture)));
        }
        SelectedDownloadBuild = DownloadBuilds.FirstOrDefault(build => string.Equals(
                                    build.BuildId,
                                    previousBuildId,
                                    StringComparison.OrdinalIgnoreCase))
                                ?? DownloadBuilds[0];
        ApplyDownloadBuildFilter();
    }

    partial void OnDownloadBuildSearchTextChanged(string value) => ApplyDownloadBuildFilter();

    private void ApplyDownloadBuildFilter()
    {
        var search = DownloadBuildSearchText.Trim();
        var filtered = string.IsNullOrEmpty(search)
            ? DownloadBuilds
            : DownloadBuilds.Where(build =>
                build.DisplayName.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                || build.BuildId.Contains(search, StringComparison.OrdinalIgnoreCase)
                || build.ManifestId?.ToString(CultureInfo.InvariantCulture).Contains(search, StringComparison.Ordinal) == true);
        VisibleDownloadBuilds.Clear();
        foreach (var build in filtered)
        {
            VisibleDownloadBuilds.Add(build);
        }
    }

    private void PopulateSpeedrunTemplates()
    {
        var previousTemplateId = SelectedSpeedrunTemplate?.Id;
        SpeedrunTemplates.Clear();
        foreach (var template in catalog.SpeedrunTemplates)
        {
            SpeedrunTemplates.Add(template);
        }
        SelectedSpeedrunTemplate = SpeedrunTemplates.FirstOrDefault(template => string.Equals(
                                       template.Id,
                                       previousTemplateId,
                                       StringComparison.OrdinalIgnoreCase))
                                   ?? SpeedrunTemplates.FirstOrDefault();
    }

    private async Task RefreshCatalogInBackgroundAsync(
        Task initialInstanceRefresh,
        Task<GameCatalog> catalogLoad)
    {
        try
        {
            catalog = await catalogLoad;
            RebuildCustomModLinksOptions();
            ModManagement.RebuildStatusOptions();
            RebuildMarketCatalog();
            PopulateSpeedrunTemplates();
            PopulateDownloadBuilds();
            if (Directory.Exists(VersionRoot))
            {
                await initialInstanceRefresh;
                await RefreshInstancesAsync(showBusy: false);
            }
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ApplicationLog.Write(
                Path.Combine(paths.ApplicationDataRoot, "logs", "crystalfly.log"),
                "catalog-background-refresh",
                $"failed: {exception.Message}");
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
                ErrorMessage = Loc.ErrorMessageFor(exception);
            }
        }
    }

    private void OnGameConfigSaved() =>
        NotifyToast(Loc["ConfigSaved"]);

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
        ModManagement.SelectedInstalledMod = item;
        OpenModMarketForSelectedInstance();
        SelectedMarketMod = manifest;
    }

    [RelayCommand]
    private void BackToMarket() => SelectedMarketMod = null;

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
        foreach (var instance in Instances.Instances)
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
                    Loc.ErrorMessageFor(exception),
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
        if (!BuildIdentity.IsKnown(instance.BuildId))
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
    private Task RefreshAsync() => RefreshInstancesAsync(showBusy: true);

    private async Task RefreshInstancesAsync(bool showBusy)
    {
        if (!Directory.Exists(VersionRoot))
        {
            return;
        }

        if (showBusy)
        {
            IsBusy = true;
        }
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
                    bool canCompleteActiveSession = runtimeSession is null
                        && !IsGameRunning
                        && !new SystemHollowKnightProcessProbe().IsRunning();
                    var scanCatalog = catalog;
                    var scanRoot = VersionRoot;
                    var inspections = await Task.Run(async () =>
                    {
                        var records = await instanceDiscovery(
                            scanRoot,
                            scanCatalog,
                            cancellationToken).ConfigureAwait(false);
                        var isolation = new LocalLowIsolationService(
                            GetSharedLocalLowPath(),
                            paths.GetVersionDataRoot(scanRoot));
                        await isolation.InitializeBaselinesAsync(
                            records.Select(static record => record.Id),
                            allowActiveSessionCompletion: canCompleteActiveSession,
                            cancellationToken).ConfigureAwait(false);
                        var sortedRecords = records
                            .OrderBy(instance => instance.Name, StringComparer.OrdinalIgnoreCase)
                            .ToArray();
                        return await Task.WhenAll(sortedRecords.Select(async record =>
                        {
                            var loaderManager = CreateLoaderManager(record);
                            var loaderInspection = await loaderManager
                                .InspectAsync(cancellationToken).ConfigureAwait(false);
                            var loaderState = loaderInspection.State;
                            var loaderReceipt = await loaderManager
                                .GetReceiptAsync(cancellationToken).ConfigureAwait(false);
                            var modCount = (await CreateModManager(record).DiscoverAsync(
                                loaderInspection.PackageId ?? loaderState.ToString(),
                                cancellationToken).ConfigureAwait(false)).Mods.Count;
                            return (record, loaderState, loaderReceipt, modCount);
                        })).ConfigureAwait(false);
                    }, cancellationToken);
                    discovered.AddRange(inspections);
                },
                lifetimeCancellation.Token);
            lastInstanceProjection = discovered;
            Instances.Instances.Clear();
            foreach (var item in discovered)
            {
                Instances.Instances.Add(ProjectInstanceItem(item, settings));
            }

            Instances.ApplyInstanceFilter();
            SelectedInstance = Instances.Instances.FirstOrDefault(instance => instance.Id == settings.CurrentInstanceId)
                ?? Instances.Instances.FirstOrDefault();
            PopulateSpeedrunInstances();
            // Restore the speedrun selection only when the remembered instance actually is one.
            // Falling back to the first speedrun instance would otherwise overwrite the remembered
            // regular-instance selection and make "remember last instance" appear broken.
            SelectedSpeedrunInstance = Instances.SpeedrunInstances.FirstOrDefault(instance =>
                instance.Id == settings.CurrentInstanceId)
                ?? (settings.CurrentInstanceId is null ? Instances.SpeedrunInstances.FirstOrDefault() : null);
            StatusMessage = Loc["StatusReady"];
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or InvalidOperationException)
        {
            ErrorMessage = Loc.ErrorMessageFor(exception);
        }
        finally
        {
            if (showBusy)
            {
                IsBusy = false;
            }
        }
    }

    private InstanceItemViewModel ProjectInstanceItem(
        (InstanceRecord Record, LoaderState LoaderState, InstalledPackageReceipt? LoaderReceipt, int ModCount) item,
        CrystalflySettings settings)
    {
        var build = catalog.Builds.FirstOrDefault(candidate => candidate.Id == item.Record.BuildId);
        return new InstanceItemViewModel(
            item.Record,
            build?.DisplayVersion ?? Loc["UnknownBuild"],
            item.LoaderReceipt is null
                ? item.LoaderState.ToString()
                : item.LoaderReceipt.IsVerified
                    ? item.LoaderReceipt.PackageId
                    : $"{item.LoaderReceipt.PackageId} · {Loc["Unverified"]}",
            item.ModCount,
            settings.FavoriteInstanceIds.Contains(item.Record.Id, StringComparer.Ordinal));
    }

    // Re-projects the instance list from the last disk inspection when the language
    // switches so DisplayVersion/LoaderDisplay localized fragments ("Unknown build",
    // "Unverified") refresh without re-scanning the game directory. The selected
    // instances are re-resolved to their new items but the details reload is suppressed:
    // re-selecting the same instance must not clear and reload the Mod list or presets
    // that were just refreshed against the new language.
    private void ReprojectInstances()
    {
        if (lastInstanceProjection is not { } discovered || discovered.Count == 0)
        {
            return;
        }
        var selectedId = SelectedInstance?.Id;
        var speedrunId = SelectedSpeedrunInstance?.Id;
        suppressInstanceDetailsReload = true;
        try
        {
            Instances.Instances.Clear();
            foreach (var item in discovered)
            {
                Instances.Instances.Add(ProjectInstanceItem(item, settings));
            }
            Instances.ApplyInstanceFilter();
            SelectedInstance = selectedId is null
                ? Instances.Instances.FirstOrDefault()
                : Instances.Instances.FirstOrDefault(instance => instance.Id == selectedId)
                    ?? Instances.Instances.FirstOrDefault();
            PopulateSpeedrunInstances();
            SelectedSpeedrunInstance = speedrunId is null
                ? (selectedId is null ? Instances.SpeedrunInstances.FirstOrDefault() : null)
                : Instances.SpeedrunInstances.FirstOrDefault(instance => instance.Id == speedrunId)
                    ?? (selectedId is null ? Instances.SpeedrunInstances.FirstOrDefault() : null);
        }
        finally
        {
            suppressInstanceDetailsReload = false;
        }
    }

    private void PopulateSpeedrunInstances()
    {
        Instances.SpeedrunInstances.Clear();
        foreach (InstanceItemViewModel instance in Instances.Instances.Where(instance =>
                     instance.Record.Purpose == InstancePurpose.OfficialSpeedrun))
        {
            Instances.SpeedrunInstances.Add(instance);
        }
        PopulateSpeedrunSourceInstances();
    }

    private void PopulateSpeedrunSourceInstances()
    {
        string? selectedId = SelectedSpeedrunSourceInstance?.Id;
        SpeedrunSourceInstances.Clear();
        if (SelectedSpeedrunTemplate is null)
        {
            SelectedSpeedrunSourceInstance = null;
            return;
        }
        foreach (InstanceItemViewModel instance in Instances.Instances.Where(instance =>
                     instance.Record.Purpose == InstancePurpose.General
                     && string.Equals(instance.Record.BuildId, SelectedSpeedrunTemplate.BuildId, StringComparison.Ordinal)
                     && instance.ModCount == 0
                     && string.Equals(instance.LoaderDisplay, LoaderState.Vanilla.ToString(), StringComparison.Ordinal)))
        {
            SpeedrunSourceInstances.Add(instance);
        }
        SelectedSpeedrunSourceInstance = SpeedrunSourceInstances.FirstOrDefault(instance => instance.Id == selectedId)
            ?? SpeedrunSourceInstances.FirstOrDefault();
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
            HasBlockingQueueTasks = DownloadCenter.DownloadQueue.Groups.Any(group =>
                string.Equals(
                    group.TargetInstanceId,
                    instanceId,
                    StringComparison.OrdinalIgnoreCase)
                && group.State is DownloadQueueGroupState.Pending
                    or DownloadQueueGroupState.Running
                    or DownloadQueueGroupState.WaitingForNetwork
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
                    if (record.SpeedrunTemplateId is not null)
                    {
                        SpeedrunReportPath = null;
                        await VerifySpeedrunLaunchAsync(record);
                    }
                    else
                    {
                        await EnsureTransactionsHealthyAsync();
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
            if (!(SpeedrunReminderIsError && SpeedrunReportPath is not null))
            {
                ErrorMessage = Loc.ErrorMessageFor(exception);
            }
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
        SteamStatus = Loc["SteamConnecting"];
        IsSteamLoggedIn = false;
        Interlocked.Exchange(ref steamQrChallengeReceived, 0);
        var gateTaken = false;
        try
        {
            await steamConnectionGate.WaitAsync(signInCancellation.Token);
            gateTaken = true;
            await DisposeCurrentSteamSessionAsync();
            RefreshTokenCredential credential = await ConnectWithQrRetryAsync(signInCancellation.Token);
            IsSteamLoggedIn = true;
            SteamStatus = credential.AccountName;
            QrCodeImage = null;
            await DownloadCenter.DownloadQueue.InitializeAsync(lifetimeCancellation.Token);
            await DownloadCenter.DownloadQueue.ResumeSteamDownloadsAsync(lifetimeCancellation.Token);
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

    [RelayCommand]
    private void TogglePasswordLogin()
    {
        IsPasswordLoginVisible = !IsPasswordLoginVisible;
        if (!IsPasswordLoginVisible)
        {
            SteamPassword = string.Empty;
        }
    }

    [RelayCommand]
    private async Task SignInWithPasswordAsync()
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
        if (string.IsNullOrWhiteSpace(SteamUsername) || string.IsNullOrEmpty(SteamPassword))
        {
            ErrorMessage = Loc["SteamCredentialsRequired"];
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
        SteamStatus = Loc["SteamConnecting"];
        IsSteamLoggedIn = false;
        var gateTaken = false;
        try
        {
            await steamConnectionGate.WaitAsync(signInCancellation.Token);
            gateTaken = true;
            await DisposeCurrentSteamSessionAsync();
            RefreshTokenCredential credential = await ConnectWithCredentialsCoreAsync(signInCancellation.Token);
            await credentialStore.SaveAsync(
                new SteamUsernameCredential(SteamUsername, SteamPassword),
                signInCancellation.Token);
            IsSteamLoggedIn = true;
            SteamStatus = credential.AccountName;
            QrCodeImage = null;
            IsPasswordLoginVisible = false;
            await DownloadCenter.DownloadQueue.InitializeAsync(lifetimeCancellation.Token);
            await DownloadCenter.DownloadQueue.ResumeSteamDownloadsAsync(lifetimeCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            SteamStatus = "Not signed in";
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

    private async Task<RefreshTokenCredential> ConnectWithCredentialsCoreAsync(CancellationToken cancellationToken)
    {
        if (passwordSignInOverride is not null)
        {
            return await passwordSignInOverride(SteamUsername, SteamPassword, cancellationToken);
        }
        steamSession = CreateSteamSession(includeQrEvents: false);
        return await steamSession.ConnectWithCredentialsAsync(
            SteamUsername,
            SteamPassword,
            cancellationToken);
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
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    await DisposeCurrentSteamSessionAsync();
                    steamSession = CreateSteamSession(includeQrEvents: false);
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetimeCancellation.Token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(20));
                    var credential = await steamSession.ConnectWithStoredTokenAsync(timeout.Token);
                    IsSteamLoggedIn = true;
                    SteamStatus = credential.AccountName;
                    SteamNetworkStatus = FormatSteamNetworkStatus(systemProxy.Current);
                    await DownloadCenter.DownloadQueue.InitializeAsync(lifetimeCancellation.Token);
                    await DownloadCenter.DownloadQueue.ResumeSteamDownloadsAsync(lifetimeCancellation.Token);
                    return;
                }
                catch (Exception exception) when (
                    attempt < 3
                    && IsTransientSteamNetworkFailure(exception)
                    && !lifetimeCancellation.IsCancellationRequested)
                {
                    await DisposeCurrentSteamSessionAsync();
                    SteamStatus = Loc["SteamNetworkReconnecting"];
                    await Task.Delay(TimeSpan.FromSeconds(attempt), lifetimeCancellation.Token);
                }
            }
        }
        catch (Exception exception) when (!lifetimeCancellation.IsCancellationRequested)
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
            SteamNetworkStatus = FormatSteamNetworkFailure(exception);
        }
        finally
        {
            if (gateTaken)
            {
                steamConnectionGate.Release();
            }
        }
    }

    private async Task<RefreshTokenCredential> ConnectWithQrRetryAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                if (qrSignInOverride is not null)
                {
                    return await qrSignInOverride(cancellationToken);
                }
                steamSession = CreateSteamSession(includeQrEvents: true);
                return await steamSession.ConnectWithQrAsync(cancellationToken);
            }
            catch (Exception exception) when (
                attempt < 3
                && !cancellationToken.IsCancellationRequested
                && Volatile.Read(ref steamQrChallengeReceived) == 0
                && IsTransientSteamNetworkFailure(exception))
            {
                await DisposeCurrentSteamSessionAsync();
                SteamStatus = Loc["SteamNetworkReconnecting"];
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken);
            }
        }

        throw new InvalidOperationException("Steam QR sign-in retry loop ended without a result.");
    }

    [RelayCommand]
    private async Task RefreshSteamNetworkAsync()
    {
        if (IsOfflineMode || lifetimeCancellation.IsCancellationRequested)
        {
            SteamNetworkStatus = Loc["OfflineMode"];
            return;
        }

        SystemProxySnapshot previous = systemProxy.Current;
        systemProxy.Refresh();
        SteamNetworkStatus = FormatSteamNetworkStatus(systemProxy.Current);
        if (File.Exists(Path.Combine(paths.ApplicationDataRoot, "steam-token.dat")))
        {
            if (previous == systemProxy.Current)
            {
                QueueSteamReconnect();
            }
            Task reconnect;
            lock (steamReconnectQueueLock)
            {
                reconnect = steamReconnectTask;
            }
            await reconnect;
            return;
        }

        Volatile.Read(ref steamSignInCancellation)?.Cancel();
        Task? signIn = SignInWithQrCommand.ExecutionTask;
        if (signIn is not null)
        {
            await signIn;
        }
        await SignInWithQrCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task SignOutSteamAsync()
    {
        try
        {
            await DownloadCenter.DownloadQueue.InitializeAsync(lifetimeCancellation.Token);
            await DownloadCenter.DownloadQueue.PauseSteamDownloadsAsync(lifetimeCancellation.Token);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            ErrorMessage = $"Steam: {exception.Message}";
        }
        finally
        {
            try
            {
                steamSession?.SignOut();
            }
            catch (Exception exception)
            {
                ErrorMessage = $"Steam: {exception.Message}";
            }
            IsSteamLoggedIn = false;
            SteamStatus = "Not signed in";
            QrCodeImage = null;
            SteamUsername = string.Empty;
            SteamPassword = string.Empty;
            IsPasswordLoginVisible = false;
            try
            {
                credentialStore.Delete();
            }
            catch (Exception exception)
            {
                ErrorMessage = $"Steam: {exception.Message}";
            }
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
        if (SelectedInstance is null)
        {
            ErrorMessage = Loc["NoInstance"];
            return;
        }

        await RunInstanceMutationAsync(async record =>
        {
            var manager = CreateLoaderManager(record);
            var receipt = await manager.GetReceiptAsync();
            if (receipt is not null)
            {
                var receiptedLoader = FindCatalogLoader(receipt.PackageId, record.BuildId)
                    ?? throw new InvalidOperationException(Loc["LoaderRepairPackageUnavailable"]);
                await manager.RepairFromUriAsync(receiptedLoader);
                return;
            }

            var loaderIds = (await CreateModManager(record).GetInstalledAsync())
                .Select(mod => mod.LoaderId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (loaderIds.Length != 1
                || FindCatalogLoader(loaderIds[0], record.BuildId) is not { } orphanedLoader)
            {
                throw new InvalidOperationException(Loc["LoaderRepairIdentityUnknown"]);
            }

            if (await manager.GetStateAsync() == LoaderState.Vanilla)
            {
                await manager.InstallFromUriAsync(orphanedLoader);
            }
            else
            {
                await manager.RecoverFromUriAsync(orphanedLoader);
            }
        });
    }

    [RelayCommand]
    private async Task UninstallLoaderAsync()
    {
        if (SelectedInstance is null)
        {
            ErrorMessage = Loc["NoInstance"];
            return;
        }
        await RunInstanceMutationAsync(async record =>
        {
            if ((await CreateModManager(record).GetInstalledAsync()).Count != 0)
            {
                throw new InvalidOperationException(Loc["LoaderUninstallBlockedByMods"]);
            }
            await CreateLoaderManager(record).UninstallAsync();
        });
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
            SetLoaderVerificationStatus("UnverifiedLocalLoader");
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
            ErrorMessage = Loc.ErrorMessageFor(exception);
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
    private async Task DeleteSnapshotAsync()
    {
        if (SelectedInstance is null || SelectedSnapshot is null)
        {
            ErrorMessage = Loc["SelectSnapshot"];
            return;
        }
        var snapshotId = SelectedSnapshot.Id;
        var deleted = false;
        await RunInstanceMutationAsync(async record =>
        {
            await CreateSnapshotService().DeleteAsync(record.Id, snapshotId);
            deleted = true;
        });
        if (deleted && SelectedSnapshot?.Id == snapshotId)
        {
            SelectedSnapshot = null;
        }
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
                ErrorMessage = Loc.ErrorMessageFor(exception);
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
        string? createdRuntimePatchesConfigurationPath = null;
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
                    var source = await FindVanillaSourceAsync(
                            SelectedSpeedrunTemplate.BuildId,
                            SelectedSpeedrunSourceInstance?.Id)
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
                        LoadNormaliserSeconds = null
                    };
                    var isolation = new LocalLowIsolationService(
                        GetSharedLocalLowPath(),
                        paths.GetVersionDataRoot(VersionRoot));
                    createdRuntimePatchesConfigurationPath = GetRuntimePatchesConfigurationPath(clone);
                    await isolation.InitializeBaselinesAsync([clone.Id]);
                    await RuntimePatchesConfiguration.WriteAsync(
                        createdRuntimePatchesConfigurationPath,
                        new RuntimePatchesConfiguration());
                    await InstanceSidecar.SaveAsync(clone);
                    createdInstance = clone;
                    createdRoot = null;
                },
                lifetimeCancellation.Token);
            var clone = createdInstance
                ?? throw new InvalidOperationException("The speedrun instance was not created.");
            SetSpeedrunReminder(SelectedSpeedrunTemplate.IsOfficial
                && catalog.SpeedrunFileManifests.Any(manifest => manifest.Id == SelectedSpeedrunTemplate.FileManifestId)
                    ? "SpeedrunNeedsVerification"
                    : "SpeedrunUnverified");
            SpeedrunReportPath = null;
            SpeedrunReminderIsError = false;
            SpeedrunEnvironmentName = string.Empty;
            await RefreshAsync();
            SelectedInstance = Instances.Instances.FirstOrDefault(instance => instance.Id == clone.Id);
            SelectedSpeedrunInstance = Instances.SpeedrunInstances.FirstOrDefault(instance => instance.Id == clone.Id);
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
            if (!preserveForRecovery && createdRuntimePatchesConfigurationPath is not null)
            {
                try
                {
                    string? configurationDirectory = Path.GetDirectoryName(createdRuntimePatchesConfigurationPath);
                    if (configurationDirectory is not null && Directory.Exists(configurationDirectory))
                    {
                        Directory.Delete(configurationDirectory, recursive: true);
                    }
                }
                catch (Exception cleanupException) when (cleanupException is IOException or UnauthorizedAccessException)
                {
                    ErrorMessage = Loc.ErrorMessageFor(cleanupException);
                }
            }
            if (!preserveForRecovery && createdRoot is not null && Directory.Exists(createdRoot))
            {
                try
                {
                    Directory.Delete(createdRoot, recursive: true);
                }
                catch (Exception cleanupException) when (cleanupException is IOException or UnauthorizedAccessException)
                {
                    ErrorMessage = Loc.ErrorMessageFor(cleanupException);
                }
            }
            ErrorMessage = string.IsNullOrWhiteSpace(ErrorMessage)
                ? Loc.ErrorMessageFor(exception)
                : $"{Loc.ErrorMessageFor(exception)} {ErrorMessage}";
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

    partial void OnSearchTextChanged(string value) => Instances.ApplyInstanceFilter();

    partial void OnMarketSearchTextChanged(string value) => ApplyMarketFilters();

    partial void OnSelectedMarketBuildOptionChanged(SettingOption<string>? value) => ApplyMarketFilters();

    partial void OnSelectedMarketLoaderOptionChanged(SettingOption<string>? value) => ApplyMarketFilters();

    partial void OnSelectedMarketSourceOptionChanged(SettingOption<string>? value) => ApplyMarketFilters();

    partial void OnSelectedMarketTagOptionChanged(SettingOption<string>? value) => ApplyMarketFilters();

    partial void OnSelectedMarketActivityOptionChanged(SettingOption<MarketActivityFilter>? value) =>
        ApplyMarketFilters();

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
        // A language-switch re-projection re-resolves the selection to a new item
        // instance without clearing and reloading the already-localized details.
        if (suppressInstanceDetailsReload)
        {
            return;
        }
        long generation = Interlocked.Increment(ref detailsLoadGeneration);
        var previousLoadCancellation = Interlocked.Exchange(ref detailsLoadCancellation, null);
        previousLoadCancellation?.Cancel();
        previousLoadCancellation?.Dispose();
        GameConfig = null;
        SaveEditor = null;
        IsLoadingInstanceDetails = value is not null;
        AvailableLoaders.Clear();
        SelectedLoader = null;
        ModManagement.ClearAvailableMods();
        ModManagement.ClearInstalledMods();
        Snapshots.Clear();
        lock (presetCollectionGate)
        {
            ModPresets.Clear();
            SelectedPreset = null;
            VisibleModPacks.Clear();
            VisibleSelectedModPackEntries.Clear();
        }
        HasPresetRestorePoint = false;
        UpdateSelectedMarketInstallationState();
        currentLoaderInspection = new LoaderInspection
        {
            State = LoaderState.Vanilla,
            Ownership = LoaderOwnership.None
        };
        InstanceLogs.Clear();
        SelectedLogFile = null;
        LaunchPreflight = new(false, false, false, false);
        Settings.QueueBackgroundAppearanceRefresh();
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
            ModManagement.ReplaceAvailableMods(value.Record);
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

    [RelayCommand]
    private async Task ReinstallSpeedrunEnvironmentAsync()
    {
        if (SelectedSpeedrunInstance is not { } selected
            || !IsSelectedSpeedrunCurrent
            || SelectedSpeedrunTemplate is null
            || IsMutationBlocked())
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await instanceOperationCoordinator.RunAsync(
                selected.Id,
                async _ =>
                {
                    await EnsureTransactionsHealthyAsync();
                    if (new SystemHollowKnightProcessProbe().IsRunning())
                    {
                        throw new InvalidOperationException(Loc["CloseGameFirst"]);
                    }
                    await new SpeedrunEnvironmentProvisioner().ProvisionAsync(new SpeedrunProvisioningRequest
                    {
                        Catalog = catalog,
                        TemplateId = SelectedSpeedrunTemplate.Id,
                        InstanceRoot = selected.RootPath,
                        TransactionRoot = Path.Combine(paths.GetVersionDataRoot(VersionRoot), "transactions"),
                        PackageCacheRoot = Path.Combine(paths.GetVersionDataRoot(VersionRoot), "packages"),
                        HttpClient = packageHttpClient
                    });
                    await RuntimePatchesConfiguration.WriteAsync(
                        GetRuntimePatchesConfigurationPath(selected.Record),
                        RuntimePatchesPolicy.Normalize(
                            selected.Record.BuildId,
                            new RuntimePatchesConfiguration
                            {
                                ScreenShakeModifier = RuntimePatchesScreenShakeModifier,
                                MiniSaveStates = RuntimePatchesMiniSaveStates,
                                FasterIntroSkip = RuntimePatchesFasterIntroSkip,
                                TextMasher = RuntimePatchesTextMasher
                            }));
                },
                lifetimeCancellation.Token);
            SetSpeedrunReminder("SpeedrunNeedsVerification");
            SpeedrunReportPath = null;
            SpeedrunReminderIsError = false;
            await RefreshAsync();
            SelectedSpeedrunInstance = Instances.SpeedrunInstances.FirstOrDefault(instance => instance.Id == selected.Id);
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or InvalidOperationException
            or UnauthorizedAccessException)
        {
            ErrorMessage = Loc.ErrorMessageFor(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedSpeedrunInstanceChanged(InstanceItemViewModel? value)
    {
        SpeedrunReportPath = null;
        SpeedrunReminderIsError = false;
        SetSpeedrunReminder(value is null
            ? null
            : RuntimePatchesPolicy.IsLegacyTemplate(value.Record.SpeedrunTemplateId)
                ? "SpeedrunTemplateExpired"
                : "SpeedrunNeedsVerification");
        if (value is null)
        {
            RuntimePatchesScreenShakeModifier = false;
            RuntimePatchesMiniSaveStates = false;
            RuntimePatchesFasterIntroSkip = false;
            RuntimePatchesTextMasher = false;
            return;
        }

        SelectedInstance = Instances.Instances.FirstOrDefault(instance => instance.Id == value.Id) ?? value;
        SelectedSpeedrunTemplate = catalog.SpeedrunTemplates.SingleOrDefault(template =>
            string.Equals(template.Id, value.Record.SpeedrunTemplateId, StringComparison.Ordinal))
            ?? catalog.SpeedrunTemplates.SingleOrDefault(template =>
                RuntimePatchesPolicy.IsLegacyTemplate(value.Record.SpeedrunTemplateId)
                && string.Equals(template.BuildId, value.Record.BuildId, StringComparison.Ordinal)
                && RuntimePatchesPolicy.IsCurrentTemplate(template.Id));
        _ = LoadRuntimePatchesConfigurationAsync(value.Record);
    }

    private async Task LoadRuntimePatchesConfigurationAsync(InstanceRecord instance)
    {
        loadingRuntimePatchesConfiguration = true;
        try
        {
            RuntimePatchesConfiguration configuration = await RuntimePatchesConfiguration.ReadAsync(
                GetRuntimePatchesConfigurationPath(instance));
            configuration = RuntimePatchesPolicy.Normalize(instance.BuildId, configuration);
            RuntimePatchesScreenShakeModifier = configuration.ScreenShakeModifier;
            RuntimePatchesMiniSaveStates = configuration.MiniSaveStates;
            RuntimePatchesFasterIntroSkip = configuration.FasterIntroSkip;
            RuntimePatchesTextMasher = configuration.TextMasher;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            RuntimePatchesScreenShakeModifier = false;
            RuntimePatchesMiniSaveStates = false;
            RuntimePatchesFasterIntroSkip = false;
            RuntimePatchesTextMasher = false;
            if (!IsSelectedSpeedrunLegacy)
            {
                SetSpeedrunReminder("SpeedrunConfigurationInvalid", suffix: $": {Loc.ErrorMessageFor(exception)}");
                SpeedrunReminderIsError = true;
            }
        }
        finally
        {
            loadingRuntimePatchesConfiguration = false;
        }
    }

    private async Task SaveRuntimePatchesConfigurationAsync()
    {
        if (loadingRuntimePatchesConfiguration
            || SelectedSpeedrunInstance is not { } selected
            || !IsSelectedSpeedrunCurrent
            || IsGameRunning)
        {
            return;
        }
        if (isGameProcessRunning())
        {
            ErrorMessage = Loc["CloseGameFirst"];
            await LoadRuntimePatchesConfigurationAsync(selected.Record);
            return;
        }
        RuntimePatchesConfiguration configuration = RuntimePatchesPolicy.Normalize(
            selected.Record.BuildId,
            new RuntimePatchesConfiguration
            {
                ScreenShakeModifier = RuntimePatchesScreenShakeModifier,
                MiniSaveStates = RuntimePatchesMiniSaveStates,
                FasterIntroSkip = RuntimePatchesFasterIntroSkip,
                TextMasher = RuntimePatchesTextMasher
            });
        string path = GetRuntimePatchesConfigurationPath(selected.Record);
        await runtimePatchesConfigurationSaveLock.WaitAsync();
        try
        {
            await RuntimePatchesConfiguration.WriteAsync(path, configuration);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ErrorMessage = Loc.ErrorMessageFor(exception);
        }
        finally
        {
            runtimePatchesConfigurationSaveLock.Release();
        }
    }

    private string GetRuntimePatchesConfigurationPath(InstanceRecord instance) =>
        Path.Combine(
            new LocalLowIsolationService(
                GetSharedLocalLowPath(),
                paths.GetVersionDataRoot(VersionRoot))
                .GetInstanceLocalLowPath(instance.Id),
            RuntimePatchesConfiguration.FileName);

    partial void OnRuntimePatchesScreenShakeModifierChanged(bool value) => _ = SaveRuntimePatchesConfigurationAsync();

    partial void OnRuntimePatchesMiniSaveStatesChanged(bool value) => _ = SaveRuntimePatchesConfigurationAsync();

    partial void OnRuntimePatchesFasterIntroSkipChanged(bool value) => _ = SaveRuntimePatchesConfigurationAsync();

    partial void OnRuntimePatchesTextMasherChanged(bool value) => _ = SaveRuntimePatchesConfigurationAsync();

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
        PopulateSpeedrunSourceInstances();
    }


    private void RequestInstalledModRemovalFromGraph(string modId)
    {
        var mod = ModManagement.InstalledMods.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, modId, StringComparison.OrdinalIgnoreCase));
        if (mod is null || !mod.CanUninstall)
        {
            return;
        }

        ModManagement.SelectedInstalledMod = mod;
        GraphModRemovalRequested?.Invoke();
    }

    private void ApplyMarketFilters()
    {
        VisibleMarketMods.Clear();
        VisibleMarketDisplayMods.Clear();
        foreach (var item in MarketDisplayMods.Where(item =>
            item.MatchesSearch(MarketSearchText)
            && MatchesMarketCompatibility(item)
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

    private bool MatchesMarketCompatibility(MarketModItemViewModel item)
    {
        var buildId = SelectedMarketBuildOption?.Value;
        var loaderId = SelectedMarketLoaderOption?.Value;
        if (!string.IsNullOrEmpty(buildId) && !string.IsNullOrEmpty(loaderId))
        {
            return ModCatalogCompatibility.Supports(item.Manifest, buildId, loaderId);
        }
        return (string.IsNullOrEmpty(buildId)
                || item.SupportedBuildIds.Contains(buildId, StringComparer.OrdinalIgnoreCase))
            && (string.IsNullOrEmpty(loaderId)
                || item.CompatibleLoaderIds.Contains(loaderId, StringComparer.OrdinalIgnoreCase));
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
            MarketDisplayMods.SelectMany(mod => mod.CompatibleLoaderIds),
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

    private void RebuildSettingOptions() => Settings.RebuildSettingOptions();

    private void RebuildCustomModLinksOptions() => Settings.RebuildCustomModLinksOptions();

    private void ApplyLanguage(UiLanguage language)
    {
        // Reuse the existing instance instead of swapping in a new one: bindings that read
        // Loc[key] and view models that captured the instance keep working, and every
        // computed string property can be refreshed against the new language.
        var previousReadyText = Loc["StatusReady"];
        Loc.Apply(language);
        OnPropertyChanged(nameof(Loc));
        // Computed properties capture Loc[key] values at evaluation time; notify them all
        // so they re-evaluate against the new language, and re-project every item list
        // whose view models snapshot localized display names.
        NotifyPreflightLabels();
        OnPropertyChanged(nameof(SelectedModContentStatusText));
        OnPropertyChanged(nameof(IsProtocolRegistered));
        OnPropertyChanged(nameof(ProtocolRegistrationStatus));
        OnPropertyChanged(nameof(SelectedSpeedrunTechnicalStatus));
        RefreshApplicationUpdateText();
        NotifyOfficialCatalogLabels();
        RefreshSpeedrunActivityLocalization();
        RefreshSpeedrunReminder();
        RefreshLoaderVerificationStatus();
        SteamNetworkStatus = FormatSteamNetworkStatus(systemProxy.Current);
        RebuildPresetModeOptions();
        RefreshPresetCopyName();
        RefreshPresetApplySteps();
        RebuildMarketCatalog();
        PopulateDownloadBuilds();
        ReprojectInstances();
        Settings.RefreshLocalization();
        ModManagement.RefreshLocalization();
        UpdateSelectedMarketInstallationState();
        Instances.RefreshGameDirectoryLabels();
        DownloadCenter.ApplyLanguage(Loc);
        if (DownloadCenter.DownloadQueueGroups.Count > 0)
        {
            DownloadCenter.QueueDownloadQueueProjection(DownloadCenter.DownloadQueue.Groups);
        }

        // StatusMessage is a computed snapshot; refresh it when it shows the ready text so a
        // language switch does not leave the old language on the status bar.
        if (string.Equals(StatusMessage, previousReadyText, StringComparison.Ordinal))
        {
            StatusMessage = Loc["StatusReady"];
        }

        if (Application.Current is { } application)
        {
            SemiTheme.OverrideLocaleResources(application, Loc.Culture);
            UrsaSemiTheme.OverrideLocaleResources(application, Loc.Culture);
        }
    }

    // Speedrun reminder/status lines snapshot Loc[key] at set time; remember the key and
    // the exact text that was set so a language switch can re-render the current line
    // without resurrecting a stale one that a later operation already replaced.
    private void SetSpeedrunReminder(string? localizationKey, bool visible = true, string? suffix = null)
    {
        speedrunReminderLocalizationKey = localizationKey;
        speedrunReminderSuffix = suffix;
        speedrunReminderVisible = visible;
        var status = localizationKey is null ? string.Empty : Loc[localizationKey] + (suffix ?? string.Empty);
        SpeedrunStatus = status;
        speedrunReminderTrackedStatus = status;
        SpeedrunReminderText = visible ? status : string.Empty;
    }

    private void RefreshSpeedrunReminder()
    {
        if (speedrunReminderLocalizationKey is not { } key
            || speedrunReminderTrackedStatus is null
            || !string.Equals(SpeedrunStatus, speedrunReminderTrackedStatus, StringComparison.Ordinal))
        {
            return;
        }
        var status = Loc[key] + (speedrunReminderSuffix ?? string.Empty);
        SpeedrunStatus = status;
        speedrunReminderTrackedStatus = status;
        SpeedrunReminderText = speedrunReminderVisible ? status : string.Empty;
    }

    private void SetLoaderVerificationStatus(string? localizationKey)
    {
        loaderVerificationLocalizationKey = localizationKey;
        loaderVerificationTrackedStatus = LoaderVerificationStatus;
        LoaderVerificationStatus = localizationKey is null ? string.Empty : Loc[localizationKey];
    }

    private void RefreshLoaderVerificationStatus()
    {
        if (loaderVerificationLocalizationKey is not { } key
            || loaderVerificationTrackedStatus is null
            || !string.Equals(LoaderVerificationStatus, loaderVerificationTrackedStatus, StringComparison.Ordinal))
        {
            return;
        }
        LoaderVerificationStatus = Loc[key];
        loaderVerificationTrackedStatus = LoaderVerificationStatus;
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
            : ModManagement.InstalledMods.FirstOrDefault(item =>
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
            SelectedModContentError = Loc.ErrorMessageFor(exception);
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
                SelectedModContentError = Loc.ErrorMessageFor(exception);
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
        NotifyToast(StatusMessage);
    }

    private void NotifyOfficialCatalogLabels()
    {
        OnPropertyChanged(nameof(OfficialModCatalogStatus));
        OnPropertyChanged(nameof(OfficialModCatalogSummary));
        OnPropertyChanged(nameof(OfficialModCatalogError));
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
            ErrorMessage = Loc.ErrorMessageFor(exception);
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
            ErrorMessage = Loc.ErrorMessageFor(exception);
        }
    }

    private static void ApplyTheme(UiTheme theme, string accentColor)
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
        AccentThemeResources.Apply(accentColor);
    }

    private async Task SaveSettingsWithLockAsync(
        CrystalflySettings value,
        CancellationToken cancellationToken)
    {
        await settingsSaveLock.WaitAsync(cancellationToken);
        try
        {
            await CrystalflySettingsStore.SaveAsync(settingsPath, value, cancellationToken);
        }
        finally
        {
            settingsSaveLock.Release();
        }
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            await SaveSettingsWithLockAsync(settings, CancellationToken.None);
        }
        catch (Exception exception) when (IsExpectedSettingsException(exception))
        {
            ErrorMessage = Loc.ErrorMessageFor(exception);
        }
    }

    private Task QueueSettingsSave()
    {
        lock (settingsSaveQueueLock)
        {
            if (settingsSavesClosed)
            {
                return settingsSaveQueue;
            }
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
                BuildIdentity.IsKnown(record.BuildId),
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

            cancellationToken.ThrowIfCancellationRequested();
            CurrentLoaderState = loaderState;
            currentLoaderInspection = loaderInspection;
            var orphanedLoaderIds = loaderReceipt is null && loaderState == LoaderState.Drifted
                ? installed
                    .Select(mod => mod.LoaderId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                : [];
            var canRecoverLoaderReceipt = orphanedLoaderIds.Length == 1
                && FindCatalogLoader(orphanedLoaderIds[0], record.BuildId) is not null;
            SetLoaderVerificationStatus(canRecoverLoaderReceipt
                ? "LoaderReceiptRecoveryAvailable"
                : loaderReceipt is null
                ? loaderState switch
                {
                    LoaderState.BepInEx or LoaderState.Drifted => "ExternalLoaderBlocked",
                    LoaderState.Conflict => "LoaderConflict",
                    _ => null
                }
                : loaderReceipt.IsVerified
                    ? "VerifiedCatalogLoader"
                    : "UnverifiedLocalLoader");
            CanAdoptExternalContent = loaderReceipt is null
                && loaderInspection.Ownership == LoaderOwnership.External
                || discovery.Mods.Any(mod => mod.Ownership == ModOwnership.External);
            ExternalModAdoptCount = discovery.Mods.Count(mod => mod.Ownership == ModOwnership.External);
            ModManagement.LoadInstalledMods(discovery.Mods, installed, modHealthReports, record);
            UpdateSelectedMarketInstallationState();
            Snapshots.Clear();
            foreach (var snapshot in snapshots)
            {
                Snapshots.Add(snapshot);
            }
            lock (presetCollectionGate)
            {
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
                RefreshModPackWorkspace();
            }
            HasPresetRestorePoint = hasPresetRestorePoint;
            InstanceLogs.Clear();
            foreach (var log in logs)
            {
                InstanceLogs.Add(log);
            }
            SelectedLogFile = InstanceLogs.FirstOrDefault();
            LaunchPreflight = preflight;
            if (generation == Volatile.Read(ref detailsLoadGeneration))
            {
                _ = PromptAdoptExternalContentAsync(record);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (IsExpectedInstanceDetailsException(exception))
        {
            ErrorMessage = Loc.ErrorMessageFor(exception);
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

    private async Task PromptAdoptExternalContentAsync(InstanceRecord record)
    {
        if (!CanAdoptExternalContent
            || string.Equals(adoptPromptedForInstance, record.Id, StringComparison.Ordinal)
            || externalContentConfirmPrompt is null)
        {
            return;
        }
        string message = ExternalModAdoptCount == 0
            ? Loc["AdoptExternalContentMessageNoMods"]
            : string.Format(
                Loc["AdoptExternalContentMessage"],
                ExternalModAdoptCount);
        bool confirmed = await externalContentConfirmPrompt(
            Loc["AdoptExternalContentTitle"],
            message,
            Loc["AdoptExternalContent"]);
        if (confirmed)
        {
            // Only mark the instance as prompted after the user confirms, so a
            // declined prompt can be reconsidered the next time the instance is
            // selected instead of being silently skipped forever.
            adoptPromptedForInstance = record.Id;
            await AdoptExternalContentCoreAsync(record);
        }
    }

    [RelayCommand]
    private async Task AdoptExternalContentAsync()
    {
        if (SelectedInstance?.Record is not { } record || !CanAdoptExternalContent)
        {
            return;
        }
        if (externalContentConfirmPrompt is not null)
        {
            string message = ExternalModAdoptCount == 0
                ? Loc["AdoptExternalContentMessageNoMods"]
                : string.Format(
                    Loc["AdoptExternalContentMessage"],
                    ExternalModAdoptCount);
            bool confirmed = await externalContentConfirmPrompt(
                Loc["AdoptExternalContentTitle"],
                message,
                Loc["AdoptExternalContent"]);
            if (!confirmed)
            {
                return;
            }
        }
        await AdoptExternalContentCoreAsync(record);
    }

    private async Task AdoptExternalContentCoreAsync(InstanceRecord record)
    {
        var loaderManager = CreateLoaderManager(record);
        var modManager = CreateModManager(record);
        var failures = new List<string>();
        try
        {
            if (loaderManager.GetReceiptAsync(lifetimeCancellation.Token).GetAwaiter().GetResult() is null)
            {
                var inspection = await loaderManager.InspectAsync(lifetimeCancellation.Token);
                if (inspection.Ownership == LoaderOwnership.External
                    && (inspection.State is LoaderState.BepInEx or LoaderState.Drifted))
                {
                    try
                    {
                        await loaderManager.AdoptExternalAsync(lifetimeCancellation.Token);
                    }
                    catch (Exception exception) when (exception is IOException
                        or InvalidDataException
                        or InvalidOperationException
                        or UnauthorizedAccessException)
                    {
                        failures.Add($"Loader: {exception.Message}");
                    }
                }
            }

            var discovery = await modManager.DiscoverAsync(
                await GetLoaderIdForDiscoveryAsync(record, loaderManager),
                lifetimeCancellation.Token);
            if (discovery.ExternalMods.Count != 0)
            {
                failures.AddRange(await modManager.TakeOverAllExternalAsync(
                    discovery, lifetimeCancellation.Token));
                failures.AddRange(await MatchTakenOverModsToCatalogAsync(
                    record, modManager, lifetimeCancellation.Token));
            }
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or InvalidOperationException
            or UnauthorizedAccessException)
        {
            failures.Add(exception.Message);
        }

        await RefreshAsync();
        if (SelectedInstance?.Id != record.Id)
        {
            SelectedInstance = Instances.Instances.FirstOrDefault(instance => instance.Id == record.Id)
                ?? new InstanceItemViewModel(record, record.BuildId, record.BuildId, 0);
        }
        await LoadInstanceDetailsAsync(
            record,
            Interlocked.Increment(ref detailsLoadGeneration),
            lifetimeCancellation.Token);
        if (failures.Count == 0)
        {
            StatusMessage = Loc["AdoptExternalContentDone"];
        }
        else
        {
            ErrorMessage = Loc["AdoptExternalContentFailed"] + " " + string.Join("; ", failures);
        }
    }

    private async Task<string> GetLoaderIdForDiscoveryAsync(
        InstanceRecord record,
        LoaderManager loaderManager)
    {
        var inspection = await loaderManager.InspectAsync(lifetimeCancellation.Token);
        return inspection.PackageId ?? inspection.State switch
            {
                LoaderState.BepInEx => "bepinex-external",
                LoaderState.ModdingApi => "modding-api-external",
                _ => inspection.State.ToString()
            };
    }

    private async Task<IReadOnlyList<string>> MatchTakenOverModsToCatalogAsync(
        InstanceRecord record,
        ModManager modManager,
        CancellationToken cancellationToken)
    {
        var failures = new List<string>();
        var installed = await modManager.GetInstalledAsync(cancellationToken);
        var projectedCatalog = ModCatalogCompatibility.ProjectForBuild(catalog, record.BuildId);
        foreach (var receipt in installed.Where(receipt =>
                     receipt.Ownership == ModOwnership.LocalTakenOver
                     && receipt.Dependencies.Count == 0))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ModDiscoveryEntry external = new()
            {
                Id = receipt.Id,
                Name = receipt.Name,
                LoaderId = receipt.LoaderId,
                InstallRoot = receipt.InstallRoot,
                Enabled = receipt.Enabled,
                Ownership = ModOwnership.LocalTakenOver,
                Files = receipt.Files.Select(file => file.RelativePath).ToArray(),
                EntryFiles = receipt.EntryFiles
            };
            var candidates = ModCatalogMatcher.Match(external, record.BuildId, projectedCatalog.Mods);
            ModManifest? selected = candidates.Count == 1
                ? candidates[0]
                : candidates.Count > 1 && catalogMatchPrompt is not null
                    ? await catalogMatchPrompt(candidates, receipt.Name)
                    : null;
            if (selected is null)
            {
                continue;
            }
            try
            {
                await modManager.RelinkReceiptToCatalogAsync(receipt.Id, selected, lifetimeCancellation.Token);
            }
            catch (Exception exception) when (exception is IOException
                or InvalidDataException
                or InvalidOperationException
                or UnauthorizedAccessException)
            {
                failures.Add($"{receipt.Name}: {exception.Message}");
            }
        }
        return failures;
    }

    [RelayCommand]
    private async Task MatchSelectedModToCatalogAsync()
    {
        if (SelectedInstance?.Record is not { } record
            || ModManagement.SelectedInstalledMod is not { } selected)
        {
            return;
        }
        var modManager = CreateModManager(record);
        var installed = await modManager.GetInstalledAsync(lifetimeCancellation.Token);
        var receipt = installed.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, selected.Receipt?.Id, StringComparison.OrdinalIgnoreCase));
        if (receipt is null || receipt.Ownership != ModOwnership.LocalTakenOver)
        {
            return;
        }
        var projectedCatalog = ModCatalogCompatibility.ProjectForBuild(catalog, record.BuildId);
        ModDiscoveryEntry external = new()
        {
            Id = receipt.Id,
            Name = receipt.Name,
            LoaderId = receipt.LoaderId,
            InstallRoot = receipt.InstallRoot,
            Enabled = receipt.Enabled,
            Ownership = ModOwnership.LocalTakenOver,
            Files = receipt.Files.Select(file => file.RelativePath).ToArray(),
            EntryFiles = receipt.EntryFiles
        };
        var candidates = ModCatalogMatcher.Match(external, record.BuildId, projectedCatalog.Mods);
        if (candidates.Count == 0)
        {
            ErrorMessage = Loc["MatchToCatalogNoMatch"];
            return;
        }
        ModManifest? selectedManifest = candidates.Count == 1
            ? candidates[0]
            : catalogMatchPrompt is not null
                ? await catalogMatchPrompt(candidates, receipt.Name)
                : null;
        if (selectedManifest is null)
        {
            return;
        }
        try
        {
            await modManager.RelinkReceiptToCatalogAsync(receipt.Id, selectedManifest, lifetimeCancellation.Token);
            await RefreshAsync();
            if (SelectedInstance?.Id != record.Id)
            {
                SelectedInstance = Instances.Instances.FirstOrDefault(instance => instance.Id == record.Id)
                    ?? new InstanceItemViewModel(record, record.BuildId, record.BuildId, 0);
            }
            await LoadInstanceDetailsAsync(
                record,
                Interlocked.Increment(ref detailsLoadGeneration),
                lifetimeCancellation.Token);
            StatusMessage = Loc["MatchToCatalogDone"];
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or InvalidOperationException
            or UnauthorizedAccessException)
        {
            ErrorMessage = Loc["MatchToCatalogFailed"] + " " + exception.Message;
        }
    }

    private async Task RefreshAfterFailedMutationAsync(string instanceId, string operationError)
    {
        try
        {
            await RefreshAsync();
            SelectedInstance = Instances.Instances.FirstOrDefault(instance => instance.Id == instanceId);
            ErrorMessage = operationError;
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or InvalidOperationException
            or System.Text.Json.JsonException)
        {
            ErrorMessage = $"{operationError} {Loc["RefreshFailed"]}: {Loc.ErrorMessageFor(exception)}";
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
            SelectedInstance = Instances.Instances.FirstOrDefault(instance => instance.Id == instanceId);
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
            var operationError = Loc.ErrorMessageFor(exception);
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

    private async Task<InstanceRecord?> FindVanillaSourceAsync(string buildId, string? selectedInstanceId = null)
    {
        foreach (var candidate in Instances.Instances.Where(instance =>
                     instance.Record.BuildId == buildId
                     && (selectedInstanceId is null || instance.Id == selectedInstanceId)))
        {
            if (candidate.Record.Purpose == InstancePurpose.General
                && await CreateLoaderManager(candidate.Record).GetStateAsync() == LoaderState.Vanilla
                && (await CreateModManager(candidate.Record).GetInstalledAsync()).Count == 0)
            {
                return candidate.Record;
            }
        }
        return null;
    }

    private async Task VerifySpeedrunLaunchAsync(InstanceRecord instance)
    {
        SpeedrunReportPath = null;
        var template = catalog.SpeedrunTemplates.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, instance.SpeedrunTemplateId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(Loc["SpeedrunTemplateMissing"]);
        var build = catalog.Builds.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, instance.BuildId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(Loc["SpeedrunBuildMissing"]);
        var fileManifest = catalog.SpeedrunFileManifests.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, template.FileManifestId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(Loc["SpeedrunManifestMissing"]);
        bool hasTransactionIssue;
        try
        {
            var recoveries = await FileTransaction.RecoverPendingAsync(
                Path.Combine(paths.GetVersionDataRoot(VersionRoot), "transactions"),
                lifetimeCancellation.Token);
            hasTransactionIssue = recoveries.Any(recovery => recovery.State == TransactionState.NeedsAttention);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            hasTransactionIssue = true;
        }
        bool hasLocalLowIssue;
        try
        {
            var isolation = new LocalLowIsolationService(
                GetSharedLocalLowPath(),
                paths.GetVersionDataRoot(VersionRoot));
            var recoveries = await isolation.RecoverPendingAsync(lifetimeCancellation.Token);
            hasLocalLowIssue = recoveries.Any(recovery => recovery.State is
                TransactionState.Prepared or TransactionState.Applying or TransactionState.NeedsAttention);
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or InvalidOperationException
            or UnauthorizedAccessException)
        {
            hasLocalLowIssue = true;
        }
        var result = await new SpeedrunEnvironmentVerifier().VerifyAndWriteReportAsync(
            new SpeedrunVerificationRequest
            {
                Instance = instance,
                Template = template,
                TemplateSource = SpeedrunTemplateSource.OfficialCatalog,
                ExpectedBuild = build,
                CurrentRulesRevision = template.RulesRevision,
                FileManifest = fileManifest,
                RuntimePatchesConfigurationPath = GetRuntimePatchesConfigurationPath(instance),
                HasTransactionIssue = hasTransactionIssue,
                HasLocalLowIssue = hasLocalLowIssue,
                ReportsDirectory = Path.Combine(GetInstanceStateRoot(instance.Id), "speedrun-reports")
            });
        var verificationStatusKey = template.IsOfficial
            ? result.Report.IsOfficiallyVerified
                ? result.Report.Issues.Any(issue => issue.Severity == SpeedrunIssueSeverity.RuleWarning)
                    ? "SpeedrunVerifiedWithWarnings"
                    : "SpeedrunVerified"
                : "SpeedrunVerificationFailed"
            : "SpeedrunUnverifiedReport";
        var reminderVisible = !(template.IsOfficial
            && result.Report.IsOfficiallyVerified
            && !result.Report.Issues.Any(issue => issue.Severity == SpeedrunIssueSeverity.RuleWarning));
        SetSpeedrunReminder(verificationStatusKey, visible: reminderVisible);
        SpeedrunReportPath = result.ReportPath;
        SpeedrunReminderIsError = !result.Report.IsOfficiallyVerified;
        if (template.IsOfficial && !result.Report.IsOfficiallyVerified)
        {
            throw new InvalidOperationException(Loc["SpeedrunVerificationFailed"]);
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

        Interlocked.Exchange(ref steamQrChallengeReceived, 1);

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
                SteamStatus = Loc["SteamScanQr"];
            }
            catch (Exception exception)
            {
                image?.Dispose();
                ErrorMessage = $"Steam QR: {exception.Message}";
            }
        });
    }

    private void OnSystemProxyChanged(object? sender, SystemProxyChangedEventArgs eventArgs)
    {
        void ApplyChange()
        {
            SteamNetworkStatus = FormatSteamNetworkStatus(eventArgs.Current);
            if (Volatile.Read(ref steamSignInCancellation) is { } signIn && !IsSteamLoggedIn)
            {
                if (Interlocked.Exchange(ref steamQrRestartPending, 1) == 0)
                {
                    _ = RestartSteamQrAfterCurrentAttemptAsync(SignInWithQrCommand.ExecutionTask);
                }
                signIn.Cancel();
                SteamStatus = Loc["SteamNetworkReconnecting"];
                return;
            }
            QueueSteamReconnect();
        }

        if (Application.Current is not null && !Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(ApplyChange);
        }
        else
        {
            ApplyChange();
        }
    }

    private async Task RestartSteamQrAfterCurrentAttemptAsync(Task? currentAttempt)
    {
        try
        {
            if (currentAttempt is not null)
            {
                await currentAttempt;
            }
            if (!IsOfflineMode && !lifetimeCancellation.IsCancellationRequested && !IsSteamLoggedIn)
            {
                await SignInWithQrCommand.ExecuteAsync(null);
            }
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            Interlocked.Exchange(ref steamQrRestartPending, 0);
        }
    }

    private void OnSteamConnectionLost(object? sender, SteamConnectionLostEventArgs eventArgs)
    {
        void ApplyDisconnect()
        {
            if (!ReferenceEquals(sender, steamSession) || lifetimeCancellation.IsCancellationRequested)
            {
                return;
            }
            IsSteamLoggedIn = false;
            SteamStatus = Loc["SteamNetworkReconnecting"];
            SteamNetworkStatus = FormatSteamNetworkFailure(eventArgs.Exception);
            QueueSteamReconnect();
        }

        if (Application.Current is not null && !Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(ApplyDisconnect);
        }
        else
        {
            ApplyDisconnect();
        }
    }

    private void QueueSteamReconnect()
    {
        if (IsOfflineMode
            || lifetimeCancellation.IsCancellationRequested
            || !File.Exists(Path.Combine(paths.ApplicationDataRoot, "steam-token.dat")))
        {
            return;
        }

        lock (steamReconnectQueueLock)
        {
            steamReconnectTask = ReconnectSteamAfterAsync(steamReconnectTask);
        }
    }

    private async Task ReconnectSteamAfterAsync(Task previous)
    {
        try
        {
            await previous;
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }

        if (IsOfflineMode || lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }

        await DownloadCenter.DownloadQueue.InitializeAsync(lifetimeCancellation.Token);
        await DownloadCenter.DownloadQueue.PauseSteamDownloadsAsync(lifetimeCancellation.Token);
        IsSteamLoggedIn = false;
        SteamStatus = Loc["SteamNetworkReconnecting"];
        await steamReconnect();
    }

    private SteamAuthenticationSession CreateSteamSession(bool includeQrEvents)
    {
        var session = new SteamAuthenticationSession(
            tokenStore,
            guardCallback: CreateGuardCallback(),
            systemProxy: systemProxy);
        session.ConnectionLost += OnSteamConnectionLost;
        if (includeQrEvents)
        {
            session.QrChallengeChanged += OnQrChallengeChanged;
        }
        return session;
    }

    private ISteamGuardCallback? CreateGuardCallback()
    {
        if (guardCodePrompt is null && deviceConfirmationPrompt is null)
        {
            return null;
        }
        return new SteamGuardPromptCallback(
            getDeviceCode: previousIncorrect => guardCodePrompt is null
                ? Task.FromResult<string?>(null)
                : guardCodePrompt(
                    Loc["SteamGuardDeviceTitle"],
                    Loc["SteamGuardDeviceMessage"],
                    previousIncorrect),
            getEmailCode: (email, previousIncorrect) => guardCodePrompt is null
                ? Task.FromResult<string?>(null)
                : guardCodePrompt(
                    Loc["SteamGuardEmailTitle"],
                    Loc["SteamGuardEmailMessage"] + " " + email,
                    previousIncorrect),
            acceptDeviceConfirmation: () => deviceConfirmationPrompt is null
                ? Task.FromResult<bool?>(false)
                : deviceConfirmationPrompt());
    }

    private HttpClientHandler CreateSystemProxyHandler() => new()
    {
        Proxy = systemProxy,
        UseProxy = true
    };

    private string FormatSteamNetworkStatus(SystemProxySnapshot snapshot) =>
        Loc[snapshot.Enabled ? "SteamProxyDetected" : "SteamProxyDirect"];

    private string FormatSteamNetworkFailure(Exception exception)
    {
        if (exception is OperationCanceledException or TimeoutException)
        {
            return Loc["SteamNetworkTimeout"];
        }
        if (exception is HttpRequestException { StatusCode: { } statusCode })
        {
            return $"{Loc["SteamNetworkHttpError"]} · {(int)statusCode}";
        }
        if (exception is HttpRequestException or IOException)
        {
            return Loc["SteamNetworkDisconnected"];
        }
        return Loc["SteamNetworkUnavailable"];
    }

    private static bool IsTransientSteamNetworkFailure(Exception exception) =>
        exception is IOException or HttpRequestException or TimeoutException or OperationCanceledException
        || (exception.InnerException is not null && IsTransientSteamNetworkFailure(exception.InnerException));

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
                customModLinksError = Loc.ErrorMessageFor(exception);
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
                customCatalogErrors.Add($"{definition.Namespace}: {Loc.ErrorMessageFor(exception)}");
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
        session.ConnectionLost -= OnSteamConnectionLost;
        await session.DisposeAsync();
    }

    private LoaderManifest? FindCatalogLoader(string id, string buildId) =>
        catalog.Loaders.FirstOrDefault(loader =>
            string.Equals(loader.Id, id, StringComparison.OrdinalIgnoreCase)
            && loader.SupportedBuildIds.Contains(buildId, StringComparer.OrdinalIgnoreCase));

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

    public string? GetSelectedInstanceSaveDirectory() => SelectedInstance is null
        ? null
        : new LocalLowIsolationService(GetSharedLocalLowPath(), paths.GetVersionDataRoot(VersionRoot))
            .GetInstanceLocalLowPath(SelectedInstance.Id);

    public string? GetSelectedInstanceModDirectory() => SelectedInstance is null
        ? null
        : CurrentLoaderState switch
        {
            LoaderState.ModdingApi => Path.Combine(
                SelectedInstance.RootPath,
                "hollow_knight_Data",
                "Managed",
                "Mods"),
            LoaderState.BepInEx => Path.Combine(SelectedInstance.RootPath, "BepInEx", "plugins"),
            _ => null
        };

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

    private string GetInstalledModGraphLayoutPath(string instanceId) =>
        Path.Combine(GetInstanceStateRoot(instanceId), "dependency-graph.layout.json");

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
        lock (settingsSaveQueueLock)
        {
            settingsSavesClosed = true;
        }
        lifetimeCancellation.Cancel();
        var pendingExternalProtocolCommand = protocolService.PendingExecution;
        var detailsCancellation = Interlocked.Exchange(ref detailsLoadCancellation, null);
        detailsCancellation?.Cancel();
        var pendingDetailsLoad = detailsLoadTask;
        var contentCancellation = Interlocked.Exchange(ref selectedModContentLoadCancellation, null);
        contentCancellation?.Cancel();
        var pendingContentLoad = selectedModContentLoadTask;
        var speedrunLeaderboardCancellation = Interlocked.Exchange(ref speedrunLeaderboardLoadCancellation, null);
        speedrunLeaderboardCancellation?.Cancel();
        var pendingSpeedrunLeaderboardLoad = speedrunLeaderboardLoadTask;
        var pendingSpeedrunActivityRefreshLoop = speedrunActivityRefreshLoopTask;
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
                    DownloadCenter.RefreshSteamChunkCacheStatusCommand.ExecutionTask ?? Task.CompletedTask,
                    DownloadCenter.ClearSteamChunkCacheCommand.ExecutionTask ?? Task.CompletedTask,
                    pendingExternalProtocolCommand,
                    steamOfflineTransitionTask,
                    pendingDetailsLoad,
                    pendingContentLoad);
                try
                {
                    await Task.WhenAll(
                        pendingSpeedrunLeaderboardLoad,
                        pendingSpeedrunActivityRefreshLoop);
                }
                catch (Exception) when (lifetimeCancellation.IsCancellationRequested)
                {
                    // Shutdown has begun; a background speedrun refresh that faulted
                    // (or was cancelled) must not abort disposal.
                }
                await catalogRefreshTask;
                await steamReconnectTask;
            }
            catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
            {
            }
            finally
            {
                try
                {
                    await FlushSettingsSavesAsync();
                    await Settings.DisposeBackgroundAppearanceAsync();
                }
                finally
                {
                    try
                    {
                        await DownloadCenter.DownloadQueue.DisposeAsync();
                    }
                    finally
                    {
                        DownloadCenter.Dispose();
                    }
                }
            }
        }
        finally
        {
            detailsCancellation?.Dispose();
            contentCancellation?.Dispose();
            speedrunLeaderboardCancellation?.Dispose();
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
                systemProxy.Changed -= OnSystemProxyChanged;
                steamConnectionGate.Dispose();
                metadataHttpClient.Dispose();
                directMetadataHttpClient.Dispose();
                packageHttpClient.Dispose();
                githubLatencyService.Dispose();
                networkPolicy.Dispose();
                systemProxy.Dispose();
                lifetimeCancellation.Dispose();
            }
        }
    }
}

using Crystalfly.Core.Thunderstore;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Models;
using Crystalfly.App.Services;

namespace Crystalfly.App.ViewModels;

public partial class MainViewModel
{
    private HttpClient? thunderstoreHttpClient;
    private HttpClient ThunderstoreHttpClient => thunderstoreHttpClient ??= new HttpClient(
        new Crystalfly.Core.Networking.NetworkPolicyHandler(networkPolicy,
            new ThunderstoreHttpHandler(new HttpClientHandler { Proxy = systemProxy, UseProxy = true, AllowAutoRedirect = false, UseCookies = false })))
        { Timeout = TimeSpan.FromMinutes(30) };
    private ThunderstoreViewModel? thunderstore;
    public ThunderstoreViewModel Thunderstore => thunderstore ??= new(Loc,
        new ThunderstoreCatalogSource(ThunderstoreHttpClient,
            Path.Combine(paths.ApplicationDataRoot, "thunderstore", "silksong", "catalog.json"), networkPolicy),
        GetThunderstorePackageStore(), () => SelectedInstance?.Record, () => IsOfflineMode,
        async group => { await DownloadCenter.EnqueueAsync(group, lifetimeCancellation.Token); },
        async () => { await RefreshInstancesAsync(showBusy: false); },
        (target, plan, repair) => instanceOperationCoordinator.RunAsync(target.Id, token =>
            new ThunderstoreInstallService(target, GetThunderstorePackageStore()).InstallAsync(plan, offline: true,
                confirmUnverified: true, repair: repair, token: token), lifetimeCancellation.Token),
        () => SelectPageCommand.Execute("Launch"),
        () => { if (CanNavigate) { CurrentPage = "Downloads"; CurrentDownloadSection = "ModMarket"; } },
        new SilksongModTranslationSource(metadataHttpClient,
            Path.Combine(paths.ApplicationDataRoot, "thunderstore", "silksong", "translations"), networkPolicy),
        new ThunderstorePackageIconLoader(ThunderstoreHttpClient,
            Path.Combine(paths.ApplicationDataRoot, "thunderstore", "silksong", "icons"), () => IsOfflineMode),
        reportError: message => { ErrorMessage = null; ErrorMessage = message; },
        getInstallTargets: () => Instances.Instances.Select(item => item.Record).ToArray(),
        getInstalledGraphLayoutPath: GetInstalledModGraphLayoutPath);

    [RelayCommand]
    private void ClearMarketFilters()
    {
        MarketSearchText = "";
        SelectedMarketBuildOption = MarketBuildOptions.FirstOrDefault();
        SelectedMarketLoaderOption = MarketLoaderOptions.FirstOrDefault();
        SelectedMarketSourceOption = MarketSourceOptions.FirstOrDefault();
        SelectedMarketTagOption = MarketTagOptions.FirstOrDefault();
        SelectedMarketActivityOption = MarketActivityOptions.FirstOrDefault();
    }

    public bool IsHollowKnightMarket => IsHollowKnight && IsModMarketDownloadSection;
    public bool IsSilksongMarket => IsSilksong && IsModMarketDownloadSection;
    public bool IsLegacyLoaderTab => IsHollowKnight && CurrentManageTab == "Loader";
    public bool IsLegacyPresetsTab => IsHollowKnight && CurrentManageTab == "Presets";
    public bool IsLegacyModsTab => IsHollowKnight && CurrentManageTab == "Mods";
    public bool IsSilksongModsTab => IsSilksong && CurrentManageTab is "Mods" or "Loader" or "Presets";
    private void NotifyGameSurfacesChanged()
    {
        OnPropertyChanged(nameof(IsHollowKnightMarket));
        OnPropertyChanged(nameof(IsSilksongMarket));
        OnPropertyChanged(nameof(IsLegacyModsTab));
        OnPropertyChanged(nameof(IsLegacyLoaderTab));
        OnPropertyChanged(nameof(IsLegacyPresetsTab));
        OnPropertyChanged(nameof(IsSilksongModsTab));
    }
    private ThunderstorePackageStore GetThunderstorePackageStore() => new(ThunderstoreHttpClient,
        Path.Combine(paths.ApplicationDataRoot, "thunderstore", "silksong", "packages"), networkPolicy);

    public GameId SelectedGameId => settings.SelectedGameId;
    public GameDefinition SelectedGame => GameDefinition.Get(SelectedGameId);
    public bool IsHollowKnight => SelectedGameId == GameId.HollowKnight;
    public bool IsSilksong => SelectedGameId == GameId.Silksong;
    public bool CanSwitchGame => CanNavigate;
    public string SelectedGameName => Loc[IsSilksong ? "GameSilksong" : "GameHollowKnight"];
    public string GameAvatarHint => string.Format(Loc.Culture, Loc["GameAvatarSwitchHint"], SelectedGameName,
        Loc[IsSilksong ? "GameHollowKnight" : "GameSilksong"]);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanNavigate))]
    [NotifyPropertyChangedFor(nameof(CanSwitchGame))]
    [NotifyPropertyChangedFor(nameof(CanLaunch))]
    [NotifyPropertyChangedFor(nameof(CanAttemptLaunch))]
    [NotifyPropertyChangedFor(nameof(CanCloneInstance))]
    [NotifyCanExecuteChangedFor(nameof(ToggleGameCommand))]
    public partial bool IsSwitchingGame { get; private set; }

    [RelayCommand(CanExecute = nameof(CanSwitchGame))]
    private Task ToggleGameAsync() => SelectGameAsync(IsSilksong ? "HollowKnight" : "Silksong");

    [RelayCommand]
    private async Task SelectGameAsync(string id)
    {
        if (!Enum.TryParse<GameId>(id, out var gameId) || gameId == SelectedGameId || !CanSwitchGame)
            return;
        if (SaveEditor?.IsDirty == true)
        {
            ErrorMessage = Loc["SaveBeforeGameSwitch"];
            return;
        }
        IsSwitchingGame = true;
        try
        {
            CaptureGameWorkspace();
            gameUiStateReady = false;
            settings = GameWorkspaceMigration.Select(settings, gameId);
            Interlocked.Increment(ref instanceRefreshVersion);
            Interlocked.Increment(ref detailsLoadGeneration);
            detailsLoadCancellation?.Cancel();
            VersionRoot = settings.VersionRoot ?? string.Empty;
            await Instances.InitializeGameDirectoriesAsync();
            // Keep the current projection until the scan can replace it without an empty frame.
            await RefreshInstancesAsync(showBusy: false);
            NotifyGameContextChanged();
            PopulateDownloadBuilds();
            RebuildMarketCatalog();
            PopulateSpeedrunTemplates();
            RestoreGameFilters();
            if (IsSilksong) CurrentSpeedrunTab = "Activity";
            await QueueSettingsSave();
            if (IsSilksong && IsModMarketDownloadSection) _ = Thunderstore.EnsureLoadedAsync();
        }
        finally { IsSwitchingGame = false; }
        NotifyToast(string.Format(Loc.Culture, Loc["GameSwitched"], SelectedGameName));
    }

    private bool gameUiStateReady;
    private void CaptureGameWorkspace()
    {
        if (!gameUiStateReady) return;
        var previous = settings.GameWorkspaces.FirstOrDefault(item => item.GameId == SelectedGameId) ?? new GameWorkspaceSettings { GameId = SelectedGameId };
        var workspace = previous with
        {
            VersionRoot = settings.VersionRoot, CurrentInstanceId = settings.CurrentInstanceId,
            InstanceSearch = SearchText, DownloadSearch = DownloadBuildSearchText,
            MarketSearch = IsSilksong ? thunderstore?.SearchText ?? previous.MarketSearch : MarketSearchText,
            MarketBuild = SelectedMarketBuildOption?.Value, MarketLoader = SelectedMarketLoaderOption?.Value,
            MarketSource = SelectedMarketSourceOption?.Value, MarketTag = SelectedMarketTagOption?.Value,
            MarketActivity = SelectedMarketActivityOption?.Value.ToString(),
            ThunderstoreCategory = thunderstore?.Category?.Value ?? previous.ThunderstoreCategory,
            ThunderstoreSort = thunderstore?.SortIndex ?? previous.ThunderstoreSort,
            ThunderstoreShowDeprecated = thunderstore?.ShowDeprecated ?? previous.ThunderstoreShowDeprecated
        };
        settings = settings with { GameWorkspaces = settings.GameWorkspaces.Where(item => item.GameId != SelectedGameId).Append(workspace).ToArray() };
    }
    private void RestoreGameFilters()
    {
        var saved = settings.GameWorkspaces.FirstOrDefault(item => item.GameId == SelectedGameId);
        SearchText = saved?.InstanceSearch ?? "";
        MarketSearchText = saved?.MarketSearch ?? "";
        DownloadBuildSearchText = saved?.DownloadSearch ?? "";
        SelectedMarketBuildOption = MarketBuildOptions.FirstOrDefault(item => item.Value == saved?.MarketBuild) ?? MarketBuildOptions.FirstOrDefault();
        SelectedMarketLoaderOption = MarketLoaderOptions.FirstOrDefault(item => item.Value == saved?.MarketLoader) ?? MarketLoaderOptions.FirstOrDefault();
        SelectedMarketSourceOption = MarketSourceOptions.FirstOrDefault(item => item.Value == saved?.MarketSource) ?? MarketSourceOptions.FirstOrDefault();
        SelectedMarketTagOption = MarketTagOptions.FirstOrDefault(item => item.Value == saved?.MarketTag) ?? MarketTagOptions.FirstOrDefault();
        SelectedMarketActivityOption = MarketActivityOptions.FirstOrDefault(item => item.Value.ToString() == saved?.MarketActivity) ?? MarketActivityOptions.FirstOrDefault();
        if (IsSilksong)
        {
            Thunderstore.SearchText = saved?.MarketSearch ?? "";
            Thunderstore.RestoreFilters(saved?.ThunderstoreCategory, saved?.ThunderstoreSort ?? 0, saved?.ThunderstoreShowDeprecated ?? false);
        }
        gameUiStateReady = true;
    }

    private IReadOnlyDictionary<string, string>? GetGameRepairHashes(GameId gameId, string buildId)
    {
        var build = catalog.Builds.FirstOrDefault(item => item.GameId == gameId && item.Id == buildId);
        if (build is null) return null;
        var game = GameDefinition.Get(gameId);
        var hashes = new Dictionary<string, string>
        {
            [game.ExecutableName] = build.ExecutableSha256,
            [game.GlobalGameManagersPath] = build.GlobalGameManagersSha256
        };
        if (build.UnityPlayerSha256 is not null) hashes["UnityPlayer.dll"] = build.UnityPlayerSha256;
        return hashes;
    }

    private void NotifyGameContextChanged()
    {
        NotifyGameSurfacesChanged();
        OnPropertyChanged(nameof(SelectedGameId));
        OnPropertyChanged(nameof(SelectedGame));
        OnPropertyChanged(nameof(IsHollowKnight));
        OnPropertyChanged(nameof(IsSilksong));
        OnPropertyChanged(nameof(SelectedGameName));
        OnPropertyChanged(nameof(GameAvatarHint));
        OnPropertyChanged(nameof(CanSwitchGame));
        OnPropertyChanged(nameof(IsSpeedrunEnvironmentTab));
        OnPropertyChanged(nameof(IsSpeedrunActivityTab));
        if (IsSilksong) CurrentSpeedrunTab = "Activity";
    }
}

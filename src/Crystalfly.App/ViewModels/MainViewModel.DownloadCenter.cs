using Crystalfly.Core.Thunderstore;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.App.Downloads;
using Crystalfly.App.ViewModels.Dialogs;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;
using Crystalfly.Core.Runtime;
using Crystalfly.Core.Instances;
using Crystalfly.Steam.Downloads;

namespace Crystalfly.App.ViewModels;

public partial class MainViewModel
{
    private readonly InstanceOperationCoordinator instanceOperationCoordinator = new();

    partial void OnVersionRootChanged(string value) =>
        _ = DownloadCenter?.RefreshSteamChunkCacheStatusCommand.ExecuteAsync(null);

    partial void OnCurrentDownloadSectionChanged(string value)
    {
        NotifyGameSurfacesChanged();
        if (IsSilksong && value == "ModMarket") _ = Thunderstore.EnsureLoadedAsync();
        if (value == "GameVersions")
            _ = DownloadCenter.RefreshSteamChunkCacheStatusCommand.ExecuteAsync(null);
    }

    private DownloadQueueService CreateDownloadQueue()
    {
        var packageExecutor = new CatalogPackageQueueExecutor(
            () => catalog,
            packageHttpClient,
            instanceOperationCoordinator,
            isGameProcessRunning,
            networkPolicy: networkPolicy);
        var executor = new SteamDownloadQueueExecutor(
            packageExecutor,
            async (request, report, cancellationToken) =>
            {
                var session = steamSession;
                if (!IsSteamSessionLoggedOn() || session is null)
                {
                    throw new InvalidOperationException(Loc["QueuedDownloadSteamSignInRequired"]);
                }
                using var content = new SteamKitContentDeliveryClient(session.Client);
                return await new SteamDepotDownloadService(content, report)
                    .DownloadAsync(request, cancellationToken);
            },
            manifestId => catalog.Builds.FirstOrDefault(build =>
                string.Equals(
                    build.ManifestId,
                    manifestId.ToString(CultureInfo.InvariantCulture),
                    StringComparison.Ordinal))?.Id,
            IsSteamSessionLoggedOn,
            operationCoordinator: instanceOperationCoordinator,
            networkPolicy: networkPolicy,
            resolveRepairHashes: buildId =>
            {
                var build = catalog.Builds.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, buildId, StringComparison.OrdinalIgnoreCase));
                if (build is null)
                {
                    return null;
                }
                var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [GameDefinition.Get(build.GameId).ExecutableName] = build.ExecutableSha256,
                    [GameDefinition.Get(build.GameId).GlobalGameManagersPath] = build.GlobalGameManagersSha256
                };
                if (build.UnityPlayerSha256 is not null)
                {
                    hashes["UnityPlayer.dll"] = build.UnityPlayerSha256;
                }
                return hashes;
            },
            resolveGameBuildId: (gameId, manifestId) => catalog.Builds.FirstOrDefault(build =>
                build.GameId == gameId && build.ManifestId == manifestId.ToString(CultureInfo.InvariantCulture))?.Id,
            resolveGameRepairHashes: (gameId, buildId) => GetGameRepairHashes(gameId, buildId));
        return new DownloadQueueService(
            Path.Combine(paths.ApplicationDataRoot, "download-queue.json"),
            new ThunderstoreQueueExecutor(executor, GetThunderstorePackageStore(), instanceOperationCoordinator, networkPolicy),
            isGameProcessRunning,
            TimeSpan.FromMilliseconds(500),
            networkPolicy,
            instanceOperationCoordinator,
            errorReporter)
        {
            IsTargetGameRunning = group => new SystemHollowKnightProcessProbe(GameDefinition.Get(group.GameId).ProcessName).IsRunning()
        };
    }

    private async Task InitializeDownloadQueueAsync()
    {
        await DownloadCenter.DownloadQueue.InitializeAsync(lifetimeCancellation.Token);
        DownloadCenter.QueueDownloadQueueProjection(DownloadCenter.DownloadQueue.Groups);
    }

    internal async Task<ModInstallPlan> CreateSelectedMarketInstallPlanAsync(
        MarketInstallTargetViewModel target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        var mod = SelectedMarketMod
            ?? throw new InvalidOperationException(Loc["SelectMod"]);
        return await CreateModInstallService(target.Instance.Record)
            .CreatePlanAsync(mod.Id, cancellationToken);
    }

    private async Task EnqueueSelectedMarketModAsync()
    {
        if (SelectedMarketMod is null
            || SelectedMarketInstallTarget is null
            || !SelectedMarketInstallTarget.IsAvailable)
        {
            ErrorMessage = Loc["NoInstallTargets"];
            return;
        }

        ErrorMessage = null;
        try
        {
            await DownloadCenter.DownloadQueue.InitializeAsync(lifetimeCancellation.Token);
            var target = SelectedMarketInstallTarget;
            var plan = await CreateSelectedMarketInstallPlanAsync(target, lifetimeCancellation.Token);
            var group = ModInstallQueueGroupFactory.Create(plan, catalog, target.Instance.Record);
            var result = await DownloadCenter.EnqueueAsync(group, lifetimeCancellation.Token);
            NotifyToast(result.Added
                ? Loc["AddedToDownloadQueue"]
                : Loc["QueueTaskAlreadyExists"]);
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
            ErrorMessage = Loc.ErrorMessageFor(exception);
        }
    }


    private async Task EnqueueModDependencyRepairAsync(ModDependencyRepairPlan plan)
    {
        if (SelectedInstance is null)
        {
            ErrorMessage = Loc["NoInstance"];
            return;
        }

        ErrorMessage = null;
        try
        {
            await DownloadCenter.DownloadQueue.InitializeAsync(lifetimeCancellation.Token);
            var group = ModDependencyRepairQueueGroupFactory.Create(plan, catalog, SelectedInstance.Record);
            var result = await DownloadCenter.EnqueueAsync(group, lifetimeCancellation.Token);
            NotifyToast(result.Added
                ? Loc["AddedToDownloadQueue"]
                : Loc["QueueTaskAlreadyExists"]);
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
            ErrorMessage = Loc.ErrorMessageFor(exception);
        }
    }
    private async Task EnqueueSteamBuildAsync()
    {
        if (!IsSteamSessionLoggedOn() || SelectedDownloadBuild is null)
        {
            ErrorMessage = Loc["SteamSelectBuildRequired"];
            return;
        }
        if (!Directory.Exists(VersionRoot))
        {
            ErrorMessage = Loc["ChooseRoot"];
            return;
        }

        var selected = SelectedDownloadBuild;
        var instanceName = UniqueInstanceName(selected.DisplayName);
        var group = BuildIdentity.TryGetSteamManifestId(selected.BuildId, out var historicalManifest)
            ? SteamDownloadQueueGroupFactory.CreateCustomManifest(
                historicalManifest, selected.DisplayName, VersionRoot, instanceName, gameId: SelectedGameId)
            : SteamDownloadQueueGroupFactory.Create(
            selected.BuildId,
            selected.DisplayName,
            selected.ManifestId,
            VersionRoot,
            instanceName, gameId: SelectedGameId);
        await DownloadCenter.DownloadQueue.InitializeAsync(lifetimeCancellation.Token);
        var result = await DownloadCenter.EnqueueAsync(group, lifetimeCancellation.Token);
        DownloadStatus = result.Added
            ? Loc["AddedToDownloadQueue"]
            : Loc["QueueTaskAlreadyExists"];
        NotifyToast(DownloadStatus);
    }

    [RelayCommand]
    private async Task EnqueueSelectedInstanceRepairAsync()
    {
        if (SelectedInstance is not { } selected)
        {
            ErrorMessage = Loc["NoInstance"];
            return;
        }
        var build = catalog.Builds.FirstOrDefault(candidate =>
            candidate.GameId == selected.Record.GameId && string.Equals(candidate.Id, selected.Record.BuildId, StringComparison.OrdinalIgnoreCase));
        var suffix = selected.Record.BuildId.StartsWith("steam-manifest-", StringComparison.Ordinal) ? selected.Record.BuildId[15..]
            : selected.Record.BuildId.StartsWith("steam-public-", StringComparison.Ordinal) ? selected.Record.BuildId[13..] : "";
        _ = ulong.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var recordedManifest);
        if (build is null && recordedManifest == 0)
        {
            ErrorMessage = Loc["UnknownBuild"];
            return;
        }

        ErrorMessage = null;
        try
        {
            var loaderManager = CreateLoaderManager(selected.Record);
            var inspection = await loaderManager.InspectAsync(lifetimeCancellation.Token);
            var receipt = await loaderManager.GetReceiptAsync(lifetimeCancellation.Token);
            LoaderManifest? loader = null;
            if (inspection.State != LoaderState.Vanilla && selected.Record.GameId == GameId.HollowKnight)
            {
                if (receipt is null)
                {
                    throw new InvalidOperationException(Loc["ExternalLoaderBlocked"]);
                }
                loader = FindCatalogLoader(receipt.PackageId, selected.Record.BuildId);
                if (loader is null)
                {
                    throw new InvalidOperationException(Loc["LoaderRepairPackageUnavailable"]);
                }
            }

            await DownloadCenter.DownloadQueue.InitializeAsync(lifetimeCancellation.Token);
            var group = build is not null ? SteamDownloadQueueGroupFactory.CreateRepair(selected.Record, build, loader)
                : SteamDownloadQueueGroupFactory.CreateManifestRepair(selected.Record, recordedManifest);
            var result = await DownloadCenter.EnqueueAsync(group, lifetimeCancellation.Token);
            NotifyToast(result.Added
                ? Loc["AddedToDownloadQueue"]
                : Loc["QueueTaskAlreadyExists"]);
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or InvalidOperationException
            or UnauthorizedAccessException
            or ArgumentException
            or System.Text.Json.JsonException)
        {
            ErrorMessage = Loc.ErrorMessageFor(exception);
        }
    }

    internal async Task<bool> EnqueueCustomSteamManifestAsync(
        HistoricalManifestDownloadRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsSteamSessionLoggedOn())
        {
            ErrorMessage = Loc["SteamLoginRequired"];
            return false;
        }
        if (!Directory.Exists(VersionRoot))
        {
            ErrorMessage = Loc["ChooseRoot"];
            return false;
        }
        if (request.ManifestId == 0)
        {
            ErrorMessage = Loc["InvalidHistoricalManifest"];
            return false;
        }

        var known = catalog.Builds.FirstOrDefault(build => build.GameId == SelectedGameId &&
            string.Equals(
                build.ManifestId,
                request.ManifestId.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal));
        var group = SteamDownloadQueueGroupFactory.CreateCustomManifest(
            request.ManifestId,
            known?.DisplayVersion ?? $"{Loc["UnverifiedHistoricalBuild"]} · {request.ManifestId}",
            VersionRoot,
            request.InstanceName, gameId: SelectedGameId);
        await DownloadCenter.DownloadQueue.InitializeAsync(cancellationToken);
        var result = await DownloadCenter.EnqueueAsync(group, cancellationToken);
        DownloadStatus = result.Added ? Loc["AddedToDownloadQueue"] : Loc["QueueTaskAlreadyExists"];
        NotifyToast(DownloadStatus);
        return result.Added;
    }

    [RelayCommand]
    private void OpenDownloadQueue()
    {
        CurrentPage = "Downloads";
        CurrentDownloadSection = "DownloadQueue";
    }
}

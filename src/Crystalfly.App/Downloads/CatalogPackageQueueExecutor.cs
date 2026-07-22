using System.Net;
using System.Net.Sockets;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Loaders;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;
using Crystalfly.Core.Networking;
using Crystalfly.Core.Packages;
using Crystalfly.Core.Transactions;

namespace Crystalfly.App.Downloads;

public sealed class CatalogPackageQueueExecutor : IDownloadQueueExecutor
{
    private readonly Func<GameCatalog> getCatalog;
    private readonly HttpClient httpClient;
    private readonly InstanceOperationCoordinator coordinator;
    private readonly Func<bool> isGameRunning;
    private readonly TimeSpan gameExitPollInterval;
    private readonly INetworkPolicy? networkPolicy;

    public CatalogPackageQueueExecutor(
        Func<GameCatalog> getCatalog,
        HttpClient httpClient,
        InstanceOperationCoordinator coordinator,
        Func<bool>? isGameRunning = null,
        TimeSpan? gameExitPollInterval = null,
        INetworkPolicy? networkPolicy = null)
    {
        this.getCatalog = getCatalog ?? throw new ArgumentNullException(nameof(getCatalog));
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        this.isGameRunning = isGameRunning ?? (static () => false);
        this.gameExitPollInterval = gameExitPollInterval ?? TimeSpan.FromMilliseconds(500);
        this.networkPolicy = networkPolicy;
        if (this.gameExitPollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(gameExitPollInterval));
        }
    }

    public bool RequiresGameExit(DownloadQueueItem item) => item.Kind is
        DownloadQueueItemKind.Loader or DownloadQueueItemKind.Dependency
        or DownloadQueueItemKind.DependencyReEnable or DownloadQueueItemKind.Mod;

    public bool IsTransient(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (exception is TimeoutException or TaskCanceledException or SocketException)
        {
            return true;
        }
        if (exception is not HttpRequestException requestException)
        {
            return false;
        }
        return requestException.StatusCode is null
            or HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            || (int)requestException.StatusCode is >= 500 and <= 599;
    }

    public async Task TransferAsync(
        DownloadQueueGroup group,
        DownloadQueueItem item,
        IProgress<PackageTransferProgress> progress,
        SemaphoreSlim networkGate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(networkGate);
        if (group.Kind == DownloadQueueGroupKind.ModDependencyRepair)
        {
            RepairContext repair = null!;
            await coordinator.RunAsync(
                group.TargetInstanceId,
                async token => repair = await InspectRepairAsync(group, item, token),
                cancellationToken);
            if (item.Kind == DownloadQueueItemKind.DependencyReEnable || repair.Receipt is not null)
            {
                progress.Report(new PackageTransferProgress(
                    item.TotalBytes,
                    item.TotalBytes,
                    0,
                    "Satisfied"));
                return;
            }
        }
        if (item.IsSatisfied
            && await IsStillSatisfiedAsync(group, item, cancellationToken))
        {
            progress.Report(new PackageTransferProgress(
                item.TotalBytes,
                item.TotalBytes,
                0,
                "Satisfied"));
            return;
        }
        var package = ResolvePackage(getCatalog(), item);
        var paths = GetPaths(group);
        await PackageInstaller.AcquireVerifiedFileFromUriAsync(
            new Uri(package.DownloadUrl),
            paths.TransactionRoot,
            package.SizeBytes,
            package.Sha256,
            paths.PackageRoot,
            httpClient,
            progress,
            cancellationToken,
            networkGate,
            networkPolicy);
    }

    private async Task<bool> IsStillSatisfiedAsync(
        DownloadQueueGroup group,
        DownloadQueueItem item,
        CancellationToken cancellationToken)
    {
        var isSatisfied = false;
        await coordinator.RunAsync(
            group.TargetInstanceId,
            async token =>
            {
                var paths = GetPaths(group);
                var instance = await InstanceSidecar.LoadAsync(paths.InstanceRoot, token);
                if (!string.Equals(instance.Id, group.TargetInstanceId, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Download target does not match the instance sidecar.");
                }

                var loaderManager = new LoaderManager(
                    paths.InstanceRoot,
                    paths.TransactionRoot,
                    Path.Combine(paths.StateRoot, "loader.json"),
                    paths.PackageRoot,
                    httpClient);
                var requestedMod = group.Items.SingleOrDefault(candidate =>
                    candidate.Kind == DownloadQueueItemKind.Mod);
                if (requestedMod is null)
                {
                    if (item.Kind != DownloadQueueItemKind.Loader)
                    {
                        throw new InvalidDataException("A mod install group must contain one requested mod.");
                    }
                    var inspection = await loaderManager.InspectAsync(token);
                    isSatisfied = inspection.State is LoaderState.ModdingApi or LoaderState.BepInEx
                        && string.Equals(inspection.PackageId, item.PackageId, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(inspection.Version, item.Version, StringComparison.OrdinalIgnoreCase);
                    return;
                }

                var catalog = getCatalog();
                var modManager = new ModManager(
                    paths.InstanceRoot,
                    paths.TransactionRoot,
                    Path.Combine(paths.StateRoot, "mods"),
                    paths.PackageRoot,
                    httpClient);
                var service = new ModInstallService(
                    instance, catalog.Mods, catalog.Loaders, loaderManager, modManager);
                var plan = await service.CreatePlanAsync(requestedMod.PackageId, token);
                isSatisfied = ResolvePlanItem(plan, item).State == ModInstallPlanItemState.Satisfied;
            },
            cancellationToken);
        return isSatisfied;
    }

    public Task InstallAsync(
        DownloadQueueGroup group,
        DownloadQueueItem item,
        CancellationToken cancellationToken) => coordinator.RunAsync(
            group.TargetInstanceId,
            async token =>
            {
                while (isGameRunning())
                {
                    await Task.Delay(gameExitPollInterval, token);
                }
                await EnsureTransactionsHealthyAsync(group, token);
                await InstallCoreAsync(group, item, token);
            },
            cancellationToken);

    private static async Task EnsureTransactionsHealthyAsync(
        DownloadQueueGroup group,
        CancellationToken cancellationToken)
    {
        var recoveries = await FileTransaction.RecoverPendingAsync(
            GetPaths(group).TransactionRoot,
            cancellationToken);
        if (recoveries.Any(recovery => recovery.State == TransactionState.NeedsAttention))
        {
            throw new InvalidOperationException(
                "A pending file transaction needs attention before package installation can continue.");
        }
    }

    private async Task InstallCoreAsync(
        DownloadQueueGroup group,
        DownloadQueueItem item,
        CancellationToken cancellationToken)
    {
        var paths = GetPaths(group);
        var instance = await InstanceSidecar.LoadAsync(paths.InstanceRoot, cancellationToken);
        if (!string.Equals(instance.Id, group.TargetInstanceId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Download target does not match the instance sidecar.");
        }
        if (group.Kind == DownloadQueueGroupKind.ModDependencyRepair)
        {
            await InstallRepairAsync(
                await InspectRepairAsync(group, item, cancellationToken),
                item,
                cancellationToken);
            return;
        }

        var catalog = getCatalog();
        var loaderManager = new LoaderManager(
            paths.InstanceRoot,
            paths.TransactionRoot,
            Path.Combine(paths.StateRoot, "loader.json"),
            paths.PackageRoot,
            httpClient);
        var modManager = new ModManager(
            paths.InstanceRoot,
            paths.TransactionRoot,
            Path.Combine(paths.StateRoot, "mods"),
            paths.PackageRoot,
            httpClient);
        var requestedMod = group.Items.SingleOrDefault(candidate => candidate.Kind == DownloadQueueItemKind.Mod);
        if (requestedMod is null)
        {
            await InstallStandaloneLoaderAsync(
                instance,
                item,
                ResolvePackage(catalog, item),
                loaderManager,
                paths.PackageRoot,
                cancellationToken);
            return;
        }

        var service = new ModInstallService(
            instance, catalog.Mods, catalog.Loaders, loaderManager, modManager);
        var plan = await service.CreatePlanAsync(requestedMod.PackageId, cancellationToken);
        var planItem = ResolvePlanItem(plan, item);
        await VerifySatisfiedPrerequisitesAsync(
            plan.Items.TakeWhile(candidate => !ReferenceEquals(candidate, planItem)),
            catalog.Mods,
            modManager,
            item.PackageId,
            cancellationToken);
        if (planItem.State == ModInstallPlanItemState.Satisfied)
        {
            if (planItem.Kind == ModInstallPlanItemKind.Loader)
            {
                return;
            }
            var installedPackage = ResolvePackage(catalog, item);
            await modManager.VerifyInstalledAsync(installedPackage.Mod!, cancellationToken);
            return;
        }

        var package = ResolvePackage(catalog, item);
        var packagePath = CachePath(paths.PackageRoot, package.Sha256);
        if (item.Kind == DownloadQueueItemKind.Loader)
        {
            await loaderManager.InstallFromFileAsync(package.Loader!, packagePath, cancellationToken);
            return;
        }
        await InstallModAsync(package.Mod!, planItem.State, packagePath, modManager, cancellationToken);
    }

    private static ModInstallPlanItem ResolvePlanItem(
        ModInstallPlan plan,
        DownloadQueueItem item)
    {
        if (plan.Items.FirstOrDefault(candidate => candidate.State == ModInstallPlanItemState.Blocked)
            is { } blocked)
        {
            throw new InvalidOperationException(blocked.Reason);
        }
        var planItem = plan.Items.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, item.PackageId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException($"Package '{item.PackageId}' is not in the current install plan.");
        if (!string.Equals(planItem.Version, item.Version, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(planItem.LoaderId, item.LoaderId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Package '{item.PackageId}' no longer matches the current install plan.");
        }
        return planItem;
    }

    private static async Task VerifySatisfiedPrerequisitesAsync(
        IEnumerable<ModInstallPlanItem> prerequisites,
        IReadOnlyList<ModManifest> catalog,
        ModManager modManager,
        string packageId,
        CancellationToken cancellationToken)
    {
        foreach (var prerequisite in prerequisites)
        {
            if (prerequisite.State != ModInstallPlanItemState.Satisfied)
            {
                throw new InvalidOperationException(
                    $"Package '{packageId}' requires '{prerequisite.Id}' to be installed first.");
            }
            if (prerequisite.Kind == ModInstallPlanItemKind.Loader)
            {
                continue;
            }
            var manifest = catalog.SingleOrDefault(candidate =>
                string.Equals(candidate.Id, prerequisite.Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(candidate.Version, prerequisite.Version, StringComparison.OrdinalIgnoreCase)
                && string.Equals(candidate.LoaderId, prerequisite.LoaderId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException(
                    $"Mod package '{prerequisite.Id}' version '{prerequisite.Version}' for loader '{prerequisite.LoaderId}' was not found in the current catalog.");
            await modManager.VerifyInstalledAsync(manifest, cancellationToken);
        }
    }

    private static async Task InstallStandaloneLoaderAsync(
        InstanceRecord instance,
        DownloadQueueItem item,
        ResolvedPackage package,
        LoaderManager loaderManager,
        string packageRoot,
        CancellationToken cancellationToken)
    {
        if (item.Kind != DownloadQueueItemKind.Loader || package.Loader is null)
        {
            throw new InvalidDataException("A mod install group must contain one requested mod.");
        }
        if (instance.Purpose == InstancePurpose.OfficialSpeedrun)
        {
            throw new InvalidOperationException("Official speedrun instances cannot be modified.");
        }
        if (string.Equals(instance.BuildId, "unknown", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Unknown game builds are restricted to vanilla mode.");
        }
        if (!package.Loader.SupportedBuildIds.Contains(instance.BuildId, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Loader '{package.Loader.Id}' is not compatible with build '{instance.BuildId}'.");
        }
        var inspection = await loaderManager.InspectAsync(cancellationToken);
        if (inspection.State is LoaderState.ModdingApi or LoaderState.BepInEx
            && string.Equals(inspection.PackageId, package.Loader.Id, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        if (inspection.State != LoaderState.Vanilla)
        {
            throw new InvalidOperationException(
                $"Loader '{package.Loader.Id}' cannot be installed over '{inspection.PackageId ?? inspection.State.ToString()}'.");
        }
        await loaderManager.InstallFromFileAsync(
            package.Loader, CachePath(packageRoot, package.Sha256), cancellationToken);
    }

    private static async Task InstallModAsync(
        ModManifest manifest,
        ModInstallPlanItemState state,
        string packagePath,
        ModManager manager,
        CancellationToken cancellationToken)
    {
        if (state == ModInstallPlanItemState.NeedsInstall)
        {
            await manager.InstallFromFileAsync(manifest, packagePath, cancellationToken);
            return;
        }
        if (state != ModInstallPlanItemState.NeedsUpdate)
        {
            throw new InvalidOperationException($"Mod '{manifest.Id}' is blocked by the current install plan.");
        }

        var current = (await manager.GetInstalledAsync(cancellationToken)).Single(receipt =>
            string.Equals(receipt.Id, manifest.Id, StringComparison.OrdinalIgnoreCase));
        if (!string.Equals(current.Version, manifest.Version, StringComparison.OrdinalIgnoreCase))
        {
            current = await manager.UpdateFromFileAsync(manifest, packagePath, cancellationToken);
        }
        if (!current.Enabled)
        {
            await manager.SetEnabledAsync(manifest.Id, enabled: true, cancellationToken);
        }
    }

    private async Task<RepairContext> InspectRepairAsync(
        DownloadQueueGroup group,
        DownloadQueueItem item,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(group.ExpectedBuildId)
            || string.IsNullOrWhiteSpace(group.ExpectedLoaderId)
            || item.Kind is not (DownloadQueueItemKind.Dependency or DownloadQueueItemKind.DependencyReEnable)
            || !string.Equals(item.LoaderId, group.ExpectedLoaderId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The dependency repair queue item is invalid.");
        }

        var paths = GetPaths(group);
        var instance = await InstanceSidecar.LoadAsync(paths.InstanceRoot, cancellationToken);
        if (!string.Equals(instance.Id, group.TargetInstanceId, StringComparison.Ordinal)
            || !string.Equals(instance.BuildId, group.ExpectedBuildId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The dependency repair target instance or build has changed.");
        }
        if (instance.Purpose == InstancePurpose.OfficialSpeedrun)
        {
            throw new InvalidOperationException("Official speedrun instances cannot be modified.");
        }

        var loaderManager = new LoaderManager(
            paths.InstanceRoot,
            paths.TransactionRoot,
            Path.Combine(paths.StateRoot, "loader.json"),
            paths.PackageRoot,
            httpClient);
        var inspection = await loaderManager.InspectAsync(cancellationToken);
        if (inspection.State is not (LoaderState.ModdingApi or LoaderState.BepInEx)
            || !string.Equals(inspection.PackageId, group.ExpectedLoaderId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The dependency repair target loader has changed.");
        }

        var modManager = new ModManager(
            paths.InstanceRoot,
            paths.TransactionRoot,
            Path.Combine(paths.StateRoot, "mods"),
            paths.PackageRoot,
            httpClient);
        var receipt = (await modManager.GetInstalledAsync(cancellationToken)).SingleOrDefault(candidate =>
            string.Equals(candidate.Id, item.PackageId, StringComparison.OrdinalIgnoreCase));
        if (receipt is not null
            && (!string.Equals(receipt.Version, item.Version, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(receipt.LoaderId, item.LoaderId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Installed dependency '{item.PackageId}' no longer matches the repair plan.");
        }
        if (item.Kind == DownloadQueueItemKind.DependencyReEnable && receipt is null)
        {
            throw new KeyNotFoundException(
                $"Installed dependency '{item.PackageId}' is missing from the target instance.");
        }

        ModManifest? manifest = null;
        if (item.Kind == DownloadQueueItemKind.Dependency)
        {
            manifest = ResolveMod(getCatalog(), item).Mod!;
            if (!manifest.SupportedBuildIds.Contains(group.ExpectedBuildId, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Dependency '{item.PackageId}' is not compatible with build '{group.ExpectedBuildId}'.");
            }
        }
        return new RepairContext(paths, modManager, receipt, manifest);
    }

    private static async Task InstallRepairAsync(
        RepairContext context,
        DownloadQueueItem item,
        CancellationToken cancellationToken)
    {
        if (context.Receipt is not null)
        {
            if (!context.Receipt.Enabled)
            {
                await context.ModManager.SetEnabledAsync(item.PackageId, enabled: true, cancellationToken);
            }
            return;
        }

        if (item.Kind != DownloadQueueItemKind.Dependency || context.Manifest is null)
        {
            throw new InvalidDataException("The dependency repair package is unavailable.");
        }
        await context.ModManager.InstallFromFileAsync(
            context.Manifest,
            CachePath(context.Paths.PackageRoot, context.Manifest.Sha256),
            cancellationToken);
    }

    private static ResolvedPackage ResolvePackage(GameCatalog catalog, DownloadQueueItem item) => item.Kind switch
    {
        DownloadQueueItemKind.Loader => ResolveLoader(catalog, item),
        DownloadQueueItemKind.Dependency or DownloadQueueItemKind.Mod => ResolveMod(catalog, item),
        _ => throw new NotSupportedException($"Package kind '{item.Kind}' is not supported by this executor.")
    };

    private static ResolvedPackage ResolveLoader(GameCatalog catalog, DownloadQueueItem item)
    {
        var manifest = catalog.Loaders.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, item.PackageId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.Version, item.Version, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.Id, item.LoaderId, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException(
                $"Loader package '{item.PackageId}' version '{item.Version}' was not found in the current catalog.");
        return new ResolvedPackage(
            manifest.DownloadUrl, manifest.SizeBytes, manifest.Sha256, manifest, null);
    }

    private static ResolvedPackage ResolveMod(GameCatalog catalog, DownloadQueueItem item)
    {
        var manifest = catalog.Mods.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, item.PackageId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.Version, item.Version, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.LoaderId, item.LoaderId, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException(
                $"Mod package '{item.PackageId}' version '{item.Version}' for loader '{item.LoaderId}' was not found in the current catalog.");
        return new ResolvedPackage(
            manifest.DownloadUrl, manifest.SizeBytes, manifest.Sha256, null, manifest);
    }

    private static ExecutorPaths GetPaths(DownloadQueueGroup group)
    {
        var instanceRoot = Path.GetFullPath(group.TargetInstanceRoot);
        var versionRoot = Directory.GetParent(instanceRoot)?.FullName
            ?? throw new ArgumentException("Instance root must have a parent directory.", nameof(group));
        var dataRoot = Path.Combine(versionRoot, ".crystalfly");
        return new ExecutorPaths(
            instanceRoot,
            InstanceDirectory.ResolveUnderRoot(Path.Combine(dataRoot, "instances"), group.TargetInstanceId),
            Path.Combine(dataRoot, "transactions"),
            Path.Combine(dataRoot, "packages"));
    }

    private static string CachePath(string packageRoot, string sha256) =>
        Path.Combine(packageRoot, $"{sha256.ToUpperInvariant()}.zip");

    private sealed record ResolvedPackage(
        string DownloadUrl,
        long? SizeBytes,
        string Sha256,
        LoaderManifest? Loader,
        ModManifest? Mod);

    private sealed record ExecutorPaths(
        string InstanceRoot,
        string StateRoot,
        string TransactionRoot,
        string PackageRoot);

    private sealed record RepairContext(
        ExecutorPaths Paths,
        ModManager ModManager,
        InstalledModReceipt? Receipt,
        ModManifest? Manifest);
}

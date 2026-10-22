using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;
using Crystalfly.Core.Networking;
using Crystalfly.Core.Packages;
using Crystalfly.Core.Thunderstore;

namespace Crystalfly.App.Downloads;

public sealed class ThunderstoreQueueExecutor(IDownloadQueueExecutor fallback, ThunderstorePackageStore store,
    InstanceOperationCoordinator coordinator, INetworkPolicy networkPolicy) : IDownloadQueueExecutor
{
    public const string Prefix = "thunderstore-plan:";
    private static bool IsThunderstore(DownloadQueueItem item) => item.PackageId.StartsWith(Prefix, StringComparison.Ordinal);
    public bool RequiresGameExit(DownloadQueueItem item) => IsThunderstore(item) || fallback.RequiresGameExit(item);
    public bool IsTransient(Exception exception) => fallback.IsTransient(exception);

    public async Task TransferAsync(DownloadQueueGroup group, DownloadQueueItem item,
        IProgress<PackageTransferProgress> progress, SemaphoreSlim networkGate, CancellationToken cancellationToken)
    {
        if (!IsThunderstore(item))
        {
            await fallback.TransferAsync(group, item, progress, networkGate, cancellationToken);
            return;
        }
        var plan = GetPlan(group);
        await networkGate.WaitAsync(cancellationToken);
        try
        {
            long completed = 0;
            var total = plan.Packages.Sum(package => package.Version.FileSize);
            foreach (var package in plan.Packages)
            {
                progress.Report(new(completed, total, 0, package.Package.FullName));
                await store.GetAsync(package, networkPolicy.IsOffline, cancellationToken,
                    refresh: group.ThunderstoreRepair && !networkPolicy.IsOffline && !package.IsLocal);
                completed += package.Version.FileSize;
                progress.Report(new(completed, total, 0, package.Package.FullName));
            }
        }
        finally { networkGate.Release(); }
    }

    public async Task InstallAsync(DownloadQueueGroup group, DownloadQueueItem item, CancellationToken cancellationToken)
    {
        if (!IsThunderstore(item))
        {
            await fallback.InstallAsync(group, item, cancellationToken);
            return;
        }
        var plan = GetPlan(group);
        await coordinator.RunAsync(group.TargetInstanceId, async token =>
        {
            var instance = await InstanceSidecar.LoadAsync(group.TargetInstanceRoot, token)
                ?? throw new InvalidDataException("The target instance no longer exists.");
            if (instance.Id != group.TargetInstanceId || instance.GameId != group.GameId || instance.BuildId != plan.BuildId)
                throw new InvalidDataException("The target instance changed while queued.");
            await new ThunderstoreInstallService(instance, store).InstallAsync(plan,
                networkPolicy.IsOffline, confirmUnverified: true, repair: group.ThunderstoreRepair, token: token);
        }, cancellationToken);
    }

    private static ThunderstoreInstallPlan GetPlan(DownloadQueueGroup group) =>
        group.ThunderstorePlan is { GameId: GameId.Silksong } plan && group.GameId == GameId.Silksong
            ? plan : throw new InvalidDataException("Missing or cross-game Thunderstore plan.");

    public static DownloadQueueGroup Create(InstanceRecord instance, ThunderstoreInstallPlan plan, string title, bool repair)
    {
        if (instance.GameId != plan.GameId || instance.BuildId != plan.BuildId)
            throw new InvalidDataException("Plan target does not match the instance.");
        var id = Guid.NewGuid().ToString("N");
        return new DownloadQueueGroup
        {
            Id = id, GameId = instance.GameId, TargetInstanceId = instance.Id, TargetInstanceName = instance.Name,
            TargetInstanceRoot = instance.RootPath, ExpectedBuildId = instance.BuildId,
            Name = title, Kind = DownloadQueueGroupKind.ModInstall,
            DeduplicationKey = $"{Prefix}{instance.Id}:{string.Join(';', plan.Packages.Select(item => item.Version.FullName))}:{repair}",
            ThunderstorePlan = plan, ThunderstoreRepair = repair, CreatedAt = DateTimeOffset.UtcNow,
            Items = [new DownloadQueueItem
            {
                Id = id + ":closure", Kind = DownloadQueueItemKind.Mod, PackageId = Prefix + id,
                Name = title, LoaderId = "bepinex-silksong", Version = "", SizeBytes = plan.Packages.Sum(item => item.Version.FileSize)
            }]
        };
    }
}

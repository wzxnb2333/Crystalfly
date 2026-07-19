using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;

namespace Crystalfly.App.Downloads;

public static class ModInstallQueueGroupFactory
{
    public static DownloadQueueGroup Create(
        ModInstallPlan plan,
        GameCatalog catalog,
        InstanceRecord instance)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(instance);
        if (!string.Equals(plan.InstanceId, instance.Id, StringComparison.Ordinal)
            || plan.Items.Count == 0
            || plan.Items[^1].Kind != ModInstallPlanItemKind.Mod
            || !string.Equals(plan.Items[^1].Id, plan.ModId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The mod install plan does not match the target instance or requested mod.");
        }
        if (plan.IsBlocked)
        {
            var reason = plan.Items.First(item => item.State == ModInstallPlanItemState.Blocked).Reason;
            throw new InvalidOperationException(reason);
        }

        return new DownloadQueueGroup
        {
            Id = Guid.NewGuid().ToString("N"),
            DeduplicationKey = $"{instance.Id}:mod:{plan.ModId}",
            Kind = DownloadQueueGroupKind.ModInstall,
            Name = plan.Items[^1].Name,
            TargetInstanceId = instance.Id,
            TargetInstanceName = instance.Name,
            TargetInstanceRoot = instance.RootPath,
            CreatedAt = DateTimeOffset.UtcNow,
            Items = plan.Items.Select(item => CreateItem(item, catalog)).ToArray()
        };
    }

    private static DownloadQueueItem CreateItem(ModInstallPlanItem item, GameCatalog catalog)
    {
        var package = item.Kind == ModInstallPlanItemKind.Loader
            ? ResolveLoader(item, catalog)
            : ResolveMod(item, catalog);
        var satisfied = item.State == ModInstallPlanItemState.Satisfied;
        var size = package.SizeBytes ?? 0;
        return new DownloadQueueItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = item.Kind switch
            {
                ModInstallPlanItemKind.Loader => DownloadQueueItemKind.Loader,
                ModInstallPlanItemKind.Dependency => DownloadQueueItemKind.Dependency,
                _ => DownloadQueueItemKind.Mod
            },
            PackageId = item.Id,
            Name = item.Name,
            Version = item.Version,
            LoaderId = item.LoaderId,
            DownloadUrl = package.DownloadUrl,
            SizeBytes = package.SizeBytes,
            Sha256 = package.Sha256,
            IsSatisfied = satisfied,
            State = DownloadQueueItemState.Pending,
            CompletedBytes = satisfied ? size : 0,
            TotalBytes = size,
            Stage = satisfied ? "Satisfied" : "Pending",
            CompletedAt = null
        };
    }

    private static PackageData ResolveLoader(ModInstallPlanItem item, GameCatalog catalog)
    {
        var manifest = catalog.Loaders.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, item.Id, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.Version, item.Version, StringComparison.OrdinalIgnoreCase));
        if (manifest is null)
        {
            if (item.State == ModInstallPlanItemState.Satisfied)
            {
                return new PackageData(null, null, null);
            }
            throw new InvalidDataException($"Loader '{item.Id}' version '{item.Version}' is missing from the catalog.");
        }
        return new PackageData(manifest.DownloadUrl, manifest.SizeBytes, manifest.Sha256);
    }

    private static PackageData ResolveMod(ModInstallPlanItem item, GameCatalog catalog)
    {
        var manifest = catalog.Mods.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, item.Id, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.Version, item.Version, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.LoaderId, item.LoaderId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException(
                $"Mod '{item.Id}' version '{item.Version}' is missing from the catalog.");
        return new PackageData(manifest.DownloadUrl, manifest.SizeBytes, manifest.Sha256);
    }

    private sealed record PackageData(string? DownloadUrl, long? SizeBytes, string? Sha256);
}

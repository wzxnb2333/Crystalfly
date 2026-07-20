using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;

namespace Crystalfly.App.Downloads;

public static class ModDependencyRepairQueueGroupFactory
{
    public static DownloadQueueGroup Create(
        ModDependencyRepairPlan plan,
        GameCatalog catalog,
        InstanceRecord instance)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(instance);
        if (!string.Equals(plan.BuildId, instance.BuildId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The dependency repair plan does not match the target instance build.");
        }

        var repairableItems = plan.Items
            .Where(item => item.Action != ModDependencyRepairAction.Unresolved)
            .ToArray();
        var items = repairableItems
            .Select(item => CreateItem(item, catalog, plan.BuildId))
            .ToArray();
        if (items.Length == 0)
        {
            throw new InvalidOperationException("The dependency repair plan contains no automatic repairs.");
        }

        return new DownloadQueueGroup
        {
            Id = Guid.NewGuid().ToString("N"),
            DeduplicationKey = $"{instance.Id}:repair:{plan.LoaderId}:{CreateStepSummary(repairableItems)}",
            Kind = DownloadQueueGroupKind.ModDependencyRepair,
            Name = "Repair dependencies",
            TargetInstanceId = instance.Id,
            TargetInstanceName = instance.Name,
            TargetInstanceRoot = instance.RootPath,
            ExpectedBuildId = plan.BuildId,
            ExpectedLoaderId = plan.LoaderId,
            CreatedAt = DateTimeOffset.UtcNow,
            Items = items
        };
    }

    private static string CreateStepSummary(IEnumerable<ModDependencyRepairPlanItem> items) =>
        string.Join(
            ";",
            items
                .OrderBy(item => item.PackageId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Action)
                .ThenBy(item => item.Version, StringComparer.OrdinalIgnoreCase)
                .Select(item => string.Join(
                    "|",
                    Uri.EscapeDataString(item.PackageId),
                    item.Action,
                    Uri.EscapeDataString(item.Version))));

    private static DownloadQueueItem CreateItem(
        ModDependencyRepairPlanItem item,
        GameCatalog catalog,
        string buildId)
    {
        if (item.Action == ModDependencyRepairAction.ReEnable)
        {
            return NewItem(item, DownloadQueueItemKind.DependencyReEnable, null);
        }

        var manifest = catalog.Mods.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, item.PackageId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.Version, item.Version, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.LoaderId, item.LoaderId, StringComparison.OrdinalIgnoreCase)
            && candidate.SupportedBuildIds.Contains(buildId, StringComparer.OrdinalIgnoreCase))
            ?? throw new InvalidDataException(
                $"Dependency package '{item.PackageId}' version '{item.Version}' is missing from the catalog.");
        return NewItem(item, DownloadQueueItemKind.Dependency, manifest);
    }

    private static DownloadQueueItem NewItem(
        ModDependencyRepairPlanItem item,
        DownloadQueueItemKind kind,
        ModManifest? manifest) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Kind = kind,
        PackageId = item.PackageId,
        Name = item.Name,
        Version = item.Version,
        LoaderId = item.LoaderId,
        DownloadUrl = manifest?.DownloadUrl,
        SizeBytes = manifest?.SizeBytes,
        Sha256 = manifest?.Sha256,
        TotalBytes = manifest?.SizeBytes ?? 0,
        State = DownloadQueueItemState.Pending,
        Stage = "Pending"
    };
}

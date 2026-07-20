namespace Crystalfly.Core.Mods;

public enum ModDependencyRepairAction
{
    ReEnable,
    DownloadAndInstall,
    Unresolved
}

public sealed record ModDependencyRepairPlanItem
{
    public required string ModId { get; init; }

    public required string PackageId { get; init; }

    public required string Name { get; init; }

    public required string Version { get; init; }

    public required string LoaderId { get; init; }

    public required ModDependencyRepairAction Action { get; init; }

    public required IReadOnlyList<string> RequiredByModIds { get; init; }

    public required string Reason { get; init; }
}

public sealed record ModDependencyRepairPlan
{
    public required string BuildId { get; init; }

    public required string LoaderId { get; init; }

    public required IReadOnlyList<ModDependencyRepairPlanItem> Items { get; init; }

    public bool HasUnresolved => Items.Any(item => item.Action == ModDependencyRepairAction.Unresolved);
}

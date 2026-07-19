namespace Crystalfly.Core.Mods;

public enum ModInstallPlanItemKind
{
    Loader,
    Dependency,
    Mod
}

public enum ModInstallPlanItemState
{
    Satisfied,
    NeedsInstall,
    NeedsUpdate,
    Blocked
}

public sealed record ModInstallPlanItem
{
    public required ModInstallPlanItemKind Kind { get; init; }

    public required ModInstallPlanItemState State { get; init; }

    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Version { get; init; }

    public required string LoaderId { get; init; }

    public required string Reason { get; init; }
}

public sealed record ModInstallPlan
{
    public required string ModId { get; init; }

    public required string InstanceId { get; init; }

    public required string InstanceName { get; init; }

    public required IReadOnlyList<ModInstallPlanItem> Items { get; init; }

    public bool IsBlocked => Items.Any(item => item.State == ModInstallPlanItemState.Blocked);
}

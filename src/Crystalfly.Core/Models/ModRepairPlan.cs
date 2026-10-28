namespace Crystalfly.Core.Models;

public enum ModRepairAction
{
    Repair,
    Update,
    Unavailable
}

public sealed record ModRepairPlan
{
    public required string ModId { get; init; }

    public required string InstalledVersion { get; init; }

    public required ModRepairAction Action { get; init; }

    public string? TargetVersion { get; init; }

    public ModManifest? Manifest { get; init; }

    public required string Reason { get; init; }
}

public sealed record ModBatchUninstallResult
{
    public IReadOnlyList<string> RemovedModIds { get; init; } = [];

    public IReadOnlyList<string> SkippedPinnedModIds { get; init; } = [];
}

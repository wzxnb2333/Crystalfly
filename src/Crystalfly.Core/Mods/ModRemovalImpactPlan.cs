namespace Crystalfly.Core.Mods;

public enum ModRemovalImpactKind
{
    WillRemove,
    DependencyWillBeMissing
}

public sealed record ModRemovalImpactNode
{
    public required string ModId { get; init; }

    public required string ReceiptName { get; init; }

    public required string InstallRoot { get; init; }

    public required bool Enabled { get; init; }

    public required ModRemovalImpactKind Kind { get; init; }

    public string? RelatedToModId { get; init; }

    public required int Depth { get; init; }
}

public sealed record ModRemovalImpactPlan
{
    public required IReadOnlyList<string> TargetModIds { get; init; }

    public required IReadOnlyList<ModRemovalImpactNode> Nodes { get; init; }
}

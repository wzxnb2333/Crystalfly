namespace Crystalfly.Core.Models;

public sealed record SpeedrunFileManifest
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string Id { get; init; }

    public required string BuildId { get; init; }

    public required string RulesRevision { get; init; }

    public IReadOnlyList<SpeedrunFileRule> Files { get; init; } = [];
}

public sealed record SpeedrunFileRule
{
    public required string RelativePath { get; init; }

    public required string Sha256 { get; init; }

    public SpeedrunFileKind Kind { get; init; }

    public string? AssetId { get; init; }

    public string? AssetVersion { get; init; }
}

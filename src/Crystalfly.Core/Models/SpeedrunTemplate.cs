namespace Crystalfly.Core.Models;

public sealed record SpeedrunTemplate
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string BuildId { get; init; }

    public bool IsOfficial { get; init; }

    public string RulesRevision { get; init; } = "";

    public string FileManifestId { get; init; } = "";

    public string? LoaderId { get; init; }

    public IReadOnlyList<string> RequiredAssetIds { get; init; } = [];

    public bool LoadNormaliserAvailable { get; init; }

    public bool RequiresLoadNormaliserSelection { get; init; }

    public IReadOnlyList<int> AllowedLoadNormaliserSeconds { get; init; } = [];
}

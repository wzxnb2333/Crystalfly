namespace Crystalfly.Core.Models;

public sealed record ModActivityEntry
{
    public required string Id { get; init; }

    public DateTimeOffset AddedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record ModActivityCatalog
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public DateTimeOffset GeneratedAt { get; init; }

    public required string SourceRevision { get; init; }

    public IReadOnlyList<ModActivityEntry> Entries { get; init; } = [];
}

public enum ModActivityLoadStatus
{
    Remote,
    Cached,
    Embedded
}

public sealed record ModActivityLoadResult(
    ModActivityLoadStatus Status,
    ModActivityCatalog Catalog,
    string? Reason);

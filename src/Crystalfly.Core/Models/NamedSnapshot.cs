namespace Crystalfly.Core.Models;

public sealed record NamedSnapshot
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string SourcePath { get; init; }

    public required string SnapshotPath { get; init; }

    public required string Sha256 { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

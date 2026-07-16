namespace Crystalfly.Core.Models;

public sealed record LocalLowTakeoverRecord
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public TransactionState State { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public required string SharedPath { get; init; }

    public required string BackupPath { get; init; }

    public required string StagingPath { get; init; }

    public required string SharedSha256 { get; init; }

    public string? Error { get; init; }
}

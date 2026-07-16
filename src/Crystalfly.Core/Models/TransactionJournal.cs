namespace Crystalfly.Core.Models;

public enum TransactionState
{
    Prepared,
    Applying,
    Committed,
    RollingBack,
    RolledBack,
    NeedsAttention
}

public sealed record TransactionJournal
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string Id { get; init; }

    public required string Operation { get; init; }

    public TransactionState State { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public IReadOnlyList<string> CreatedPaths { get; init; } = [];

    public IReadOnlyDictionary<string, string> BackupPaths { get; init; } =
        new Dictionary<string, string>();
}

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

    public string RootPath { get; init; } = string.Empty;

    public string RestorePointPath { get; init; } = string.Empty;

    public IReadOnlyList<string> CreatedPaths { get; init; } = [];

    public IReadOnlyList<string> CreatedDirectories { get; init; } = [];

    public IReadOnlyList<string> RemovedDirectories { get; init; } = [];

    public IReadOnlyDictionary<string, string> BackupPaths { get; init; } =
        new Dictionary<string, string>();

    public IReadOnlyList<TransactionFileChange> Changes { get; init; } = [];

    public string? Error { get; init; }
}

public sealed record TransactionFileChange
{
    public required string RelativePath { get; init; }

    public bool IsDeletion { get; init; }

    public string? BackupRelativePath { get; init; }

    public string? OriginalSha256 { get; init; }

    public required string AppliedSha256 { get; init; }
}

namespace Crystalfly.Core.Models;

public sealed record ModContentDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string RepositoryUrl { get; init; }

    public string? ReadmeMarkdown { get; init; }

    public string? ReadmeETag { get; init; }

    public string? ReleaseNotesMarkdown { get; init; }

    public string? ReleaseETag { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

public enum ModContentLoadStatus
{
    Remote,
    Cached,
    Unavailable
}

public sealed record ModContentLoadResult(
    ModContentLoadStatus Status,
    ModContentDocument? Document,
    string? Reason);

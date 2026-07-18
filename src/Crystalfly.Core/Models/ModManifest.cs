namespace Crystalfly.Core.Models;

public sealed record ModManifest
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? DisplayName { get; init; }

    public string? SourceName { get; init; }

    public string? Description { get; init; }

    public IReadOnlyList<string> Authors { get; init; } = [];

    public IReadOnlyList<string> Tags { get; init; } = [];

    public IReadOnlyList<string> Integrations { get; init; } = [];

    public string? RepositoryUrl { get; init; }

    public string? IssuesUrl { get; init; }

    public required string Version { get; init; }

    public required string DownloadUrl { get; init; }

    public long? SizeBytes { get; init; }

    public required string Sha256 { get; init; }

    public required string LoaderId { get; init; }

    public IReadOnlyList<string> SupportedBuildIds { get; init; } = [];

    public IReadOnlyList<string> Dependencies { get; init; } = [];

    public IReadOnlyList<string> FlatFiles { get; init; } = [];
}

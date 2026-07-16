namespace Crystalfly.Core.Models;

public sealed record LoaderManifest
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Version { get; init; }

    public required string DownloadUrl { get; init; }

    public long? SizeBytes { get; init; }

    public required string Sha256 { get; init; }

    public IReadOnlyList<string> SupportedBuildIds { get; init; } = [];

    public IReadOnlyList<string> ManagedFiles { get; init; } = [];
}

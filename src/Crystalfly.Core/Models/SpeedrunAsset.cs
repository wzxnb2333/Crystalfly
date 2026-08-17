namespace Crystalfly.Core.Models;

public sealed record SpeedrunAsset
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Version { get; init; }

    public required string DownloadUrl { get; init; }

    public long? SizeBytes { get; init; }

    public required string Sha256 { get; init; }

    /// <summary>Optional archive entry when a publisher keeps the DLL in a named folder.</summary>
    public string? ArchiveEntryPath { get; init; }

    public IReadOnlyList<string> SupportedBuildIds { get; init; } = [];
}

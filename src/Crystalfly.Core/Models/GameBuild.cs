namespace Crystalfly.Core.Models;

public sealed record GameBuild
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string Id { get; init; }

    public required string DisplayVersion { get; init; }

    public uint DepotId { get; init; }

    public required string ManifestId { get; init; }

    public required string ExecutableSha256 { get; init; }

    public string? UnityPlayerSha256 { get; init; }

    public required string GlobalGameManagersSha256 { get; init; }
}

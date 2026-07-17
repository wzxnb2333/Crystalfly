namespace Crystalfly.Core.Models;

public sealed record BuildFingerprint
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string ExecutableSha256 { get; init; }

    public string? UnityPlayerSha256 { get; init; }

    public required string GlobalGameManagersSha256 { get; init; }
}

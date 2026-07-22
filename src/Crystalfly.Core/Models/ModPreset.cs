namespace Crystalfly.Core.Models;

public enum ModPresetApplyMode
{
    Append,
    Exact
}

public sealed record ModPresetEntry
{
    public string? Id { get; init; }

    public required string Name { get; init; }

    public string? Version { get; init; }

    public IReadOnlyList<string> FileHashes { get; init; } = [];
}

public sealed record ModPreset
{
    public const int CurrentSchemaVersion = 1;
    public const int MaxDocumentBytes = 128 * 1024;
    public const int MaxEntries = 1000;
    public const int MaxPresetIdLength = 160;
    public const int MaxPresetNameLength = 120;
    public const int MaxBuildIdLength = 160;
    public const int MaxLoaderIdLength = 160;
    public const int MaxEntryIdLength = 256;
    public const int MaxEntryNameLength = 256;
    public const int MaxEntryVersionLength = 128;
    public const int MaxFileHashesPerEntry = 1024;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string GameBuildId { get; init; }

    public required string LoaderId { get; init; }

    public ModPresetApplyMode ApplyMode { get; init; }

    public IReadOnlyList<ModPresetEntry> Entries { get; init; } = [];
}

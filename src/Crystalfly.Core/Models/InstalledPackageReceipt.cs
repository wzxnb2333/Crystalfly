namespace Crystalfly.Core.Models;

public enum LoaderState
{
    Vanilla,
    ModdingApi,
    BepInEx,
    Conflict,
    Drifted
}

public sealed record InstalledPackageReceipt
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string PackageId { get; init; }

    public LoaderState LoaderState { get; init; }

    public IReadOnlyList<InstalledFileReceipt> Files { get; init; } = [];
}

public sealed record InstalledFileReceipt
{
    public required string RelativePath { get; init; }

    public required string Sha256 { get; init; }
}

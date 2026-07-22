namespace Crystalfly.Core.Models;

public enum ModOwnership
{
    Managed,
    External,
    LocalTakenOver
}

public sealed record InstalledModReceipt
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Version { get; init; }

    public required string LoaderId { get; init; }

    public required string InstallRoot { get; init; }

    public bool Enabled { get; init; } = true;

    public bool IsLocal { get; init; }

    public ModOwnership Ownership { get; init; }

    public bool Pinned { get; init; }

    public IReadOnlyList<string> EntryFiles { get; init; } = [];

    public IReadOnlyList<string> Dependencies { get; init; } = [];

    public IReadOnlyList<InstalledFileReceipt> Files { get; init; } = [];
}

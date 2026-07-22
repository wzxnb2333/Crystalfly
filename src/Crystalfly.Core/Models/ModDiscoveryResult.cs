namespace Crystalfly.Core.Models;

public sealed record ModDiscoveryEntry
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string LoaderId { get; init; }

    public required string InstallRoot { get; init; }

    public bool Enabled { get; init; } = true;

    public ModOwnership Ownership { get; init; }

    public IReadOnlyList<string> Files { get; init; } = [];

    public IReadOnlyList<string> EntryFiles { get; init; } = [];

    public bool IsReadOnly => Ownership == ModOwnership.External;
}

public sealed record ModDiscoveryResult
{
    public IReadOnlyList<InstalledModReceipt> InstalledReceipts { get; init; } = [];

    public IReadOnlyList<ModDiscoveryEntry> Mods { get; init; } = [];

    public IReadOnlyList<ModDiscoveryEntry> ExternalMods =>
        Mods.Where(mod => mod.Ownership == ModOwnership.External).ToArray();
}

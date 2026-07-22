using System.Security.Cryptography;
using System.Text;
using Crystalfly.Core.Models;

namespace Crystalfly.Core.Mods;

public sealed class ModDiscoveryService
{
    private readonly string instanceRoot;
    private readonly string receiptsRoot;
    private readonly ModPathPolicy pathPolicy;

    public ModDiscoveryService(string instanceRoot, string receiptsRoot)
    {
        this.instanceRoot = Path.GetFullPath(instanceRoot);
        this.receiptsRoot = Path.GetFullPath(receiptsRoot);
        pathPolicy = new ModPathPolicy(this.instanceRoot);
    }

    public async Task<ModDiscoveryResult> DiscoverAsync(
        string loaderId,
        CancellationToken cancellationToken = default)
    {
        var installed = await InstalledModReceiptStore.ReadAllAsync(receiptsRoot, cancellationToken);
        return Discover(loaderId, installed);
    }

    internal ModDiscoveryResult Discover(
        string loaderId,
        IReadOnlyList<InstalledModReceipt> installed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(loaderId);
        var owned = installed.SelectMany(receipt => receipt.Files)
            .Select(file => Normalize(file.RelativePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var external = new List<ModDiscoveryEntry>();
        var managedLoader = loaderId.StartsWith("modding-api-", StringComparison.OrdinalIgnoreCase)
            ? loaderId
            : "modding-api-external";
        var bepInExLoader = loaderId.StartsWith("bepinex-", StringComparison.OrdinalIgnoreCase)
            ? loaderId
            : "bepinex-external";

        ScanRoot(
            "hollow_knight_Data/Managed/Mods", managedLoader, enabled: true, owned, external,
            excludedDirectoryName: "Disabled");
        ScanRoot(
            "hollow_knight_Data/Managed/Mods/Disabled", managedLoader, enabled: false, owned, external);
        ScanRoot("BepInEx/plugins", bepInExLoader, enabled: true, owned, external);
        ScanRoot("BepInEx/Disabled", bepInExLoader, enabled: false, owned, external);

        var managedEntries = installed.Select(receipt => new ModDiscoveryEntry
        {
            Id = receipt.Id,
            Name = receipt.Name,
            LoaderId = receipt.LoaderId,
            InstallRoot = receipt.InstallRoot,
            Enabled = receipt.Enabled,
            Ownership = receipt.Ownership,
            Files = receipt.Files.Select(file => file.RelativePath).ToArray(),
            EntryFiles = receipt.EntryFiles
        });
        return new ModDiscoveryResult
        {
            InstalledReceipts = installed,
            Mods = managedEntries.Concat(external)
                .OrderBy(mod => mod.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    private void ScanRoot(
        string relativeRoot,
        string loaderId,
        bool enabled,
        IReadOnlySet<string> owned,
        ICollection<ModDiscoveryEntry> results,
        string? excludedDirectoryName = null)
    {
        var fullRoot = pathPolicy.ResolveRecognized(relativeRoot).FullPath;
        if (!Directory.Exists(fullRoot) || pathPolicy.HasReparsePoint(fullRoot))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(fullRoot, "*", SearchOption.TopDirectoryOnly))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }
            var relativePath = pathPolicy.ToRelativePath(file);
            if (!owned.Contains(relativePath))
            {
                results.Add(CreateExternal(
                    Path.GetFileNameWithoutExtension(file), loaderId, relativeRoot, enabled, [relativePath]));
            }
        }

        foreach (var directory in Directory.EnumerateDirectories(fullRoot, "*", SearchOption.TopDirectoryOnly))
        {
            if (string.Equals(Path.GetFileName(directory), excludedDirectoryName, StringComparison.OrdinalIgnoreCase)
                || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }
            var files = pathPolicy.EnumerateFilesSafely(directory, rejectReparsePoints: false)
                .Select(pathPolicy.ToRelativePath)
                .Where(path => !owned.Contains(path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (files.Length == 0)
            {
                continue;
            }
            var installRoot = pathPolicy.ToRelativePath(directory);
            results.Add(CreateExternal(Path.GetFileName(directory), loaderId, installRoot, enabled, files));
        }
    }

    private static ModDiscoveryEntry CreateExternal(
        string name,
        string loaderId,
        string installRoot,
        bool enabled,
        IReadOnlyList<string> files)
    {
        var normalizedRoot = Normalize(installRoot);
        var idHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedRoot + "\n" + name)));
        return new ModDiscoveryEntry
        {
            Id = $"external-{idHash[..12].ToLowerInvariant()}",
            Name = name,
            LoaderId = loaderId,
            InstallRoot = normalizedRoot,
            Enabled = enabled,
            Ownership = ModOwnership.External,
            Files = files,
            EntryFiles = files.Where(IsEntryFile).ToArray()
        };
    }

    private static bool IsEntryFile(string path) =>
        string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string path) => path.Replace('\\', '/');
}

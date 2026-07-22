using System.Security.Cryptography;
using Crystalfly.Core.Models;

namespace Crystalfly.Core.Mods;

public sealed class ModHealthService
{
    private readonly string instanceRoot;

    public ModHealthService(string instanceRoot)
    {
        this.instanceRoot = Path.GetFullPath(instanceRoot);
    }

    public async Task<ModHealthReport> AssessAsync(
        InstalledModReceipt receipt,
        IReadOnlyList<InstalledModReceipt> installed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(installed);
        if (receipt.Files.Count == 0)
        {
            return Report(receipt.Id, ModHealthStatus.Indeterminate, detail: "Receipt does not list files.");
        }

        try
        {
            var missing = new List<string>();
            var modified = new List<string>();
            foreach (var file in receipt.Files)
            {
                var path = ResolveUnderRoot(file.RelativePath);
                if (!File.Exists(path))
                {
                    missing.Add(file.RelativePath);
                }
                else if (!string.Equals(
                    file.Sha256, await HashFileAsync(path, cancellationToken), StringComparison.OrdinalIgnoreCase))
                {
                    modified.Add(file.RelativePath);
                }
            }
            if (missing.Count != 0)
            {
                return Report(receipt.Id, ModHealthStatus.CriticalFileMissing, missing: missing);
            }
            if (modified.Count != 0)
            {
                return Report(receipt.Id, ModHealthStatus.ModifiedFile, modified: modified);
            }

            var owned = installed.SelectMany(mod => mod.Files)
                .Select(file => Normalize(file.RelativePath))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var normalizedInstallRoot = Normalize(receipt.InstallRoot).TrimEnd('/');
            var sharedFlatRoot = SharedRoots.Contains(normalizedInstallRoot);
            var installRoot = ResolveUnderRoot(receipt.InstallRoot);
            var extra = !sharedFlatRoot && Directory.Exists(installRoot)
                ? Directory.EnumerateFiles(installRoot, "*", SearchOption.AllDirectories)
                    .Select(path => Normalize(Path.GetRelativePath(instanceRoot, path)))
                    .Where(path => !owned.Contains(path))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                : [];
            return extra.Length == 0
                ? Report(receipt.Id, ModHealthStatus.Healthy)
                : Report(receipt.Id, ModHealthStatus.ExtraFile, extra: extra);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException)
        {
            return Report(receipt.Id, ModHealthStatus.Indeterminate, detail: exception.Message);
        }
    }

    public ModHealthReport AssessExternal(ModDiscoveryEntry external)
    {
        ArgumentNullException.ThrowIfNull(external);
        return Report(
            external.Id,
            external.Ownership == ModOwnership.External
                ? ModHealthStatus.UnmanagedExternal
                : ModHealthStatus.Indeterminate);
    }

    private static ModHealthReport Report(
        string id,
        ModHealthStatus status,
        IReadOnlyList<string>? missing = null,
        IReadOnlyList<string>? modified = null,
        IReadOnlyList<string>? extra = null,
        string? detail = null) => new()
        {
            ModId = id,
            Status = status,
            MissingFiles = missing ?? [],
            ModifiedFiles = modified ?? [],
            ExtraFiles = extra ?? [],
            Detail = detail
        };

    private string ResolveUnderRoot(string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(
            instanceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(instanceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Path escapes the instance root: '{relativePath}'.");
        }
        return path;
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static readonly IReadOnlySet<string> SharedRoots = new HashSet<string>(
        [
            "hollow_knight_Data/Managed/Mods",
            "hollow_knight_Data/Managed/Mods/Disabled",
            "BepInEx/plugins",
            "BepInEx/Disabled"
        ],
        StringComparer.OrdinalIgnoreCase);
}

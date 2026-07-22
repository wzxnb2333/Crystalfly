using System.Security.Cryptography;
using Crystalfly.Core.Models;

namespace Crystalfly.Core.Mods;

public sealed class ModHealthService
{
    private readonly ModPathPolicy pathPolicy;

    public ModHealthService(string instanceRoot)
    {
        pathPolicy = new ModPathPolicy(instanceRoot);
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
            var installRoot = pathPolicy.ResolveUnderInstance(receipt.InstallRoot);
            pathPolicy.EnsureNoReparsePoints(installRoot.FullPath);
            var missing = new List<string>();
            var modified = new List<string>();
            var currentFileSha256ByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in receipt.Files)
            {
                var path = pathPolicy.ResolveUnderOwnedRoot(file.RelativePath, installRoot);
                pathPolicy.EnsureNoReparsePoints(path.FullPath);
                if (!File.Exists(path.FullPath))
                {
                    missing.Add(file.RelativePath);
                }
                else
                {
                    string currentSha256 = await HashFileAsync(path.FullPath, cancellationToken);
                    currentFileSha256ByPath[path.RelativePath] = currentSha256;
                    if (!string.Equals(file.Sha256, currentSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        modified.Add(file.RelativePath);
                    }
                }
            }
            if (missing.Count != 0)
            {
                return Report(
                    receipt.Id,
                    ModHealthStatus.CriticalFileMissing,
                    missing: missing,
                    currentFileSha256ByPath: currentFileSha256ByPath);
            }
            if (modified.Count != 0)
            {
                return Report(
                    receipt.Id,
                    ModHealthStatus.ModifiedFile,
                    modified: modified,
                    currentFileSha256ByPath: currentFileSha256ByPath);
            }

            var owned = installed.SelectMany(mod => mod.Files)
                .Select(file => Normalize(file.RelativePath))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var extraFiles = !pathPolicy.IsSharedRoot(installRoot) && Directory.Exists(installRoot.FullPath)
                ? pathPolicy.EnumerateFilesSafely(
                    installRoot.FullPath,
                    rejectReparsePoints: true)
                : [];
            var extra = extraFiles.Count != 0
                ? extraFiles
                    .Select(pathPolicy.ToRelativePath)
                    .Where(path => !owned.Contains(path))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                : [];
            foreach (string relativePath in extra)
            {
                var extraPath = pathPolicy.ResolveUnderOwnedRoot(relativePath, installRoot);
                currentFileSha256ByPath[relativePath] = await HashFileAsync(
                    extraPath.FullPath,
                    cancellationToken);
            }
            return extra.Length == 0
                ? Report(
                    receipt.Id,
                    ModHealthStatus.Healthy,
                    currentFileSha256ByPath: currentFileSha256ByPath)
                : Report(
                    receipt.Id,
                    ModHealthStatus.ExtraFile,
                    extra: extra,
                    currentFileSha256ByPath: currentFileSha256ByPath);
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
        IReadOnlyDictionary<string, string>? currentFileSha256ByPath = null,
        string? detail = null) => new()
        {
            ModId = id,
            Status = status,
            MissingFiles = missing ?? [],
            ModifiedFiles = modified ?? [],
            ExtraFiles = extra ?? [],
            CurrentFileSha256ByPath = currentFileSha256ByPath
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Detail = detail
        };

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

}

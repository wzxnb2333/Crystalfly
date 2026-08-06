using System.Collections.Concurrent;
using System.Security.Cryptography;
using Crystalfly.Core.Models;

namespace Crystalfly.Core.Mods;

public sealed class ModHealthService
{
    /// <summary>
    /// Per-file SHA-256 results keyed by full path. Entries are valid only while the file's
    /// size and LastWriteTimeUtc match the cached stamp, which makes repeated health checks
    /// (e.g. every instance load) skip re-reading unchanged assemblies entirely.
    /// </summary>
    private const int MaxCachedFiles = 4096;

    private static readonly ConcurrentDictionary<string, CachedFileHash> FileHashCache =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly int HashParallelism = Math.Clamp(Environment.ProcessorCount, 2, 8);

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
            var pending = new List<FileCheck>();
            foreach (var file in receipt.Files)
            {
                var path = pathPolicy.ResolveUnderOwnedRoot(file.RelativePath, installRoot);
                pathPolicy.EnsureNoReparsePoints(path.FullPath);
                if (!IsModAssembly(file.RelativePath))
                {
                    continue;
                }
                var stamp = GetStamp(path.FullPath);
                if (!stamp.Exists)
                {
                    missing.Add(file.RelativePath);
                    FileHashCache.TryRemove(path.FullPath, out _);
                }
                else if (TryGetCached(path.FullPath, stamp, out var cachedSha256))
                {
                    currentFileSha256ByPath[path.RelativePath] = cachedSha256;
                    if (!string.Equals(file.Sha256, cachedSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        modified.Add(file.RelativePath);
                    }
                }
                else
                {
                    pending.Add(new FileCheck(file.RelativePath, path.RelativePath, path.FullPath, file.Sha256));
                }
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
                    .Where(path => !owned.Contains(path) && IsModAssembly(path))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                : [];
            foreach (string relativePath in extra)
            {
                var extraPath = pathPolicy.ResolveUnderOwnedRoot(relativePath, installRoot);
                pending.Add(new FileCheck(relativePath, relativePath, extraPath.FullPath, ExpectedSha256: null));
            }
            var hashed = await HashFilesAsync(
                pending.Select(check => check.FullPath).ToArray(),
                cancellationToken);
            foreach (var check in pending)
            {
                if (!hashed.TryGetValue(check.FullPath, out var sha256))
                {
                    continue;
                }
                if (sha256 is null)
                {
                    if (!check.IsExtra)
                    {
                        missing.Add(check.RelativePath);
                    }
                    continue;
                }
                currentFileSha256ByPath[check.ReportKey] = sha256;
                if (check.ExpectedSha256 is not null
                    && !string.Equals(check.ExpectedSha256, sha256, StringComparison.OrdinalIgnoreCase))
                {
                    modified.Add(check.RelativePath);
                }
            }
            var status = missing.Count != 0
                ? ModHealthStatus.CriticalFileMissing
                : modified.Count != 0
                    ? ModHealthStatus.ModifiedFile
                    : extra.Length != 0
                        ? ModHealthStatus.ExtraFile
                        : ModHealthStatus.Healthy;
            return Report(
                receipt.Id,
                status,
                missing,
                modified,
                extra,
                currentFileSha256ByPath);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException)
        {
            return Report(receipt.Id, ModHealthStatus.Indeterminate, detail: exception.Message);
        }
    }

    public async Task<ModHealthReport> AssessExternalAsync(
        ModDiscoveryEntry external,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(external);
        if (external.Ownership != ModOwnership.External)
        {
            return Report(
                external.Id,
                ModHealthStatus.Indeterminate,
                detail: "Discovery entry is not externally owned.");
        }
        if (external.Files.Count == 0)
        {
            return Report(
                external.Id,
                ModHealthStatus.Indeterminate,
                detail: "External discovery does not list files.");
        }

        try
        {
            var installRoot = pathPolicy.ResolveRecognized(external.InstallRoot);
            pathPolicy.EnsureNoReparsePoints(installRoot.FullPath);
            var pending = new List<FileCheck>();
            foreach (var relativePath in external.Files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var path = pathPolicy.ResolveUnderOwnedRoot(relativePath, installRoot);
                pathPolicy.EnsureNoReparsePoints(path.FullPath);
                if (!File.Exists(path.FullPath))
                {
                    return Report(
                        external.Id,
                        ModHealthStatus.Indeterminate,
                        detail: $"External file disappeared: {relativePath}");
                }
                pending.Add(new FileCheck(relativePath, path.RelativePath, path.FullPath, ExpectedSha256: null));
            }
            var currentFileSha256ByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var hashed = await HashFilesAsync(
                pending.Select(check => check.FullPath).ToArray(),
                cancellationToken);
            foreach (var check in pending)
            {
                if (!hashed.TryGetValue(check.FullPath, out var sha256) || sha256 is null)
                {
                    return Report(
                        external.Id,
                        ModHealthStatus.Indeterminate,
                        detail: $"External file disappeared: {check.RelativePath}");
                }
                currentFileSha256ByPath[check.ReportKey] = sha256;
            }
            return Report(
                external.Id,
                ModHealthStatus.UnmanagedExternal,
                currentFileSha256ByPath: currentFileSha256ByPath);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException)
        {
            return Report(
                external.Id,
                ModHealthStatus.Indeterminate,
                detail: exception.Message);
        }
    }

    private async Task<IReadOnlyDictionary<string, string?>> HashFilesAsync(
        IReadOnlyList<string> fullPaths,
        CancellationToken cancellationToken)
    {
        if (fullPaths.Count == 0)
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }
        var results = new ConcurrentDictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        await Parallel.ForEachAsync(
            fullPaths,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = HashParallelism,
                CancellationToken = cancellationToken
            },
            async (fullPath, token) =>
            {
                token.ThrowIfCancellationRequested();
                var before = GetStamp(fullPath);
                if (!before.Exists)
                {
                    // Vanished between classification and hashing; caller treats null as missing.
                    results[fullPath] = null;
                    return;
                }
                if (TryGetCached(fullPath, before, out var cachedSha256))
                {
                    results[fullPath] = cachedSha256;
                    return;
                }
                var sha256 = await HashFileAsync(fullPath, token);
                var after = GetStamp(fullPath);
                if (before == after)
                {
                    // Only cache when the file did not change while it was being hashed,
                    // so the stamp always matches the content the hash was computed from.
                    Cache(fullPath, after, sha256);
                }
                results[fullPath] = sha256;
            });
        return results;
    }

    private static bool TryGetCached(string fullPath, FileStamp stamp, out string sha256)
    {
        if (FileHashCache.TryGetValue(fullPath, out var entry)
            && entry.Length == stamp.Length
            && entry.LastWriteTimeUtc == stamp.LastWriteTimeUtc)
        {
            sha256 = entry.Sha256;
            return true;
        }
        sha256 = string.Empty;
        return false;
    }

    private static void Cache(string fullPath, FileStamp stamp, string sha256)
    {
        if (FileHashCache.Count >= MaxCachedFiles)
        {
            FileHashCache.Clear();
        }
        FileHashCache[fullPath] = new CachedFileHash(stamp.Length, stamp.LastWriteTimeUtc, sha256);
    }

    private static FileStamp GetStamp(string fullPath)
    {
        var info = new FileInfo(fullPath);
        if (!info.Exists)
        {
            return new FileStamp(Exists: false, Length: 0, LastWriteTimeUtc: default);
        }
        return new FileStamp(Exists: true, info.Length, info.LastWriteTimeUtc);
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

    private static bool IsModAssembly(string relativePath) =>
        string.Equals(Path.GetExtension(relativePath), ".dll", StringComparison.OrdinalIgnoreCase);

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    private sealed record FileCheck(
        string RelativePath,
        string ReportKey,
        string FullPath,
        string? ExpectedSha256)
    {
        public bool IsExtra => ExpectedSha256 is null;
    }

    private readonly record struct FileStamp(bool Exists, long Length, DateTime LastWriteTimeUtc);

    private readonly record struct CachedFileHash(long Length, DateTime LastWriteTimeUtc, string Sha256);
}

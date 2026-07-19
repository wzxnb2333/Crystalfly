using System.Collections.Concurrent;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Diagnostics;
using Crystalfly.Core.Models;
using Crystalfly.Core.Transactions;

namespace Crystalfly.Core.Packages;

public static class PackageInstaller
{
    private const long MaxPackageBytes = 256L * 1024 * 1024;
    private const long MaxExtractedFileBytes = 512L * 1024 * 1024;
    private const long MaxExtractedPackageBytes = 1024L * 1024 * 1024;
    private static readonly HttpClient SharedHttpClient = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> CacheGates =
        new(StringComparer.OrdinalIgnoreCase);

    public static async Task<string> AcquireVerifiedFileFromFileAsync(
        string packagePath,
        string transactionRoot,
        long? expectedSize,
        string expectedSha256,
        string cacheRoot,
        CancellationToken cancellationToken = default)
    {
        var package = new FileInfo(Path.GetFullPath(packagePath));
        if (!package.Exists)
        {
            throw new FileNotFoundException("Package was not found.", packagePath);
        }
        var verifiedSize = expectedSize ?? package.Length;
        ValidateExpectedSize(verifiedSize, remote: false);
        var normalizedHash = ValidateSha256(expectedSha256);
        await VerifyPackageAsync(package.FullName, verifiedSize, normalizedHash, cancellationToken);
        var cachePath = GetCachePath(cacheRoot, normalizedHash);
        return await UseCacheAsync(cachePath, async () =>
        {
            if (!await IsValidPackageAsync(cachePath, verifiedSize, normalizedHash, cancellationToken))
            {
                await WriteCacheAsync(package.FullName, cachePath, verifiedSize, cancellationToken);
            }
            return cachePath;
        }, cancellationToken);
    }

    public static async Task<string> AcquireVerifiedFileFromUriAsync(
        Uri packageUri,
        string transactionRoot,
        long? expectedSize,
        string expectedSha256,
        string cacheRoot,
        HttpClient? httpClient = null,
        IProgress<PackageTransferProgress>? progress = null,
        CancellationToken cancellationToken = default,
        SemaphoreSlim? networkGate = null)
    {
        if (!packageUri.IsAbsoluteUri || packageUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Package URL must use HTTPS.", nameof(packageUri));
        }

        ValidateExpectedSize(expectedSize, remote: true);
        var normalizedHash = ValidateSha256(expectedSha256);
        var cachePath = GetCachePath(cacheRoot, normalizedHash);
        return await UseCacheAsync(cachePath, async () =>
        {
            if (await IsValidPackageAsync(cachePath, expectedSize, normalizedHash, cancellationToken))
            {
                var cachedSize = new FileInfo(cachePath).Length;
                progress?.Report(new PackageTransferProgress(cachedSize, cachedSize, 0, "Cached"));
                return cachePath;
            }

            var workspace = CreateWorkspace(transactionRoot);
            var downloadPath = Path.Combine(workspace, "package.download");
            try
            {
                long downloadSize;
                if (networkGate is not null)
                {
                    await networkGate.WaitAsync(cancellationToken);
                }
                try
                {
                    using var response = await (httpClient ?? SharedHttpClient).GetAsync(
                        packageUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    response.EnsureSuccessStatusCode();
                    downloadSize = ResolveRemoteSize(response.Content.Headers.ContentLength, expectedSize);
                    await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
                    await using (var destination = new FileStream(
                        downloadPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920,
                        FileOptions.Asynchronous | FileOptions.WriteThrough))
                    {
                        await CopyWithSizeLimitAsync(
                            source, destination, downloadSize, "Downloading", progress, cancellationToken);
                    }
                }
                finally
                {
                    networkGate?.Release();
                }
                await VerifyPackageAsync(downloadPath, downloadSize, normalizedHash, cancellationToken);
                await WriteCacheAsync(downloadPath, cachePath, downloadSize, cancellationToken);
                return cachePath;
            }
            finally
            {
                DeleteWorkspace(workspace);
            }
        }, cancellationToken);
    }

    public static async Task<TransactionJournal> InstallFromUriAsync(
        Uri packageUri,
        string targetRoot,
        string transactionRoot,
        long? expectedSize,
        string expectedSha256,
        string? cacheRoot = null,
        HttpClient? httpClient = null,
        IProgress<PackageTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!packageUri.IsAbsoluteUri || packageUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Package URL must use HTTPS.", nameof(packageUri));
        }

        ValidateExpectedSize(expectedSize, remote: true);
        var normalizedHash = ValidateSha256(expectedSha256);
        if (cacheRoot is not null)
        {
            var cachePath = await AcquireVerifiedFileFromUriAsync(
                packageUri,
                transactionRoot,
                expectedSize,
                normalizedHash,
                cacheRoot,
                httpClient,
                progress,
                cancellationToken);
            var cachedSize = new FileInfo(cachePath).Length;
            var cachedWorkspace = CreateWorkspace(transactionRoot);
            try
            {
                return await InstallVerifiedAsync(
                    cachePath, targetRoot, transactionRoot, cachedWorkspace,
                    cachedSize, normalizedHash, cancellationToken);
            }
            finally
            {
                DeleteWorkspace(cachedWorkspace);
            }
        }

        var workspace = CreateWorkspace(transactionRoot);
        var packagePath = Path.Combine(workspace, "package.zip");
        try
        {
            using var response = await (httpClient ?? SharedHttpClient).GetAsync(
                packageUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            var downloadSize = ResolveRemoteSize(response.Content.Headers.ContentLength, expectedSize);
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var destination = new FileStream(
                packagePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await CopyWithSizeLimitAsync(
                    source, destination, downloadSize, "Downloading", progress, cancellationToken);
            }

            await VerifyPackageAsync(packagePath, downloadSize, normalizedHash, cancellationToken);
            return await InstallVerifiedAsync(
                packagePath, targetRoot, transactionRoot, workspace, downloadSize, normalizedHash, cancellationToken);
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    public static async Task<TransactionJournal> InstallFromFileAsync(
        string packagePath,
        string targetRoot,
        string transactionRoot,
        long? expectedSize,
        string expectedSha256,
        CancellationToken cancellationToken = default)
    {
        var workspace = CreateWorkspace(transactionRoot);
        try
        {
            var package = new FileInfo(Path.GetFullPath(packagePath));
            if (!package.Exists)
            {
                throw new FileNotFoundException("Package was not found.", packagePath);
            }
            var verifiedSize = expectedSize ?? package.Length;
            ValidateExpectedSize(verifiedSize, remote: false);
            return await InstallVerifiedAsync(
                package.FullName, targetRoot, transactionRoot, workspace,
                verifiedSize, expectedSha256, cancellationToken);
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    private static async Task<TransactionJournal> InstallVerifiedAsync(
        string packagePath,
        string targetRoot,
        string transactionRoot,
        string workspace,
        long expectedSize,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        var normalizedHash = ValidateSha256(expectedSha256);
        await VerifyPackageAsync(packagePath, expectedSize, normalizedHash, cancellationToken);

        var staging = Path.Combine(workspace, "staging");
        Directory.CreateDirectory(staging);
        ExtractSafely(packagePath, staging);
        return await FileTransaction.ApplyDirectoryAsync(
            staging, targetRoot, transactionRoot, "install-package", cancellationToken);
    }

    private static void ExtractSafely(string packagePath, string stagingRoot)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        var plans = ValidateEntries(archive);
        foreach (var plan in plans.Where(plan => plan.IsDirectory))
        {
            Directory.CreateDirectory(ResolveUnderRoot(stagingRoot, plan.RelativePath));
        }
        var buffer = new byte[81920];
        long extractedTotal = 0;
        foreach (var plan in plans.Where(plan => !plan.IsDirectory))
        {
            var targetPath = ResolveUnderRoot(stagingRoot, plan.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            using var source = plan.Entry.Open();
            using var destination = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            long extractedFile = 0;
            int read;
            while ((read = source.Read(buffer)) != 0)
            {
                if (read > plan.Entry.Length - extractedFile)
                {
                    throw new InvalidDataException(
                        $"ZIP entry '{plan.Entry.FullName}' exceeds its declared uncompressed size.");
                }
                if (read > MaxExtractedFileBytes - extractedFile)
                {
                    throw new InvalidDataException(
                        $"ZIP entry '{plan.Entry.FullName}' exceeds the {MaxExtractedFileBytes}-byte single-file extraction limit.");
                }
                if (read > MaxExtractedPackageBytes - extractedTotal)
                {
                    throw new InvalidDataException(
                        $"ZIP contents exceed the {MaxExtractedPackageBytes}-byte total extraction limit.");
                }
                destination.Write(buffer.AsSpan(0, read));
                extractedFile += read;
                extractedTotal += read;
            }
            if (extractedFile != plan.Entry.Length)
            {
                throw new InvalidDataException(
                    $"ZIP entry '{plan.Entry.FullName}' does not match its declared uncompressed size.");
            }
        }
    }

    private static IReadOnlyList<ArchiveEntryPlan> ValidateEntries(ZipArchive archive)
    {
        var plans = new List<ArchiveEntryPlan>(archive.Entries.Count);
        var explicitTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long declaredTotal = 0;

        foreach (var entry in archive.Entries)
        {
            var path = entry.FullName.Replace('\\', '/');
            if (path.Length == 0
                || path.StartsWith("/", StringComparison.Ordinal)
                || (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':'))
            {
                throw new InvalidDataException($"Unsafe ZIP path: '{entry.FullName}'.");
            }

            var isDirectory = path.EndsWith("/", StringComparison.Ordinal);
            if (!isDirectory && entry.Length > MaxExtractedFileBytes)
            {
                throw new InvalidDataException(
                    $"ZIP entry '{entry.FullName}' exceeds the {MaxExtractedFileBytes}-byte single-file extraction limit.");
            }
            if (!isDirectory && entry.Length > MaxExtractedPackageBytes - declaredTotal)
            {
                throw new InvalidDataException(
                    $"ZIP contents exceed the {MaxExtractedPackageBytes}-byte total extraction limit.");
            }
            if (!isDirectory)
            {
                declaredTotal += entry.Length;
            }
            var segments = path.Split('/');
            var segmentCount = isDirectory ? segments.Length - 1 : segments.Length;
            if (segmentCount == 0
                || segments.Take(segmentCount).Any(segment =>
                    segment.Length == 0
                    || segment == "."
                    || segment == ".."
                    || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                    || segment.EndsWith(".", StringComparison.Ordinal)
                    || char.IsWhiteSpace(segment[^1])))
            {
                throw new InvalidDataException($"Unsafe ZIP path: '{entry.FullName}'.");
            }
            var relativePath = string.Join('/', segments.Take(segmentCount));
            if (!explicitTargets.Add(relativePath))
            {
                throw new InvalidDataException($"Duplicate ZIP target: '{relativePath}'.");
            }

            var ancestors = Ancestors(relativePath).ToArray();
            if (ancestors.Any(files.Contains))
            {
                throw new InvalidDataException($"ZIP file/directory conflict: '{relativePath}'.");
            }
            if (isDirectory)
            {
                if (files.Contains(relativePath))
                {
                    throw new InvalidDataException($"ZIP file/directory conflict: '{relativePath}'.");
                }
                directories.Add(relativePath);
            }
            else
            {
                if (directories.Contains(relativePath))
                {
                    throw new InvalidDataException($"ZIP file/directory conflict: '{relativePath}'.");
                }
                files.Add(relativePath);
                foreach (var ancestor in ancestors)
                {
                    directories.Add(ancestor);
                }
            }
            plans.Add(new ArchiveEntryPlan(entry, relativePath, isDirectory));
        }
        return plans;
    }

    private static IEnumerable<string> Ancestors(string relativePath)
    {
        for (var index = relativePath.IndexOf('/'); index >= 0; index = relativePath.IndexOf('/', index + 1))
        {
            yield return relativePath[..index];
        }
    }

    private static async Task CopyWithSizeLimitAsync(
        Stream source,
        Stream destination,
        long expectedSize,
        string stage,
        IProgress<PackageTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        var stopwatch = Stopwatch.StartNew();
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) != 0)
        {
            total += read;
            if (total > expectedSize)
            {
                throw new InvalidDataException($"Package exceeds expected size {expectedSize}.");
            }
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            progress?.Report(new PackageTransferProgress(
                total,
                expectedSize,
                stopwatch.Elapsed.TotalSeconds <= 0
                    ? total
                    : total / stopwatch.Elapsed.TotalSeconds,
                stage));
        }
        if (total != expectedSize)
        {
            throw new InvalidDataException($"Package size mismatch. Expected {expectedSize}, received {total}.");
        }
        progress?.Report(new PackageTransferProgress(
            total,
            expectedSize,
            stopwatch.Elapsed.TotalSeconds <= 0
                ? total
                : total / stopwatch.Elapsed.TotalSeconds,
            stage));
    }

    private static long ResolveRemoteSize(long? contentLength, long? expectedSize)
    {
        if (contentLength is null)
        {
            return expectedSize
                ?? throw new InvalidDataException("Package response does not declare Content-Length.");
        }
        if (contentLength <= 0 || contentLength > MaxPackageBytes)
        {
            throw new InvalidDataException($"Package Content-Length must be between 1 and {MaxPackageBytes} bytes.");
        }
        if (expectedSize is long expected && contentLength != expected)
        {
            throw new InvalidDataException($"Package size mismatch. Expected {expected}, received {contentLength}.");
        }
        return contentLength.Value;
    }

    private static void ValidateExpectedSize(long? expectedSize, bool remote)
    {
        if (expectedSize is null)
        {
            return;
        }
        if (expectedSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedSize));
        }
        if (expectedSize > MaxPackageBytes)
        {
            throw new InvalidDataException(
                $"{(remote ? "Remote" : "Local")} package exceeds the {MaxPackageBytes}-byte limit.");
        }
    }

    private static async Task<bool> IsValidPackageAsync(
        string path,
        long? expectedSize,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        var package = new FileInfo(path);
        if (!package.Exists
            || package.Length <= 0
            || package.Length > MaxPackageBytes
            || expectedSize is long size && package.Length != size)
        {
            return false;
        }
        return StringComparer.OrdinalIgnoreCase.Equals(
            expectedSha256, await HashFileAsync(path, cancellationToken));
    }

    private static async Task VerifyPackageAsync(
        string path,
        long expectedSize,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        var package = new FileInfo(path);
        if (!package.Exists)
        {
            throw new FileNotFoundException("Package was not found.", path);
        }
        if (package.Length != expectedSize)
        {
            throw new InvalidDataException($"Package size mismatch. Expected {expectedSize}, received {package.Length}.");
        }
        if (!StringComparer.OrdinalIgnoreCase.Equals(
                expectedSha256, await HashFileAsync(path, cancellationToken)))
        {
            throw new InvalidDataException("Package SHA-256 mismatch.");
        }
    }

    private static string GetCachePath(string cacheRoot, string normalizedHash)
    {
        var root = Path.GetFullPath(cacheRoot);
        Directory.CreateDirectory(root);
        return Path.Combine(root, $"{normalizedHash}.zip");
    }

    private static async Task<T> UseCacheAsync<T>(
        string cachePath,
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        var gate = CacheGates.GetOrAdd(cachePath, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await operation();
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task WriteCacheAsync(
        string sourcePath,
        string cachePath,
        long expectedSize,
        CancellationToken cancellationToken)
    {
        var temporaryPath = Path.Combine(
            Path.GetDirectoryName(cachePath)!, $".{Path.GetFileName(cachePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var source = new FileStream(
                sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var destination = new FileStream(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await CopyWithSizeLimitAsync(
                    source, destination, expectedSize, "Caching", progress: null, cancellationToken);
            }
            File.Move(temporaryPath, cachePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string ValidateSha256(string value)
    {
        if (value.Length != 64)
        {
            throw new ArgumentException("SHA-256 must contain 64 hexadecimal characters.", nameof(value));
        }
        try
        {
            _ = Convert.FromHexString(value);
            return value.ToUpperInvariant();
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("SHA-256 must contain only hexadecimal characters.", nameof(value), exception);
        }
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private static string CreateWorkspace(string transactionRoot)
    {
        var root = Path.GetFullPath(transactionRoot);
        Directory.CreateDirectory(root);
        var workspace = Path.Combine(root, $".package-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspace);
        return workspace;
    }

    private static string ResolveUnderRoot(string root, string relativePath)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"ZIP path escapes staging root: '{relativePath}'.");
        }
        return path;
    }

    private static void DeleteWorkspace(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed record ArchiveEntryPlan(ZipArchiveEntry Entry, string RelativePath, bool IsDirectory);
}

using System.IO.Compression;
using System.Security.Cryptography;
using Crystalfly.Core.Models;
using Crystalfly.Core.Transactions;

namespace Crystalfly.Core.Packages;

public static class PackageInstaller
{
    private static readonly HttpClient HttpClient = new();

    public static async Task<TransactionJournal> InstallFromUriAsync(
        Uri packageUri,
        string targetRoot,
        string transactionRoot,
        long expectedSize,
        string expectedSha256,
        CancellationToken cancellationToken = default)
    {
        if (!packageUri.IsAbsoluteUri || packageUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Package URL must use HTTPS.", nameof(packageUri));
        }

        var workspace = CreateWorkspace(transactionRoot);
        var packagePath = Path.Combine(workspace, "package.zip");
        try
        {
            using var response = await HttpClient.GetAsync(
                packageUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long contentLength && contentLength != expectedSize)
            {
                throw new InvalidDataException($"Package size mismatch. Expected {expectedSize}, received {contentLength}.");
            }
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var destination = new FileStream(
                packagePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await CopyWithSizeLimitAsync(source, destination, expectedSize, cancellationToken);
            }
            return await InstallVerifiedAsync(
                packagePath, targetRoot, transactionRoot, workspace, expectedSize, expectedSha256, cancellationToken);
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
        long expectedSize,
        string expectedSha256,
        CancellationToken cancellationToken = default)
    {
        var workspace = CreateWorkspace(transactionRoot);
        try
        {
            return await InstallVerifiedAsync(
                Path.GetFullPath(packagePath), targetRoot, transactionRoot, workspace,
                expectedSize, expectedSha256, cancellationToken);
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
        if (expectedSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedSize));
        }
        var normalizedHash = ValidateSha256(expectedSha256);
        var package = new FileInfo(packagePath);
        if (!package.Exists)
        {
            throw new FileNotFoundException("Package was not found.", packagePath);
        }
        if (package.Length != expectedSize)
        {
            throw new InvalidDataException($"Package size mismatch. Expected {expectedSize}, received {package.Length}.");
        }
        if (!StringComparer.OrdinalIgnoreCase.Equals(normalizedHash, await HashFileAsync(packagePath, cancellationToken)))
        {
            throw new InvalidDataException("Package SHA-256 mismatch.");
        }

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
        foreach (var plan in plans.Where(plan => !plan.IsDirectory))
        {
            var targetPath = ResolveUnderRoot(stagingRoot, plan.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            using var source = plan.Entry.Open();
            using var destination = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            source.CopyTo(destination);
        }
    }

    private static IReadOnlyList<ArchiveEntryPlan> ValidateEntries(ZipArchive archive)
    {
        var plans = new List<ArchiveEntryPlan>(archive.Entries.Count);
        var explicitTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) != 0)
        {
            total += read;
            if (total > expectedSize)
            {
                throw new InvalidDataException($"Package exceeds expected size {expectedSize}.");
            }
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        if (total != expectedSize)
        {
            throw new InvalidDataException($"Package size mismatch. Expected {expectedSize}, received {total}.");
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

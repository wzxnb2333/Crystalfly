using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Loaders;

public sealed record LocalLoaderPackage(
    LoaderManifest Manifest,
    string PackagePath,
    LoaderState LoaderState)
{
    public bool IsVerified => false;

    public string VerificationStatus => "Unverified";
}

public static class LocalLoaderPackageManifest
{
    public static async Task<LocalLoaderPackage> LoadAsync(
        string manifestPath,
        string expectedBuildId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedBuildId);

        var fullManifestPath = Path.GetFullPath(manifestPath);
        if (!File.Exists(fullManifestPath))
        {
            throw new FileNotFoundException("Local loader manifest was not found.", fullManifestPath);
        }

        LocalManifest document;
        try
        {
            await using var stream = new FileStream(
                fullManifestPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            document = await JsonSerializer.DeserializeAsync<LocalManifest>(
                stream,
                CrystalflyJson.Options,
                cancellationToken) ?? throw new InvalidDataException("Local loader manifest is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Local loader manifest is invalid JSON.", exception);
        }

        ValidateDocument(document, expectedBuildId);
        var manifestDirectory = Path.GetDirectoryName(fullManifestPath)!;
        var packagePath = ResolvePackagePath(manifestDirectory, document.PackageFile!);
        var package = new FileInfo(packagePath);
        if (!package.Exists)
        {
            throw new InvalidDataException($"Local loader package was not found: '{document.PackageFile}'.");
        }
        RejectReparsePoints(manifestDirectory, packagePath);
        if (!string.Equals(package.Extension, ".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Local loader package must be a ZIP file.");
        }
        if (document.SizeBytes <= 0 || package.Length != document.SizeBytes)
        {
            throw new InvalidDataException(
                $"Local loader package size mismatch. Expected {document.SizeBytes}, received {package.Length}.");
        }

        var sha256 = ValidateSha256(document.Sha256!);
        if (!string.Equals(
                sha256,
                await HashFileAsync(package.FullName, cancellationToken),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Local loader package SHA-256 mismatch.");
        }
        string[] managedFiles = document.ManagedFiles!
            .Select(NormalizeRelativePath)
            .ToArray();
        ValidateZip(package.FullName, document.LoaderState, managedFiles);

        var manifest = new LoaderManifest
        {
            Id = document.Id!,
            Name = document.Name!,
            Version = document.Version!,
            DownloadUrl = new Uri(package.FullName).AbsoluteUri,
            SizeBytes = package.Length,
            Sha256 = sha256,
            SupportedBuildIds = document.SupportedBuildIds!,
            ManagedFiles = managedFiles
        };
        return new LocalLoaderPackage(manifest, package.FullName, document.LoaderState);
    }

    private static void ValidateDocument(LocalManifest document, string expectedBuildId)
    {
        if (document.SchemaVersion != 1)
        {
            throw new InvalidDataException($"Unsupported local loader manifest schema {document.SchemaVersion}.");
        }
        if (document.LoaderState is not (LoaderState.ModdingApi or LoaderState.BepInEx))
        {
            throw new InvalidDataException("Local loader state must be ModdingApi or BepInEx.");
        }

        RequireText(document.Id, "id");
        RequireText(document.Name, "name");
        RequireText(document.Version, "version");
        RequireText(document.PackageFile, "packageFile");
        RequireText(document.Sha256, "sha256");
        if (document.SupportedBuildIds is null
            || document.SupportedBuildIds.Count == 0
            || document.SupportedBuildIds.Any(string.IsNullOrWhiteSpace)
            || !document.SupportedBuildIds.Contains(expectedBuildId, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Local loader does not support build '{expectedBuildId}'.");
        }
        if (document.ManagedFiles is null || document.ManagedFiles.Count == 0)
        {
            throw new InvalidDataException("Local loader manifest must declare managedFiles.");
        }
        foreach (var path in document.ManagedFiles)
        {
            _ = NormalizeRelativePath(path);
        }
    }

    private static string ResolvePackagePath(string root, string relativePath)
    {
        if (Path.IsPathRooted(relativePath) || Path.IsPathFullyQualified(relativePath))
        {
            throw new InvalidDataException("packageFile must be relative to the manifest directory.");
        }
        var normalized = NormalizeRelativePath(relativePath);
        var fullRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(
            fullRoot,
            normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("packageFile escapes the manifest directory.");
        }
        return fullPath;
    }

    private static string NormalizeRelativePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || Path.IsPathRooted(value)
            || Path.IsPathFullyQualified(value))
        {
            throw new InvalidDataException("Manifest file paths must be non-empty relative paths.");
        }
        var segments = value.Replace('\\', '/').Split('/');
        if (segments.Any(segment =>
            segment.Length == 0
            || segment is "." or ".."
            || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
        {
            throw new InvalidDataException($"Unsafe manifest file path: '{value}'.");
        }
        return string.Join('/', segments);
    }

    private static void RejectReparsePoints(string root, string path)
    {
        var fullRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (var current = path; !string.Equals(current, fullRoot, StringComparison.OrdinalIgnoreCase);)
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException($"packageFile contains a reparse point: '{current}'.");
            }
            current = Path.GetDirectoryName(current)
                ?? throw new InvalidDataException("packageFile escapes the manifest directory.");
        }
    }

    private static string ValidateSha256(string value)
    {
        if (value.Length != 64)
        {
            throw new InvalidDataException("sha256 must contain 64 hexadecimal characters.");
        }
        try
        {
            _ = Convert.FromHexString(value);
            return value.ToUpperInvariant();
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("sha256 must contain only hexadecimal characters.", exception);
        }
    }

    private static void ValidateZip(
        string packagePath,
        LoaderState loaderState,
        IReadOnlyList<string> managedFiles)
    {
        try
        {
            using var archive = ZipFile.OpenRead(packagePath);
            string prefix = loaderState == LoaderState.ModdingApi
                ? "hollow_knight_Data/Managed/"
                : string.Empty;
            var actualFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ZipArchiveEntry entry in archive.Entries.Where(entry => entry.Name.Length > 0))
            {
                string path = prefix + NormalizeRelativePath(entry.FullName);
                if (!actualFiles.Add(path))
                {
                    throw new InvalidDataException($"Local loader ZIP contains duplicate file '{path}'.");
                }
            }

            var declaredFiles = managedFiles.ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (declaredFiles.Count != managedFiles.Count || !declaredFiles.SetEquals(actualFiles))
            {
                throw new InvalidDataException(
                    "Local loader managedFiles must exactly match the files installed from the ZIP.");
            }
        }
        catch (InvalidDataException exception)
        {
            if (exception.Message.StartsWith("Local loader", StringComparison.Ordinal))
            {
                throw;
            }
            throw new InvalidDataException("Local loader package is not a valid ZIP file.", exception);
        }
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private static void RequireText(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"Local loader manifest field '{fieldName}' is required.");
        }
    }

    private sealed record LocalManifest
    {
        public int SchemaVersion { get; init; }

        public string? Id { get; init; }

        public string? Name { get; init; }

        public string? Version { get; init; }

        public LoaderState LoaderState { get; init; }

        public string? PackageFile { get; init; }

        public long SizeBytes { get; init; }

        public string? Sha256 { get; init; }

        public IReadOnlyList<string>? SupportedBuildIds { get; init; }

        public IReadOnlyList<string>? ManagedFiles { get; init; }
    }
}

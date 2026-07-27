using System.Security.Cryptography;

namespace Crystalfly.Core.Instances;

public sealed class GameDirectoryMigrationService
{
    private readonly Func<string, string, bool> isSameVolume;
    private readonly Func<string> operationIdFactory;
    private readonly Action<string, string> moveDirectory;
    private readonly Action<string> deleteDirectory;

    public GameDirectoryMigrationService()
        : this(
            (source, destination) => string.Equals(
                Path.GetPathRoot(source),
                Path.GetPathRoot(destination),
                StringComparison.OrdinalIgnoreCase),
            () => Guid.NewGuid().ToString("N"),
            Directory.Move,
            path => Directory.Delete(path, recursive: true))
    {
    }

    internal GameDirectoryMigrationService(
        Func<string, string, bool> isSameVolume,
        Func<string> operationIdFactory,
        Action<string, string> moveDirectory,
        Action<string> deleteDirectory)
    {
        ArgumentNullException.ThrowIfNull(isSameVolume);
        ArgumentNullException.ThrowIfNull(operationIdFactory);
        ArgumentNullException.ThrowIfNull(moveDirectory);
        ArgumentNullException.ThrowIfNull(deleteDirectory);
        this.isSameVolume = isSameVolume;
        this.operationIdFactory = operationIdFactory;
        this.moveDirectory = moveDirectory;
        this.deleteDirectory = deleteDirectory;
    }

    public async Task<GameDirectoryMigrationResult> MigrateAsync(
        string sourceRoot,
        string destinationRoot,
        CancellationToken cancellationToken = default)
    {
        var source = Normalize(sourceRoot, nameof(sourceRoot));
        var destination = Normalize(destinationRoot, nameof(destinationRoot));
        ValidatePaths(source, destination);
        cancellationToken.ThrowIfCancellationRequested();

        if (isSameVolume(source, destination))
        {
            moveDirectory(source, destination);
            return new GameDirectoryMigrationResult(source, destination, true, null);
        }

        var destinationParent = Path.GetDirectoryName(destination)!;
        var stagingRoot = Path.Combine(
            destinationParent,
            ".crystalfly",
            "staging",
            $"migration-{operationIdFactory()}");
        if (Directory.Exists(stagingRoot) || File.Exists(stagingRoot))
        {
            throw new IOException($"Migration staging path '{stagingRoot}' already exists.");
        }

        try
        {
            var sourceManifest = await CreateManifestAsync(source, cancellationToken);
            await CopyDirectoryAsync(source, stagingRoot, cancellationToken);
            var stagingManifest = await CreateManifestAsync(stagingRoot, cancellationToken);
            if (!sourceManifest.SequenceEqual(stagingManifest))
            {
                throw new InvalidDataException("Migrated game directory failed file-list or SHA-256 verification.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            moveDirectory(stagingRoot, destination);
        }
        catch
        {
            TryDeleteStaging(stagingRoot);
            throw;
        }

        try
        {
            deleteDirectory(source);
            return new GameDirectoryMigrationResult(source, destination, true, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new GameDirectoryMigrationResult(source, destination, false, exception.Message);
        }
    }

    private static void ValidatePaths(string source, string destination)
    {
        if (!Directory.Exists(source))
        {
            throw new DirectoryNotFoundException($"Source game directory '{source}' was not found.");
        }
        RejectReparsePoint(source);

        var destinationParent = Path.GetDirectoryName(destination)
            ?? throw new ArgumentException("Destination must have a parent directory.", nameof(destination));
        if (!Directory.Exists(destinationParent))
        {
            throw new DirectoryNotFoundException($"Destination parent '{destinationParent}' was not found.");
        }
        RejectReparseAncestors(destinationParent);

        if (IsSameOrDescendant(source, destination) || IsSameOrDescendant(destination, source))
        {
            throw new ArgumentException("Source and destination game directories cannot be equal or nested.", nameof(destination));
        }
        if (Directory.Exists(destination) || File.Exists(destination))
        {
            throw new IOException($"Destination '{destination}' already exists.");
        }

        var integrity = GameDirectoryIntegrityChecker.Inspect(source);
        if (!integrity.IsValid)
        {
            throw new InvalidDataException("Source is not a complete game directory or has a pending download.");
        }
    }

    private static async Task CopyDirectoryAsync(
        string sourceRoot,
        string destinationRoot,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationRoot);
        foreach (var directory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RejectReparsePoint(directory);
            Directory.CreateDirectory(Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RejectReparsePoint(file);
            var destination = Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using var input = new FileStream(
                file,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var output = new FileStream(
                destination,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await input.CopyToAsync(output, cancellationToken);
            await output.FlushAsync(cancellationToken);
        }
    }

    private static async Task<IReadOnlyList<FileManifestEntry>> CreateManifestAsync(
        string root,
        CancellationToken cancellationToken)
    {
        var entries = new List<FileManifestEntry>();
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .Order(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RejectReparsePoint(file);
            await using var stream = new FileStream(
                file,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            entries.Add(new FileManifestEntry(
                Path.GetRelativePath(root, file).Replace('\\', '/'),
                Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken))));
        }
        return entries;
    }

    private static string Normalize(string path, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, parameterName);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private static bool IsSameOrDescendant(string path, string parent)
    {
        if (string.Equals(path, parent, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return path.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static void RejectReparseAncestors(string path)
    {
        for (var current = new DirectoryInfo(path); current is not null; current = current.Parent)
        {
            RejectReparsePoint(current.FullName);
        }
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Game directory migration cannot traverse reparse point '{path}'.");
        }
    }

    private static void TryDeleteStaging(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed record FileManifestEntry(string RelativePath, string Sha256);
}

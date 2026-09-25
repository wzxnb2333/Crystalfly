using System.Security.Cryptography;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

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

        var sameVolume = isSameVolume(source, destination);
        var record = await InstanceSidecar.LoadAsync(source, cancellationToken);
        var sourceState = record is null ? null
            : Path.GetDirectoryName(InstanceSidecar.GetMetadataPath(source, record.Id));
        var destinationState = record is null ? null
            : Path.GetDirectoryName(InstanceSidecar.GetMetadataPath(destination, record.Id));
        var migrateState = sourceState is not null
            && !string.Equals(sourceState, destinationState, StringComparison.OrdinalIgnoreCase);
        if (migrateState)
        {
            InstanceDirectory.RejectReparseAncestors(sourceState!);
            InstanceDirectory.RejectReparseAncestors(destinationState!);
            if (Directory.Exists(destinationState) || File.Exists(destinationState))
            {
                throw new IOException($"Destination instance state '{destinationState}' already exists.");
            }
        }
        var destinationParent = Path.GetDirectoryName(destination)!;
        var operationRoot = Path.Combine(
            destinationParent,
            ".crystalfly",
            "staging",
            $"migration-{operationIdFactory()}");
        InstanceDirectory.RejectReparseAncestors(operationRoot);
        if (Directory.Exists(operationRoot) || File.Exists(operationRoot))
        {
            throw new IOException($"Migration staging path '{operationRoot}' already exists.");
        }
        var stagingRoot = Path.Combine(operationRoot, "game");
        var stateStagingRoot = Path.Combine(operationRoot, "instance-state");
        var statePublished = false;
        try
        {
            if (migrateState)
            {
                await CopyAndVerifyAsync(sourceState!, stateStagingRoot, cancellationToken);
                await RelocateStateMetadataAsync(
                    stateStagingRoot, sourceState!, destinationState!,
                    record! with { RootPath = destination }, cancellationToken);
            }
            if (!sameVolume)
            {
                await CopyAndVerifyAsync(source, stagingRoot, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (migrateState)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destinationState!)!);
                moveDirectory(stateStagingRoot, destinationState!);
                statePublished = true;
            }
            moveDirectory(sameVolume ? source : stagingRoot, destination);
        }
        catch
        {
            if (statePublished)
            {
                TryDeleteStaging(destinationState!);
            }
            throw;
        }
        finally
        {
            TryDeleteStaging(operationRoot);
        }

        try
        {
            if (!sameVolume)
            {
                InstanceDirectory.RejectReparseAncestors(source);
                deleteDirectory(source);
            }
            if (migrateState)
            {
                InstanceDirectory.RejectReparseAncestors(sourceState!);
                deleteDirectory(sourceState!);
            }
            return new GameDirectoryMigrationResult(source, destination, true, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new GameDirectoryMigrationResult(source, destination, false, exception.Message);
        }
    }

    private static async Task RelocateStateMetadataAsync(
        string staging, string sourceState, string destinationState,
        InstanceRecord record, CancellationToken cancellationToken)
    {
        await WriteStagedMetadataAsync(Path.Combine(staging, "instance.json"), record, cancellationToken);
        var loaderPath = Path.Combine(staging, "loader.json");
        if (File.Exists(loaderPath) || File.Exists(loaderPath + ".bak"))
        {
            var receipt = await AtomicJsonStore.ReadAsync<InstalledPackageReceipt>(loaderPath, cancellationToken);
            if (receipt.SchemaVersion != InstalledPackageReceipt.CurrentSchemaVersion)
            {
                throw new InvalidDataException("Unsupported loader receipt schema version.");
            }
            if (!string.IsNullOrEmpty(receipt.BackupRoot))
            {
                var backupRoot = Normalize(receipt.BackupRoot, nameof(receipt.BackupRoot));
                var expectedRoot = Path.Combine(sourceState, "loader-backups");
                if (!Path.IsPathFullyQualified(receipt.BackupRoot)
                    || !backupRoot.StartsWith(expectedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("Loader backup path escapes the source instance state.");
                }
                receipt = receipt with
                {
                    BackupRoot = Path.Combine(destinationState, Path.GetRelativePath(sourceState, backupRoot))
                };
            }
            else if (receipt.IsVerified)
            {
                throw new InvalidDataException("Verified loader receipt has no backup root.");
            }
            await WriteStagedMetadataAsync(loaderPath, receipt, cancellationToken);
        }

        var snapshotsRoot = Path.Combine(staging, "snapshots");
        if (!Directory.Exists(snapshotsRoot))
        {
            return;
        }
        foreach (var directory in Directory.EnumerateDirectories(snapshotsRoot)
                     .Where(path => !Path.GetFileName(path).StartsWith(".", StringComparison.Ordinal)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var metadataPath = Path.Combine(directory, "snapshot.json");
            var snapshot = await AtomicJsonStore.ReadAsync<NamedSnapshot>(metadataPath, cancellationToken);
            var snapshotId = Path.GetFileName(directory);
            if (snapshot.SchemaVersion != NamedSnapshot.CurrentSchemaVersion
                || !string.Equals(snapshot.Id, snapshotId, StringComparison.Ordinal)
                || !PathEquals(snapshot.SourcePath, Path.Combine(sourceState, "local-low"))
                || !PathEquals(snapshot.SnapshotPath, Path.Combine(sourceState, "snapshots", snapshotId, "data")))
            {
                throw new InvalidDataException("Named snapshot metadata does not belong to the source instance.");
            }
            await WriteStagedMetadataAsync(metadataPath, snapshot with
            {
                SourcePath = Path.Combine(destinationState, "local-low"),
                SnapshotPath = Path.Combine(destinationState, "snapshots", snapshotId, "data")
            }, cancellationToken);
        }
    }

    private static bool PathEquals(string path, string expected) =>
        Path.IsPathFullyQualified(path)
        && string.Equals(Normalize(path, nameof(path)), expected, StringComparison.OrdinalIgnoreCase);

    private static async Task WriteStagedMetadataAsync<T>(
        string path, T value, CancellationToken cancellationToken)
    {
        // Only the private staging copy is modified. Keep recovery metadata consistent
        // with the copied data, rather than retaining backups that reference the old root.
        var json = CrystalflyJson.Serialize(value);
        await File.WriteAllTextAsync(path, json, cancellationToken);
        if (File.Exists(path + ".bak"))
        {
            await File.WriteAllTextAsync(path + ".bak", json, cancellationToken);
        }
    }

    private static void ValidatePaths(string source, string destination)
    {
        if (!Directory.Exists(source))
        {
            throw new DirectoryNotFoundException($"Source game directory '{source}' was not found.");
        }
        InstanceDirectory.RejectReparseAncestors(source);

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
        foreach (var file in EnumerateSafeFiles(root)
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

    private static IEnumerable<string> EnumerateSafeFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out var directory))
        {
            RejectReparsePoint(directory);
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                RejectReparsePoint(entry);
                if (Directory.Exists(entry))
                {
                    pending.Push(entry);
                }
                else
                {
                    yield return entry;
                }
            }
        }
    }

    private static async Task CopyAndVerifyAsync(
        string source, string destination, CancellationToken cancellationToken)
    {
        var sourceManifest = await CreateManifestAsync(source, cancellationToken);
        await CopyDirectoryAsync(source, destination, cancellationToken);
        var destinationManifest = await CreateManifestAsync(destination, cancellationToken);
        if (!sourceManifest.SequenceEqual(destinationManifest))
        {
            throw new InvalidDataException("Migrated directory failed file-list or SHA-256 verification.");
        }
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
                InstanceDirectory.RejectReparseAncestors(path);
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed record FileManifestEntry(string RelativePath, string Sha256);
}

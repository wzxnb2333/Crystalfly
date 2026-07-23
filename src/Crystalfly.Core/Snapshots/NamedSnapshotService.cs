using Crystalfly.Core.LocalLow;
using Crystalfly.Core.Models;
using Crystalfly.Core.Runtime;
using Crystalfly.Core.Saves;
using Crystalfly.Core.Serialization;
using Crystalfly.Core.Transactions;

namespace Crystalfly.Core.Snapshots;

public sealed class NamedSnapshotService
{
    private const string MetadataFileName = "snapshot.json";
    private readonly string storagePath;
    private readonly string mutexName;
    private readonly IHollowKnightProcessProbe? processProbe;

    public NamedSnapshotService(
        string storageRoot,
        string mutexName = HollowKnightProcessGuard.DefaultMutexName,
        IHollowKnightProcessProbe? processProbe = null)
    {
        storagePath = Path.GetFullPath(storageRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(mutexName);
        this.mutexName = mutexName;
        this.processProbe = processProbe;
    }

    public async Task<NamedSnapshot> CreateAsync(
        string instanceId,
        string name,
        CancellationToken cancellationToken = default)
    {
        ValidateSegment(instanceId, nameof(instanceId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        using var guard = HollowKnightProcessGuard.Acquire(mutexName, processProbe);
        var sourcePath = GetInstanceLocalLowPath(instanceId);
        if (!Directory.Exists(sourcePath))
        {
            throw new DirectoryNotFoundException($"Instance LocalLow data does not exist: '{sourcePath}'.");
        }

        var snapshotsRoot = GetSnapshotsRoot(instanceId);
        Directory.CreateDirectory(snapshotsRoot);
        var id = Guid.NewGuid().ToString("N");
        var snapshotRoot = Path.Combine(snapshotsRoot, id);
        var stagingRoot = Path.Combine(snapshotsRoot, $".{id}.staging");
        var stagingDataPath = Path.Combine(stagingRoot, "data");
        var finalDataPath = Path.Combine(snapshotRoot, "data");
        try
        {
            await LocalLowDirectory.CopyAsync(
                sourcePath,
                stagingDataPath,
                includeLogs: false,
                cancellationToken);
            var hash = await LocalLowDirectory.HashFilesAsync(
                stagingDataPath,
                includeLogs: false,
                cancellationToken);
            var snapshot = new NamedSnapshot
            {
                Id = id,
                Name = name.Trim(),
                SourcePath = sourcePath,
                SnapshotPath = finalDataPath,
                Sha256 = hash,
                CreatedAt = DateTimeOffset.UtcNow
            };
            await AtomicJsonStore.WriteAsync(
                Path.Combine(stagingRoot, MetadataFileName),
                snapshot,
                cancellationToken);
            Directory.Move(stagingRoot, snapshotRoot);
            return snapshot;
        }
        catch
        {
            LocalLowDirectory.DeleteIfExists(stagingRoot);
            throw;
        }
    }

    public async Task<IReadOnlyList<NamedSnapshot>> ListAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        ValidateSegment(instanceId, nameof(instanceId));
        var snapshotsRoot = GetSnapshotsRoot(instanceId);
        if (!Directory.Exists(snapshotsRoot))
        {
            return [];
        }

        var snapshots = new List<NamedSnapshot>();
        foreach (var directory in Directory
            .EnumerateDirectories(snapshotsRoot)
            .Where(path => !Path.GetFileName(path).StartsWith(".", StringComparison.Ordinal))
            .Order(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await AtomicJsonStore.ReadAsync<NamedSnapshot>(
                Path.Combine(directory, MetadataFileName),
                cancellationToken);
            ValidateSnapshot(instanceId, directory, snapshot);
            snapshots.Add(snapshot);
        }
        return snapshots.OrderBy(snapshot => snapshot.CreatedAt).ToArray();
    }

    public async Task RestoreAsync(
        string instanceId,
        string snapshotId,
        CancellationToken cancellationToken = default)
    {
        ValidateSegment(instanceId, nameof(instanceId));
        ValidateSegment(snapshotId, nameof(snapshotId));
        using var guard = HollowKnightProcessGuard.Acquire(mutexName, processProbe);
        var snapshotRoot = Path.Combine(GetSnapshotsRoot(instanceId), snapshotId);
        var snapshot = await AtomicJsonStore.ReadAsync<NamedSnapshot>(
            Path.Combine(snapshotRoot, MetadataFileName),
            cancellationToken);
        ValidateSnapshot(instanceId, snapshotRoot, snapshot);
        await RequireHashAsync(snapshot.SnapshotPath, snapshot.Sha256, cancellationToken);

        var instancePath = GetInstanceLocalLowPath(instanceId);
        if (!Directory.Exists(instancePath))
        {
            throw new DirectoryNotFoundException($"Instance LocalLow data does not exist: '{instancePath}'.");
        }
        var operationId = Guid.NewGuid().ToString("N");
        var stagingPath = instancePath + $".snapshot-{operationId}-staging";
        try
        {
            await LocalLowDirectory.CopyAsync(
                snapshot.SnapshotPath,
                stagingPath,
                includeLogs: false,
                cancellationToken);
            await RequireHashAsync(stagingPath, snapshot.Sha256, cancellationToken);
            var stagedFiles = LocalLowDirectory.EnumerateRelativeFiles(stagingPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var removals = LocalLowDirectory.EnumerateRelativeFiles(instancePath)
                .Where(path => !stagedFiles.Contains(path));
            var recoveries = await FileTransaction.RecoverPendingAsync(
                Path.Combine(storagePath, "transactions"),
                cancellationToken);
            if (recoveries.Any(recovery => recovery.State == TransactionState.NeedsAttention))
            {
                throw new InvalidOperationException("A pending file transaction needs attention.");
            }
            await FileTransaction.ReplaceDirectoryAsync(
                stagingPath,
                instancePath,
                Path.Combine(storagePath, "transactions"),
                "restore-named-snapshot",
                removals,
                cancellationToken);
            await RequireHashAsync(instancePath, snapshot.Sha256, cancellationToken);
        }
        finally
        {
            LocalLowDirectory.DeleteIfExists(stagingPath);
        }
    }

    public Task<IReadOnlyList<string>> ListSaveSlotsAsync(
        string instanceId,
        string? snapshotId,
        CancellationToken cancellationToken = default)
    {
        ValidateSegment(instanceId, nameof(instanceId));
        if (snapshotId is not null)
        {
            ValidateSegment(snapshotId, nameof(snapshotId));
        }

        var basePath = snapshotId is null
            ? GetInstanceLocalLowPath(instanceId)
            : Path.Combine(GetSnapshotsRoot(instanceId), snapshotId, "data");
        if (!Directory.Exists(basePath))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var slots = Enumerable.Range(1, 4)
            .Select(slot => $"user{slot}.dat")
            .Where(slot => File.Exists(Path.Combine(basePath, slot)))
            .ToArray();
        return Task.FromResult<IReadOnlyList<string>>(slots);
    }

    public async Task<string> DecryptSaveAsync(
        string instanceId,
        string? snapshotId,
        string slotRelativePath,
        CancellationToken cancellationToken = default)
    {
        ValidateSegment(instanceId, nameof(instanceId));
        if (snapshotId is not null)
        {
            ValidateSegment(snapshotId, nameof(snapshotId));
        }
        ValidateEditableSaveSlot(slotRelativePath);

        var basePath = snapshotId is null
            ? GetInstanceLocalLowPath(instanceId)
            : Path.Combine(GetSnapshotsRoot(instanceId), snapshotId, "data");
        var filePath = ResolveUnderBase(basePath, slotRelativePath);
        return await SaveFileCodec.DecryptAsync(filePath, cancellationToken);
    }

    public async Task UpdateSaveAsync(
        string instanceId,
        string? snapshotId,
        string slotRelativePath,
        string json,
        CancellationToken cancellationToken = default)
    {
        ValidateSegment(instanceId, nameof(instanceId));
        if (snapshotId is not null)
        {
            ValidateSegment(snapshotId, nameof(snapshotId));
        }
        ValidateEditableSaveSlot(slotRelativePath);

        using var guard = HollowKnightProcessGuard.Acquire(mutexName, processProbe);
        var basePath = snapshotId is null
            ? GetInstanceLocalLowPath(instanceId)
            : Path.Combine(GetSnapshotsRoot(instanceId), snapshotId, "data");
        var filePath = ResolveUnderBase(basePath, slotRelativePath);
        await SaveFileCodec.EncryptAsync(filePath, json, cancellationToken);

        if (snapshotId is not null)
        {
            var snapshotRoot = Path.Combine(GetSnapshotsRoot(instanceId), snapshotId);
            var metadataPath = Path.Combine(snapshotRoot, "snapshot.json");
            var snapshot = await AtomicJsonStore.ReadAsync<NamedSnapshot>(metadataPath, cancellationToken);
            var newHash = await LocalLowDirectory.HashFilesAsync(
                snapshot.SnapshotPath, includeLogs: false, cancellationToken);
            await AtomicJsonStore.WriteAsync(
                metadataPath,
                snapshot with { Sha256 = newHash },
                cancellationToken);
        }
    }

    private static string ResolveUnderBase(string basePath, string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(basePath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var normalizedBase = Path.GetFullPath(basePath);
        if (!full.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Path escapes the base directory.", nameof(relativePath));
        }

        return full;
    }

    private static void ValidateEditableSaveSlot(string slotRelativePath)
    {
        if (slotRelativePath.Length != "user1.dat".Length
            || !slotRelativePath.StartsWith("user", StringComparison.OrdinalIgnoreCase)
            || slotRelativePath[4] is < '1' or > '4'
            || !slotRelativePath.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Save editor slots must be one of user1.dat through user4.dat.",
                nameof(slotRelativePath));
        }
    }

    private string GetInstanceLocalLowPath(string instanceId) =>
        Path.Combine(storagePath, "instances", instanceId, "local-low");

    private string GetSnapshotsRoot(string instanceId) =>
        Path.Combine(storagePath, "instances", instanceId, "snapshots");

    private void ValidateSnapshot(string instanceId, string snapshotRoot, NamedSnapshot snapshot)
    {
        ValidateSegment(snapshot.Id, nameof(snapshot.Id));
        var expectedRoot = Path.Combine(GetSnapshotsRoot(instanceId), snapshot.Id);
        if (!LocalLowDirectory.PathEquals(snapshotRoot, expectedRoot)
            || !LocalLowDirectory.PathEquals(snapshot.SourcePath, GetInstanceLocalLowPath(instanceId))
            || !LocalLowDirectory.PathEquals(snapshot.SnapshotPath, Path.Combine(expectedRoot, "data")))
        {
            throw new InvalidDataException("Named snapshot paths are invalid.");
        }
        ValidateHash(snapshot.Sha256);
    }

    private static async Task RequireHashAsync(
        string path,
        string expectedHash,
        CancellationToken cancellationToken)
    {
        var actualHash = await LocalLowDirectory.HashFilesAsync(
            path,
            includeLogs: false,
            cancellationToken);
        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Named snapshot hash mismatch for '{path}'.");
        }
    }

    private static void ValidateHash(string hash)
    {
        if (hash.Length != 64 || hash.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException("Named snapshot hash is not a SHA-256 value.");
        }
    }

    private static void ValidateSegment(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value is "." or ".."
            || !string.Equals(Path.GetFileName(value), value, StringComparison.Ordinal)
            || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("Value must be a single valid path segment.", parameterName);
        }
    }
}

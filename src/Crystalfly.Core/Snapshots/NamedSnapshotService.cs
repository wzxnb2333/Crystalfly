using Crystalfly.Core.LocalLow;
using Crystalfly.Core.Models;
using Crystalfly.Core.Runtime;
using Crystalfly.Core.Serialization;

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
            var hash = await LocalLowDirectory.HashAsync(
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
        var previousPath = instancePath + $".snapshot-{operationId}-previous";
        try
        {
            await LocalLowDirectory.CopyAsync(
                snapshot.SnapshotPath,
                stagingPath,
                includeLogs: false,
                cancellationToken);
            await RequireHashAsync(stagingPath, snapshot.Sha256, cancellationToken);
            Directory.Move(instancePath, previousPath);
            Directory.Move(stagingPath, instancePath);
            await RequireHashAsync(instancePath, snapshot.Sha256, cancellationToken);
            LocalLowDirectory.DeleteIfExists(previousPath);
        }
        catch
        {
            if (!Directory.Exists(instancePath) && Directory.Exists(previousPath))
            {
                Directory.Move(previousPath, instancePath);
            }
            LocalLowDirectory.DeleteIfExists(stagingPath);
            throw;
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
        var actualHash = await LocalLowDirectory.HashAsync(
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

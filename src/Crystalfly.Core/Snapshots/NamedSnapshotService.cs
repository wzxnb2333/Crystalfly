using Crystalfly.Core.LocalLow;
using Crystalfly.Core.Models;
using Crystalfly.Core.Runtime;
using Crystalfly.Core.Saves;
using Crystalfly.Core.Serialization;
using Crystalfly.Core.Transactions;

namespace Crystalfly.Core.Snapshots;

public sealed partial class NamedSnapshotService
{
    private const string MetadataFileName = "snapshot.json";
    private readonly GameId gameId;
    private readonly string storagePath;
    private readonly string mutexName;
    private readonly IHollowKnightProcessProbe? processProbe;

    public NamedSnapshotService(
        string storageRoot,
        string mutexName = HollowKnightProcessGuard.DefaultMutexName,
        IHollowKnightProcessProbe? processProbe = null, GameId gameId = GameId.HollowKnight)
    {
        this.gameId = gameId;
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
            CopySaveFiles(sourcePath, stagingDataPath, cancellationToken);
            var hash = await LocalLowDirectory.HashFilesAsync(
                stagingDataPath,
                includeLogs: false,
                cancellationToken);
            var snapshot = new NamedSnapshot
            {
                GameId = gameId, Id = id,
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
            await LocalLowDirectory.MoveAsync(stagingRoot, snapshotRoot, cancellationToken);
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

    public async Task DeleteAsync(
        string instanceId,
        string snapshotId,
        CancellationToken cancellationToken = default)
    {
        ValidateSegment(instanceId, nameof(instanceId));
        ValidateSegment(snapshotId, nameof(snapshotId));
        using var guard = HollowKnightProcessGuard.Acquire(mutexName, processProbe);
        var snapshotRoot = Path.Combine(GetSnapshotsRoot(instanceId), snapshotId);
        await Task.Run(
            () => LocalLowDirectory.DeleteIfExists(snapshotRoot),
            cancellationToken);
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
            CopySaveFiles(snapshot.SnapshotPath, stagingPath, cancellationToken);
            var stagedFiles = LocalLowDirectory.EnumerateRelativeFiles(stagingPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var removals = LocalLowDirectory.EnumerateRelativeFiles(instancePath)
                .Where(path => IsSaveFile(path) || stagedFiles.Any(save => path.StartsWith(
                    save + "/",
                    StringComparison.OrdinalIgnoreCase)))
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
        var slots = LocalLowDirectory.EnumerateRelativeFiles(basePath)
            .Where(slot => SaveSlotPath.IsEditable(gameId, slot))
            .Order(StringComparer.OrdinalIgnoreCase)
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

    public async Task<bool> CanEditSaveAsync(string instanceId, string? snapshotId, string slotRelativePath, CancellationToken token = default)
    {
        ValidateSegment(instanceId, nameof(instanceId));
        if (snapshotId is not null) ValidateSegment(snapshotId, nameof(snapshotId));
        ValidateEditableSaveSlot(slotRelativePath);
        var basePath = snapshotId is null ? GetInstanceLocalLowPath(instanceId) : Path.Combine(GetSnapshotsRoot(instanceId), snapshotId, "data");
        var bytes = await File.ReadAllBytesAsync(ResolveUnderBase(basePath, slotRelativePath), token);
        var json = SaveFileCodec.Decrypt(bytes);
        var rebuilt = SaveGameEditor.Rebuild(json, SaveGameEditor.Flatten(json));
        return bytes.AsSpan().SequenceEqual(SaveFileCodec.Encrypt(json))
            && System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(json), System.Text.Json.Nodes.JsonNode.Parse(rebuilt));
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
        if (!await CanEditSaveAsync(instanceId, snapshotId, slotRelativePath, cancellationToken))
            throw new InvalidDataException("This save format cannot round-trip safely and is read-only.");
        _ = SaveGameEditor.Flatten(json);
        var backupRoot = ResolveUnderBase(storagePath, $"instances/{instanceId}/save-edit-backups");
        Directory.CreateDirectory(backupRoot);
        File.Copy(filePath, Path.Combine(backupRoot, $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}-{Path.GetFileName(filePath)}"));
        var transactionRoot = ResolveUnderBase(storagePath, "transactions");
        var recoveries = await FileTransaction.RecoverPendingAsync(transactionRoot, cancellationToken);
        if (recoveries.Any(recovery => recovery.State == TransactionState.NeedsAttention))
        {
            throw new InvalidOperationException("A pending file transaction needs attention.");
        }
        var workspace = ResolveUnderBase(transactionRoot, $".save-edit-{Guid.NewGuid():N}");
        var staging = Path.Combine(workspace, "staging");
        try
        {
            Directory.CreateDirectory(staging);
            var targetRoot = basePath;
            if (snapshotId is null)
            {
                var stagedPath = ResolveUnderBase(staging, slotRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(stagedPath)!);
                await SaveFileCodec.EncryptAsync(stagedPath, json, cancellationToken);
            }
            else
            {
                targetRoot = Path.Combine(GetSnapshotsRoot(instanceId), snapshotId);
                var metadataPath = ResolveUnderBase(targetRoot, MetadataFileName);
                var snapshot = await AtomicJsonStore.ReadAsync<NamedSnapshot>(metadataPath, cancellationToken);
                ValidateSnapshot(instanceId, targetRoot, snapshot);
                await RequireHashAsync(snapshot.SnapshotPath, snapshot.Sha256, cancellationToken);
                var preview = Path.Combine(workspace, "preview");
                await LocalLowDirectory.CopyAsync(snapshot.SnapshotPath, preview, includeLogs: true, cancellationToken);
                var previewSave = Path.Combine(preview, slotRelativePath);
                await SaveFileCodec.EncryptAsync(previewSave, json, cancellationToken);
                var newHash = await LocalLowDirectory.HashFilesAsync(preview, includeLogs: false, cancellationToken);
                var stagedPath = ResolveUnderBase(Path.Combine(staging, "data"), slotRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(stagedPath)!);
                File.Copy(previewSave, stagedPath);
                await AtomicJsonStore.WriteAsync(
                    Path.Combine(staging, MetadataFileName), snapshot with { Sha256 = newHash }, cancellationToken);
            }
            var stagedSave = ResolveUnderBase(snapshotId is null ? staging : Path.Combine(staging, "data"), slotRelativePath);
            if (await SaveFileCodec.DecryptAsync(stagedSave, cancellationToken) != json)
                throw new InvalidDataException("Save verification failed before commit.");
            var journal = await FileTransaction.ApplyDirectoryAsync(staging, targetRoot, transactionRoot, "edit-save", cancellationToken);
            if (await SaveFileCodec.DecryptAsync(filePath, CancellationToken.None) != json)
            {
                await FileTransaction.RollBackLatestAsync(Path.Combine(journal.RestorePointPath, "journal.json"), CancellationToken.None);
                throw new InvalidDataException("Save verification failed; the previous file was restored.");
            }
        }
        finally
        {
            LocalLowDirectory.DeleteIfExists(workspace);
        }
    }

    private static string ResolveUnderBase(string basePath, string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(basePath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var normalizedBase = Path.TrimEndingDirectorySeparator(Path.GetFullPath(basePath)) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Path escapes the base directory.", nameof(relativePath));
        }
        for (string? current = full; current is not null; current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current))
                && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("Save paths must not contain reparse points.");
            }
        }

        return full;
    }

    private void ValidateEditableSaveSlot(string slotRelativePath)
    {
        if (!SaveSlotPath.IsEditable(gameId, slotRelativePath))
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
        if (snapshot.GameId != gameId) throw new InvalidDataException("Snapshot belongs to a different game.");
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

    private void CopySaveFiles(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationPath);
        foreach (var relativePath in LocalLowDirectory.EnumerateRelativeFiles(sourcePath)
            .Where(IsSaveFile)
            .Order(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = LocalLowDirectory.ResolveUnderRoot(sourcePath, relativePath);
            var destination = LocalLowDirectory.ResolveUnderRoot(destinationPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, overwrite: true);
        }
    }

    private bool IsSaveFile(string relativePath) => SaveSlotPath.IsSaveFile(gameId, relativePath);

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

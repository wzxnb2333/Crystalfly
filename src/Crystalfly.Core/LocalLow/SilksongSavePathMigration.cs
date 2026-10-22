using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.LocalLow;

/// <summary>Preserves the old, incorrectly scoped backup before adopting the game's actual save root.</summary>
public static class SilksongSavePathMigration
{
    private sealed record Migration
    {
        public required string SourcePath { get; init; }
        public required string TargetPath { get; init; }
        public IReadOnlyList<string> EmptyInstanceIds { get; init; } = [];
        public bool Completed { get; init; }
    }

    public static async Task MigrateAsync(string sharedPath, string storageRoot, IEnumerable<string> instanceIds,
        bool allowActiveSessionCompletion = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        sharedPath = Path.GetFullPath(sharedPath);
        storageRoot = Path.GetFullPath(storageRoot);
        if (!string.Equals(Path.GetFileName(sharedPath), "Hollow Knight Silksong", StringComparison.OrdinalIgnoreCase)) return;
        var legacyPath = Path.Combine(Path.GetDirectoryName(sharedPath)!, "Silksong");
        var statePath = Path.Combine(storageRoot, "local-low");
        var takeoverPath = Path.Combine(statePath, "takeover.json");
        var markerPath = Path.Combine(storageRoot, "silksong-save-path-migration.json");
        var archivePath = Path.Combine(storageRoot, "local-low-legacy-silksong");
        Instances.InstanceDirectory.RejectReparseAncestors(statePath);
        Instances.InstanceDirectory.RejectReparseAncestors(archivePath);
        Instances.InstanceDirectory.RejectReparseAncestors(markerPath);
        Migration migration;
        var oldIsolation = new LocalLowIsolationService(legacyPath, storageRoot);
        if (File.Exists(markerPath))
        {
            migration = await AtomicJsonStore.ReadAsync<Migration>(markerPath, cancellationToken);
            if (!LocalLowDirectory.PathEquals(migration.SourcePath, legacyPath)
                || !LocalLowDirectory.PathEquals(migration.TargetPath, sharedPath))
                throw new InvalidDataException("Silksong save migration paths differ from this library.");
            if (migration.Completed) return;
        }
        else
        {
            if (!File.Exists(takeoverPath)) return;
            var takeover = await AtomicJsonStore.ReadAsync<LocalLowTakeoverRecord>(takeoverPath, cancellationToken);
            if (LocalLowDirectory.PathEquals(takeover.SharedPath, sharedPath)) return;
            if (!LocalLowDirectory.PathEquals(takeover.SharedPath, legacyPath))
                throw new InvalidDataException("The existing save backup belongs to another shared directory.");
            if (Directory.Exists(archivePath)) throw new IOException("A previous Silksong save migration archive needs inspection.");
            var recovery = await oldIsolation.RecoverPendingAsync(cancellationToken, allowActiveSessionCompletion);
            if (recovery.Any(item => item.State is TransactionState.NeedsAttention or TransactionState.Prepared or TransactionState.Applying))
                throw new InvalidOperationException("The previous Silksong save session must finish before migration.");
            var emptyIds = new List<string>();
            foreach (var id in instanceIds.Distinct(StringComparer.Ordinal))
            {
                var path = oldIsolation.GetInstanceLocalLowPath(id);
                if (Directory.Exists(path) && LocalLowDirectory.EnumerateRelativeFiles(path).Count == 0) emptyIds.Add(id);
            }
            migration = new() { SourcePath = legacyPath, TargetPath = sharedPath, EmptyInstanceIds = emptyIds };
            await AtomicJsonStore.WriteAsync(markerPath, migration, cancellationToken);
        }
        // Moving preserves every legacy journal and backup; the marker makes interrupted migration resumable.
        if (Directory.Exists(statePath) && !Directory.Exists(archivePath))
            await LocalLowDirectory.MoveAsync(statePath, archivePath, cancellationToken);
        else if (!Directory.Exists(archivePath))
            throw new DirectoryNotFoundException("The legacy Silksong save backup is missing.");

        foreach (var id in migration.EmptyInstanceIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = oldIsolation.GetInstanceLocalLowPath(id);
            if (!Directory.Exists(path) || LocalLowDirectory.EnumerateRelativeFiles(path).Count != 0) continue;
            var preserved = Path.Combine(archivePath, "empty-baselines", id);
            Instances.InstanceDirectory.RejectReparseAncestors(preserved);
            Directory.CreateDirectory(Path.GetDirectoryName(preserved)!);
            await LocalLowDirectory.MoveAsync(path, preserved, cancellationToken);
        }
        await AtomicJsonStore.WriteAsync(markerPath, migration with { Completed = true }, cancellationToken);
    }
}

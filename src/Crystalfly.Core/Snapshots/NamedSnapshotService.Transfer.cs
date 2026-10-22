using System.Text.Json.Nodes;
using Crystalfly.Core.Models;
using Crystalfly.Core.Runtime;
using Crystalfly.Core.Saves;
using Crystalfly.Core.LocalLow;
using Crystalfly.Core.Transactions;

namespace Crystalfly.Core.Snapshots;

public sealed partial class NamedSnapshotService
{
    public async Task<bool> ImportNeedsVersionWarningAsync(string source, string? targetVersion, CancellationToken token = default)
    {
        var (_, json) = await ReadImportAsync(source, token);
        return NeedsVersionWarning(json, targetVersion);
    }

    private static bool NeedsVersionWarning(string json, string? targetVersion)
    {
        var player = JsonNode.Parse(json)?["playerData"];
        var sourceVersion = player?["version"]?.GetValue<string>();
        var target = targetVersion?.Replace("silksong-", "", StringComparison.Ordinal);
        return !Version.TryParse(sourceVersion, out var from) || !Version.TryParse(target, out var to) || from > to;
    }

    private async Task<(byte[] Bytes, string Json)> ReadImportAsync(string source, CancellationToken token)
    {
        if (new FileInfo(source).Length > 64 * 1024 * 1024) throw new InvalidDataException("Save exceeds size limit.");
        var bytes = await File.ReadAllBytesAsync(source, token);
        var json = SaveFileCodec.Decrypt(bytes);
        var root = JsonNode.Parse(json) ?? throw new InvalidDataException("Empty save.");
        var player = root["playerData"] as JsonObject ?? throw new InvalidDataException("Missing playerData.");
        var detected = player.ContainsKey("silk") || player.ContainsKey("CurrentCrestID") ? GameId.Silksong
            : player.ContainsKey("MPCharge") || player.ContainsKey("nailDamage") ? GameId.HollowKnight : (GameId?)null;
        if (detected != gameId) throw new InvalidDataException("Save game identity is unknown or differs from the target.");
        if (!bytes.AsSpan().SequenceEqual(SaveFileCodec.Encrypt(json))
            || !JsonNode.DeepEquals(root, JsonNode.Parse(SaveGameEditor.Rebuild(json, SaveGameEditor.Flatten(json)))))
            throw new InvalidDataException("Save cannot round-trip safely.");
        return (bytes, json);
    }

    public async Task ImportSaveAsync(string instanceId, string slot, string source, string? targetVersion,
        bool acceptVersionRisk = false, CancellationToken token = default)
    {
        ValidateSegment(instanceId, nameof(instanceId));
        ValidateEditableSaveSlot(slot);
        using var guard = HollowKnightProcessGuard.Acquire(mutexName, processProbe);
        var (bytes, json) = await ReadImportAsync(source, token);
        if (!acceptVersionRisk && NeedsVersionWarning(json, targetVersion))
            throw new InvalidOperationException("Acknowledge importing a newer or unknown save version first.");
        var destination = GetInstanceLocalLowPath(instanceId);
        var target = ResolveUnderBase(destination, slot);
        var backupRoot = ResolveUnderBase(storagePath, $"instances/{instanceId}/save-edit-backups");
        if (File.Exists(target))
        {
            Directory.CreateDirectory(backupRoot);
            File.Copy(target, Path.Combine(backupRoot, $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}-{Path.GetFileName(target)}"));
        }
        var transactions = ResolveUnderBase(storagePath, "transactions");
        if ((await FileTransaction.RecoverPendingAsync(transactions, token)).Any(item => item.State == TransactionState.NeedsAttention))
            throw new InvalidOperationException("A save transaction needs recovery.");
        var staging = ResolveUnderBase(transactions, $".save-import-{Guid.NewGuid():N}");
        try
        {
            var stagedSave = ResolveUnderBase(staging, slot);
            Directory.CreateDirectory(Path.GetDirectoryName(stagedSave)!);
            await File.WriteAllBytesAsync(stagedSave, bytes, token);
            if (await SaveFileCodec.DecryptAsync(stagedSave, token) != json)
                throw new InvalidDataException("Save verification failed.");
            var journal = await FileTransaction.ApplyDirectoryAsync(staging, destination, transactions, "import-save", token);
            if (await SaveFileCodec.DecryptAsync(target, CancellationToken.None) != json)
            {
                await FileTransaction.RollBackLatestAsync(Path.Combine(journal.RestorePointPath, "journal.json"), CancellationToken.None);
                throw new InvalidDataException("Save import was rolled back.");
            }
        }
        finally { LocalLowDirectory.DeleteIfExists(staging); }
    }

    public async Task ExportSaveAsync(string instanceId, string? snapshotId, string slot, string destination, CancellationToken token = default)
    {
        ValidateSegment(instanceId, nameof(instanceId));
        if (snapshotId is not null) ValidateSegment(snapshotId, nameof(snapshotId));
        ValidateEditableSaveSlot(slot);
        using var guard = HollowKnightProcessGuard.Acquire(mutexName, processProbe);
        var source = ResolveUnderBase(snapshotId is null ? GetInstanceLocalLowPath(instanceId) : Path.Combine(GetSnapshotsRoot(instanceId), snapshotId, "data"), slot);
        var target = Path.GetFullPath(destination);
        Crystalfly.Core.Instances.InstanceDirectory.RejectReparseAncestors(target);
        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Choose a separate export file.");
        var temporary = target + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var input = File.OpenRead(source))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                await input.CopyToAsync(output, token);
            File.Move(temporary, target, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }
}

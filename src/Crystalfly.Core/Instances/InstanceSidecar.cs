using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Instances;

public static class InstanceSidecar
{
    private sealed record InstanceMarker
    {
        public int SchemaVersion { get; init; } = 1;

        public required string InstanceId { get; init; }
    }

    public static async Task SaveAsync(
        InstanceRecord record,
        CancellationToken cancellationToken = default)
    {
        var metadataPath = GetMetadataPath(record.RootPath, record.Id);
        ValidatePath(metadataPath);
        ValidatePath(GetMarkerPath(record.RootPath));
        await AtomicJsonStore.WriteAsync(metadataPath, record, cancellationToken);
        var marker = new InstanceMarker { InstanceId = record.Id };
        await AtomicJsonStore.WriteAsync(
            GetMarkerPath(record.RootPath),
            marker,
            cancellationToken);
        // A marker backup must identify this instance, not the original instance
        // whose marker may have been copied into this game directory.
        await AtomicJsonStore.WriteAsync(
            GetMarkerPath(record.RootPath),
            marker,
            cancellationToken);
    }

    public static async Task<InstanceRecord?> LoadAsync(
        string instanceRoot,
        CancellationToken cancellationToken = default)
    {
        var record = await LoadStoredAsync(instanceRoot, cancellationToken);
        return record is null ? null : record with { RootPath = Path.GetFullPath(instanceRoot) };
    }

    internal static async Task<InstanceRecord?> LoadStoredAsync(
        string instanceRoot,
        CancellationToken cancellationToken = default)
    {
        var marker = await ReadMarkerAsync(instanceRoot, cancellationToken);
        if (marker is null)
        {
            return null;
        }
        var metadataPath = GetMetadataPath(instanceRoot, marker.InstanceId);
        ValidatePath(metadataPath);
        if (!File.Exists(metadataPath) && !File.Exists(metadataPath + ".bak"))
        {
            return null;
        }
        var record = await AtomicJsonStore.ReadAsync<InstanceRecord>(metadataPath, cancellationToken);
        if (!string.Equals(record.Id, marker.InstanceId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Instance marker and metadata IDs do not match.");
        }
        if (record.SchemaVersion != InstanceRecord.CurrentSchemaVersion)
        {
            throw new InvalidDataException("Unsupported instance metadata schema version.");
        }
        return record;
    }

    public static async Task<string?> ReadMarkerInstanceIdAsync(
        string instanceRoot,
        CancellationToken cancellationToken = default)
    {
        var marker = await ReadMarkerAsync(instanceRoot, cancellationToken);
        return marker?.InstanceId;
    }

    private static async Task<InstanceMarker?> ReadMarkerAsync(
        string instanceRoot,
        CancellationToken cancellationToken)
    {
        var markerPath = GetMarkerPath(instanceRoot);
        ValidatePath(markerPath);
        if (!File.Exists(markerPath) && !File.Exists(markerPath + ".bak"))
        {
            return null;
        }
        var marker = await AtomicJsonStore.ReadAsync<InstanceMarker>(markerPath, cancellationToken);
        if (marker.SchemaVersion != 1)
        {
            throw new InvalidDataException("Unsupported instance marker schema version.");
        }
        return marker;
    }

    private static void ValidatePath(string path)
    {
        InstanceDirectory.RejectReparseAncestors(path);
        InstanceDirectory.RejectReparseAncestors(path + ".bak");
    }

    public static string GetMarkerPath(string instanceRoot) =>
        Path.Combine(instanceRoot, ".crystalfly-instance.json");

    public static string GetMetadataPath(string instanceRoot, string instanceId)
    {
        var versionRoot = Directory.GetParent(Path.GetFullPath(instanceRoot))?.FullName
            ?? throw new ArgumentException("Instance root must have a parent directory.", nameof(instanceRoot));
        var instancesRoot = Path.Combine(versionRoot, ".crystalfly", "instances");
        return Path.Combine(InstanceDirectory.ResolveUnderRoot(instancesRoot, instanceId), "instance.json");
    }
}

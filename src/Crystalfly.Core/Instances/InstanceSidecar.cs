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
        await AtomicJsonStore.WriteAsync(GetMetadataPath(record.RootPath, record.Id), record, cancellationToken);
        await AtomicJsonStore.WriteAsync(
            GetMarkerPath(record.RootPath),
            new InstanceMarker { InstanceId = record.Id },
            cancellationToken);
    }

    public static async Task<InstanceRecord?> LoadAsync(
        string instanceRoot,
        CancellationToken cancellationToken = default)
    {
        var marker = await ReadMarkerAsync(instanceRoot, cancellationToken);
        if (marker is null)
        {
            return null;
        }
        var metadataPath = GetMetadataPath(instanceRoot, marker.InstanceId);
        if (!File.Exists(metadataPath))
        {
            return null;
        }
        var record = await AtomicJsonStore.ReadAsync<InstanceRecord>(metadataPath, cancellationToken);
        if (!string.Equals(record.Id, marker.InstanceId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Instance marker and metadata IDs do not match.");
        }
        return record with { RootPath = Path.GetFullPath(instanceRoot) };
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
        if (!File.Exists(markerPath))
        {
            return null;
        }
        return await AtomicJsonStore.ReadAsync<InstanceMarker>(markerPath, cancellationToken);
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

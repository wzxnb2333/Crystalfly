using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;
using System.Text.Json;

namespace Crystalfly.Core.Tests.Instances;

public sealed class InstanceSidecarTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"crystalfly-sidecar-{Guid.NewGuid():N}");

    [Fact]
    public async Task Save_places_minimal_marker_at_instance_root()
    {
        var record = new InstanceRecord
        {
            Id = "practice",
            Name = "Practice",
            RootPath = root,
            BuildId = "1.2.2.1",
            CreatedAt = DateTimeOffset.Parse("2026-07-16T12:00:00Z")
        };

        await InstanceSidecar.SaveAsync(record);

        var markerPath = Path.Combine(root, ".crystalfly-instance.json");
        Assert.True(File.Exists(markerPath));
        using var marker = JsonDocument.Parse(await File.ReadAllTextAsync(markerPath));
        Assert.Equal(["instanceId", "schemaVersion"], marker.RootElement
            .EnumerateObject()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray());
        Assert.Equal(record.Id, marker.RootElement.GetProperty("instanceId").GetString());
        Assert.True(File.Exists(Path.Combine(
            Directory.GetParent(root)!.FullName,
            ".crystalfly",
            "instances",
            record.Id,
            "instance.json")));
        Assert.Equal(record, await InstanceSidecar.LoadAsync(root));
    }

    [Fact]
    public async Task Load_uses_the_renamed_instance_directory_as_root_path()
    {
        var versionRoot = Directory.CreateDirectory(Path.Combine(root, "versions")).FullName;
        var originalRoot = Directory.CreateDirectory(Path.Combine(versionRoot, "Original")).FullName;
        var renamedRoot = Path.Combine(versionRoot, "Renamed");
        await InstanceSidecar.SaveAsync(CreateRecord(originalRoot));
        Directory.Move(originalRoot, renamedRoot);

        var record = await InstanceSidecar.LoadAsync(renamedRoot);

        Assert.Equal(renamedRoot, record.RootPath);
    }

    [Fact]
    public async Task Load_does_not_trust_a_root_path_outside_the_scanned_directory()
    {
        var versionRoot = Directory.CreateDirectory(Path.Combine(root, "versions")).FullName;
        var scannedRoot = Directory.CreateDirectory(Path.Combine(versionRoot, "Scanned")).FullName;
        var outsideRoot = Directory.CreateDirectory(Path.Combine(root, "outside")).FullName;
        var record = CreateRecord(scannedRoot);
        await InstanceSidecar.SaveAsync(record);
        await AtomicJsonStore.WriteAsync(
            InstanceSidecar.GetMetadataPath(scannedRoot, record.Id),
            record with { RootPath = outsideRoot });

        var loaded = await InstanceSidecar.LoadAsync(scannedRoot);

        Assert.Equal(scannedRoot, loaded.RootPath);
    }

    [Theory]
    [InlineData(@"..\..\escaped")]
    [InlineData(@"C:\escaped")]
    public async Task Save_rejects_instance_ids_that_are_not_single_directory_names(string instanceId)
    {
        var instanceRoot = Directory.CreateDirectory(Path.Combine(root, "versions", "Practice")).FullName;
        var record = CreateRecord(instanceRoot) with { Id = instanceId };

        await Assert.ThrowsAsync<ArgumentException>(() => InstanceSidecar.SaveAsync(record));
    }

    [Fact]
    public async Task Load_rejects_metadata_with_a_different_instance_id()
    {
        var instanceRoot = Directory.CreateDirectory(Path.Combine(root, "versions", "Practice")).FullName;
        var record = CreateRecord(instanceRoot);
        await InstanceSidecar.SaveAsync(record);
        await AtomicJsonStore.WriteAsync(
            InstanceSidecar.GetMetadataPath(instanceRoot, record.Id),
            record with { Id = "different" });

        await Assert.ThrowsAsync<InvalidDataException>(() => InstanceSidecar.LoadAsync(instanceRoot));
    }

    private static InstanceRecord CreateRecord(string instanceRoot) => new()
    {
        Id = "practice",
        Name = "Practice",
        RootPath = instanceRoot,
        BuildId = "1.2.2.1",
        CreatedAt = DateTimeOffset.Parse("2026-07-16T12:00:00Z")
    };

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;
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

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

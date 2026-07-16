using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;

namespace Crystalfly.Core.Tests.Instances;

public sealed class InstanceSidecarTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"crystalfly-sidecar-{Guid.NewGuid():N}");

    [Fact]
    public async Task Save_places_record_in_instance_metadata_directory()
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

        Assert.True(File.Exists(Path.Combine(root, ".crystalfly", "instance.json")));
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

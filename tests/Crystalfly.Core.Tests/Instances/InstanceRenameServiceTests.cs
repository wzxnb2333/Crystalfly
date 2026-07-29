using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;

namespace Crystalfly.Core.Tests.Instances;

public sealed class InstanceRenameServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"crystalfly-rename-{Guid.NewGuid():N}");

    [Fact]
    public async Task Rename_moves_instance_directory_and_updates_sidecar()
    {
        var source = Directory.CreateDirectory(Path.Combine(root, "Old Name")).FullName;
        await File.WriteAllTextAsync(Path.Combine(source, "hollow_knight.exe"), "game");
        var original = new InstanceRecord
        {
            Id = "instance-id",
            Name = "Old Name",
            RootPath = source,
            BuildId = "1.5.78.11833",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await InstanceSidecar.SaveAsync(original);

        var renamed = await InstanceRenameService.RenameAsync(original, "New Name");

        Assert.False(Directory.Exists(source));
        Assert.Equal(Path.Combine(root, "New Name"), renamed.RootPath);
        Assert.Equal("New Name", renamed.Name);
        Assert.Equal(renamed, await InstanceSidecar.LoadAsync(renamed.RootPath));
        Assert.Equal("game", await File.ReadAllTextAsync(Path.Combine(renamed.RootPath, "hollow_knight.exe")));
    }

    [Fact]
    public async Task Rename_rejects_existing_destination_without_moving_source()
    {
        var source = Directory.CreateDirectory(Path.Combine(root, "Source")).FullName;
        Directory.CreateDirectory(Path.Combine(root, "Existing"));
        var original = new InstanceRecord
        {
            Id = "instance-id",
            Name = "Source",
            RootPath = source,
            BuildId = "1.5.78.11833",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await InstanceSidecar.SaveAsync(original);

        await Assert.ThrowsAsync<IOException>(() =>
            InstanceRenameService.RenameAsync(original, "Existing"));

        Assert.True(Directory.Exists(source));
        Assert.Equal(original, await InstanceSidecar.LoadAsync(source));
    }

    [Theory]
    [InlineData("..")]
    [InlineData("nested/name")]
    [InlineData("nested\\name")]
    public async Task Rename_rejects_names_outside_version_root(string name)
    {
        var source = Directory.CreateDirectory(Path.Combine(root, "Source")).FullName;
        var original = new InstanceRecord
        {
            Id = "instance-id",
            Name = "Source",
            RootPath = source,
            BuildId = "1.5.78.11833",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await InstanceSidecar.SaveAsync(original);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            InstanceRenameService.RenameAsync(original, name));

        Assert.True(Directory.Exists(source));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

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

    [Fact]
    public async Task Rename_preserves_official_speedrun_metadata_patches_and_saves()
    {
        var source = Directory.CreateDirectory(Path.Combine(root, "Official")).FullName;
        var original = new InstanceRecord
        {
            Id = "official-instance",
            Name = "Official",
            RootPath = source,
            BuildId = "1.5.78.11833",
            Purpose = InstancePurpose.OfficialSpeedrun,
            ProvisioningMode = InstanceProvisioningMode.FullCopy,
            SpeedrunTemplateId = "runtime-patches-1578",
            SpeedrunRulesRevision = "rules-revision",
            SpeedrunSaveStatesMode = SpeedrunSaveStatesMode.Multi,
            CreatedAt = DateTimeOffset.UtcNow
        };
        string[] files = ["hollow_knight_Data/Managed/Assembly-CSharp.dll", "RuntimePatches.json", "saves/user1.dat"];
        byte[] content = [0, 1, 2, 127, 128, 255];
        foreach (string relativePath in files)
        {
            string path = Path.Combine(source, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, content);
        }
        await InstanceSidecar.SaveAsync(original);

        var renamed = await InstanceRenameService.RenameAsync(original, "正式速通练习");

        Assert.Equal(original with { Name = "正式速通练习", RootPath = Path.Combine(root, "正式速通练习") }, renamed);
        Assert.Equal(renamed, await InstanceSidecar.LoadAsync(renamed.RootPath));
        Assert.False(Directory.Exists(source));
        foreach (string relativePath in files)
        {
            Assert.Equal(content, await File.ReadAllBytesAsync(Path.Combine(renamed.RootPath, relativePath)));
        }
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

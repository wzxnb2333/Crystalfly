using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;

namespace Crystalfly.Core.Tests.Instances;

public sealed class InstanceCloneServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"crystalfly-clone-{Guid.NewGuid():N}");

    [Fact]
    public async Task Clone_copies_game_files_and_writes_a_new_instance_marker()
    {
        var source = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
        Directory.CreateDirectory(Path.Combine(source, "hollow_knight_Data", "Managed"));
        await File.WriteAllTextAsync(Path.Combine(source, "hollow_knight.exe"), "game");
        await File.WriteAllTextAsync(
            Path.Combine(source, "hollow_knight_Data", "Managed", "Assembly-CSharp.dll"),
            "assembly");
        await InstanceSidecar.SaveAsync(new InstanceRecord
        {
            Id = "old-id",
            Name = "Source",
            RootPath = source,
            BuildId = "1.5.78.11833",
            CreatedAt = DateTimeOffset.UtcNow
        });

        var clone = await InstanceCloneService.CloneAsync(source, "Practice Copy", "new-id");

        Assert.Equal(Path.Combine(root, "Practice Copy"), clone.RootPath);
        Assert.Equal("new-id", clone.Id);
        Assert.Equal("Practice Copy", clone.Name);
        Assert.Equal("1.5.78.11833", clone.BuildId);
        Assert.Equal("game", await File.ReadAllTextAsync(Path.Combine(clone.RootPath, "hollow_knight.exe")));
        Assert.Equal("assembly", await File.ReadAllTextAsync(Path.Combine(
            clone.RootPath,
            "hollow_knight_Data",
            "Managed",
            "Assembly-CSharp.dll")));
        Assert.Equal(clone, await InstanceSidecar.LoadAsync(clone.RootPath));
    }

    [Fact]
    public async Task Concurrent_clones_to_the_same_name_keep_the_winning_destination()
    {
        var source = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
        await File.WriteAllBytesAsync(Path.Combine(source, "hollow_knight.exe"), new byte[4 * 1024 * 1024]);
        await InstanceSidecar.SaveAsync(new InstanceRecord
        {
            Id = "source",
            Name = "Source",
            RootPath = source,
            BuildId = "1.5.78.11833",
            CreatedAt = DateTimeOffset.UtcNow
        });

        using var start = new Barrier(3);
        var first = Task.Run(async () =>
        {
            start.SignalAndWait();
            return await InstanceCloneService.CloneAsync(source, "Shared Copy", "first");
        });
        var second = Task.Run(async () =>
        {
            start.SignalAndWait();
            return await InstanceCloneService.CloneAsync(source, "Shared Copy", "second");
        });
        start.SignalAndWait();

        await Assert.ThrowsAsync<IOException>(() => Task.WhenAll(first, second));

        var destination = Path.Combine(root, "Shared Copy");
        var clones = new[] { first, second };
        Assert.Equal(1, clones.Count(task => task.IsCompletedSuccessfully));
        Assert.IsType<IOException>(clones.Single(task => task.IsFaulted).Exception!.GetBaseException());
        Assert.True(Directory.Exists(destination));
        Assert.True(File.Exists(Path.Combine(destination, "hollow_knight.exe")));
        Assert.Contains((await InstanceSidecar.LoadAsync(destination)).Id, new[] { "first", "second" });
    }

    [Theory]
    [InlineData("..")]
    [InlineData("nested/copy")]
    [InlineData("nested\\copy")]
    public async Task Clone_rejects_names_that_escape_the_version_root(string name)
    {
        var source = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
        await InstanceSidecar.SaveAsync(new InstanceRecord
        {
            Id = "source",
            Name = "Source",
            RootPath = source,
            BuildId = "1.2.2.1",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            InstanceCloneService.CloneAsync(source, name, "new-id"));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

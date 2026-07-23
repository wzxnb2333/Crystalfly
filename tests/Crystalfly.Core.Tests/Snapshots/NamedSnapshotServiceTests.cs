using Crystalfly.Core.LocalLow;
using Crystalfly.Core.Runtime;
using Crystalfly.Core.Snapshots;

namespace Crystalfly.Core.Tests.Snapshots;

public sealed class NamedSnapshotServiceTests
{
    [Fact]
    public async Task Create_and_restore_preserve_named_snapshot_permanently_and_restore_exact_directory()
    {
        using var test = new TestDirectory();
        var storage = test.CreateDirectory("version", ".crystalfly");
        var instance = test.CreateDirectory(
            "version", ".crystalfly", "instances", "practice", "local-low");
        await test.WriteAsync(instance, "user1.dat", "before-boss");
        await test.WriteAsync(instance, "settings.json", "settings-before");
        var service = CreateService(storage);

        var snapshot = await service.CreateAsync("practice", "Before Watcher Knights");
        await File.WriteAllTextAsync(Path.Combine(instance, "user1.dat"), "after-boss");
        await test.WriteAsync(instance, "later-file.json", "remove-on-restore");

        await service.RestoreAsync("practice", snapshot.Id);

        Assert.Equal("before-boss", await File.ReadAllTextAsync(Path.Combine(instance, "user1.dat")));
        Assert.Equal("settings-before", await File.ReadAllTextAsync(Path.Combine(instance, "settings.json")));
        Assert.False(File.Exists(Path.Combine(instance, "later-file.json")));
        Assert.True(Directory.Exists(snapshot.SnapshotPath));
        Assert.Equal(snapshot, Assert.Single(await service.ListAsync("practice")));
    }

    [Fact]
    public async Task Restore_rejects_tampered_snapshot_and_keeps_instance_unchanged()
    {
        using var test = new TestDirectory();
        var storage = test.CreateDirectory("version", ".crystalfly");
        var instance = test.CreateDirectory(
            "version", ".crystalfly", "instances", "practice", "local-low");
        await test.WriteAsync(instance, "user1.dat", "snapshot-content");
        var service = CreateService(storage);
        var snapshot = await service.CreateAsync("practice", "Clean save");
        await File.WriteAllTextAsync(Path.Combine(snapshot.SnapshotPath, "user1.dat"), "tampered");
        await File.WriteAllTextAsync(Path.Combine(instance, "user1.dat"), "current-instance");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.RestoreAsync("practice", snapshot.Id));

        Assert.Equal("current-instance", await File.ReadAllTextAsync(Path.Combine(instance, "user1.dat")));
        Assert.True(Directory.Exists(snapshot.SnapshotPath));
    }

    [Fact]
    public async Task Restore_replaces_current_file_with_snapshot_directory()
    {
        using var test = new TestDirectory();
        var storage = test.CreateDirectory("version", ".crystalfly");
        var instance = test.CreateDirectory(
            "version", ".crystalfly", "instances", "practice", "local-low");
        await test.WriteAsync(instance, "slot", "user1.dat", "snapshot-save");
        var service = CreateService(storage);
        var snapshot = await service.CreateAsync("practice", "Directory save");
        Directory.Delete(Path.Combine(instance, "slot"), recursive: true);
        await File.WriteAllTextAsync(Path.Combine(instance, "slot"), "current-file");

        await service.RestoreAsync("practice", snapshot.Id);

        Assert.True(Directory.Exists(Path.Combine(instance, "slot")));
        Assert.Equal(
            "snapshot-save",
            await File.ReadAllTextAsync(Path.Combine(instance, "slot", "user1.dat")));
    }

    [Fact]
    public async Task Restore_replaces_current_directory_with_snapshot_file()
    {
        using var test = new TestDirectory();
        var storage = test.CreateDirectory("version", ".crystalfly");
        var instance = test.CreateDirectory(
            "version", ".crystalfly", "instances", "practice", "local-low");
        await File.WriteAllTextAsync(Path.Combine(instance, "slot"), "snapshot-save");
        var service = CreateService(storage);
        var snapshot = await service.CreateAsync("practice", "File save");
        File.Delete(Path.Combine(instance, "slot"));
        await test.WriteAsync(instance, "slot", "user1.dat", "current-save");

        await service.RestoreAsync("practice", snapshot.Id);

        Assert.True(File.Exists(Path.Combine(instance, "slot")));
        Assert.Equal("snapshot-save", await File.ReadAllTextAsync(Path.Combine(instance, "slot")));
    }

    [Fact]
    public async Task Create_is_blocked_when_hollow_knight_process_is_running()
    {
        using var test = new TestDirectory();
        var storage = test.CreateDirectory("version", ".crystalfly");
        var instance = test.CreateDirectory(
            "version", ".crystalfly", "instances", "practice", "local-low");
        await test.WriteAsync(instance, "user1.dat", "save");
        var service = new NamedSnapshotService(
            storage,
            UniqueMutexName(),
            new StubProcessProbe(isRunning: true));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync("practice", "Blocked"));
    }

    [Fact]
    public async Task List_save_slots_returns_only_root_user1_through_user4_for_selected_instance()
    {
        using var test = new TestDirectory();
        var storage = test.CreateDirectory("version", ".crystalfly");
        var selected = test.CreateDirectory(
            "version", ".crystalfly", "instances", "practice", "local-low");
        var other = test.CreateDirectory(
            "version", ".crystalfly", "instances", "race", "local-low");
        await test.WriteAsync(selected, "user1.dat", "slot-1");
        await test.WriteAsync(selected, "user3.dat", "slot-3");
        await test.WriteAsync(selected, "user1_1.4.3.2.dat", "version-backup");
        await test.WriteAsync(selected, "archive", "user2.dat", "nested-backup");
        await test.WriteAsync(selected, "user5.dat", "unsupported-slot");
        await test.WriteAsync(other, "user2.dat", "other-instance");
        var service = CreateService(storage);

        var slots = await service.ListSaveSlotsAsync("practice", snapshotId: null);

        Assert.Equal(["user1.dat", "user3.dat"], slots);
    }

    [Theory]
    [InlineData("user1_1.4.3.2.dat")]
    [InlineData("archive/user2.dat")]
    [InlineData("user5.dat")]
    public async Task Save_editor_operations_reject_non_slot_paths(string relativePath)
    {
        using var test = new TestDirectory();
        var storage = test.CreateDirectory("version", ".crystalfly");
        test.CreateDirectory("version", ".crystalfly", "instances", "practice", "local-low");
        var service = CreateService(storage);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.DecryptSaveAsync("practice", snapshotId: null, relativePath));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateSaveAsync("practice", snapshotId: null, relativePath, "{}"));
    }

    private static NamedSnapshotService CreateService(string storage) => new(
        storage,
        UniqueMutexName(),
        new StubProcessProbe(isRunning: false));

    private static string UniqueMutexName() => $"Crystalfly.Tests.{Guid.NewGuid():N}";

    private sealed class StubProcessProbe(bool isRunning) : IHollowKnightProcessProbe
    {
        public bool IsRunning() => isRunning;
    }

    private sealed class TestDirectory : IDisposable
    {
        private readonly string root = Path.Combine(
            Path.GetTempPath(), "Crystalfly.Tests", Guid.NewGuid().ToString("N"));

        public string CreateDirectory(params string[] parts)
        {
            var path = parts.Aggregate(root, Path.Combine);
            Directory.CreateDirectory(path);
            return path;
        }

        public async Task WriteAsync(string directory, params string[] pathAndContent)
        {
            var content = pathAndContent[^1];
            var path = pathAndContent[..^1].Aggregate(directory, Path.Combine);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, content);
        }

        public void Dispose()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}

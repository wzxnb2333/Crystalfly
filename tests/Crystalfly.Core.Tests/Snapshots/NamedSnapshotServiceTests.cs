using Crystalfly.Core.LocalLow;
using Crystalfly.Core.Runtime;
using Crystalfly.Core.Serialization;
using Crystalfly.Core.Snapshots;

namespace Crystalfly.Core.Tests.Snapshots;

public sealed class NamedSnapshotServiceTests
{
    [Fact]
    public async Task Create_and_restore_only_save_files_without_changing_other_instance_data()
    {
        using var test = new TestDirectory();
        var storage = test.CreateDirectory("version", ".crystalfly");
        var instance = test.CreateDirectory(
            "version", ".crystalfly", "instances", "practice", "local-low");
        await test.WriteAsync(instance, "user1.dat", "before-boss");
        await test.WriteAsync(instance, "user1.dat.bak1", "before-boss-backup");
        await test.WriteAsync(instance, "settings.json", "settings-before");
        var service = CreateService(storage);

        var snapshot = await service.CreateAsync("practice", "Before Watcher Knights");
        Assert.True(File.Exists(Path.Combine(snapshot.SnapshotPath, "user1.dat")));
        Assert.True(File.Exists(Path.Combine(snapshot.SnapshotPath, "user1.dat.bak1")));
        Assert.False(File.Exists(Path.Combine(snapshot.SnapshotPath, "settings.json")));

        await File.WriteAllTextAsync(Path.Combine(instance, "user1.dat"), "after-boss");
        await File.WriteAllTextAsync(Path.Combine(instance, "user1.dat.bak1"), "after-boss-backup");
        await File.WriteAllTextAsync(Path.Combine(instance, "settings.json"), "settings-after");
        await test.WriteAsync(instance, "user2.dat", "new-slot");
        await test.WriteAsync(instance, "later-file.json", "remove-on-restore");

        await service.RestoreAsync("practice", snapshot.Id);

        Assert.Equal("before-boss", await File.ReadAllTextAsync(Path.Combine(instance, "user1.dat")));
        Assert.Equal(
            "before-boss-backup",
            await File.ReadAllTextAsync(Path.Combine(instance, "user1.dat.bak1")));
        Assert.False(File.Exists(Path.Combine(instance, "user2.dat")));
        Assert.Equal("settings-after", await File.ReadAllTextAsync(Path.Combine(instance, "settings.json")));
        Assert.True(File.Exists(Path.Combine(instance, "later-file.json")));
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
    public async Task Restore_legacy_snapshot_ignores_non_save_files()
    {
        using var test = new TestDirectory();
        var storage = test.CreateDirectory("version", ".crystalfly");
        var instance = test.CreateDirectory(
            "version", ".crystalfly", "instances", "practice", "local-low");
        await test.WriteAsync(instance, "user1.dat", "snapshot-save");
        var service = CreateService(storage);
        var snapshot = await service.CreateAsync("practice", "Legacy snapshot");
        await test.WriteAsync(snapshot.SnapshotPath, "settings.json", "legacy-settings");
        var legacySnapshot = snapshot with
        {
            Sha256 = await LocalLowDirectory.HashFilesAsync(
                snapshot.SnapshotPath,
                includeLogs: false,
                CancellationToken.None)
        };
        await AtomicJsonStore.WriteAsync(
            Path.Combine(Path.GetDirectoryName(snapshot.SnapshotPath)!, "snapshot.json"),
            legacySnapshot);
        await File.WriteAllTextAsync(Path.Combine(instance, "user1.dat"), "current-save");
        await File.WriteAllTextAsync(Path.Combine(instance, "settings.json"), "current-settings");

        await service.RestoreAsync("practice", snapshot.Id);

        Assert.Equal("snapshot-save", await File.ReadAllTextAsync(Path.Combine(instance, "user1.dat")));
        Assert.Equal("current-settings", await File.ReadAllTextAsync(Path.Combine(instance, "settings.json")));
    }

    [Fact]
    public async Task Restore_removes_current_save_when_snapshot_slot_is_empty()
    {
        using var test = new TestDirectory();
        var storage = test.CreateDirectory("version", ".crystalfly");
        var instance = test.CreateDirectory(
            "version", ".crystalfly", "instances", "practice", "local-low");
        await test.WriteAsync(instance, "settings.json", "keep-settings");
        var service = CreateService(storage);
        var snapshot = await service.CreateAsync("practice", "Directory save");
        await File.WriteAllTextAsync(Path.Combine(instance, "user1.dat"), "current-save");

        await service.RestoreAsync("practice", snapshot.Id);

        Assert.False(File.Exists(Path.Combine(instance, "user1.dat")));
        Assert.Equal("keep-settings", await File.ReadAllTextAsync(Path.Combine(instance, "settings.json")));
    }

    [Fact]
    public async Task Restore_replaces_current_save_directory_with_snapshot_file()
    {
        using var test = new TestDirectory();
        var storage = test.CreateDirectory("version", ".crystalfly");
        var instance = test.CreateDirectory(
            "version", ".crystalfly", "instances", "practice", "local-low");
        await File.WriteAllTextAsync(Path.Combine(instance, "user1.dat"), "snapshot-save");
        var service = CreateService(storage);
        var snapshot = await service.CreateAsync("practice", "File save");
        File.Delete(Path.Combine(instance, "user1.dat"));
        await test.WriteAsync(instance, "user1.dat", "nested", "current-save");

        await service.RestoreAsync("practice", snapshot.Id);

        Assert.True(File.Exists(Path.Combine(instance, "user1.dat")));
        Assert.Equal("snapshot-save", await File.ReadAllTextAsync(Path.Combine(instance, "user1.dat")));
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

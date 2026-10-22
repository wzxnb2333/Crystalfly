using Crystalfly.Core.LocalLow;

namespace Crystalfly.Core.Tests.LocalLow;

public sealed class SilksongSavePathMigrationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Crystalfly.SaveMigration", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Wrong_root_empty_baselines_are_reseeded_without_overwriting_existing_saves()
    {
        var correct = Path.Combine(root, "Team Cherry", "Hollow Knight Silksong");
        var legacy = Path.Combine(root, "Team Cherry", "Silksong");
        var storage = Path.Combine(root, "storage");
        Directory.CreateDirectory(Path.Combine(correct, "123456789"));
        await File.WriteAllTextAsync(Path.Combine(correct, "123456789", "user1.dat"), "original progress");
        var oldIsolation = new LocalLowIsolationService(legacy, storage);
        await oldIsolation.InitializeBaselinesAsync(["empty", "edited"]);
        await File.WriteAllTextAsync(Path.Combine(oldIsolation.GetInstanceLocalLowPath("edited"), "user2.dat"), "private progress");
        await SilksongSavePathMigration.MigrateAsync(correct, storage, ["empty", "edited"]);
        var isolation = new LocalLowIsolationService(correct, storage);
        await isolation.InitializeBaselinesAsync(["empty", "edited"]);
        Assert.Equal("original progress", await File.ReadAllTextAsync(Path.Combine(isolation.GetInstanceLocalLowPath("empty"), "123456789/user1.dat")));
        Assert.Equal("private progress", await File.ReadAllTextAsync(Path.Combine(isolation.GetInstanceLocalLowPath("edited"), "user2.dat")));
        Assert.True(File.Exists(Path.Combine(storage, "local-low-legacy-silksong", "takeover.json")));
        await SilksongSavePathMigration.MigrateAsync(correct, storage, ["empty", "edited"]);
        var session = await isolation.SwitchInAsync("empty");
        await File.WriteAllTextAsync(Path.Combine(correct, "123456789/user1.dat"), "session progress");
        await isolation.SwitchOutAsync(session.Id);
        Assert.Equal("original progress", await File.ReadAllTextAsync(Path.Combine(correct, "123456789/user1.dat")));
        Assert.Equal("session progress", await File.ReadAllTextAsync(Path.Combine(isolation.GetInstanceLocalLowPath("empty"), "123456789/user1.dat")));
    }

    [Fact]
    public async Task Active_legacy_session_blocks_migration_until_recovered()
    {
        var correct = Path.Combine(root, "Team Cherry", "Hollow Knight Silksong");
        var storage = Path.Combine(root, "storage");
        var oldIsolation = new LocalLowIsolationService(Path.Combine(root, "Team Cherry", "Silksong"), storage);
        var session = await oldIsolation.SwitchInAsync("test");
        await Assert.ThrowsAsync<InvalidOperationException>(() => SilksongSavePathMigration.MigrateAsync(correct, storage, ["test"]));
        Assert.False(Directory.Exists(Path.Combine(storage, "local-low-legacy-silksong")));
        await oldIsolation.SwitchOutAsync(session.Id);
        await SilksongSavePathMigration.MigrateAsync(correct, storage, ["test"]);
        Assert.True(Directory.Exists(Path.Combine(storage, "local-low-legacy-silksong")));
    }

    [Fact]
    public async Task Cancelled_migration_leaves_legacy_state_in_place()
    {
        var correct = Path.Combine(root, "Team Cherry", "Hollow Knight Silksong");
        var storage = Path.Combine(root, "storage");
        await new LocalLowIsolationService(Path.Combine(root, "Team Cherry", "Silksong"), storage).InitializeBaselinesAsync(["test"]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SilksongSavePathMigration.MigrateAsync(correct, storage, ["test"],
            cancellationToken: new CancellationToken(true)));
        Assert.True(File.Exists(Path.Combine(storage, "local-low", "takeover.json")));
        Assert.False(Directory.Exists(Path.Combine(storage, "local-low-legacy-silksong")));
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

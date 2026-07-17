using Crystalfly.Core.LocalLow;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Tests.LocalLow;

public sealed class LocalLowIsolationServiceTests
{
    [Fact]
    public async Task Initial_takeover_creates_the_same_non_log_baseline_for_every_discovered_instance()
    {
        using var test = new TestDirectory();
        var shared = test.CreateDirectory("local-low", "Hollow Knight");
        var storage = test.CreateDirectory("version", ".crystalfly");
        await test.WriteAsync(shared, "user1.dat", "shared-save");
        await test.WriteAsync(shared, "output_log.txt", "unity-log");
        var service = new LocalLowIsolationService(shared, storage);

        await service.InitializeBaselinesAsync(["practice-1221", "race-1578"]);

        Assert.Equal("unity-log", await File.ReadAllTextAsync(
            Path.Combine(service.SharedBackupPath, "output_log.txt")));
        foreach (var instanceId in new[] { "practice-1221", "race-1578" })
        {
            var instance = service.GetInstanceLocalLowPath(instanceId);
            Assert.Equal("shared-save", await File.ReadAllTextAsync(Path.Combine(instance, "user1.dat")));
            Assert.False(File.Exists(Path.Combine(instance, "output_log.txt")));
        }
    }

    [Fact]
    public async Task First_switch_backs_up_all_shared_data_and_excludes_logs_from_instance_baseline()
    {
        using var test = new TestDirectory();
        var shared = test.CreateDirectory("local-low", "Hollow Knight");
        var storage = test.CreateDirectory("version", ".crystalfly");
        await test.WriteAsync(shared, "user1.dat", "shared-save");
        await test.WriteAsync(shared, "output_log.txt", "unity-log");
        await test.WriteAsync(shared, "Logs", "ModLog.log", "mod-log");

        var service = new LocalLowIsolationService(shared, storage);
        var session = await service.SwitchInAsync("practice-1221");

        Assert.Equal("shared-save", await File.ReadAllTextAsync(Path.Combine(shared, "user1.dat")));
        Assert.False(File.Exists(Path.Combine(shared, "output_log.txt")));
        Assert.False(Directory.Exists(Path.Combine(shared, "Logs")));
        Assert.Equal("unity-log", await File.ReadAllTextAsync(
            Path.Combine(service.SharedBackupPath, "output_log.txt")));
        Assert.Equal("mod-log", await File.ReadAllTextAsync(
            Path.Combine(service.SharedBackupPath, "Logs", "ModLog.log")));

        var instance = service.GetInstanceLocalLowPath("practice-1221");
        Assert.Equal("shared-save", await File.ReadAllTextAsync(Path.Combine(instance, "user1.dat")));
        Assert.False(File.Exists(Path.Combine(instance, "output_log.txt")));
        Assert.False(Directory.Exists(Path.Combine(instance, "Logs")));

        await service.SwitchOutAsync(session.Id);

        Assert.Equal("shared-save", await File.ReadAllTextAsync(Path.Combine(shared, "user1.dat")));
        Assert.Equal("unity-log", await File.ReadAllTextAsync(Path.Combine(shared, "output_log.txt")));
    }

    [Fact]
    public async Task Switch_out_writes_non_log_data_to_instance_then_restores_shared_data()
    {
        using var test = new TestDirectory();
        var shared = test.CreateDirectory("local-low", "Hollow Knight");
        var storage = test.CreateDirectory("version", ".crystalfly");
        await test.WriteAsync(shared, "user1.dat", "shared-before");
        await test.WriteAsync(shared, "Player.log", "shared-log");
        var service = new LocalLowIsolationService(shared, storage);

        var session = await service.SwitchInAsync("steel-soul");
        await File.WriteAllTextAsync(Path.Combine(shared, "user1.dat"), "instance-after");
        await test.WriteAsync(shared, "settings.json", "instance-settings");
        await test.WriteAsync(shared, "output_log.txt", "instance-log");

        var result = await service.SwitchOutAsync(session.Id);

        Assert.Equal(TransactionState.Committed, result.State);
        var instance = service.GetInstanceLocalLowPath("steel-soul");
        Assert.Equal("instance-after", await File.ReadAllTextAsync(Path.Combine(instance, "user1.dat")));
        Assert.Equal("instance-settings", await File.ReadAllTextAsync(Path.Combine(instance, "settings.json")));
        Assert.False(File.Exists(Path.Combine(instance, "output_log.txt")));
        Assert.Equal("shared-before", await File.ReadAllTextAsync(Path.Combine(shared, "user1.dat")));
        Assert.Equal("shared-log", await File.ReadAllTextAsync(Path.Combine(shared, "Player.log")));
    }

    [Fact]
    public async Task Baseline_refresh_preserves_an_active_session_until_game_exit_is_confirmed()
    {
        using var test = new TestDirectory();
        var shared = test.CreateDirectory("local-low", "Hollow Knight");
        var storage = test.CreateDirectory("version", ".crystalfly");
        await test.WriteAsync(shared, "user1.dat", "shared-save");
        var service = new LocalLowIsolationService(shared, storage);
        _ = await service.SwitchInAsync("practice");
        await File.WriteAllTextAsync(Path.Combine(shared, "user1.dat"), "active-save");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.InitializeBaselinesAsync(["practice"]));

        Assert.Equal("active-save", await File.ReadAllTextAsync(Path.Combine(shared, "user1.dat")));
        Assert.Equal("shared-save", await File.ReadAllTextAsync(Path.Combine(
            service.GetInstanceLocalLowPath("practice"),
            "user1.dat")));

        await service.InitializeBaselinesAsync(["practice"], allowActiveSessionCompletion: true);

        Assert.Equal("shared-save", await File.ReadAllTextAsync(Path.Combine(shared, "user1.dat")));
        Assert.Equal("active-save", await File.ReadAllTextAsync(Path.Combine(
            service.GetInstanceLocalLowPath("practice"),
            "user1.dat")));
    }

    [Fact]
    public async Task Recovery_rolls_back_when_failure_occurs_after_shared_directory_is_preserved()
    {
        using var test = new TestDirectory();
        var shared = test.CreateDirectory("local-low", "Hollow Knight");
        var storage = test.CreateDirectory("version", ".crystalfly");
        await test.WriteAsync(shared, "user1.dat", "shared-save");
        var failing = new LocalLowIsolationService(
            shared,
            storage,
            checkpoint => ThrowAt(checkpoint, LocalLowCheckpoint.SharedPreserved));

        await Assert.ThrowsAsync<InjectedFailureException>(() => failing.SwitchInAsync("practice"));

        var recovered = Assert.Single(await new LocalLowIsolationService(shared, storage).RecoverPendingAsync());

        Assert.Equal(TransactionState.RolledBack, recovered.State);
        Assert.Equal("shared-save", await File.ReadAllTextAsync(Path.Combine(shared, "user1.dat")));
        Assert.Empty(Directory.EnumerateDirectories(
            Path.Combine(storage, "local-low", "transactions")));
    }

    [Fact]
    public async Task Recovery_commits_verified_first_takeover_backup_after_interrupted_metadata_write()
    {
        using var test = new TestDirectory();
        var shared = test.CreateDirectory("local-low", "Hollow Knight");
        var storage = test.CreateDirectory("version", ".crystalfly");
        await test.WriteAsync(shared, "user1.dat", "shared-save");
        await test.WriteAsync(shared, "output_log.txt", "shared-log");
        var failing = new LocalLowIsolationService(
            shared,
            storage,
            checkpoint => ThrowAt(checkpoint, LocalLowCheckpoint.TakeoverBackupCommitted));

        await Assert.ThrowsAsync<InjectedFailureException>(() => failing.SwitchInAsync("practice"));

        var recoveredService = new LocalLowIsolationService(shared, storage);
        Assert.Empty(await recoveredService.RecoverPendingAsync());
        var session = await recoveredService.SwitchInAsync("practice");

        Assert.Equal("shared-log", await File.ReadAllTextAsync(Path.Combine(
            recoveredService.SharedBackupPath,
            "output_log.txt")));
        await recoveredService.SwitchOutAsync(session.Id);
    }

    [Fact]
    public async Task Recovery_finishes_write_back_when_failure_occurs_after_instance_capture()
    {
        using var test = new TestDirectory();
        var shared = test.CreateDirectory("local-low", "Hollow Knight");
        var storage = test.CreateDirectory("version", ".crystalfly");
        await test.WriteAsync(shared, "user1.dat", "shared-save");
        var observer = new ConditionalObserver();
        var failing = new LocalLowIsolationService(shared, storage, observer.Reached);
        var session = await failing.SwitchInAsync("practice");
        await File.WriteAllTextAsync(Path.Combine(shared, "user1.dat"), "captured-save");
        observer.FailurePoint = LocalLowCheckpoint.InstanceCaptured;

        await Assert.ThrowsAsync<InjectedFailureException>(() => failing.SwitchOutAsync(session.Id));

        var recovered = Assert.Single(await new LocalLowIsolationService(shared, storage)
            .RecoverPendingAsync(allowActiveSessionCompletion: true));

        Assert.Equal(TransactionState.Committed, recovered.State);
        Assert.Equal("captured-save", await File.ReadAllTextAsync(Path.Combine(
            failing.GetInstanceLocalLowPath("practice"), "user1.dat")));
        Assert.Equal("shared-save", await File.ReadAllTextAsync(Path.Combine(shared, "user1.dat")));
    }

    [Fact]
    public async Task Recovery_commits_verified_final_state_when_previous_was_cleaned_before_completed_write()
    {
        using var test = new TestDirectory();
        var shared = test.CreateDirectory("local-low", "Hollow Knight");
        var storage = test.CreateDirectory("version", ".crystalfly");
        await test.WriteAsync(shared, "user1.dat", "shared-save");
        var observer = new ConditionalObserver();
        var failing = new LocalLowIsolationService(shared, storage, observer.Reached);
        var session = await failing.SwitchInAsync("practice");
        await File.WriteAllTextAsync(Path.Combine(shared, "user1.dat"), "captured-save");
        observer.FailurePoint = LocalLowCheckpoint.SharedRestored;

        await Assert.ThrowsAsync<InjectedFailureException>(() => failing.SwitchOutAsync(session.Id));

        var journalPath = Assert.Single(Directory.EnumerateFiles(
            Path.Combine(storage, "local-low", "transactions"),
            "journal.json",
            SearchOption.AllDirectories));
        var journal = await AtomicJsonStore.ReadAsync<LocalLowSessionJournal>(journalPath);
        await AtomicJsonStore.WriteAsync(
            journalPath,
            journal with { Phase = LocalLowSessionPhase.SharedRestored });
        Directory.Delete(journal.InstancePreviousPath, recursive: true);

        var recovered = Assert.Single(await new LocalLowIsolationService(shared, storage)
            .RecoverPendingAsync(allowActiveSessionCompletion: true));

        Assert.Equal(TransactionState.Committed, recovered.State);
        Assert.Equal("captured-save", await File.ReadAllTextAsync(Path.Combine(
            failing.GetInstanceLocalLowPath("practice"), "user1.dat")));
        Assert.Equal("shared-save", await File.ReadAllTextAsync(Path.Combine(shared, "user1.dat")));
        Assert.False(File.Exists(journalPath));
    }

    [Fact]
    public async Task Recovery_marks_needs_attention_when_preserved_shared_data_hash_changed()
    {
        using var test = new TestDirectory();
        var shared = test.CreateDirectory("local-low", "Hollow Knight");
        var storage = test.CreateDirectory("version", ".crystalfly");
        await test.WriteAsync(shared, "user1.dat", "shared-save");
        var failing = new LocalLowIsolationService(
            shared,
            storage,
            checkpoint => ThrowAt(checkpoint, LocalLowCheckpoint.SharedPreserved));
        await Assert.ThrowsAsync<InjectedFailureException>(() => failing.SwitchInAsync("practice"));
        var journalPath = Assert.Single(Directory.EnumerateFiles(
            Path.Combine(storage, "local-low", "transactions"),
            "journal.json",
            SearchOption.AllDirectories));
        var journal = await AtomicJsonStore.ReadAsync<LocalLowSessionJournal>(journalPath);
        await File.WriteAllTextAsync(Path.Combine(journal.SharedPreservedPath, "user1.dat"), "tampered");

        var recovered = Assert.Single(await new LocalLowIsolationService(shared, storage).RecoverPendingAsync());

        Assert.Equal(TransactionState.NeedsAttention, recovered.State);
        Assert.True(File.Exists(journalPath));
        Assert.False(Directory.Exists(shared));
        Assert.Equal("tampered", await File.ReadAllTextAsync(
            Path.Combine(journal.SharedPreservedPath, "user1.dat")));
    }

    private static void ThrowAt(LocalLowCheckpoint actual, LocalLowCheckpoint expected)
    {
        if (actual == expected)
        {
            throw new InjectedFailureException();
        }
    }

    private sealed class ConditionalObserver
    {
        public LocalLowCheckpoint? FailurePoint { get; set; }

        public void Reached(LocalLowCheckpoint checkpoint)
        {
            if (checkpoint == FailurePoint)
            {
                throw new InjectedFailureException();
            }
        }
    }

    private sealed class InjectedFailureException : Exception
    {
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

using Crystalfly.Core.LocalLow;
using Crystalfly.Core.Runtime;

namespace Crystalfly.Core.Tests.Runtime;

public sealed class HollowKnightRuntimeTests
{
    [Fact]
    public void Guard_rejects_launch_when_any_hollow_knight_process_is_running()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            HollowKnightProcessGuard.Acquire(UniqueMutexName(), new StubProcessProbe(isRunning: true)));

        Assert.Contains("hollow_knight", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Named_mutex_prevents_two_runtime_guards()
    {
        var mutexName = UniqueMutexName();
        using var first = HollowKnightProcessGuard.Acquire(
            mutexName,
            new StubProcessProbe(isRunning: false));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            HollowKnightProcessGuard.Acquire(mutexName, new StubProcessProbe(isRunning: false)));

        Assert.Contains("already", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Runtime_session_switches_in_and_dispose_writes_back_then_restores_shared_data()
    {
        using var test = new TestDirectory();
        var shared = test.CreateDirectory("local-low", "Hollow Knight");
        var storage = test.CreateDirectory("version", ".crystalfly");
        await File.WriteAllTextAsync(Path.Combine(shared, "user1.dat"), "shared-save");
        var isolation = new LocalLowIsolationService(shared, storage);

        await using (var runtime = await InstanceRuntimeSession.StartAsync(
            isolation,
            "practice",
            UniqueMutexName(),
            new StubProcessProbe(isRunning: false)))
        {
            await File.WriteAllTextAsync(Path.Combine(shared, "user1.dat"), "instance-save");
        }

        Assert.Equal("shared-save", await File.ReadAllTextAsync(Path.Combine(shared, "user1.dat")));
        Assert.Equal("instance-save", await File.ReadAllTextAsync(Path.Combine(
            isolation.GetInstanceLocalLowPath("practice"),
            "user1.dat")));
    }

    [Fact]
    public async Task Complete_blocks_while_game_process_is_running_and_can_retry_after_exit()
    {
        using var test = new TestDirectory();
        var shared = test.CreateDirectory("local-low", "Hollow Knight");
        var storage = test.CreateDirectory("version", ".crystalfly");
        await File.WriteAllTextAsync(Path.Combine(shared, "user1.dat"), "shared-save");
        var isolation = new LocalLowIsolationService(shared, storage);
        var probe = new MutableProcessProbe();
        var runtime = await InstanceRuntimeSession.StartAsync(
            isolation,
            "practice",
            UniqueMutexName(),
            probe);
        await File.WriteAllTextAsync(Path.Combine(shared, "user1.dat"), "instance-save");
        probe.IsGameRunning = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.CompleteAsync());
        Assert.Equal("instance-save", await File.ReadAllTextAsync(Path.Combine(shared, "user1.dat")));

        probe.IsGameRunning = false;
        await runtime.CompleteAsync();
        await runtime.DisposeAsync();
        Assert.Equal("shared-save", await File.ReadAllTextAsync(Path.Combine(shared, "user1.dat")));
    }

    private static string UniqueMutexName() => $"Crystalfly.Tests.{Guid.NewGuid():N}";

    private sealed class StubProcessProbe(bool isRunning) : IHollowKnightProcessProbe
    {
        public bool IsRunning() => isRunning;
    }

    private sealed class MutableProcessProbe : IHollowKnightProcessProbe
    {
        public bool IsGameRunning { get; set; }

        public bool IsRunning() => IsGameRunning;
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

        public void Dispose()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}

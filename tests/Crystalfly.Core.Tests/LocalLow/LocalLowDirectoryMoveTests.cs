using Crystalfly.Core.LocalLow;

namespace Crystalfly.Core.Tests.LocalLow;

public sealed class LocalLowDirectoryMoveTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Crystalfly.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Move_waits_for_a_transient_reader_without_losing_data()
    {
        var (source, destination, file) = CreateSource();
        using var reader = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var release = ReleaseReaderAsync(reader);
        try
        {
            await LocalLowDirectory.MoveAsync(source, destination, CancellationToken.None);
            Assert.False(Directory.Exists(source));
            Assert.Equal("save", await File.ReadAllTextAsync(Path.Combine(destination, "data", "user1.dat")));
        }
        finally
        {
            await release;
        }
    }

    [Fact]
    public async Task Move_cancellation_while_waiting_preserves_the_source()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (source, destination, file) = CreateSource();
        using var reader = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            LocalLowDirectory.MoveAsync(source, destination, cancellation.Token));

        Assert.Equal("save", await File.ReadAllTextAsync(file));
        Assert.False(Directory.Exists(destination));
    }

    [Fact]
    public async Task Move_does_not_move_an_already_cancelled_request()
    {
        var (source, destination, file) = CreateSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            LocalLowDirectory.MoveAsync(source, destination, new CancellationToken(canceled: true)));
        Assert.True(File.Exists(file));
        Assert.False(Directory.Exists(destination));
    }

    [Fact]
    public async Task Move_stops_retrying_a_persistent_lock_and_preserves_the_source()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (source, destination, file) = CreateSource();
        using var reader = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        await Assert.ThrowsAsync<IOException>(() =>
            LocalLowDirectory.MoveAsync(source, destination, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Equal("save", await File.ReadAllTextAsync(file));
        Assert.False(Directory.Exists(destination));
    }

    [Fact]
    public async Task Move_never_overwrites_an_existing_destination()
    {
        var (source, destination, file) = CreateSource();
        Directory.CreateDirectory(destination);
        await File.WriteAllTextAsync(Path.Combine(destination, "keep.txt"), "keep");

        await Assert.ThrowsAsync<IOException>(() => LocalLowDirectory.MoveAsync(source, destination, CancellationToken.None));

        Assert.Equal("save", await File.ReadAllTextAsync(file));
        Assert.Equal("keep", await File.ReadAllTextAsync(Path.Combine(destination, "keep.txt")));
    }

    [Fact]
    public async Task Move_reports_a_missing_source()
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => LocalLowDirectory.MoveAsync(
            Path.Combine(root, "missing"), Path.Combine(root, "destination"), CancellationToken.None));
    }

    [Fact]
    public async Task Baseline_takeover_commits_after_a_staging_reader_releases_the_file()
    {
        var shared = Path.Combine(root, "shared");
        Directory.CreateDirectory(shared);
        await File.WriteAllTextAsync(Path.Combine(shared, "user1.dat"), "save");
        var storage = Path.Combine(root, "storage");
        FileStream? reader = null;
        Task release = Task.CompletedTask;
        var service = new LocalLowIsolationService(shared, storage, checkpoint =>
        {
            if (checkpoint != LocalLowCheckpoint.TakeoverBackupStaged) return;
            reader = File.Open(Path.Combine(storage, "local-low", "shared-backup.staging", "user1.dat"),
                FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            release = ReleaseReaderAsync(reader);
        });
        try
        {
            await service.InitializeBaselinesAsync(["practice"]);
            Assert.Equal("save", await File.ReadAllTextAsync(Path.Combine(service.SharedBackupPath, "user1.dat")));
            Assert.Equal("save", await File.ReadAllTextAsync(Path.Combine(service.GetInstanceLocalLowPath("practice"), "user1.dat")));
            Assert.Equal("save", await File.ReadAllTextAsync(Path.Combine(shared, "user1.dat")));
        }
        finally
        {
            await release;
            reader?.Dispose();
        }
    }

    private (string Source, string Destination, string File) CreateSource()
    {
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(Path.Combine(source, "data"));
        var file = Path.Combine(source, "data", "user1.dat");
        File.WriteAllText(file, "save");
        return (source, Path.Combine(root, "destination"), file);
    }

    private static async Task ReleaseReaderAsync(FileStream reader)
    {
        await Task.Delay(150);
        await reader.DisposeAsync();
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}

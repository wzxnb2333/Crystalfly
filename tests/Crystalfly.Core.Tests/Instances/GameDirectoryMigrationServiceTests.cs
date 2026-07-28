using Crystalfly.Core.Instances;

namespace Crystalfly.Core.Tests.Instances;

public sealed class GameDirectoryMigrationServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"crystalfly-game-migrate-{Guid.NewGuid():N}");

    [Fact]
    public async Task Migrate_same_volume_uses_atomic_directory_move()
    {
        var source = CreateGame(Path.Combine(root, "source"));
        var destination = Path.Combine(root, "target", "source");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var moves = new List<(string Source, string Destination)>();
        var service = CreateService(
            sameVolume: true,
            move: (from, to) =>
            {
                moves.Add((from, to));
                Directory.Move(from, to);
            });

        var result = await service.MigrateAsync(source, destination);

        Assert.True(result.SourceCleanupCompleted);
        Assert.Equal((source, destination), Assert.Single(moves));
        Assert.False(Directory.Exists(source));
        Assert.True(File.Exists(Path.Combine(destination, "hollow_knight.exe")));
    }

    [Fact]
    public async Task Migrate_cross_volume_copies_verifies_publishes_and_deletes_source()
    {
        var source = CreateGame(Path.Combine(root, "source"));
        File.WriteAllText(Path.Combine(source, "custom.png"), "texture");
        var destination = Path.Combine(root, "target", "source");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var service = CreateService(sameVolume: false);

        var result = await service.MigrateAsync(source, destination);

        Assert.True(result.SourceCleanupCompleted);
        Assert.False(Directory.Exists(source));
        Assert.Equal("texture", File.ReadAllText(Path.Combine(destination, "custom.png")));
        Assert.False(Directory.Exists(Path.Combine(root, "target", ".crystalfly", "staging", "migration-fixed")));
    }

    [Fact]
    public async Task Migrate_cross_volume_preserves_both_directories_when_source_cleanup_fails()
    {
        var source = CreateGame(Path.Combine(root, "source"));
        var destination = Path.Combine(root, "target", "source");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var service = CreateService(
            sameVolume: false,
            delete: _ => throw new IOException("locked"));

        var result = await service.MigrateAsync(source, destination);

        Assert.False(result.SourceCleanupCompleted);
        Assert.Contains("locked", result.SourceCleanupError, StringComparison.Ordinal);
        Assert.True(Directory.Exists(source));
        Assert.True(Directory.Exists(destination));
    }

    [Fact]
    public async Task Migrate_rejects_conflicting_nested_and_reparse_paths()
    {
        var source = CreateGame(Path.Combine(root, "source"));
        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService(true).MigrateAsync(source, Path.Combine(source, "nested")));

        var conflict = Directory.CreateDirectory(Path.Combine(root, "conflict")).FullName;
        await Assert.ThrowsAsync<IOException>(
            () => CreateService(true).MigrateAsync(source, conflict));

        var linkTarget = Directory.CreateDirectory(Path.Combine(root, "link-target")).FullName;
        var link = Path.Combine(root, "link");
        Directory.CreateSymbolicLink(link, linkTarget);
        await Assert.ThrowsAsync<IOException>(
            () => CreateService(true).MigrateAsync(source, Path.Combine(link, "source")));
    }

    [Fact]
    public async Task Migrate_cross_volume_cancellation_cleans_staging_and_keeps_source()
    {
        var source = CreateGame(Path.Combine(root, "source"));
        var destination = Path.Combine(root, "target", "source");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateService(false).MigrateAsync(source, destination, cancellation.Token));

        Assert.True(Directory.Exists(source));
        Assert.False(Directory.Exists(destination));
        Assert.False(Directory.Exists(Path.Combine(root, "target", ".crystalfly", "staging", "migration-fixed")));
    }

    private GameDirectoryMigrationService CreateService(
        bool sameVolume,
        Action<string, string>? move = null,
        Action<string>? delete = null) =>
        new(
            (_, _) => sameVolume,
            () => "fixed",
            move ?? Directory.Move,
            delete ?? (path => Directory.Delete(path, recursive: true)));

    private static string CreateGame(string path)
    {
        Directory.CreateDirectory(Path.Combine(path, "hollow_knight_Data"));
        File.WriteAllText(Path.Combine(path, "hollow_knight.exe"), "exe");
        File.WriteAllText(Path.Combine(path, "hollow_knight_Data", "globalgamemanagers"), "data");
        return Path.GetFullPath(path);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

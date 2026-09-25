using System.IO.Compression;
using System.Security.Cryptography;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Loaders;
using Crystalfly.Core.Models;
using Crystalfly.Core.Runtime;
using Crystalfly.Core.Serialization;
using Crystalfly.Core.Snapshots;

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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Migrate_preserves_managed_metadata_saves_and_snapshots(bool sameVolume)
    {
        var source = CreateGame(Path.Combine(root, "source-versions", "game"));
        var destination = Path.Combine(root, "target-versions", "game");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var record = new InstanceRecord
        {
            Id = "managed-id", Name = "Practice", RootPath = source, BuildId = "verified-build",
            LoaderId = "ModdingApi", CreatedAt = DateTimeOffset.UtcNow
        };
        await InstanceSidecar.SaveAsync(record);
        var sourceState = Path.GetDirectoryName(InstanceSidecar.GetMetadataPath(source, record.Id))!;
        Directory.CreateDirectory(Path.Combine(sourceState, "local-low"));
        await File.WriteAllTextAsync(Path.Combine(sourceState, "local-low", "user1.dat"), "private save");
        var snapshot = await CreateSnapshotService(Path.GetDirectoryName(source)!).CreateAsync(record.Id, "Before migration");
        var snapshotMetadata = Path.Combine(sourceState, "snapshots", snapshot.Id, "snapshot.json");
        File.Copy(snapshotMetadata, snapshotMetadata + ".bak");

        var result = await CreateService(sameVolume).MigrateAsync(source, destination);

        Assert.True(result.SourceCleanupCompleted);
        Assert.Equal(record with { RootPath = destination }, await InstanceSidecar.LoadAsync(destination));
        var targetState = Path.GetDirectoryName(InstanceSidecar.GetMetadataPath(destination, record.Id))!;
        Assert.Equal("private save", await File.ReadAllTextAsync(Path.Combine(targetState, "local-low", "user1.dat")));
        var snapshots = CreateSnapshotService(Path.GetDirectoryName(destination)!);
        Assert.Equal(snapshot.Id, Assert.Single(await snapshots.ListAsync(record.Id)).Id);
        await File.WriteAllTextAsync(Path.Combine(targetState, "snapshots", snapshot.Id, "snapshot.json"), "broken");
        await File.WriteAllTextAsync(Path.Combine(targetState, "local-low", "user1.dat"), "changed");
        await snapshots.RestoreAsync(record.Id, snapshot.Id);
        Assert.Equal("private save", await File.ReadAllTextAsync(Path.Combine(targetState, "local-low", "user1.dat")));
        Assert.False(Directory.Exists(source));
        Assert.False(Directory.Exists(sourceState));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Migrate_preserves_loader_uninstall_and_backup_receipt_recovery(bool sameVolume)
    {
        var source = CreateGame(Path.Combine(root, "source-versions", "game"));
        var destination = Path.Combine(root, "target-versions", "game");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var record = new InstanceRecord
        {
            Id = "managed-id", Name = "Practice", RootPath = source, BuildId = "verified-build",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await InstanceSidecar.SaveAsync(record);
        var assembly = Path.Combine("hollow_knight_Data", "Managed", "Assembly-CSharp.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(source, assembly))!);
        await File.WriteAllTextAsync(Path.Combine(source, assembly), "vanilla");
        var package = Path.Combine(root, "loader.zip");
        using (var zip = ZipFile.Open(package, ZipArchiveMode.Create))
        {
            foreach (var (name, contents) in new[] { ("Assembly-CSharp.dll", "patched"), ("MMHOOK_Assembly-CSharp.dll", "api") })
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open());
                writer.Write(contents);
            }
        }
        var manifest = new LoaderManifest
        {
            Id = "modding-api-77", Name = "Modding API", Version = "1.0",
            DownloadUrl = "https://example.invalid/loader.zip", SizeBytes = new FileInfo(package).Length,
            Sha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(package)))
        };
        await CreateLoaderManager(source, record.Id).InstallFromFileAsync(manifest, package);
        var receipt = Path.Combine(Path.GetDirectoryName(InstanceSidecar.GetMetadataPath(source, record.Id))!, "loader.json");
        File.Copy(receipt, receipt + ".bak", overwrite: true);

        await CreateService(sameVolume).MigrateAsync(source, destination);

        var manager = CreateLoaderManager(destination, record.Id);
        var migratedReceipt = (await manager.GetReceiptAsync())!;
        Assert.True(Directory.Exists(migratedReceipt.BackupRoot));
        var destinationReceipt = Path.Combine(Path.GetDirectoryName(InstanceSidecar.GetMetadataPath(destination, record.Id))!, "loader.json");
        File.Delete(destinationReceipt);
        Assert.NotNull(await manager.GetReceiptAsync());
        await File.WriteAllTextAsync(destinationReceipt, "broken");
        await manager.UninstallAsync();
        Assert.Equal(LoaderState.Vanilla, await manager.GetStateAsync());
        Assert.Equal("vanilla", await File.ReadAllTextAsync(Path.Combine(destination, assembly)));
        Assert.False(File.Exists(Path.Combine(destination, "hollow_knight_Data", "Managed", "MMHOOK_Assembly-CSharp.dll")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Migrate_rejects_existing_destination_state_without_moving_the_game(bool sameVolume)
    {
        var source = CreateGame(Path.Combine(root, "source-versions", "game"));
        var destination = Path.Combine(root, "target-versions", "game");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var record = new InstanceRecord
        {
            Id = "managed-id", Name = "Practice", RootPath = source, BuildId = "unknown",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await InstanceSidecar.SaveAsync(record);
        var targetState = Path.GetDirectoryName(InstanceSidecar.GetMetadataPath(destination, record.Id))!;
        Directory.CreateDirectory(targetState);
        await File.WriteAllTextAsync(Path.Combine(targetState, "keep.txt"), "existing state");

        await Assert.ThrowsAsync<IOException>(() => CreateService(sameVolume).MigrateAsync(source, destination));

        Assert.True(Directory.Exists(source));
        Assert.False(Directory.Exists(destination));
        Assert.Equal(record, await InstanceSidecar.LoadAsync(source));
        Assert.Equal("existing state", await File.ReadAllTextAsync(Path.Combine(targetState, "keep.txt")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Migrate_keeps_source_state_when_game_publication_fails(bool sameVolume)
    {
        var source = CreateGame(Path.Combine(root, "source-versions", "game"));
        var destination = Path.Combine(root, "target-versions", "game");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var record = new InstanceRecord
        {
            Id = "managed-id", Name = "Practice", RootPath = source, BuildId = "unknown",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await InstanceSidecar.SaveAsync(record);
        var service = CreateService(sameVolume, move: (from, to) =>
        {
            if (to == destination)
            {
                throw new IOException("publication failed");
            }
            Directory.Move(from, to);
        });

        await Assert.ThrowsAsync<IOException>(() => service.MigrateAsync(source, destination));

        Assert.Equal(record, await InstanceSidecar.LoadAsync(source));
        Assert.True(Directory.Exists(source));
        Assert.False(Directory.Exists(destination));
        Assert.False(Directory.Exists(Path.GetDirectoryName(InstanceSidecar.GetMetadataPath(destination, record.Id))));
    }

    [Fact]
    public async Task Migrate_cleanup_failure_preserves_state_at_both_locations()
    {
        var source = CreateGame(Path.Combine(root, "source-versions", "game"));
        var destination = Path.Combine(root, "target-versions", "game");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var record = new InstanceRecord
        {
            Id = "managed-id", Name = "Practice", RootPath = source, BuildId = "unknown",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await InstanceSidecar.SaveAsync(record);
        var service = CreateService(false, delete: _ => throw new IOException("source is locked"));

        var result = await service.MigrateAsync(source, destination);

        Assert.False(result.SourceCleanupCompleted);
        Assert.Equal(record, await InstanceSidecar.LoadAsync(source));
        Assert.Equal(record with { RootPath = destination }, await InstanceSidecar.LoadAsync(destination));
        Assert.True(Directory.Exists(source));
        Assert.True(Directory.Exists(destination));
    }

    [Fact]
    public async Task Migrate_rejects_snapshot_paths_outside_the_instance_without_moving_source()
    {
        var source = CreateGame(Path.Combine(root, "source-versions", "game"));
        var destination = Path.Combine(root, "target-versions", "game");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var record = new InstanceRecord
        {
            Id = "managed-id", Name = "Practice", RootPath = source, BuildId = "unknown",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await InstanceSidecar.SaveAsync(record);
        var sourceState = Path.GetDirectoryName(InstanceSidecar.GetMetadataPath(source, record.Id))!;
        Directory.CreateDirectory(Path.Combine(sourceState, "local-low"));
        await File.WriteAllTextAsync(Path.Combine(sourceState, "local-low", "user1.dat"), "save");
        var snapshot = await CreateSnapshotService(Path.GetDirectoryName(source)!).CreateAsync(record.Id, "Snapshot");
        await AtomicJsonStore.WriteAsync(Path.Combine(sourceState, "snapshots", snapshot.Id, "snapshot.json"),
            snapshot with { SourcePath = Path.Combine(root, "external") });

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateService(true).MigrateAsync(source, destination));

        Assert.True(Directory.Exists(source));
        Assert.True(Directory.Exists(sourceState));
        Assert.False(Directory.Exists(destination));
        Assert.Equal(record, await InstanceSidecar.LoadAsync(source));
    }

    [Fact]
    public async Task Migrate_preserves_external_loader_receipts_without_managed_backups()
    {
        var source = CreateGame(Path.Combine(root, "source-versions", "game"));
        var destination = Path.Combine(root, "target-versions", "game");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var record = new InstanceRecord
        {
            Id = "managed-id", Name = "Practice", RootPath = source, BuildId = "unknown",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await InstanceSidecar.SaveAsync(record);
        var sourceState = Path.GetDirectoryName(InstanceSidecar.GetMetadataPath(source, record.Id))!;
        var receipt = new InstalledPackageReceipt
        {
            PackageId = "external-api", LoaderState = LoaderState.ModdingApi, IsVerified = false
        };
        await AtomicJsonStore.WriteAsync(Path.Combine(sourceState, "loader.json"), receipt);

        await CreateService(true).MigrateAsync(source, destination);

        var migrated = (await CreateLoaderManager(destination, record.Id).GetReceiptAsync())!;
        Assert.Equal(receipt.PackageId, migrated.PackageId);
        Assert.False(migrated.IsVerified);
        Assert.Empty(migrated.BackupRoot);
    }

    private static NamedSnapshotService CreateSnapshotService(string versionRoot) => new(
        Path.Combine(versionRoot, ".crystalfly"), $"Crystalfly.Tests.{Guid.NewGuid():N}", new IdleProcessProbe());

    private static LoaderManager CreateLoaderManager(string gameRoot, string id) => new(
        gameRoot, Path.Combine(Path.GetDirectoryName(gameRoot)!, ".crystalfly", "transactions"),
        Path.Combine(Path.GetDirectoryName(InstanceSidecar.GetMetadataPath(gameRoot, id))!, "loader.json"));

    private sealed class IdleProcessProbe : IHollowKnightProcessProbe
    {
        public bool IsRunning() => false;
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

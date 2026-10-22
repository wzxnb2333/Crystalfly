using System.Text.Json;
using Crystalfly.Core.Catalog;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;
using Crystalfly.Core.Runtime;
using Crystalfly.Core.Snapshots;

namespace Crystalfly.Core.Tests.Instances;

public sealed class MultiGameTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "crystalfly-games-" + Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task Legacy_settings_are_backed_up_without_moving_directories_or_losing_preferences()
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "settings.json");
        var original = "{\"versionRoot\":\"" + root.Replace("\\", "\\\\") + "\",\"currentInstanceId\":\"original\",\"favoriteInstanceIds\":[\"original\"],\"errorReportingEnabled\":false}";
        await File.WriteAllTextAsync(path, original);
        var settings = await CrystalflySettingsStore.LoadAsync(path);
        Assert.Equal(original, await File.ReadAllTextAsync(path + ".pre-multigame.bak"));
        Assert.Equal(GameId.HollowKnight, settings.SelectedGameId);
        Assert.Equal("original", Assert.Single(settings.GameWorkspaces).CurrentInstanceId);
        Assert.False(settings.ErrorReportingEnabled);
        var switched = GameWorkspaceMigration.Select(settings, GameId.Silksong) with { VersionRoot = root + "-silksong", CurrentInstanceId = "silk" };
        var back = GameWorkspaceMigration.Select(switched, GameId.HollowKnight);
        Assert.Equal(root, back.VersionRoot);
        Assert.Equal("original", back.CurrentInstanceId);
        Assert.Equal("silk", GameWorkspaceMigration.Select(back, GameId.Silksong).CurrentInstanceId);
        Assert.Equal(["original"], back.FavoriteInstanceIds);
    }
    [Fact]
    public async Task Same_version_and_same_hashes_do_not_match_another_game()
    {
        var data = Path.Combine(root, GameDefinition.Silksong.DataDirectoryName);
        Directory.CreateDirectory(data);
        await File.WriteAllTextAsync(Path.Combine(root, GameDefinition.Silksong.ExecutableName), "exe");
        await File.WriteAllTextAsync(Path.Combine(data, "globalgamemanagers"), "game");
        var fingerprint = await BuildFingerprintService.CalculateAsync(root);
        Assert.Equal(GameId.Silksong, fingerprint.GameId);
        var first = new GameBuild { Id = "1.0.0", DisplayVersion = "1.0.0", ManifestId = "1", DepotId = 367521,
            ExecutableSha256 = fingerprint.ExecutableSha256, GlobalGameManagersSha256 = fingerprint.GlobalGameManagersSha256 };
        var second = first with { GameId = GameId.Silksong, ManifestId = "2", DepotId = 1030301 };
        var catalog = CatalogMerger.Merge(new() { Builds = [first], Channels = [new() { Name = "public", BuildId = first.Id }] }, null,
            new() { Builds = [second], Channels = [new() { GameId = GameId.Silksong, Name = "public", BuildId = second.Id }] });
        Assert.Equal(second, BuildFingerprintService.FindBuild(catalog.Builds, fingerprint));
        Assert.Equal(second, CatalogResolver.ResolveBuild(catalog, "public", GameId.Silksong));
        Assert.Equal(first, CatalogResolver.ResolveBuild(catalog, "public"));
        CatalogProvider.ValidateResolved(catalog);
        Assert.True(GameDirectoryIntegrityChecker.Inspect(root, gameId: GameId.Silksong).IsValid);
        Assert.False(GameDirectoryIntegrityChecker.Inspect(root, gameId: GameId.HollowKnight).IsValid);
    }
    [Fact]
    public void Legacy_instance_defaults_to_hollow_knight_and_save_roots_are_separate()
    {
        var record = JsonSerializer.Deserialize<InstanceRecord>("{\"id\":\"old\",\"name\":\"Old\",\"rootPath\":\"game\",\"buildId\":\"1.5.78\"}", CrystalflyJson.Options)!;
        Assert.Equal(GameId.HollowKnight, record.GameId);
        var paths = new CrystalflyPaths(root, false);
        Assert.NotEqual(paths.GetVersionDataRoot(root, GameId.HollowKnight), paths.GetVersionDataRoot(root, GameId.Silksong));
        Assert.NotEqual(GameDefinition.HollowKnight.GetLocalLowPath(), GameDefinition.Silksong.GetLocalLowPath());
        Assert.Equal("Hollow Knight Silksong", Path.GetFileName(GameDefinition.Silksong.GetLocalLowPath()));
    }

    [Fact]
    public async Task Silksong_clone_copies_mod_files_current_save_and_independent_snapshot_paths()
    {
        var source = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
        var plugins = Directory.CreateDirectory(Path.Combine(source, "BepInEx/plugins/Author-Mod")).FullName;
        await File.WriteAllTextAsync(Path.Combine(plugins, "mod.txt"), "payload");
        var record = new InstanceRecord { Id = "old", Name = "source", RootPath = source, BuildId = "silksong-1.0.30000", GameId = GameId.Silksong };
        await InstanceSidecar.SaveAsync(record);
        var storage = new CrystalflyPaths(root, false).GetVersionDataRoot(root, GameId.Silksong);
        var saves = Directory.CreateDirectory(Path.Combine(storage, "instances/old/local-low")).FullName;
        await File.WriteAllTextAsync(Path.Combine(saves, "user1.dat"), "source save");
        var snapshots = new NamedSnapshotService(storage, "Crystalfly.CloneTest." + Guid.NewGuid().ToString("N"), new IdleProbe(), GameId.Silksong);
        var snapshot = await snapshots.CreateAsync("old", "Before boss");
        var clone = await InstanceCloneService.CloneAsync(source, "copy", "new");
        Assert.Equal(GameId.Silksong, clone.GameId);
        Assert.Equal("payload", await File.ReadAllTextAsync(Path.Combine(clone.RootPath, "BepInEx/plugins/Author-Mod/mod.txt")));
        var clonedSaves = Path.Combine(storage, "instances/new/local-low");
        Assert.Equal("source save", await File.ReadAllTextAsync(Path.Combine(clonedSaves, "user1.dat")));
        var copied = Assert.Single(await snapshots.ListAsync("new"));
        Assert.Equal(snapshot.Name, copied.Name);
        Assert.Equal(Path.GetFullPath(clonedSaves), copied.SourcePath);
        await File.WriteAllTextAsync(Path.Combine(clonedSaves, "user1.dat"), "changed clone");
        await snapshots.RestoreAsync("new", copied.Id);
        Assert.Equal("source save", await File.ReadAllTextAsync(Path.Combine(clonedSaves, "user1.dat")));
        Assert.Equal("source save", await File.ReadAllTextAsync(Path.Combine(saves, "user1.dat")));
    }

    [Fact]
    public async Task Silksong_delete_and_cleanup_recovery_remove_its_save_state_only()
    {
        var source = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
        var record = new InstanceRecord { Id = "silk", Name = "source", RootPath = source, BuildId = "test", GameId = GameId.Silksong };
        await InstanceSidecar.SaveAsync(record);
        var saves = Directory.CreateDirectory(Path.Combine(root, ".crystalfly/games/silksong/instances/silk/local-low")).FullName;
        await File.WriteAllTextAsync(Path.Combine(saves, "user1.dat"), "silk");
        var hk = Directory.CreateDirectory(Path.Combine(root, ".crystalfly/instances/hk/local-low")).FullName;
        await File.WriteAllTextAsync(Path.Combine(hk, "user1.dat"), "keep HK");
        var interrupted = new InstanceDeletionService(root, new IdleProbe(), () => "delete-test", Directory.Move,
            _ => throw new IOException("Simulated cleanup failure"));
        var result = await interrupted.DeleteAsync(record, new InstanceDeletionConditions { TransactionsHealthy = true });
        Assert.False(result.CleanupCompleted);
        Assert.False(Directory.Exists(saves));
        Assert.True(File.Exists(Path.Combine(result.PendingPath, "game-state/local-low/user1.dat")));
        var recovered = Assert.Single(await new InstanceDeletionService(root, new IdleProbe()).RecoverPendingAsync());
        Assert.True(recovered.Completed);
        Assert.False(Directory.Exists(result.PendingPath));
        Assert.Equal("keep HK", await File.ReadAllTextAsync(Path.Combine(hk, "user1.dat")));
    }

    private sealed class IdleProbe : IHollowKnightProcessProbe { public bool IsRunning() => false; }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

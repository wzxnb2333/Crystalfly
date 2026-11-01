using System.Security.Cryptography;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;
using Crystalfly.Core.Runtime;
using Crystalfly.Core.Transactions;

namespace Crystalfly.Core.Tests.Instances;

public sealed class SharedGameContentStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "crystalfly-shared-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(GameId.HollowKnight, InstancePurpose.General)]
    [InlineData(GameId.HollowKnight, InstancePurpose.OfficialSpeedrun)]
    [InlineData(GameId.Silksong, InstancePurpose.General)]
    public async Task Clones_share_protected_resources_but_mods_and_assemblies_remain_independent(GameId game, InstancePurpose purpose)
    {
        if (!OperatingSystem.IsWindows()) return;
        var source = (await CreateAsync("source", game)) with { Purpose = purpose };
        await InstanceSidecar.SaveAsync(source);
        var clone = await InstanceCloneService.CloneAsync(source.RootPath, "clone", "clone");
        var resource = Resource(source);
        Assert.True(clone.UsesSharedContent);
        Assert.Equal(source.Id, clone.ParentInstanceId);
        Assert.Equal(source.Name, clone.ParentInstanceName);
        Assert.Equal(source.Id, (await InstanceSidecar.LoadAsync(clone.RootPath))!.ParentInstanceId);
        Assert.True((await InstanceSidecar.LoadAsync(source.RootPath))!.UsesSharedContent);
        Assert.Equal(SharedGameContentStore.Identity(resource), SharedGameContentStore.Identity(Resource(clone)));
        Assert.Equal(3u, SharedGameContentStore.LinkCount(resource));
        Assert.Throws<UnauthorizedAccessException>(() => File.WriteAllText(Resource(clone), "corruption"));
        Assert.Equal(2 * 1024 * 1024, new FileInfo(resource).Length);
        var managed = Path.Combine(GameDefinition.Get(game).DataDirectoryName, "Managed", "Assembly-CSharp.dll");
        Assert.NotEqual(SharedGameContentStore.Identity(Path.Combine(source.RootPath, managed)),
            SharedGameContentStore.Identity(Path.Combine(clone.RootPath, managed)));
        await File.WriteAllTextAsync(Path.Combine(clone.RootPath, "BepInEx", "config", "mod.cfg"), "clone config");
        Assert.Equal("source config", await File.ReadAllTextAsync(Path.Combine(source.RootPath, "BepInEx", "config", "mod.cfg")));
        var third = await InstanceCloneService.CloneAsync(clone.RootPath, "third", "third");
        Assert.Equal(clone.Id, third.ParentInstanceId);
        Assert.Equal(SharedGameContentStore.Identity(resource), SharedGameContentStore.Identity(Resource(third)));
        Assert.Equal(4u, SharedGameContentStore.LinkCount(resource));
    }

    [Fact]
    public async Task Independent_copy_and_transaction_replacement_do_not_modify_other_instances()
    {
        if (!OperatingSystem.IsWindows()) return;
        var source = await CreateAsync("source", GameId.HollowKnight);
        var clone = await InstanceCloneService.CloneAsync(source.RootPath, "clone", "clone");
        var original = await HashAsync(Resource(source));
        var staging = Path.Combine(root, "patch");
        var relative = Path.GetRelativePath(source.RootPath, Resource(source));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(staging, relative))!);
        await File.WriteAllTextAsync(Path.Combine(staging, relative), "mod replacement");
        await FileTransaction.ApplyDirectoryAsync(staging, clone.RootPath, Path.Combine(root, "transactions"), "mod");
        Assert.Equal("mod replacement", await File.ReadAllTextAsync(Resource(clone)));
        Assert.Equal(original, await HashAsync(Resource(source)));

        source = await new SharedGameContentStore(root).MaterializeAsync(source);
        Assert.False(source.UsesSharedContent);
        Assert.Equal(1u, SharedGameContentStore.LinkCount(Resource(source)));
        Assert.Equal(original, await HashAsync(Resource(source)));
        await File.WriteAllTextAsync(Resource(source), "independent edit");
        Assert.Equal("mod replacement", await File.ReadAllTextAsync(Resource(clone)));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(root, ".crystalfly", "shared-content-v1"), "*.bin", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Rename_delete_and_last_reference_cleanup_preserve_remaining_instances()
    {
        if (!OperatingSystem.IsWindows()) return;
        var source = await CreateAsync("source", GameId.Silksong);
        var clone = await InstanceCloneService.CloneAsync(source.RootPath, "clone", "clone");
        var expected = await HashAsync(Resource(clone));
        var renamedRoot = Path.Combine(root, "renamed");
        Directory.Move(clone.RootPath, renamedRoot);
        clone = clone with { RootPath = renamedRoot, Name = "renamed" };
        await InstanceSidecar.SaveAsync(clone);
        var deletion = new InstanceDeletionService(root, new IdleProbe());
        var conditions = new InstanceDeletionConditions { TransactionsHealthy = true };
        Assert.True((await deletion.DeleteAsync(source, conditions)).CleanupCompleted);
        Assert.Equal(expected, await HashAsync(Resource(clone)));
        Assert.True((await deletion.DeleteAsync(clone, conditions)).CleanupCompleted);
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(root, ".crystalfly", "shared-content-v1"), "*.bin", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Full_copy_opt_out_remains_independent()
    {
        if (!OperatingSystem.IsWindows()) return;
        var source = await CreateAsync("source", GameId.HollowKnight);
        var store = new SharedGameContentStore(root);
        source = await store.OptimizeAsync(source);
        var full = await InstanceCloneService.CloneAsync(source.RootPath, "full", "full", shareContent: false);
        Assert.Equal(source.Id, full.ParentInstanceId);
        Assert.False(full.UsesSharedContent);
        Assert.Equal(1u, SharedGameContentStore.LinkCount(Resource(full)));
        await File.WriteAllTextAsync(Resource(full), "private");
        Assert.Equal(2 * 1024 * 1024, new FileInfo(Resource(source)).Length);
    }

    [Fact]
    public async Task Cancellation_and_reparse_paths_do_not_damage_original_data()
    {
        if (!OperatingSystem.IsWindows()) return;
        var source = await CreateAsync("source", GameId.Silksong);
        var original = await HashAsync(Resource(source));
        var store = new SharedGameContentStore(root);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.OptimizeAsync(source, cancellation.Token));
        Assert.Equal(original, await HashAsync(Resource(source)));
        Assert.Equal(1u, SharedGameContentStore.LinkCount(Resource(source)));
        var outside = Directory.CreateDirectory(Path.Combine(root, "outside")).FullName;
        var link = Path.Combine(source.RootPath, "linked");
        Directory.CreateSymbolicLink(link, outside);
        try { await Assert.ThrowsAsync<IOException>(() => store.OptimizeAsync(source)); }
        finally { Directory.Delete(link); }
        Assert.Equal(original, await HashAsync(Resource(source)));
    }

    [Fact]
    public async Task Failed_mod_transaction_restores_shared_resource_without_changing_sibling()
    {
        if (!OperatingSystem.IsWindows()) return;
        var source = await CreateAsync("source", GameId.Silksong);
        var clone = await InstanceCloneService.CloneAsync(source.RootPath, "clone", "clone");
        var expected = await HashAsync(Resource(source));
        var staging = Path.Combine(root, "patch");
        var stagedResource = Path.Combine(staging, Path.GetRelativePath(clone.RootPath, Resource(clone)));
        Directory.CreateDirectory(Path.GetDirectoryName(stagedResource)!);
        await File.WriteAllTextAsync(stagedResource, "replacement");
        await File.WriteAllTextAsync(Path.Combine(staging, "z-blocked.txt"), "new");
        await using (var locked = new FileStream(Path.Combine(clone.RootPath, "z-blocked.txt"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            await Assert.ThrowsAnyAsync<IOException>(() => FileTransaction.ApplyDirectoryAsync(staging, clone.RootPath,
                Path.Combine(root, "transactions"), "failure"));
        Assert.Equal(expected, await HashAsync(Resource(source)));
        Assert.Equal(expected, await HashAsync(Resource(clone)));
    }

    [Fact]
    public async Task Interrupted_conversion_preserves_bytes_and_can_be_materialized()
    {
        if (!OperatingSystem.IsWindows()) return;
        var source = await CreateAsync("source", GameId.Silksong);
        var another = Path.Combine(Path.GetDirectoryName(Resource(source))!, "second.bundle");
        File.Copy(Resource(source), another);
        var expected = await HashAsync(Resource(source));
        using var cancellation = new CancellationTokenSource();
        var store = new SharedGameContentStore(root);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.OptimizeAsync(source, cancellation.Token,
            new CancelAfterFirst(cancellation)));
        Assert.True((await InstanceSidecar.LoadAsync(source.RootPath))!.UsesSharedContent);
        Assert.Equal(expected, await HashAsync(Resource(source)));
        Assert.Equal(expected, await HashAsync(another));
        source = await store.MaterializeAsync(source);
        Assert.False(source.UsesSharedContent);
        Assert.Equal(1u, SharedGameContentStore.LinkCount(Resource(source)));
        Assert.Equal(1u, SharedGameContentStore.LinkCount(another));
        await File.WriteAllTextAsync(Resource(source), "private");
        await File.WriteAllTextAsync(another, "private");
    }

    [Fact]
    public async Task Storage_counts_hardlinks_once_and_does_not_count_the_pool_as_savings()
    {
        if (!OperatingSystem.IsWindows()) return;
        var source = await CreateAsync("source", GameId.Silksong);
        var clone = await InstanceCloneService.CloneAsync(source.RootPath, "clone", "clone");
        var pool = Path.Combine(root, ".crystalfly", "shared-content-v1", GameId.Silksong.ToString());
        var single = InstanceStorageUsage.Measure([source.RootPath], [pool]);
        Assert.Equal(0, single.SharedSavingsBytes);
        Assert.Equal(0, single.UnreadableEntries);
        var both = InstanceStorageUsage.Measure([source.RootPath, clone.RootPath], [pool]);
        Assert.Equal(2 * 1024 * 1024, both.SharedSavingsBytes);
        Assert.Equal(0, both.UnreadableEntries);
        Assert.True(both.AllocatedBytes < single.AllocatedBytes + 1024 * 1024);
        await new SharedGameContentStore(root).MaterializeAsync(clone);
        var detached = InstanceStorageUsage.Measure([source.RootPath, clone.RootPath], [pool]);
        Assert.Equal(0, detached.SharedSavingsBytes);
        Assert.Equal(both.AllocatedBytes + 2 * 1024 * 1024, detached.AllocatedBytes);
    }

    [Fact]
    public async Task Storage_supports_locked_files_reports_missing_roots_and_honors_cancellation()
    {
        if (!OperatingSystem.IsWindows()) return;
        var source = await CreateAsync("source", GameId.Silksong);
        var expected = InstanceStorageUsage.Measure([source.RootPath], []);
        using (var locked = new FileStream(Resource(source), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.Equal(expected, InstanceStorageUsage.Measure([source.RootPath], []));
        var partial = InstanceStorageUsage.Measure([source.RootPath, Path.Combine(root, "missing")], []);
        Assert.Equal(expected.AllocatedBytes, partial.AllocatedBytes);
        Assert.Equal(1, partial.UnreadableEntries);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => InstanceStorageUsage.Measure([source.RootPath], [], canceled.Token));
    }

    private sealed class CancelAfterFirst(CancellationTokenSource cancellation) : IProgress<(int Completed, int Total)>
    {
        public void Report((int Completed, int Total) value) { if (value.Completed == 1) cancellation.Cancel(); }
    }

    [Fact]
    public async Task Concurrent_clones_share_one_base_and_different_content_never_aliases()
    {
        if (!OperatingSystem.IsWindows()) return;
        var source = await CreateAsync("source", GameId.HollowKnight);
        var clones = await Task.WhenAll(
            Task.Run(() => InstanceCloneService.CloneAsync(source.RootPath, "one", "one")),
            Task.Run(() => InstanceCloneService.CloneAsync(source.RootPath, "two", "two")));
        Assert.Equal(SharedGameContentStore.Identity(Resource(clones[0])), SharedGameContentStore.Identity(Resource(clones[1])));
        var different = await CreateAsync("different", GameId.HollowKnight, 42);
        different = await new SharedGameContentStore(root).OptimizeAsync(different);
        Assert.NotEqual(SharedGameContentStore.Identity(Resource(source)), SharedGameContentStore.Identity(Resource(different)));
    }

    private async Task<InstanceRecord> CreateAsync(string name, GameId game, byte value = 7)
    {
        var instance = new InstanceRecord { Id = name, Name = name, RootPath = Path.Combine(root, name), GameId = game, BuildId = "test" };
        Directory.CreateDirectory(Path.GetDirectoryName(Resource(instance))!);
        await File.WriteAllBytesAsync(Resource(instance), Enumerable.Repeat(value, 2 * 1024 * 1024).ToArray());
        var managed = Path.Combine(instance.RootPath, GameDefinition.Get(game).DataDirectoryName, "Managed");
        Directory.CreateDirectory(managed);
        await File.WriteAllTextAsync(Path.Combine(managed, "Assembly-CSharp.dll"), "assembly");
        var config = Path.Combine(instance.RootPath, "BepInEx", "config");
        Directory.CreateDirectory(config);
        await File.WriteAllTextAsync(Path.Combine(config, "mod.cfg"), "source config");
        await InstanceSidecar.SaveAsync(instance);
        return instance;
    }

    private static string Resource(InstanceRecord instance) => Path.Combine(instance.RootPath,
        GameDefinition.Get(instance.GameId).DataDirectoryName, "StreamingAssets", "large.bundle");

    private static async Task<string> HashAsync(string path)
    {
        await using var input = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(input));
    }

    private sealed class IdleProbe : IHollowKnightProcessProbe { public bool IsRunning() => false; }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

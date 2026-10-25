using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;
using Crystalfly.Core.Thunderstore;

namespace Crystalfly.Core.Tests.Thunderstore;

public sealed partial class ThunderstoreTests
{
    [Fact]
    public async Task Batch_management_preserves_dependencies_and_commits_all_files_together()
    {
        var (sharedPath, shared) = await ArchiveAsync("Shared", ("shared.txt", "shared"));
        var (mainPath, main) = await ArchiveAsync("Main", ("main.txt", "main"));
        var store = Store;
        await store.ImportLocalAsync(sharedPath, shared);
        await store.ImportLocalAsync(mainPath, main);
        var service = new ThunderstoreInstallService(Instance, store, () => false);
        await service.InstallAsync(new(GameId.Silksong, Instance.BuildId, [shared, main], []), offline: true);
        var state = await service.ReadStateAsync();
        await AtomicJsonStore.WriteAsync(Path.Combine(Instance.RootPath, ThunderstoreInstallService.MetadataDirectory, "installed.json"),
            state with { Packages = state.Packages.Select(item => item.FullName == main.Package.FullName
                ? item with { Dependencies = ["Author-Shared-1.0.0"] } : item).ToArray() });
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetEnabledAsync([shared.Package.FullName], false));
        Assert.All((await service.ReadStateAsync()).Packages, item => Assert.True(item.Enabled));
        using (var canceled = new CancellationTokenSource())
        {
            canceled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SetEnabledAsync(
                [shared.Package.FullName, main.Package.FullName], false, canceled.Token));
        }
        var file = Path.Combine(Instance.RootPath, "BepInEx/plugins/Author-Main/main.txt");
        using (var locked = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
            await Assert.ThrowsAnyAsync<IOException>(() => service.SetEnabledAsync([shared.Package.FullName, main.Package.FullName], false));
        Assert.All((await service.ReadStateAsync()).Packages, item => Assert.True(item.Enabled));
        Assert.True(File.Exists(Path.Combine(Instance.RootPath, "BepInEx/plugins/Author-Shared/shared.txt")));
        await service.SetEnabledAsync([shared.Package.FullName, main.Package.FullName], false);
        Assert.All((await service.ReadStateAsync()).Packages, item => Assert.False(item.Enabled));
        state = await service.ReadStateAsync();
        var receipt = Path.Combine(Instance.RootPath, ThunderstoreInstallService.MetadataDirectory, "installed.json");
        await AtomicJsonStore.WriteAsync(receipt, state with { Packages = state.Packages.Select(item => item.FullName == shared.Package.FullName
            ? item with { Version = "0.9.0" } : item).ToArray() });
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetEnabledAsync([main.Package.FullName, shared.Package.FullName], true));
        Assert.All((await service.ReadStateAsync()).Packages, item => Assert.False(item.Enabled));
        await AtomicJsonStore.WriteAsync(receipt, state);
        await service.SetEnabledAsync([main.Package.FullName, shared.Package.FullName], true);
        Assert.All((await service.ReadStateAsync()).Packages, item => Assert.True(item.Enabled));

        await File.WriteAllTextAsync(file, "user edit");
        await Assert.ThrowsAsync<InvalidDataException>(() => service.UninstallAsync([shared.Package.FullName, main.Package.FullName]));
        Assert.Equal(2, (await service.ReadStateAsync()).Packages.Count);
        Assert.True(File.Exists(Path.Combine(Instance.RootPath, "BepInEx/plugins/Author-Shared/shared.txt")));
        await File.WriteAllTextAsync(file, "main");
        await service.UninstallAsync([shared.Package.FullName, main.Package.FullName]);
        Assert.Empty((await service.ReadStateAsync()).Packages);
    }
}

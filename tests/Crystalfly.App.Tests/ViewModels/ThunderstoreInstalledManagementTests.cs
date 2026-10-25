using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;
using Crystalfly.Core.Thunderstore;

namespace Crystalfly.App.Tests.ViewModels;

public sealed partial class ThunderstoreViewModelTests
{
    [Fact]
    public async Task Installed_list_range_selection_and_graph_selection_keep_only_manageable_packages()
    {
        var packages = new[] { "First", "Second", "Third" }.Select(name => Package("Author", name, "2.0.0") with
        {
            Versions = [Version("Author", name, "2.0.0") with { IconUrl = "https://ccdn.thunderstore.io/new.png" },
                Version("Author", name, "1.0.0") with { IconUrl = "https://ccdn.thunderstore.io/installed.png" }]
        }).ToArray();
        var catalogPath = Path.Combine(root, "selection-catalog.json");
        await AtomicJsonStore.WriteAsync(catalogPath, new ThunderstoreCatalog { Packages = packages });
        var target = new InstanceRecord { Id = "silk", GameId = GameId.Silksong, Name = "Silk", BuildId = "test", RootPath = Path.Combine(root, "selection-game") };
        var loader = new ThunderstoreInstalledPackage { FullName = SilksongLoaderAdapter.PackageName, Version = SilksongLoaderAdapter.PackageVersion, ArchiveSha256 = new string('A', 64), IsLoader = true };
        await AtomicJsonStore.WriteAsync(Path.Combine(target.RootPath, ThunderstoreInstallService.MetadataDirectory, "installed.json"),
            new ThunderstoreInstallState { Packages = packages.Select(package => new ThunderstoreInstalledPackage
            {
                FullName = package.FullName, Version = "1.0.0", ArchiveSha256 = new string('A', 64), Dependencies = ["Author-Missing-1.0.0"]
            }).Append(loader).ToArray() });
        using var client = new HttpClient(new Handler(() => throw new InvalidOperationException("No network")));
        var vm = new Crystalfly.App.ViewModels.ThunderstoreViewModel(new(), new(client, catalogPath), new(client, Path.Combine(root, "cache")),
            () => target, () => true, _ => Task.CompletedTask, () => Task.CompletedTask);
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Null(vm.Error);
        Assert.Equal(3, vm.InstalledMods.Count);
        Assert.All(vm.InstalledDisplayItems, item => Assert.EndsWith("/installed.png", item.IconUrl));
        vm.SelectInstalledItem(vm.InstalledMods[0], false, false);
        vm.SelectInstalledItem(vm.InstalledMods[2], false, true);
        Assert.Equal(3, vm.SelectedInstalledCount);
        vm.SelectInstalledItem(vm.InstalledMods[1], true, false);
        Assert.Equal(2, vm.SelectedInstalledCount);
        Assert.False(vm.InstalledDisplayItems[1].IsSelected);
        vm.ClearInstalledSelectionCommand.Execute(null);
        vm.SelectInstalledItem(vm.InstalledMods[2], false, true);
        Assert.Equal(1, vm.SelectedInstalledCount);
        vm.ShowInstalledGraphCommand.Execute(null);
        Assert.Single(vm.InstalledGraph.SelectedNodes);
        var missing = vm.InstalledGraph.Nodes.Single(node => node.IsMissing);
        vm.InstalledGraph.SetSelection(vm.InstalledGraph.Nodes.Select(node => node.Id), missing.Id, notify: true);
        Assert.Equal(5, vm.InstalledGraph.SelectedNodes.Count);
        Assert.Equal(3, vm.SelectedInstalledCount);
        Assert.Null(vm.SelectedInstalled);
        Assert.All(vm.InstalledDisplayItems, item => Assert.True(item.IsSelected));
        vm.InstalledGraph.SetSelection([], notify: true);
        Assert.Empty(vm.InstalledGraph.SelectedNodes);
        Assert.Equal(0, vm.SelectedInstalledCount);
        Assert.False(vm.RequestUninstallSelectedCommand.CanExecute(null));
    }

    [Fact]
    public async Task Installed_health_filters_missing_files_and_folder_actions_reject_traversal()
    {
        var target = new InstanceRecord { Id = "silk", GameId = GameId.Silksong, Name = "Silk", BuildId = "test", RootPath = Path.Combine(root, "health-game") };
        var first = new ThunderstoreInstalledPackage { FullName = "Author-Healthy", Version = "1.0.0", ArchiveSha256 = new string('A', 64),
            Files = [new("BepInEx/plugins/Author-Healthy/main.dll", new string('A', 64))] };
        var missing = first with { FullName = "Author-Missing", Files = [new("BepInEx/plugins/Author-Missing/missing.dll", new string('A', 64))] };
        var path = Path.Combine(target.RootPath, first.Files[0].Path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "managed bytes");
        first = first with { Files = [first.Files[0] with { Sha256 = await ThunderstorePackageStore.HashAsync(path) }] };
        await AtomicJsonStore.WriteAsync(Path.Combine(target.RootPath, ThunderstoreInstallService.MetadataDirectory, "installed.json"),
            new ThunderstoreInstallState { Packages = [first, missing] });
        using var client = new HttpClient(new Handler(() => throw new InvalidOperationException("No network")));
        var vm = new Crystalfly.App.ViewModels.ThunderstoreViewModel(new(), new(client, Path.Combine(root, "unused.json")), new(client, Path.Combine(root, "cache")),
            () => target, () => true, _ => Task.CompletedTask, () => Task.CompletedTask);
        await vm.SetContextAsync();
        await vm.CheckInstalledHealthCommand.ExecuteAsync(null);
        Assert.Null(vm.Error);
        vm.InstalledStatus = vm.InstalledStatusOptions.Single(item => item.Value == "attention");
        Assert.Equal(missing.FullName, Assert.Single(vm.InstalledMods).FullName);
        vm.SelectedInstalled = missing;
        Assert.Contains("missing.dll", vm.InstalledHealthDetails);
        Assert.Equal(Path.GetDirectoryName(path), vm.GetInstalledDirectory(first));
        Assert.Throws<InvalidDataException>(() => vm.GetInstalledDirectory(first with { Files = [new("../outside.dll", new string('A', 64))] }));
        var disabled = first with { Enabled = false };
        var disabledDirectory = Path.Combine(target.RootPath, ThunderstoreInstallService.MetadataDirectory, "disabled", first.FullName, "BepInEx/plugins/Author-Healthy");
        Directory.CreateDirectory(disabledDirectory);
        Assert.Equal(Path.GetFullPath(disabledDirectory), vm.GetInstalledDirectory(disabled));
    }

    [Fact]
    public async Task Installed_batch_update_merges_dependencies_and_keeps_pinned_versions()
    {
        var main = Package("Author", "Main", "2.0.0");
        main = main with { Versions = [main.Latest! with { Dependencies = ["Author-Shared-2.0.0"] }, Version("Author", "Main", "1.0.0")] };
        var shared = Package("Author", "Shared", "2.0.0") with { Versions = [Version("Author", "Shared", "2.0.0"), Version("Author", "Shared", "1.0.0")] };
        var pinned = Package("Author", "Pinned", "2.0.0") with { Versions = [Version("Author", "Pinned", "2.0.0"), Version("Author", "Pinned", "1.0.0")] };
        var loader = Package("silksong_modding", "BepInExPack_Silksong", "1.0.3");
        var catalogPath = Path.Combine(root, "batch-update.json");
        await AtomicJsonStore.WriteAsync(catalogPath, new ThunderstoreCatalog { Packages = [main, shared, pinned, loader] });
        var target = new InstanceRecord { Id = "silk", GameId = GameId.Silksong, Name = "Silk", BuildId = "test", RootPath = Path.Combine(root, "update-game") };
        await AtomicJsonStore.WriteAsync(Path.Combine(target.RootPath, ThunderstoreInstallService.MetadataDirectory, "installed.json"),
            new ThunderstoreInstallState { Packages = new[] { main, shared, pinned }.Select(item => new ThunderstoreInstalledPackage {
                FullName = item.FullName, Version = "1.0.0", Pinned = item == pinned, ArchiveSha256 = new string('A', 64) }).ToArray() });
        using var client = new HttpClient(new Handler(() => throw new InvalidOperationException("No network")));
        Crystalfly.App.Downloads.DownloadQueueGroup? queued = null;
        var vm = new Crystalfly.App.ViewModels.ThunderstoreViewModel(new(), new(client, catalogPath), new(client, Path.Combine(root, "cache")),
            () => target, () => true, group => { queued = group; return Task.CompletedTask; }, () => Task.CompletedTask);
        await vm.RefreshCommand.ExecuteAsync(null);
        vm.SelectAllInstalledCommand.Execute(null);
        await vm.UpdateInstalledSelectedCommand.ExecuteAsync(null);
        Assert.Null(vm.Error);
        Assert.True(vm.HasPlan);
        await vm.ConfirmInstallCommand.ExecuteAsync(null);
        Assert.NotNull(queued?.ThunderstorePlan);
        Assert.Equal("2.0.0", queued.ThunderstorePlan.Packages.Single(item => item.Package.FullName == main.FullName).Version.VersionNumber);
        Assert.Equal("2.0.0", queued.ThunderstorePlan.Packages.Single(item => item.Package.FullName == shared.FullName).Version.VersionNumber);
        Assert.Equal("1.0.0", queued.ThunderstorePlan.Packages.Single(item => item.Package.FullName == pinned.FullName).Version.VersionNumber);
    }

    [Fact]
    public async Task Installed_graph_filters_batch_selection_and_context_use_receipt_dependencies()
    {
        var main = Package("Author", "Main", "2.0.0");
        var shared = Package("Author", "Shared", "1.0.0");
        var catalogPath = Path.Combine(root, "installed-catalog.json");
        await AtomicJsonStore.WriteAsync(catalogPath, new ThunderstoreCatalog { Packages = [main, shared] });
        var target = new InstanceRecord { Id = "silk", GameId = GameId.Silksong, Name = "Silk", BuildId = "test", RootPath = Path.Combine(root, "game") };
        var installed = new ThunderstoreInstalledPackage { FullName = main.FullName, Version = "1.0.0",
            ArchiveSha256 = new string('A', 64), Dependencies = ["Author-Shared-1.0.0"] };
        await AtomicJsonStore.WriteAsync(Path.Combine(target.RootPath, ThunderstoreInstallService.MetadataDirectory, "installed.json"),
            new ThunderstoreInstallState { Packages = [installed] });
        using var client = new HttpClient(new Handler(() => throw new InvalidOperationException("No network")));
        var vm = new Crystalfly.App.ViewModels.ThunderstoreViewModel(new(), new(client, catalogPath), new(client, Path.Combine(root, "cache")),
            () => target, () => true, _ => Task.CompletedTask, () => Task.CompletedTask);
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Null(vm.Error);
        Assert.Equal(2, vm.InstalledGraph.Nodes.Count);
        Assert.Single(vm.InstalledGraph.Edges);
        Assert.True(vm.InstalledGraph.Nodes.Single(item => item.Id == shared.FullName).IsMissing);
        vm.InstalledStatus = vm.InstalledStatusOptions.Single(item => item.Value == "updates");
        Assert.Single(vm.InstalledMods);
        vm.SetInstalledSelected(main.FullName, true);
        vm.InstalledSearchText = "no match";
        Assert.Empty(vm.InstalledMods);
        Assert.Equal(1, vm.SelectedInstalledCount);
        vm.InstalledSearchText = "";
        vm.InstalledStatus = vm.InstalledStatusOptions.Single(item => item.Value == "attention");
        Assert.Single(vm.InstalledMods);
        vm.SelectedInstalled = installed;
        Assert.Contains("Shared", vm.InstalledDependenciesText);
        vm.InstalledGraph.SelectNodeCommand.Execute(vm.InstalledGraph.Nodes.Single(item => item.IsMissing));
        Assert.Null(vm.SelectedInstalled);
        Assert.Equal(shared.FullName, vm.InstalledGraph.SelectedNode?.Id);
        vm.RequestUninstallSelectedCommand.Execute(null);
        Assert.True(vm.HasUninstallConfirmation);
        target = target with { Id = "other", RootPath = Path.Combine(root, "other") };
        await vm.ConfirmUninstallCommand.ExecuteAsync(null);
        Assert.Equal(vm.Loc["TsTargetChanged"], vm.Error);
        await vm.SetContextAsync();
        Assert.Equal(0, vm.SelectedInstalledCount);
        Assert.False(vm.HasUninstallConfirmation);
        Assert.Empty(vm.InstalledGraph.Nodes);
    }
}

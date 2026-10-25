using System.Net;
using Crystalfly.App.ViewModels;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;
using Crystalfly.Core.Thunderstore;

namespace Crystalfly.App.Tests.ViewModels;

public sealed partial class ThunderstoreViewModelTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Crystalfly.Thunderstore.UI", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Batch_preview_merges_dependencies_and_retargets_before_queueing()
    {
        var dependency = Package("Author", "Shared", "1.0.0");
        var one = Package("Author", "One", "1.0.0");
        var two = Package("Author", "Two", "1.0.0");
        one = one with { Versions = [one.Latest! with { Dependencies = ["Author-Shared-1.0.0"] }] };
        two = two with { Versions = [two.Latest! with { Dependencies = ["Author-Shared-1.0.0"] }] };
        var loader = Package("silksong_modding", "BepInExPack_Silksong", "1.0.3");
        string catalogPath = Path.Combine(root, "batch-catalog.json");
        await AtomicJsonStore.WriteAsync(catalogPath, new ThunderstoreCatalog { Packages = [one, two, dependency, loader] });
        using var client = new HttpClient(new Handler(() => throw new InvalidOperationException("No network")));
        var first = new InstanceRecord { Id = "first", GameId = GameId.Silksong, Name = "First", BuildId = "first-build", RootPath = Path.Combine(root, "first") };
        var second = first with { Id = "second", Name = "Second", BuildId = "second-build", RootPath = Path.Combine(root, "second") };
        var hk = first with { Id = "hk", GameId = GameId.HollowKnight };
        var targets = new List<InstanceRecord> { first, second, hk };
        Crystalfly.App.Downloads.DownloadQueueGroup? queued = null;
        var vm = new ThunderstoreViewModel(new(), new(client, catalogPath), new(client, Path.Combine(root, "cache")),
            () => first, () => true, group => { queued = group; return Task.CompletedTask; }, () => Task.CompletedTask,
            getInstallTargets: () => targets);
        await vm.RefreshCommand.ExecuteAsync(null);
        vm.SetPackageSelected(one.FullName, true);
        vm.SetPackageSelected(two.FullName, true);
        vm.SearchText = "One";
        Assert.Equal(2, vm.SelectedPackageCount);
        await vm.PreviewSelectedCommand.ExecuteAsync(null);
        Assert.True(vm.HasPlan);
        Assert.Equal(2, vm.InstallTargets.Count);
        Assert.Equal(4, vm.PlanItems.Count);
        await vm.SelectInstallTargetAsync(second);
        Assert.Equal(second.Name, vm.InstallTargetName);
        await vm.ConfirmInstallCommand.ExecuteAsync(null);
        Assert.NotNull(queued);
        Assert.Equal(second.Id, queued.TargetInstanceId);
        Assert.Equal(second.BuildId, queued.ThunderstorePlan!.BuildId);
        Assert.Single(queued.ThunderstorePlan.Packages, item => item.Package.FullName == dependency.FullName);
        Assert.False(vm.HasPlan);
    }

    [Fact]
    public async Task Removed_install_target_cannot_receive_a_confirmed_plan()
    {
        var package = Package("Author", "Main", "1.0.0");
        var loader = Package("silksong_modding", "BepInExPack_Silksong", "1.0.3");
        var catalogPath = Path.Combine(root, "target-catalog.json");
        await AtomicJsonStore.WriteAsync(catalogPath, new ThunderstoreCatalog { Packages = [package, loader] });
        using var client = new HttpClient(new Handler(() => { }));
        var target = new InstanceRecord { Id = "silk", GameId = GameId.Silksong, Name = "Silk", BuildId = "build", RootPath = Path.Combine(root, "game") };
        var targets = new List<InstanceRecord> { target };
        var queued = false;
        var vm = new ThunderstoreViewModel(new(), new(client, catalogPath), new(client, Path.Combine(root, "cache")),
            () => target, () => true, _ => { queued = true; return Task.CompletedTask; }, () => Task.CompletedTask,
            getInstallTargets: () => targets);
        await vm.RefreshCommand.ExecuteAsync(null);
        vm.SelectedPackage = package;
        await vm.PreviewInstallCommand.ExecuteAsync(null);
        targets.Clear();
        await vm.ConfirmInstallCommand.ExecuteAsync(null);
        Assert.False(queued);
        Assert.Equal(vm.Loc["TsTargetChanged"], vm.Error);
    }

    [Fact]
    public async Task Cached_catalog_filters_and_offline_confirmation_preserve_author_text_and_bind_target()
    {
        var main = Package("Author", "Main", "1.0.0") with { Categories = ["Mods"] };
        var deprecated = Package("Author", "Old", "1.0.0") with { IsDeprecated = true };
        var loader = Package("silksong_modding", "BepInExPack_Silksong", "1.0.3");
        var catalog = Path.Combine(root, "catalog.json");
        await AtomicJsonStore.WriteAsync(catalog, new ThunderstoreCatalog { Packages = [main, deprecated, loader] });
        var networkCalls = 0;
        using var client = new HttpClient(new Handler(() => networkCalls++));
        var target = new InstanceRecord { Id = "silk", GameId = GameId.Silksong, Name = "Silk", BuildId = "silksong-1.0.30000", RootPath = Path.Combine(root, "game") };
        ThunderstoreInstallPlan? installed = null;
        var loc = new LocalizationViewModel();
        loc.Apply(UiLanguage.SimplifiedChinese);
        var vm = new ThunderstoreViewModel(loc, new(client, catalog), new(client, Path.Combine(root, "cache")), () => target,
            () => true, _ => throw new InvalidOperationException("Offline install must not enqueue network work"), () => Task.CompletedTask,
            (instance, plan, repair) => { Assert.Same(target, instance); Assert.False(repair); installed = plan; return Task.CompletedTask; });
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Null(vm.Error);
        Assert.DoesNotContain(vm.Packages, item => item.IsDeprecated);
        vm.SearchText = "Main";
        vm.SelectedPackage = Assert.Single(vm.Packages);
        Assert.Equal("Original author description", vm.SelectedPackage.Latest?.Description);
        await vm.PreviewInstallCommand.ExecuteAsync(null);
        Assert.True(vm.HasPlan);
        Assert.True(vm.ConfirmInstallCommand.CanExecute(null));
        await vm.ConfirmInstallCommand.ExecuteAsync(null);
        Assert.NotNull(installed);
        Assert.Equal(GameId.Silksong, installed.GameId);
        Assert.Equal(2, installed.Packages.Count);
        Assert.False(vm.HasPlan);
        Assert.Equal(0, networkCalls);
        loc.Apply(UiLanguage.English);
        vm.RefreshLocalization();
        Assert.Equal(loc["TsAllCategories"], vm.Categories[0].Name);
    }

    [Fact]
    public async Task Repair_uses_installed_selection_even_when_another_package_is_browsed()
    {
        var installed = Package("Author", "Installed", "1.0.0");
        var other = Package("Author", "Other", "2.0.0");
        var loader = Package("silksong_modding", "BepInExPack_Silksong", "1.0.3");
        var catalogPath = Path.Combine(root, "catalog.json");
        await AtomicJsonStore.WriteAsync(catalogPath, new ThunderstoreCatalog { Packages = [installed, other, loader] });
        var target = new InstanceRecord { Id = "silk", GameId = GameId.Silksong, Name = "Silk", BuildId = "test", RootPath = Path.Combine(root, "game") };
        await AtomicJsonStore.WriteAsync(Path.Combine(target.RootPath, ThunderstoreInstallService.MetadataDirectory, "installed.json"),
            new ThunderstoreInstallState { Packages = [new() { FullName = installed.FullName, Version = "1.0.0", ArchiveSha256 = new string('A', 64) }] });
        using var client = new HttpClient(new Handler(() => throw new InvalidOperationException("No network")));
        var vm = new ThunderstoreViewModel(new(), new(client, catalogPath), new(client, Path.Combine(root, "cache")),
            () => target, () => true, _ => Task.CompletedTask, () => Task.CompletedTask);
        await vm.RefreshCommand.ExecuteAsync(null);
        vm.SelectedInstalled = Assert.Single(vm.Installed);
        vm.SelectedPackage = other;
        await vm.PreviewRepairCommand.ExecuteAsync(null);
        Assert.Null(vm.Error);
        Assert.True(vm.HasPlan);
        Assert.Contains("Author-Installed", vm.PreviewText);
        Assert.DoesNotContain("Author-Other", vm.PreviewText);
    }

    [Fact]
    public async Task Search_locates_package_and_version_from_thunderstore_link()
    {
        var package = Package("Author", "Main", "1.0.0");
        var catalogPath = Path.Combine(root, "link-catalog.json");
        await AtomicJsonStore.WriteAsync(catalogPath, new ThunderstoreCatalog { Packages = [package] });
        var target = new InstanceRecord { Id = "silk", GameId = GameId.Silksong, Name = "Silk", BuildId = "test", RootPath = Path.Combine(root, "game") };
        using var client = new HttpClient(new Handler(() => throw new InvalidOperationException("No network")));
        var vm = new ThunderstoreViewModel(new(), new(client, catalogPath), new(client, Path.Combine(root, "cache-link")),
            () => target, () => true, _ => Task.CompletedTask, () => Task.CompletedTask);

        await vm.RefreshCommand.ExecuteAsync(null);
        vm.SearchText = "https://thunderstore.io/c/hollow-knight-silksong/p/Author/Main/1.0.0/";

        Assert.Equal(package.FullName, vm.SelectedPackage?.FullName);
        Assert.Equal("1.0.0", vm.SelectedVersion?.VersionNumber);
        Assert.Single(vm.Packages);
        Assert.Null(vm.Error);
    }

    [Fact]
    public async Task Search_locator_preserves_requested_version_and_ignores_invalid_urls()
    {
        var author = Package("Author", "Shared", "2.0.0") with
        {
            Versions = [Version("Author", "Shared", "1.0.0"), Version("Author", "Shared", "2.0.0")]
        };
        var otherAuthor = Package("Other", "Shared", "2.0.0");
        var catalogPath = Path.Combine(root, "locator-catalog.json");
        await AtomicJsonStore.WriteAsync(catalogPath, new ThunderstoreCatalog { Packages = [author, otherAuthor] });
        var target = new InstanceRecord { Id = "silk", GameId = GameId.Silksong, Name = "Silk", BuildId = "test", RootPath = Path.Combine(root, "game") };
        using var client = new HttpClient(new Handler(() => throw new InvalidOperationException("No network")));
        var loc = new LocalizationViewModel();
        loc.Apply(UiLanguage.SimplifiedChinese);
        var vm = new ThunderstoreViewModel(loc, new(client, catalogPath), new(client, Path.Combine(root, "cache-locator")),
            () => target, () => true, _ => Task.CompletedTask, () => Task.CompletedTask);
        vm.SearchText = "https://thunderstore.io/c/hollow-knight-silksong/p/Author/Shared/1.0.0/";

        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal("Author-Shared", vm.SelectedPackage?.FullName);
        Assert.Equal("1.0.0", vm.SelectedVersion?.VersionNumber);

        vm.SearchText = "https://thunderstore.io/c/hollow-knight-silksong/p/Author/Shared/9.9.9/";
        Assert.Equal(loc["TsVersionMissing"], vm.Error);
        Assert.Null(vm.SelectedVersion);
        vm.Sort = vm.SortOptions[^1];
        Assert.Null(vm.SelectedVersion);
        Assert.Same(vm.Packages.Single(), vm.SelectedPackage);

        vm.SearchText = "Other-Shared";
        Assert.Null(vm.Error);
        Assert.Equal("Other-Shared", vm.SelectedPackage?.FullName);
        Assert.Equal("2.0.0", vm.SelectedVersion?.VersionNumber);
        vm.SelectedPackage = null;
        vm.Sort = vm.SortOptions[^1];
        loc.Apply(UiLanguage.English);
        vm.RefreshLocalization();
        Assert.Null(vm.SelectedPackage);

        vm.SearchText = "https://thunderstore.io/c/hollow-knight/p/Author/Shared/1.0.0/";
        Assert.Null(vm.SelectedPackage);
        vm.SearchText = "this is not a url";
        Assert.Null(vm.SelectedPackage);
        Assert.Null(vm.Error);

        vm.SearchText = "Shared";
        Assert.Equal(2, vm.Packages.Count);
        Assert.Null(vm.SelectedPackage);
    }

    [Fact]
    public async Task Installed_selection_and_pin_label_survive_instance_refresh()
    {
        var package = Package("Author", "Installed", "1.0.0");
        var catalogPath = Path.Combine(root, "catalog.json");
        await AtomicJsonStore.WriteAsync(catalogPath, new ThunderstoreCatalog { Packages = [package] });
        var target = new InstanceRecord { Id = "silk", GameId = GameId.Silksong, Name = "Silk", BuildId = "test", RootPath = Path.Combine(root, "game") };
        await AtomicJsonStore.WriteAsync(Path.Combine(target.RootPath, ThunderstoreInstallService.MetadataDirectory, "installed.json"),
            new ThunderstoreInstallState { Packages = [new() { FullName = package.FullName, Version = "1.0.0", ArchiveSha256 = new string('A', 64) }] });
        using var client = new HttpClient(new Handler(() => throw new InvalidOperationException("No network")));
        ThunderstoreViewModel? vm = null;
        vm = new(new(), new(client, catalogPath), new(client, Path.Combine(root, "cache")),
            () => target, () => true, _ => Task.CompletedTask, () => vm!.SetContextAsync());
        await vm.RefreshCommand.ExecuteAsync(null);
        vm.SelectedInstalled = Assert.Single(vm.InstalledMods);
        await vm.TogglePinnedCommand.ExecuteAsync(null);
        Assert.Null(vm.Error);
        Assert.NotNull(vm.SelectedInstalled);
        Assert.True(vm.SelectedInstalled.Pinned);
        Assert.Equal(vm.Loc["TsUnpin"], vm.PinActionLabel);
        Assert.True(vm.TogglePinnedCommand.CanExecute(null));
    }

    [Fact]
    public void Actions_without_a_valid_target_or_selection_are_disabled()
    {
        using var client = new HttpClient();
        var vm = new ThunderstoreViewModel(new(), new(client, Path.Combine(root, "catalog.json")), new(client, Path.Combine(root, "cache")),
            () => null, () => true, _ => Task.CompletedTask, () => Task.CompletedTask);
        Assert.False(vm.PreviewInstallCommand.CanExecute(null));
        Assert.False(vm.PreviewRepairCommand.CanExecute(null));
        Assert.False(vm.ToggleEnabledCommand.CanExecute(null));
        Assert.False(vm.RequestUninstallCommand.CanExecute(null));
        Assert.False(vm.ConfirmInstallCommand.CanExecute(null));
    }

    [Fact]
    public async Task Missing_offline_catalog_does_not_crash_zip_import()
    {
        using var client = new HttpClient(new Handler(() => throw new InvalidOperationException("No network")));
        var target = new InstanceRecord { Id = "silk", GameId = GameId.Silksong, Name = "Silk", BuildId = "test", RootPath = Path.Combine(root, "game") };
        var loc = new LocalizationViewModel();
        var vm = new ThunderstoreViewModel(loc, new(client, Path.Combine(root, "missing.json")), new(client, Path.Combine(root, "cache")),
            () => target, () => true, _ => Task.CompletedTask, () => Task.CompletedTask);
        await vm.ImportZipAsync(Path.Combine(root, "unused.zip"));
        Assert.Equal(loc["TsCatalogRequired"], vm.Error);
        Assert.False(vm.HasPlan);
    }

    private static ThunderstorePackage Package(string owner, string name, string version) => new()
    {
        Name = name, Owner = owner, FullName = owner + "-" + name,
        Versions = [new() { Name = name, FullName = owner + "-" + name + "-" + version, VersionNumber = version,
            FileSize = 100, DownloadUrl = $"https://thunderstore.io/package/download/{owner}/{name}/{version}/", Description = "Original author description" }]
    };
    private static ThunderstoreVersion Version(string owner, string name, string version) => new()
    {
        Name = name, FullName = $"{owner}-{name}-{version}", VersionNumber = version, FileSize = 100,
        DownloadUrl = $"https://thunderstore.io/package/download/{owner}/{name}/{version}/", Description = "Original author description"
    };
    private sealed class Handler(Action call) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { call(); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)); }
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

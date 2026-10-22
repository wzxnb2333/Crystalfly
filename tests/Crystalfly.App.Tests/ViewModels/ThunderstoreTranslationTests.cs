using System.Net;
using Crystalfly.App.ViewModels;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;
using Crystalfly.Core.Thunderstore;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class ThunderstoreTranslationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Crystalfly.Translation.Vm", Guid.NewGuid().ToString("N"));
    private static ThunderstorePackage Package(string owner = "Author") => new()
    {
        Name = "Test", Owner = owner, FullName = owner + "-Test", Categories = ["Utility"],
        Versions = [new() { Name = "Test", FullName = owner + "-Test-1.0.0", VersionNumber = "1.0.0", Description = "Original description",
            DownloadUrl = "https://thunderstore.io/package/download/" + owner + "/Test/1.0.0/", FileSize = 100 }]
    };
    private static SilksongModTranslationCatalog Translation() => new()
    {
        CategoryNames = new Dictionary<string, string> { ["Utility"] = "实用工具" },
        Mods = [new() { FullName = "Author-Test", DisplayName = "中文测试", SourceUrl = "https://thunderstore.io/c/hollow-knight-silksong/p/Author/Test/",
            Versions = [new() { Version = "1.0.0", Description = "自定义工具说明", DescriptionSha256 = SilksongModTranslationSource.Hash("Original description"),
                Documents = new Dictionary<string, SilksongModDocumentTranslation> { ["README.md"] = new()
                { SourceSha256 = SilksongModTranslationSource.Hash("# Original\n"), TranslationSha256 = SilksongModTranslationSource.Hash("# 中文文档\n") } } }] }]
    };

    [Fact]
    public async Task Chinese_search_and_language_switch_preserve_original_identity_version_and_filters()
    {
        var original = Package();
        using var client = new HttpClient(new Handler());
        var loc = new LocalizationViewModel();
        loc.Apply(UiLanguage.SimplifiedChinese);
        var vm = await CreateAsync(client, loc, [original, Package("Other")]);
        vm.SearchText = "中文测试";
        var result = Assert.Single(vm.Packages);
        Assert.Equal(original.FullName, result.FullName);
        Assert.Equal("Original description", result.Latest!.Description);
        Assert.Equal("中文测试", Assert.Single(vm.PackageDisplayItems).PrimaryName);
        vm.SelectedPackage = result;
        var version = vm.SelectedVersion;
        vm.Category = vm.Categories.Single(item => item.Value == "Utility");
        loc.Apply(UiLanguage.English);
        vm.RefreshLocalization();
        Assert.Equal("Test", Assert.Single(vm.PackageDisplayItems).PrimaryName);
        Assert.Same(result, vm.SelectedPackage);
        Assert.Same(version, vm.SelectedVersion);
        Assert.Equal("Utility", vm.Category?.Value);
        Assert.Equal("中文测试", vm.SearchText);
        vm.SearchText = "Other-Test";
        Assert.Equal("Test", Assert.Single(vm.PackageDisplayItems).PrimaryName);
        Assert.Equal("Other-Test", Assert.Single(vm.PackageDisplayItems).FullName);
    }

    [Fact]
    public async Task Version_or_description_changes_show_original_instead_of_reusing_stale_translation()
    {
        using var client = new HttpClient(new Handler());
        var loc = new LocalizationViewModel();
        loc.Apply(UiLanguage.SimplifiedChinese);
        var changed = Package() with { Versions = [Package().Versions[0] with { Description = "Updated text" }] };
        var vm = await CreateAsync(client, loc, [changed]);
        vm.SelectedPackage = Assert.Single(vm.Packages);
        Assert.Equal("Updated text", vm.SelectedDescription);
        Assert.NotEmpty(vm.TranslationNotice);
        vm.SelectedVersion = vm.SelectedVersion! with { VersionNumber = "0.9.0", Description = "Older text" };
        Assert.Equal("Older text", vm.SelectedDescription);
        Assert.NotEmpty(vm.TranslationNotice);
    }

    [Fact]
    public async Task Offline_document_switch_uses_translation_cache_without_installing_the_mod()
    {
        using var client = new HttpClient(new Handler());
        var loc = new LocalizationViewModel();
        loc.Apply(UiLanguage.SimplifiedChinese);
        var directory = Path.Combine(root, "translations", "documents", "Author-Test", "1.0.0");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "README.en.md"), "# Original\n");
        await File.WriteAllTextAsync(Path.Combine(directory, "README.zh-CN.md"), "# 中文文档\n");
        var vm = await CreateAsync(client, loc, [Package()]);
        vm.SelectedPackage = Assert.Single(vm.Packages);
        await vm.ReadDocumentCommand.ExecuteAsync("README.md");
        Assert.Null(vm.Error);
        Assert.Equal("# 中文文档\n", vm.Document);
        Assert.False(Directory.Exists(Path.Combine(root, "packages")));
        vm.SetDocumentLanguageCommand.Execute("original");
        Assert.Equal("# Original\n", vm.Document);
        loc.Apply(UiLanguage.English);
        vm.RefreshLocalization();
        Assert.Equal("# Original\n", vm.Document);
    }

    [Theory]
    [InlineData("package")]
    [InlineData("version")]
    [InlineData("game")]
    public async Task Delayed_document_cannot_replace_a_new_package_version_or_game(string change)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new HttpClient(new DelayedHandler(started, release));
        var loc = new LocalizationViewModel();
        loc.Apply(UiLanguage.SimplifiedChinese);
        var offline = true;
        var target = new InstanceRecord { GameId = GameId.Silksong, Id = "test", BuildId = "test", Name = "test", RootPath = root };
        var path = Path.Combine(root, "catalog.json");
        await AtomicJsonStore.WriteAsync(path, new ThunderstoreCatalog { Packages = [Package(), Package("Other")] });
        var vm = new ThunderstoreViewModel(loc, new(client, path), new(client, Path.Combine(root, "packages")),
            () => target, () => offline, _ => Task.CompletedTask, () => Task.CompletedTask,
            translationSource: new(client, Path.Combine(root, "translations"), baseline: Translation()));
        await vm.RefreshCommand.ExecuteAsync(null);
        vm.SelectedPackage = vm.Packages.Single(item => item.Owner == "Author");
        offline = false;
        var pending = vm.ReadDocumentCommand.ExecuteAsync("README.md");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (change == "package") vm.SelectedPackage = vm.Packages.Single(item => item.Owner == "Other");
        else if (change == "version") vm.SelectedVersion = vm.SelectedVersion! with { VersionNumber = "0.9.0" };
        else target = target with { GameId = GameId.HollowKnight };
        release.SetResult();
        await pending;
        Assert.Empty(vm.Document);
        Assert.Null(vm.Error);
        Assert.False(Directory.Exists(Path.Combine(root, "packages")));
    }

    [Fact]
    public async Task Installed_projection_and_uninstall_keep_the_canonical_package_identity()
    {
        using var client = new HttpClient(new Handler());
        var loc = new LocalizationViewModel();
        loc.Apply(UiLanguage.SimplifiedChinese);
        var vm = await CreateAsync(client, loc, [Package()]);
        var installed = new ThunderstoreInstalledPackage { FullName = "Author-Test", Version = "1.0.0", Enabled = true };
        vm.Installed.Add(installed);
        vm.InstalledMods.Add(installed);
        var display = Assert.Single(vm.InstalledDisplayItems);
        Assert.Equal("中文测试", display.PrimaryName);
        Assert.Same(installed, display.Package);
        vm.PendingUninstall = installed.FullName;
        Assert.Contains("中文测试", vm.PendingUninstallDisplay);
        Assert.Contains("Author-Test", vm.PendingUninstallDisplay);
        Assert.Equal("Author-Test", vm.PendingUninstall);
    }

    private async Task<ThunderstoreViewModel> CreateAsync(HttpClient client, LocalizationViewModel loc, ThunderstorePackage[] packages)
    {
        var path = Path.Combine(root, "catalog.json");
        await AtomicJsonStore.WriteAsync(path, new ThunderstoreCatalog { Packages = packages });
        var vm = new ThunderstoreViewModel(loc, new(client, path), new(client, Path.Combine(root, "packages")),
            () => null, () => true, _ => throw new InvalidOperationException("No install"), () => Task.CompletedTask,
            translationSource: new(client, Path.Combine(root, "translations"), baseline: Translation()));
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Null(vm.Error);
        return vm;
    }
    private sealed class Handler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => throw new InvalidOperationException("No network requests");
    }
    private sealed class DelayedHandler(TaskCompletionSource started, TaskCompletionSource release) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var original = request.RequestUri!.AbsolutePath.EndsWith(".en.md", StringComparison.Ordinal);
            if (original) { started.TrySetResult(); await release.Task.WaitAsync(token); }
            return new(HttpStatusCode.OK) { Content = new StringContent(original ? "# Original\n" : "# 中文文档\n") };
        }
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
}

using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;
using Crystalfly.Core.Thunderstore;

namespace Crystalfly.Core.Tests.Thunderstore;

public sealed partial class ThunderstoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "crystalfly-thunderstore-tests-" + Guid.NewGuid().ToString("N"));
    private static ThunderstorePackage Package(string name, string version = "1.0.0", params string[] dependencies) => new()
    {
        Owner = "Author", Name = name, FullName = "Author-" + name,
        Versions = [new() { Name = name, FullName = "Author-" + name + "-" + version, VersionNumber = version,
            Dependencies = dependencies, DownloadUrl = $"https://thunderstore.io/package/download/Author/{name}/{version}/", FileSize = 100 }]
    };
    private static ThunderstorePackage Loader => Package("BepInExPack_Silksong", "1.0.3") with
    {
        Owner = "silksong_modding", FullName = SilksongLoaderAdapter.PackageName,
        Versions = [new() { Name = "BepInExPack_Silksong", VersionNumber = "1.0.3",
            FullName = SilksongLoaderAdapter.PackageName + "-1.0.3",
            DownloadUrl = "https://thunderstore.io/package/download/silksong_modding/BepInExPack_Silksong/1.0.3/", FileSize = 694940 }]
    };
    private static ThunderstoreCatalog Catalog(params ThunderstorePackage[] packages) => new() { Packages = packages.Append(Loader).ToArray() };
    private ThunderstorePackageStore Store => new(new HttpClient(new Handler(_ => throw new InvalidOperationException("Unexpected network"))), Path.Combine(root, "cache"));
    private InstanceRecord Instance => new() { GameId = GameId.Silksong, Id = "test", Name = "Test", RootPath = Path.Combine(root, "game"), BuildId = "1.0.30000" };

    [Fact]
    public void Resolver_reuses_sufficient_installed_dependency_and_selects_latest_missing()
    {
        var dependency = Package("Dependency") with { Versions = [Package("Dependency").Versions[0], Package("Dependency", "2.0.0").Versions[0]] };
        var catalog = Catalog(Package("Main", "1.0.0", "Author-Dependency-1.0.0"), dependency);
        var resolver = new ThunderstoreDependencyResolver(catalog);
        var reused = resolver.Resolve(GameId.Silksong, "test", [("Author-Main", null)],
            [new() { FullName = "Author-Dependency", Version = "1.0.0" }]);
        Assert.Equal("1.0.0", reused.Packages.Single(item => item.Package.FullName == "Author-Dependency").Version.VersionNumber);
        var missing = resolver.Resolve(GameId.Silksong, "test", [("Author-Main", null)]);
        Assert.Equal("2.0.0", missing.Packages.Single(item => item.Package.FullName == "Author-Dependency").Version.VersionNumber);
        Assert.NotEmpty(missing.Warnings);
    }

    [Fact]
    public void Resolver_upgrades_insufficient_dependency_but_respects_lock()
    {
        var dependency = Package("Dependency") with { Versions = [Package("Dependency").Versions[0], Package("Dependency", "2.0.0").Versions[0]] };
        var resolver = new ThunderstoreDependencyResolver(Catalog(Package("Main", "1.0.0", "Author-Dependency-2.0.0"), dependency));
        ThunderstoreInstalledPackage old = new() { FullName = "Author-Dependency", Version = "1.0.0" };
        Assert.Equal("2.0.0", resolver.Resolve(GameId.Silksong, "test", [("Author-Main", null)], [old])
            .Packages.Single(item => item.Package.FullName == old.FullName).Version.VersionNumber);
        Assert.Throws<InvalidDataException>(() => resolver.Resolve(GameId.Silksong, "test", [("Author-Main", null)], [old with { Pinned = true }]));
    }

    [Fact]
    public void Modpack_pins_exact_dependencies_and_detects_conflicting_packs()
    {
        var dependency = Package("Dependency") with { Versions = [Package("Dependency").Versions[0], Package("Dependency", "2.0.0").Versions[0]] };
        var first = Package("Pack", "1.0.0", "Author-Dependency-1.0.0") with { Categories = ["Modpacks"] };
        var second = Package("OtherPack", "1.0.0", "Author-Dependency-2.0.0") with { Categories = ["Modpacks"] };
        var resolver = new ThunderstoreDependencyResolver(Catalog(first, second, dependency));
        Assert.Equal("1.0.0", resolver.Resolve(GameId.Silksong, "test", [(first.FullName, null)])
            .Packages.Single(item => item.Package.FullName == dependency.FullName).Version.VersionNumber);
        Assert.Throws<InvalidDataException>(() => resolver.Resolve(GameId.Silksong, "test", [(first.FullName, null), (second.FullName, null)]));
    }

    [Theory]
    [InlineData("5.4.2303")]
    [InlineData("5.4.2304")]
    [InlineData("5.4.75304")]
    public void Legacy_loader_dependency_resolves_to_one_approved_loader(string version)
    {
        var plan = new ThunderstoreDependencyResolver(Catalog(Package("Main", "1.0.0", "BepInEx-BepInExPack_Silksong-" + version)))
            .Resolve(GameId.Silksong, "test", [("Author-Main", null)]);
        Assert.Single(plan.Packages, item => SilksongLoaderAdapter.IsLoader(item.Package.FullName));
        Assert.DoesNotContain(plan.Packages, item => item.Package.FullName.StartsWith("BepInEx-", StringComparison.Ordinal));
        Assert.Equal("Similar-BepInEx-1.0.0", SilksongLoaderAdapter.CanonicalDependency("Similar-BepInEx-1.0.0"));
    }

    [Fact]
    public void Resolver_rejects_cross_game_missing_dependency_cycles_and_known_incompatibility()
    {
        var main = Package("Main", "1.0.0", "Author-Missing-1.0.0");
        Assert.Throws<InvalidDataException>(() => new ThunderstoreDependencyResolver(Catalog(main)).Resolve(GameId.HollowKnight, "test", [(main.FullName, null)]));
        Assert.Throws<InvalidDataException>(() => new ThunderstoreDependencyResolver(Catalog(main)).Resolve(GameId.Silksong, "test", [(main.FullName, null)]));
        var cyclic = Catalog(Package("Main", "1.0.0", "Author-Dep-1.0.0"), Package("Dep", "1.0.0", "Author-Main-1.0.0"));
        Assert.Throws<InvalidDataException>(() => new ThunderstoreDependencyResolver(cyclic).Resolve(GameId.Silksong, "test", [(main.FullName, null)]));
        var rules = new[] { new ThunderstoreCompatibilityRule(main.FullName, "1.0.0", "test", GameCompatibility.Incompatible, "Known conflict") };
        Assert.Throws<InvalidDataException>(() => new ThunderstoreDependencyResolver(Catalog(Package("Main")), rules).Resolve(GameId.Silksong, "test", [(main.FullName, null)]));
    }

    [Fact]
    public async Task Gzip_index_and_chunks_publish_complete_cache_and_offline_makes_no_request()
    {
        int requests = 0;
        var catalog = Catalog(Package("Main"));
        using var client = new HttpClient(new Handler(uri =>
        {
            requests++;
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(Gzip(uri.AbsoluteUri == ThunderstoreCatalogSource.IndexUrl
                ? JsonSerializer.SerializeToUtf8Bytes(new[] { "https://ccdn.thunderstore.io/chunk" })
                : JsonSerializer.SerializeToUtf8Bytes(catalog.Packages))) };
        }));
        var source = new ThunderstoreCatalogSource(client, Path.Combine(root, "catalog.json"));
        Assert.Equal(2, (await source.LoadAsync()).Packages.Count);
        Assert.Equal(2, requests);
        Assert.Equal(2, (await source.LoadAsync(offline: true)).Packages.Count);
        Assert.Equal(2, requests);
        Assert.True(source.UsingCache);
    }

    [Fact]
    public async Task Rate_limit_retains_complete_cache_and_obeys_retry_after()
    {
        var cache = Path.Combine(root, "catalog.json");
        await AtomicJsonStore.WriteAsync(cache, Catalog(Package("Previous")));
        int requests = 0;
        using var client = new HttpClient(new Handler(_ =>
        {
            requests++;
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new(TimeSpan.FromMinutes(1));
            return response;
        }));
        var source = new ThunderstoreCatalogSource(client, cache);
        Assert.Contains((await source.LoadAsync()).Packages, item => item.Name == "Previous");
        Assert.Contains((await source.LoadAsync()).Packages, item => item.Name == "Previous");
        Assert.Equal(1, requests);
        Assert.True(source.UsingCache);
    }

    [Theory]
    [InlineData("plugins/A.dll", "BepInEx/plugins/Author-Mod/A.dll")]
    [InlineData("BepInEx/patchers/A.dll", "BepInEx/patchers/Author-Mod/A.dll")]
    [InlineData("core/A.dll", "BepInEx/core/Author-Mod/A.dll")]
    [InlineData("A.mm.dll", "BepInEx/monomod/Author-Mod/A.mm.dll")]
    [InlineData("config/a.cfg", "BepInEx/config/a.cfg")]
    [InlineData("assets/sound.wav", "BepInEx/plugins/Author-Mod/assets/sound.wav")]
    public void Routes_special_directories_and_resources(string source, string expected) =>
        Assert.Equal(expected, ThunderstorePackageStore.MapPath("Author-Mod", source, false));

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("/absolute.txt")]
    [InlineData("a/../escape.txt")]
    [InlineData("C:/escape.txt")]
    [InlineData("a:stream")]
    [InlineData("plugins/CON.dll")]
    [InlineData("plugins/com1.txt")]
    [InlineData("plugins/trailing. ")]
    public async Task Archive_rejects_unsafe_paths(string entry)
    {
        var (path, package) = await ArchiveAsync("Bad", (entry, "bad"));
        await Assert.ThrowsAsync<InvalidDataException>(() => ThunderstorePackageStore.ValidateAsync(path, package));
    }

    [Fact]
    public async Task Multi_root_lifecycle_preserves_user_config_and_records_hashes()
    {
        var (path, package) = await ArchiveAsync("Main", ("patchers/a.txt", "patch"), ("assets/skin.txt", "asset"), ("config/main.cfg", "default"));
        var store = Store;
        await store.ImportLocalAsync(path, package);
        var service = new ThunderstoreInstallService(Instance, store, () => false);
        var plan = new ThunderstoreInstallPlan(GameId.Silksong, Instance.BuildId, [package], [package.Package.FullName]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.InstallAsync(plan));
        await service.InstallAsync(plan, offline: true, confirmUnverified: true);
        var installed = Assert.Single((await service.ReadStateAsync()).Packages);
        Assert.Equal(3, installed.Files.Count);
        var config = Path.Combine(Instance.RootPath, "BepInEx/config/main.cfg");
        await File.WriteAllTextAsync(config, "user");
        await service.SetEnabledAsync(installed.FullName, false);
        Assert.False(File.Exists(Path.Combine(Instance.RootPath, "BepInEx/patchers/Author-Main/a.txt")));
        Assert.Equal("user", await File.ReadAllTextAsync(config));
        await service.SetEnabledAsync(installed.FullName, true);
        Assert.Empty(await service.VerifyAsync());
        await service.SetPinnedAsync(installed.FullName, true);
        Assert.True(Assert.Single((await service.ReadStateAsync()).Packages).Pinned);
        await service.UninstallAsync(installed.FullName);
        Assert.Empty((await service.ReadStateAsync()).Packages);
        Assert.Equal("user", await File.ReadAllTextAsync(config));
    }

    [Fact]
    public async Task Conflict_in_last_package_leaves_entire_closure_uninstalled()
    {
        var (firstPath, first) = await ArchiveAsync("First", ("config/shared.cfg", "one"), ("asset.txt", "first"));
        var (secondPath, second) = await ArchiveAsync("Second", ("config/shared.cfg", "two"));
        var store = Store;
        await store.ImportLocalAsync(firstPath, first);
        await store.ImportLocalAsync(secondPath, second);
        var service = new ThunderstoreInstallService(Instance, store, () => false);
        await Assert.ThrowsAsync<InvalidDataException>(() => service.InstallAsync(new(GameId.Silksong, Instance.BuildId, [first, second], []), offline: true));
        Assert.Empty((await service.ReadStateAsync()).Packages);
        Assert.False(File.Exists(Path.Combine(Instance.RootPath, "BepInEx/plugins/Author-First/asset.txt")));
    }

    [Fact]
    public async Task Changed_archive_same_version_and_running_game_are_rejected()
    {
        var (path, package) = await ArchiveAsync("Main", ("asset.txt", "first"));
        var store = Store;
        await store.ImportLocalAsync(path, package);
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
        { zip.GetEntry("asset.txt")!.Delete(); using var writer = new StreamWriter(zip.CreateEntry("asset.txt").Open()); writer.Write("changed"); }
        await Assert.ThrowsAsync<InvalidDataException>(() => store.ImportLocalAsync(path, package));
        var service = new ThunderstoreInstallService(Instance, store, () => true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.InstallAsync(new(GameId.Silksong, Instance.BuildId, [package], []), offline: true));
        Assert.Empty((await service.ReadStateAsync()).Packages);
    }

    [Fact]
    public void Package_link_rejects_other_communities_and_dependency_parses_from_right()
    {
        Assert.Equal(("Team-With-Dashes-Mod", "1.2.3"), ThunderstoreIdentity.Dependency("Team-With-Dashes-Mod-1.2.3"));
        Assert.Equal(("Author-Mod", (string?)"1.2.3"), ThunderstoreIdentity.ParseLink("https://thunderstore.io/c/hollow-knight-silksong/p/Author/Mod/1.2.3/"));
        Assert.Throws<InvalidDataException>(() => ThunderstoreIdentity.ParseLink("https://thunderstore.io/c/other/p/Author/Mod/"));
    }

    [Fact]
    public async Task Package_version_icon_field_survives_catalog_parse_and_cache()
    {
        const string iconUrl = "https://ccdn.thunderstore.io/live/repository/icons/Author-Mod-1.0.0.png";
        var json = Encoding.UTF8.GetBytes("""[{"name":"Mod","owner":"Author","full_name":"Author-Mod","versions":[{"name":"Mod","full_name":"Author-Mod-1.0.0","version_number":"1.0.0","download_url":"https://thunderstore.io/package/download/Author/Mod/1.0.0/","file_size":100,"icon":"https://ccdn.thunderstore.io/live/repository/icons/Author-Mod-1.0.0.png"}]}]""");
        var package = Assert.Single(ThunderstoreCatalogSource.Parse(json));
        Assert.Equal(iconUrl, package.Latest!.IconUrl);
        var cachePath = Path.Combine(root, "icon-catalog.json");
        await AtomicJsonStore.WriteAsync(cachePath, new ThunderstoreCatalog { Packages = [package] });
        using var client = new HttpClient(new Handler(_ => throw new InvalidOperationException("Offline cache must not request.")));
        var catalog = await new ThunderstoreCatalogSource(client, cachePath).LoadAsync(offline: true);
        Assert.Equal(iconUrl, Assert.Single(catalog.Packages).Latest!.IconUrl);
    }

    private Task<(string Path, ThunderstoreResolvedPackage Package)> ArchiveAsync(string name, params (string Path, string Content)[] files) =>
        ArchiveVersionAsync(name, "1.0.0", files);

    private async Task<(string Path, ThunderstoreResolvedPackage Package)> ArchiveVersionAsync(string name, string version, params (string Path, string Content)[] files)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, name + "-" + version + ".zip");
        var package = Package(name, version);
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            await using (var stream = archive.CreateEntry("manifest.json").Open())
                await JsonSerializer.SerializeAsync(stream, new ThunderstoreArchiveManifest { Name = name, Version = version });
            foreach (var file in files)
            { await using var writer = new StreamWriter(archive.CreateEntry(file.Path).Open()); await writer.WriteAsync(file.Content); }
        }
        return (path, new(package, package.Versions[0] with { FileSize = new FileInfo(path).Length }));
    }
    private static byte[] Gzip(byte[] bytes)
    {
        using var stream = new MemoryStream();
        using (var gzip = new GZipStream(stream, CompressionLevel.Fastest, true)) gzip.Write(bytes);
        return stream.ToArray();
    }
    private sealed class Handler(Func<Uri, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response(request.RequestUri!));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

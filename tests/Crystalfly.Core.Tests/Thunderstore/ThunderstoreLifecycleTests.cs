using System.Net;
using System.Text.Json;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;
using Crystalfly.Core.Thunderstore;
using Crystalfly.Core.Transactions;

namespace Crystalfly.Core.Tests.Thunderstore;

public sealed partial class ThunderstoreTests
{
    [Fact]
    public async Task Redirect_is_rejected_before_contacting_an_untrusted_origin()
    {
        var requests = new List<Uri>();
        using var client = new HttpClient(new ThunderstoreHttpHandler(new Handler(uri =>
        {
            requests.Add(uri);
            var response = new HttpResponseMessage(HttpStatusCode.Redirect);
            response.Headers.Location = new Uri("https://untrusted.example/package.zip");
            return response;
        })));
        await Assert.ThrowsAsync<InvalidDataException>(() => client.GetAsync(ThunderstoreCatalogSource.IndexUrl));
        Assert.Equal("thunderstore.io", Assert.Single(requests).Host);
    }

    [Fact]
    public async Task Semantic_cache_corruption_uses_last_complete_backup_without_network()
    {
        var path = Path.Combine(root, "catalog.json");
        await AtomicJsonStore.WriteAsync(path, Catalog(Package("Previous")));
        File.Copy(path, path + ".bak", overwrite: true);
        await File.WriteAllTextAsync(path, "{}");
        using var client = new HttpClient(new Handler(_ => throw new InvalidOperationException("No network")));
        var catalog = await new ThunderstoreCatalogSource(client, path).LoadAsync(offline: true);
        Assert.Contains(catalog.Packages, item => item.Name == "Previous");
    }

    [Fact]
    public async Task Unsupported_chunk_index_falls_back_to_community_v1()
    {
        var requests = new List<Uri>();
        using var client = new HttpClient(new Handler(uri =>
        {
            requests.Add(uri);
            return uri.AbsoluteUri == ThunderstoreCatalogSource.IndexUrl
                ? new(HttpStatusCode.NotFound)
                : new(HttpStatusCode.OK) { Content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(Catalog(Package("Fallback")).Packages)) };
        }));
        var catalog = await new ThunderstoreCatalogSource(client, Path.Combine(root, "catalog.json")).LoadAsync();
        Assert.Contains(catalog.Packages, item => item.Name == "Fallback");
        Assert.Equal(ThunderstoreCatalogSource.LegacyUrl, requests[1].AbsoluteUri);
    }

    [Fact]
    public void Large_resource_metadata_does_not_invalidate_a_complete_catalog()
    {
        var resource = Package("Resource");
        ThunderstoreCatalogSource.Validate(Catalog(resource with
        {
            Versions = [resource.Versions[0] with { FileSize = 1_300_000_000 }]
        }));
        Assert.Throws<InvalidDataException>(() => ThunderstoreCatalogSource.Validate(Catalog(resource with
        {
            Versions = [resource.Versions[0] with { FileSize = 17L * 1024 * 1024 * 1024 }]
        })));
    }

    [Fact]
    public async Task Upgrade_downgrade_repair_and_lock_preserve_configuration_and_disabled_state()
    {
        var (firstPath, first) = await ArchiveVersionAsync("Main", "1.0.0", ("patchers/old.txt", "old"), ("config/main.cfg", "default"));
        var (secondPath, second) = await ArchiveVersionAsync("Main", "2.0.0", ("patchers/new.txt", "new"), ("config/main.cfg", "changed default"));
        var store = Store;
        await store.ImportLocalAsync(firstPath, first);
        await store.ImportLocalAsync(secondPath, second);
        var service = new ThunderstoreInstallService(Instance, store, () => false);
        ThunderstoreInstallPlan Plan(ThunderstoreResolvedPackage item) => new(GameId.Silksong, Instance.BuildId, [item], []);
        await service.InstallAsync(Plan(first), offline: true);
        var config = Path.Combine(Instance.RootPath, "BepInEx/config/main.cfg");
        await File.WriteAllTextAsync(config, "user setting");
        await service.SetEnabledAsync(first.Package.FullName, false);
        await service.InstallAsync(Plan(second with { PreviousVersion = "1.0.0" }), offline: true);
        Assert.False(Assert.Single((await service.ReadStateAsync()).Packages).Enabled);
        Assert.False(File.Exists(Path.Combine(Instance.RootPath, "BepInEx/patchers/Author-Main/new.txt")));
        await service.SetEnabledAsync(first.Package.FullName, true);
        var payload = Path.Combine(Instance.RootPath, "BepInEx/patchers/Author-Main/new.txt");
        Assert.Equal("new", await File.ReadAllTextAsync(payload));
        Assert.False(File.Exists(Path.Combine(Instance.RootPath, "BepInEx/patchers/Author-Main/old.txt")));
        await File.WriteAllTextAsync(payload, "damaged");
        Assert.NotEmpty(await service.VerifyAsync());
        await service.InstallAsync(Plan(second with { PreviousVersion = "2.0.0" }), offline: true, repair: true);
        Assert.Empty(await service.VerifyAsync());
        await service.InstallAsync(Plan(first with { PreviousVersion = "2.0.0" }), offline: true);
        Assert.False(File.Exists(payload));
        Assert.Equal("user setting", await File.ReadAllTextAsync(config));
        await service.SetPinnedAsync(first.Package.FullName, true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.InstallAsync(Plan(second with { PreviousVersion = "1.0.0" }), offline: true));
        Assert.Equal("1.0.0", Assert.Single((await service.ReadStateAsync()).Packages).Version);
    }

    [Fact]
    public async Task Cancelled_install_and_stale_preview_leave_existing_files_unchanged()
    {
        var (path, package) = await ArchiveAsync("Main", ("asset.txt", "one"));
        var store = Store;
        await store.ImportLocalAsync(path, package);
        var service = new ThunderstoreInstallService(Instance, store, () => false);
        var plan = new ThunderstoreInstallPlan(GameId.Silksong, Instance.BuildId, [package], []);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.InstallAsync(plan, offline: true, token: cancellation.Token));
        Assert.Empty((await service.ReadStateAsync()).Packages);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.InstallAsync(plan with
        {
            Packages = [package with { PreviousVersion = "2.0.0" }]
        }, offline: true));
        Assert.Empty((await service.ReadStateAsync()).Packages);
    }

    [Fact]
    public async Task Tampered_receipt_cannot_remove_files_outside_managed_directories()
    {
        var store = Store;
        var service = new ThunderstoreInstallService(Instance, store, () => false);
        var receipt = Path.Combine(Instance.RootPath, ThunderstoreInstallService.MetadataDirectory, "installed.json");
        await AtomicJsonStore.WriteAsync(receipt, new ThunderstoreInstallState
        {
            Packages = [new() { FullName = "Author-Main", Version = "1.0.0", ArchiveSha256 = new string('A', 64),
                Files = [new("BepInEx/plugins/Author-Main/../../../user1.dat", new string('B', 64))] }]
        });
        await Assert.ThrowsAsync<InvalidDataException>(() => service.UninstallAsync("Author-Main"));
    }

    [Fact]
    public async Task Previous_loader_receipt_root_doorstop_config_is_normalized_as_configuration()
    {
        var instance = Instance;
        Directory.CreateDirectory(instance.RootPath);
        var store = Store;
        var service = new ThunderstoreInstallService(instance, store, () => false);
        await AtomicJsonStore.WriteAsync(Path.Combine(instance.RootPath, ThunderstoreInstallService.MetadataDirectory, "installed.json"),
            new ThunderstoreInstallState
            {
                Packages = [new ThunderstoreInstalledPackage
                {
                    FullName = SilksongLoaderAdapter.PackageName,
                    Version = SilksongLoaderAdapter.PackageVersion,
                    ArchiveSha256 = SilksongLoaderAdapter.Sha256,
                    IsLoader = true,
                    RuntimeVersion = SilksongLoaderAdapter.RuntimeVersion,
                    Files = [new("doorstop_config.ini", new string('A', 64), IsConfiguration: false)]
                }]
            });

        var state = await service.ReadStateAsync();

        Assert.True(Assert.Single(Assert.Single(state.Packages).Files).IsConfiguration);
        Assert.Empty(await service.VerifyAsync());
    }

    [Fact]
    public async Task Uninstall_refuses_to_overwrite_a_new_file_at_a_previous_loader_backup_path()
    {
        var instance = Instance;
        Directory.CreateDirectory(instance.RootPath);
        var store = Store;
        var service = new ThunderstoreInstallService(instance, store, () => false);
        var packageName = SilksongLoaderAdapter.PackageName;
        const string previousFile = "BepInEx/core/BepInEx.dll";
        var backupPath = $"{ThunderstoreInstallService.MetadataDirectory}/loader-backup/{packageName}/{previousFile}";
        var backup = Path.Combine(instance.RootPath, backupPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
        await File.WriteAllTextAsync(backup, "previous loader bytes");
        var target = Path.Combine(instance.RootPath, previousFile.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await File.WriteAllTextAsync(target, "new user file");
        await AtomicJsonStore.WriteAsync(Path.Combine(instance.RootPath, ThunderstoreInstallService.MetadataDirectory, "installed.json"),
            new ThunderstoreInstallState
            {
                Packages = [new ThunderstoreInstalledPackage
                {
                    FullName = packageName,
                    Version = SilksongLoaderAdapter.PackageVersion,
                    ArchiveSha256 = SilksongLoaderAdapter.Sha256,
                    IsLoader = true,
                    RuntimeVersion = SilksongLoaderAdapter.RuntimeVersion,
                    LoaderBackups = [new(previousFile, backupPath, await ThunderstorePackageStore.HashAsync(backup))]
                }]
            });

        await Assert.ThrowsAsync<InvalidDataException>(() => service.UninstallAsync(packageName));

        Assert.Equal("new user file", await File.ReadAllTextAsync(target));
        Assert.Equal("previous loader bytes", await File.ReadAllTextAsync(backup));
        Assert.Single((await service.ReadStateAsync()).Packages);
    }

    [Fact]
    public async Task Local_package_metadata_survives_restart_for_offline_resolution()
    {
        var (path, package) = await ArchiveAsync("Local", ("asset.txt", "asset"));
        var store = Store;
        await store.ImportLocalAsync(path, package);
        var service = new ThunderstoreInstallService(Instance, store, () => false);
        await service.InstallAsync(new(GameId.Silksong, Instance.BuildId, [package with { IsLocal = true }], []), offline: true);
        var installed = Assert.Single((await new ThunderstoreInstallService(Instance, store).ReadStateAsync()).Packages);
        Assert.True(installed.IsLocal);
        Assert.Equal(package.Package.FullName, installed.LocalPackage?.FullName);
        var plan = new ThunderstoreDependencyResolver(Catalog(Package("Another")))
            .Resolve(GameId.Silksong, Instance.BuildId, [(installed.FullName, installed.Version)], [installed]);
        Assert.True(plan.Packages.Single(item => item.Package.FullName == installed.FullName).IsLocal);
    }

    [Fact]
    public async Task Failed_update_restores_receipt_and_all_payloads_after_file_lock_is_released()
    {
        var (firstPath, first) = await ArchiveVersionAsync("Main", "1.0.0", ("a.txt", "old-a"), ("z.txt", "old-z"));
        var (secondPath, second) = await ArchiveVersionAsync("Main", "2.0.0", ("a.txt", "new-a"), ("z.txt", "new-z"));
        var store = Store;
        await store.ImportLocalAsync(firstPath, first);
        await store.ImportLocalAsync(secondPath, second);
        var service = new ThunderstoreInstallService(Instance, store, () => false);
        await service.InstallAsync(new(GameId.Silksong, Instance.BuildId, [first], []), offline: true);
        var receipt = Path.Combine(Instance.RootPath, ThunderstoreInstallService.MetadataDirectory, "installed.json");
        var before = await File.ReadAllBytesAsync(receipt);
        var files = Path.Combine(Instance.RootPath, "BepInEx/plugins/Author-Main");
        using (var locked = new FileStream(Path.Combine(files, "z.txt"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await Assert.ThrowsAnyAsync<IOException>(() => service.InstallAsync(new(GameId.Silksong, Instance.BuildId,
                [second with { PreviousVersion = "1.0.0" }], []), offline: true));
        }
        var recoveries = await FileTransaction.RecoverPendingAsync(Path.Combine(root, ".crystalfly/transactions"));
        Assert.DoesNotContain(recoveries, journal => journal.State == TransactionState.NeedsAttention);
        Assert.Equal(before, await File.ReadAllBytesAsync(receipt));
        Assert.Equal("old-a", await File.ReadAllTextAsync(Path.Combine(files, "a.txt")));
        Assert.Equal("old-z", await File.ReadAllTextAsync(Path.Combine(files, "z.txt")));
        Assert.Empty(await service.VerifyAsync());
    }
}

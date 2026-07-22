using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Tests.Mods;

public sealed class ModRepairAndLifecycleTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(), $"crystalfly-mod-lifecycle-{Guid.NewGuid():N}");

    [Fact]
    public async Task Pin_blocks_direct_uninstall_and_batch_uninstall_skips_pinned_mods()
    {
        var manager = CreateManager();
        var firstPackage = CreateZip(("first.dll", "first"));
        var secondPackage = CreateZip(("second.dll", "second"));
        await manager.InstallFromFileAsync(Manifest("first", "First", firstPackage), firstPackage);
        await manager.InstallFromFileAsync(Manifest("second", "Second", secondPackage), secondPackage);

        var pinned = await manager.SetPinnedAsync("first", pinned: true);

        Assert.True(pinned.Pinned);
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.UninstallAsync("first"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.UpdateFromFileAsync(
            Manifest("first", "First", firstPackage) with { Version = "2.0" },
            firstPackage));
        var result = await manager.UninstallBatchAsync(["first", "second"]);
        Assert.Equal(["second"], result.RemovedModIds);
        Assert.Equal(["first"], result.SkippedPinnedModIds);
        Assert.Equal("first", Assert.Single(await manager.GetInstalledAsync()).Id);
        Assert.False((await manager.SetPinnedAsync("first", pinned: false)).Pinned);
        await manager.UninstallAsync("first");
    }

    [Fact]
    public async Task Uninstall_suggests_only_unused_managed_unpinned_dependencies()
    {
        var manager = CreateManager();
        await AddReceiptAsync(Receipt("unused"));
        await AddReceiptAsync(Receipt("pinned") with { Pinned = true });
        await AddReceiptAsync(Receipt("local") with
        {
            IsLocal = true,
            Ownership = ModOwnership.LocalTakenOver
        });
        await AddReceiptAsync(Receipt("shared"));
        await AddReceiptAsync(Receipt("other", ["shared"]));
        await AddReceiptAsync(Receipt(
            "application", ["unused", "pinned", "local", "shared"]));

        var result = await manager.UninstallWithSuggestionsAsync("application");

        Assert.Equal("application", result.RemovedModId);
        Assert.Equal(["unused"], result.UnusedDependencies.Select(mod => mod.Id));
        Assert.True(File.Exists(Path.Combine(
            InstanceRoot,
            "hollow_knight_Data",
            "Managed",
            "Mods",
            "unused",
            "unused.dll")));
    }

    [Fact]
    public async Task Repair_plan_distinguishes_exact_repair_from_catalog_update()
    {
        var package = CreateZip(("mod.dll", "official"));
        var exact = Manifest("test", "Test", package);
        var manager = CreateManager();
        await manager.InstallFromFileAsync(exact, package);

        var repair = await manager.GetRepairPlanAsync("test", [exact]);
        var update = await manager.GetRepairPlanAsync(
            "test", [exact with { Version = "2.0", Sha256 = new string('B', 64) }]);
        var older = await manager.GetRepairPlanAsync(
            "test", [exact with { Version = "0.9", Sha256 = new string('C', 64) }]);

        Assert.Equal(ModRepairAction.Repair, repair.Action);
        Assert.Equal("1.0", repair.TargetVersion);
        Assert.Equal(ModRepairAction.Update, update.Action);
        Assert.Equal("2.0", update.TargetVersion);
        Assert.Equal(ModRepairAction.Unavailable, older.Action);

        await manager.SetPinnedAsync("test", pinned: true);
        var pinned = await manager.GetRepairPlanAsync("test", [exact]);
        Assert.Equal(ModRepairAction.Unavailable, pinned.Action);
    }

    [Fact]
    public async Task Repair_reuses_verified_package_cache_and_restores_drifted_official_file()
    {
        var package = CreateZip(("mod.dll", "official"));
        var bytes = await File.ReadAllBytesAsync(package);
        var handler = new SingleResponseHandler(bytes);
        using var httpClient = new HttpClient(handler);
        var manager = CreateManager(Path.Combine(root, "cache"), httpClient);
        var manifest = Manifest("test", "Test", package);
        await manager.InstallFromUriAsync(manifest);
        await manager.SetEnabledAsync("test", enabled: false);
        var installedPath = Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "Mods", "Disabled", "Test", "mod.dll");
        await File.WriteAllTextAsync(installedPath, "drifted");

        var repaired = await manager.RepairFromUriAsync(manifest);

        Assert.Equal("official", await File.ReadAllTextAsync(installedPath));
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(ModOwnership.Managed, repaired.Ownership);
        Assert.False(repaired.Enabled);
    }

    [Fact]
    public async Task Local_accept_rebuilds_receipt_and_reimport_replaces_current_files()
    {
        var manager = CreateManager();
        var package = CreateZip(("local.dll", "old"));
        await manager.ImportLocalZipAsync("local", "Local", "modding-api-77", package);
        var installRoot = Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "Mods", "Local");
        var localDll = Path.Combine(installRoot, "local.dll");
        await File.WriteAllTextAsync(localDll, "accepted");
        await File.WriteAllTextAsync(Path.Combine(installRoot, "settings.json"), "{}");

        var accepted = await manager.AcceptCurrentLocalFilesAsync("local");

        Assert.Equal(2, accepted.Files.Count);
        Assert.Equal(
            ModHealthStatus.Healthy,
            (await new ModHealthService(InstanceRoot).AssessAsync(accepted, [accepted])).Status);

        var replacement = CreateZip(("local.dll", "new"), ("new.txt", "new"));
        var reimported = await manager.ReimportLocalZipAsync("local", replacement);

        Assert.Equal(ModOwnership.LocalTakenOver, reimported.Ownership);
        Assert.Equal("new", await File.ReadAllTextAsync(localDll));
        Assert.True(File.Exists(Path.Combine(installRoot, "new.txt")));
        Assert.False(File.Exists(Path.Combine(installRoot, "settings.json")));
        await manager.RemoveLocalAsync("local");
        Assert.Empty(await manager.GetInstalledAsync());
    }

    [Fact]
    public async Task Local_accept_rejects_dot_segment_additions_outside_the_owned_root()
    {
        var manager = CreateManager();
        var package = CreateZip(("local.dll", "local"));
        await manager.ImportLocalZipAsync("local", "Local", "modding-api-77", package);
        var outside = Path.Combine(InstanceRoot, "hollow_knight_Data", "Managed", "outside.dll");
        await File.WriteAllTextAsync(outside, "outside");

        await Assert.ThrowsAsync<InvalidDataException>(() => manager.AcceptCurrentLocalFilesAsync(
            "local",
            ["hollow_knight_Data/Managed/Mods/Local/../../outside.dll"]));

        Assert.Equal("outside", await File.ReadAllTextAsync(outside));
        Assert.Single((await manager.GetInstalledAsync()).Single().Files);
    }

    [Theory]
    [InlineData("hollow_knight_Data/Managed/Mods", "modding-api-60", true)]
    [InlineData("hollow_knight_Data/Managed/Mods/Disabled", "modding-api-60", false)]
    [InlineData("BepInEx/plugins", "bepinex-5.4.23.4", true)]
    [InlineData("BepInEx/Disabled", "bepinex-5.4.23.4", false)]
    public async Task Local_accept_in_shared_roots_uses_only_receipt_and_explicit_additions(
        string sharedRoot,
        string loaderId,
        bool enabled)
    {
        var manager = CreateManager();
        var ownedPath = $"{sharedRoot}/owned.dll";
        var additionalPath = $"{sharedRoot}/additional.dll";
        await WriteInstanceFileAsync(ownedPath, "owned");
        await WriteInstanceFileAsync(additionalPath, "additional");
        var external = new ModDiscoveryEntry
        {
            Id = "owned",
            Name = "Owned",
            LoaderId = loaderId,
            InstallRoot = sharedRoot,
            Enabled = enabled,
            Ownership = ModOwnership.External,
            Files = [ownedPath],
            EntryFiles = [ownedPath]
        };
        await manager.TakeOverAsync("owned", external);

        var accepted = await manager.AcceptCurrentLocalFilesAsync("owned");

        Assert.Equal([ownedPath], accepted.Files.Select(file => file.RelativePath));

        accepted = await manager.AcceptCurrentLocalFilesAsync("owned", [additionalPath]);

        Assert.Equal(
            [additionalPath, ownedPath],
            accepted.Files.Select(file => file.RelativePath).Order(StringComparer.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemoveLocal_tolerates_modified_and_missing_files(bool missing)
    {
        var manager = CreateManager();
        var package = CreateZip(("local.dll", "local"));
        await manager.ImportLocalZipAsync("local", "Local", "modding-api-77", package);
        var installedPath = Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "Mods", "Local", "local.dll");
        if (missing)
        {
            File.Delete(installedPath);
        }
        else
        {
            await File.WriteAllTextAsync(installedPath, "modified");
        }

        await manager.RemoveLocalAsync("local");

        Assert.False(File.Exists(installedPath));
        Assert.Empty(await manager.GetInstalledAsync());
    }

    [Fact]
    public async Task User_confirmed_uninstall_routes_local_receipts_through_drift_tolerant_removal()
    {
        var manager = CreateManager();
        var package = CreateZip(("local.dll", "local"));
        await manager.ImportLocalZipAsync("local", "Local", "modding-api-77", package);
        var installedPath = Path.Combine(
            InstanceRoot,
            "hollow_knight_Data",
            "Managed",
            "Mods",
            "Local",
            "local.dll");
        await File.WriteAllTextAsync(installedPath, "modified");

        await manager.UninstallIgnoringDependentsAsync("local");

        Assert.False(File.Exists(installedPath));
        Assert.Empty(await manager.GetInstalledAsync());
    }

    [Fact]
    public async Task RemoveLocal_rejects_dot_segment_receipt_paths_outside_the_owned_root()
    {
        var manager = CreateManager();
        var outsidePath = "hollow_knight_Data/Managed/Mods/Local/../../outside.dll";
        await AddReceiptAsync(new InstalledModReceipt
        {
            Id = "local",
            Name = "Local",
            Version = "local",
            LoaderId = "modding-api-77",
            InstallRoot = "hollow_knight_Data/Managed/Mods/Local",
            IsLocal = true,
            Ownership = ModOwnership.LocalTakenOver,
            Files =
            [
                new InstalledFileReceipt
                {
                    RelativePath = outsidePath,
                    Sha256 = new string('0', 64)
                }
            ]
        });
        var resolvedOutside = Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "outside.dll");

        await Assert.ThrowsAsync<InvalidDataException>(() => manager.RemoveLocalAsync("local"));

        Assert.True(File.Exists(resolvedOutside));
        await Assert.ThrowsAsync<InvalidDataException>(() => manager.GetInstalledAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private string InstanceRoot => Path.Combine(root, "instance");

    private ModManager CreateManager(string? cacheRoot = null, HttpClient? httpClient = null)
    {
        Directory.CreateDirectory(InstanceRoot);
        return new ModManager(
            InstanceRoot,
            Path.Combine(root, "transactions"),
            Path.Combine(root, "state", "mods"),
            cacheRoot,
            httpClient);
    }

    private async Task AddReceiptAsync(InstalledModReceipt receipt)
    {
        var relativePath = receipt.Files[0].RelativePath;
        var path = Path.Combine(InstanceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, receipt.Id);
        var completed = receipt with
        {
            Files = [receipt.Files[0] with { Sha256 = FileSha256(path) }]
        };
        var receiptsRoot = Path.Combine(root, "state", "mods");
        Directory.CreateDirectory(receiptsRoot);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(receipt.Id)));
        await AtomicJsonStore.WriteAsync(Path.Combine(receiptsRoot, $"{hash}.json"), completed);
    }

    private async Task WriteInstanceFileAsync(string relativePath, string content)
    {
        var path = Path.Combine(InstanceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
    }

    private static InstalledModReceipt Receipt(
        string id,
        IReadOnlyList<string>? dependencies = null) => new()
        {
            Id = id,
            Name = id,
            Version = "1.0",
            LoaderId = "modding-api-77",
            InstallRoot = $"hollow_knight_Data/Managed/Mods/{id}",
            Dependencies = dependencies ?? [],
            Ownership = ModOwnership.Managed,
            Files =
            [
                new InstalledFileReceipt
                {
                    RelativePath = $"hollow_knight_Data/Managed/Mods/{id}/{id}.dll",
                    Sha256 = new string('0', 64)
                }
            ],
            EntryFiles = [$"hollow_knight_Data/Managed/Mods/{id}/{id}.dll"]
        };

    private string CreateZip(params (string Name, string Content)[] entries)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, $"{Guid.NewGuid():N}.zip");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var item in entries)
        {
            var entry = archive.CreateEntry(item.Name);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8, leaveOpen: false);
            writer.Write(item.Content);
        }
        return path;
    }

    private static ModManifest Manifest(string id, string name, string package) => new()
    {
        Id = id,
        Name = name,
        Version = "1.0",
        DownloadUrl = "https://example.invalid/mod.zip",
        SizeBytes = new FileInfo(package).Length,
        Sha256 = FileSha256(package),
        LoaderId = "modding-api-77"
    };

    private static string FileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private sealed class SingleResponseHandler(byte[] content) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            if (RequestCount > 1)
            {
                throw new HttpRequestException("Verified package cache was not reused.");
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
            });
        }
    }
}

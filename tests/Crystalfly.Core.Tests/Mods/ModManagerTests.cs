using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;

namespace Crystalfly.Core.Tests.Mods;

public sealed class ModManagerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Crystalfly.Tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("modding-api-37", "hollow_knight_Data/Managed/Mods/mod.dll")]
    [InlineData("modding-api-60", "hollow_knight_Data/Managed/Mods/mod.dll")]
    [InlineData("modding-api-77", "hollow_knight_Data/Managed/Mods/TestMod/folder/mod.dll")]
    [InlineData("bepinex-5.4.23.4", "BepInEx/plugins/TestMod/folder/mod.dll")]
    public async Task Install_uses_loader_specific_layout(string loaderId, string expectedPath)
    {
        var manager = CreateManager();
        var package = CreateZip(("folder/mod.dll", "mod"));

        var receipt = await manager.InstallFromFileAsync(Manifest("test", "TestMod", loaderId, package), package);

        Assert.True(receipt.Enabled);
        Assert.False(receipt.IsLocal);
        Assert.True(File.Exists(Path.Combine(InstanceRoot, expectedPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.Single(await manager.GetInstalledAsync());
    }

    [Fact]
    public async Task Disable_and_enable_move_mod_files_out_of_the_loader_scan_path()
    {
        var manager = CreateManager();
        var package = CreateZip(("mod.dll", "mod"));
        await manager.InstallFromFileAsync(Manifest("test", "TestMod", "modding-api-77", package), package);

        var disabled = await manager.SetEnabledAsync("test", enabled: false);

        Assert.False(disabled.Enabled);
        Assert.True(File.Exists(Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "Mods", "Disabled", "TestMod", "mod.dll")));
        Assert.False(File.Exists(Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "Mods", "TestMod", "mod.dll")));

        var enabled = await manager.SetEnabledAsync("test", enabled: true);

        Assert.True(enabled.Enabled);
        Assert.True(File.Exists(Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "Mods", "TestMod", "mod.dll")));
    }

    [Fact]
    public async Task Uninstall_blocks_reverse_dependencies_and_drifted_files()
    {
        var manager = CreateManager();
        var basePackage = CreateZip(("base.dll", "base"));
        await manager.InstallFromFileAsync(Manifest("base", "Base", "modding-api-77", basePackage), basePackage);
        var dependentPackage = CreateZip(("dependent.dll", "dependent"));
        await manager.InstallFromFileAsync(
            Manifest("dependent", "Dependent", "modding-api-77", dependentPackage, "base"),
            dependentPackage);

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.UninstallAsync("base"));
        await manager.UninstallAsync("dependent");
        var basePath = Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "Mods", "Base", "base.dll");
        await File.WriteAllTextAsync(basePath, "changed");

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.UninstallAsync("base"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.SetEnabledAsync("base", false));
    }

    [Fact]
    public async Task Install_rejects_missing_dependencies_before_writing_files()
    {
        var manager = CreateManager();
        var package = CreateZip(("mod.dll", "mod"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.InstallFromFileAsync(
            Manifest("dependent", "Dependent", "modding-api-77", package, "missing"), package));

        Assert.Empty(await manager.GetInstalledAsync());
    }

    [Fact]
    public async Task Local_zip_and_dll_imports_are_marked_local()
    {
        var manager = CreateManager();
        var package = CreateZip(("zip.dll", "zip"));
        var zipReceipt = await manager.ImportLocalZipAsync(
            "local-zip", "LocalZip", "modding-api-77", package);
        var flatPackage = CreateZip(("flat.dll", "flat"), ("readme.txt", "ignore"));
        var flatReceipt = await manager.ImportLocalZipAsync(
            "local-flat", "LocalFlat", "modding-api-60", flatPackage);
        var dll = Path.Combine(_root, "single.dll");
        await File.WriteAllTextAsync(dll, "dll");

        var dllReceipt = await manager.ImportLocalDllAsync(
            "local-dll", "LocalDll", "bepinex-5.4.23.4", dll);

        Assert.True(zipReceipt.IsLocal);
        Assert.True(flatReceipt.IsLocal);
        Assert.True(dllReceipt.IsLocal);
        Assert.True(File.Exists(Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "Mods", "flat.dll")));
        Assert.False(File.Exists(Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "Mods", "readme.txt")));
        Assert.True(File.Exists(Path.Combine(InstanceRoot, "BepInEx", "plugins", "LocalDll", "single.dll")));
    }

    [Fact]
    public async Task Receipt_write_failure_removes_new_mod_files()
    {
        Directory.CreateDirectory(Path.Combine(_root, "state"));
        await File.WriteAllTextAsync(Path.Combine(_root, "state", "mods"), "blocks receipt directory");
        var manager = CreateManager();
        var package = CreateZip(("mod.dll", "mod"));

        await Assert.ThrowsAnyAsync<IOException>(() =>
            manager.InstallFromFileAsync(Manifest("test", "TestMod", "modding-api-77", package), package));

        Assert.False(File.Exists(Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "Mods", "TestMod", "mod.dll")));
    }

    [Fact]
    public async Task Update_replaces_files_and_receipt_atomically()
    {
        var manager = CreateManager();
        var oldPackage = CreateZip(("mod.dll", "old"), ("removed.txt", "remove"));
        await manager.InstallFromFileAsync(
            Manifest("test", "TestMod", "modding-api-77", oldPackage), oldPackage);
        var newPackage = CreateZip(("mod.dll", "new"), ("added.txt", "add"));

        var updated = await manager.UpdateFromFileAsync(
            Manifest("test", "TestMod", "modding-api-77", newPackage) with { Version = "2.0" },
            newPackage);

        var installRoot = Path.Combine(InstanceRoot, "hollow_knight_Data", "Managed", "Mods", "TestMod");
        Assert.Equal("2.0", updated.Version);
        Assert.Equal("new", await File.ReadAllTextAsync(Path.Combine(installRoot, "mod.dll")));
        Assert.True(File.Exists(Path.Combine(installRoot, "added.txt")));
        Assert.False(File.Exists(Path.Combine(installRoot, "removed.txt")));
        Assert.Equal("2.0", Assert.Single(await manager.GetInstalledAsync()).Version);
    }

    [Fact]
    public async Task Update_rejects_a_loader_change_before_writing_files()
    {
        var manager = CreateManager();
        var oldPackage = CreateZip(("mod.dll", "old"));
        await manager.InstallFromFileAsync(
            Manifest("test", "TestMod", "modding-api-77", oldPackage), oldPackage);
        var newPackage = CreateZip(("mod.dll", "new"));
        var changedLoader = Manifest("test", "TestMod", "bepinex-5.4.23.4", newPackage) with
        {
            Version = "2.0"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.UpdateFromFileAsync(changedLoader, newPackage));

        Assert.Equal("old", await File.ReadAllTextAsync(Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "Mods", "TestMod", "mod.dll")));
        Assert.False(Directory.Exists(Path.Combine(InstanceRoot, "BepInEx", "plugins", "TestMod")));
        Assert.Equal("modding-api-77", Assert.Single(await manager.GetInstalledAsync()).LoaderId);
    }

    [Fact]
    public async Task Update_package_failure_leaves_old_files_and_receipt_unchanged()
    {
        var manager = CreateManager();
        var oldPackage = CreateZip(("mod.dll", "old"));
        var original = await manager.InstallFromFileAsync(
            Manifest("test", "TestMod", "modding-api-77", oldPackage), oldPackage);
        var receiptPath = ReceiptPath("test");
        var receiptBefore = await File.ReadAllBytesAsync(receiptPath);
        var newPackage = CreateZip(("mod.dll", "new"));
        var invalid = Manifest("test", "TestMod", "modding-api-77", newPackage) with
        {
            Version = "2.0",
            Sha256 = new string('0', 64)
        };

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            manager.UpdateFromFileAsync(invalid, newPackage));

        Assert.Equal("old", await File.ReadAllTextAsync(Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "Mods", "TestMod", "mod.dll")));
        Assert.Equal(receiptBefore, await File.ReadAllBytesAsync(receiptPath));
        Assert.Equal(original.Version, Assert.Single(await manager.GetInstalledAsync()).Version);
    }

    [Fact]
    public async Task Update_receipt_failure_rolls_back_new_files()
    {
        var manager = CreateManager();
        var oldPackage = CreateZip(("mod.dll", "old"), ("kept.txt", "keep"));
        await manager.InstallFromFileAsync(
            Manifest("test", "TestMod", "modding-api-77", oldPackage), oldPackage);
        var receiptPath = ReceiptPath("test");
        var receiptBefore = await File.ReadAllBytesAsync(receiptPath);
        var newPackage = CreateZip(("mod.dll", "new"), ("added.txt", "add"));

        await using (new FileStream(receiptPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await Assert.ThrowsAnyAsync<IOException>(() => manager.UpdateFromFileAsync(
                Manifest("test", "TestMod", "modding-api-77", newPackage) with { Version = "2.0" },
                newPackage));
        }

        var installRoot = Path.Combine(InstanceRoot, "hollow_knight_Data", "Managed", "Mods", "TestMod");
        Assert.Equal("old", await File.ReadAllTextAsync(Path.Combine(installRoot, "mod.dll")));
        Assert.True(File.Exists(Path.Combine(installRoot, "kept.txt")));
        Assert.False(File.Exists(Path.Combine(installRoot, "added.txt")));
        Assert.Equal(receiptBefore, await File.ReadAllBytesAsync(receiptPath));
        Assert.Equal("1.0", Assert.Single(await manager.GetInstalledAsync()).Version);
    }

    [Fact]
    public async Task Update_preserves_the_existing_logical_id_when_manifest_casing_changes()
    {
        var manager = CreateManager();
        var oldPackage = CreateZip(("mod.dll", "old"));
        await manager.InstallFromFileAsync(
            Manifest("test", "TestMod", "modding-api-77", oldPackage), oldPackage);
        var newPackage = CreateZip(("mod.dll", "new"));

        var updated = await manager.UpdateFromFileAsync(
            Manifest("TEST", "TestMod", "modding-api-77", newPackage) with { Version = "2.0" },
            newPackage);

        Assert.Equal("test", updated.Id);
        Assert.Equal("test", Assert.Single(await manager.GetInstalledAsync()).Id);
        Assert.Single(Directory.EnumerateFiles(Path.Combine(_root, "state", "mods"), "*.json"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Receipt_lock_rolls_back_toggle_and_uninstall_file_changes(bool uninstall)
    {
        var manager = CreateManager();
        var package = CreateZip(("mod.dll", "old"));
        await manager.InstallFromFileAsync(
            Manifest("test", "TestMod", "modding-api-77", package), package);
        var activePath = Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "Mods", "TestMod", "mod.dll");
        var disabledPath = Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "Mods", "Disabled", "TestMod", "mod.dll");

        await using (new FileStream(ReceiptPath("test"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            if (uninstall)
            {
                await Assert.ThrowsAnyAsync<IOException>(() => manager.UninstallAsync("test"));
            }
            else
            {
                await Assert.ThrowsAnyAsync<IOException>(() => manager.SetEnabledAsync("test", enabled: false));
            }
        }

        Assert.True(File.Exists(activePath));
        Assert.False(File.Exists(disabledPath));
        Assert.True(Assert.Single(await manager.GetInstalledAsync()).Enabled);
    }

    [Fact]
    public async Task InstallWithDependencies_installs_in_dependency_order_and_updates_old_versions()
    {
        var manager = CreateManager();
        var oldBasePackage = CreateZip(("base.dll", "old"));
        await manager.InstallFromFileAsync(
            Manifest("base", "Base", "modding-api-77", oldBasePackage), oldBasePackage);
        var newBasePackage = CreateZip(("base.dll", "new"));
        var featurePackage = CreateZip(("feature.dll", "feature"));
        var baseManifest = Manifest("base", "Base", "modding-api-77", newBasePackage) with { Version = "2.0" };
        var featureManifest = Manifest(
            "feature", "Feature", "modding-api-77", featurePackage, "base") with
        { Version = "2.0" };

        var results = await manager.InstallWithDependenciesFromFilesAsync(
            [featureManifest, baseManifest],
            ["feature"],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["feature"] = featurePackage,
                ["base"] = newBasePackage
            });

        Assert.Equal(["base", "feature"], results.Select(receipt => receipt.Id));
        Assert.Equal("new", await File.ReadAllTextAsync(Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "Mods", "Base", "base.dll")));
        Assert.Equal(2, (await manager.GetInstalledAsync()).Count);
    }

    [Fact]
    public async Task InstallWithDependencies_skips_matching_dependencies_and_enables_them()
    {
        var manager = CreateManager();
        var basePackage = CreateZip(("base.dll", "base"));
        var baseManifest = Manifest("base", "Base", "modding-api-77", basePackage);
        await manager.InstallFromFileAsync(baseManifest, basePackage);
        await manager.SetEnabledAsync("base", enabled: false);
        var featurePackage = CreateZip(("feature.dll", "feature"));
        var featureManifest = Manifest(
            "feature", "Feature", "modding-api-77", featurePackage, "base");

        var results = await manager.InstallWithDependenciesFromFilesAsync(
            [featureManifest, baseManifest],
            ["feature"],
            new Dictionary<string, string> { ["feature"] = featurePackage });

        Assert.Equal(["base", "feature"], results.Select(receipt => receipt.Id));
        Assert.All(results, receipt => Assert.True(receipt.Enabled));
    }

    [Fact]
    public async Task InstallWithDependencies_rejects_drifted_matching_version()
    {
        var manager = CreateManager();
        var basePackage = CreateZip(("base.dll", "base"));
        var baseManifest = Manifest("base", "Base", "modding-api-77", basePackage);
        await manager.InstallFromFileAsync(baseManifest, basePackage);
        await File.WriteAllTextAsync(Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "Mods", "Base", "base.dll"), "drifted");
        var featurePackage = CreateZip(("feature.dll", "feature"));
        var featureManifest = Manifest(
            "feature", "Feature", "modding-api-77", featurePackage, "base");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.InstallWithDependenciesFromFilesAsync(
                [featureManifest, baseManifest],
                ["feature"],
                new Dictionary<string, string> { ["feature"] = featurePackage }));

        Assert.False(Directory.Exists(Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "Mods", "Feature")));
    }

    [Fact]
    public async Task InstallWithDependencies_rejects_matching_version_with_changed_metadata()
    {
        var manager = CreateManager();
        var basePackage = CreateZip(("base.dll", "base"));
        var installedManifest = Manifest("base", "Base", "modding-api-77", basePackage);
        await manager.InstallFromFileAsync(installedManifest, basePackage);
        var featurePackage = CreateZip(("feature.dll", "feature"));
        var featureManifest = Manifest(
            "feature", "Feature", "modding-api-77", featurePackage, "base");
        var changedBase = installedManifest with { LoaderId = "modding-api-60" };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.InstallWithDependenciesFromFilesAsync(
                [featureManifest, changedBase],
                ["feature"],
                new Dictionary<string, string> { ["feature"] = featurePackage }));
    }

    [Fact]
    public async Task Flat_layout_installs_only_manifest_declared_package_files()
    {
        var manager = CreateManager();
        var package = CreateZip(
            ("runtime/mod.dll", "selected"),
            ("duplicate/mod.dll", "not-selected"),
            ("docs/readme.txt", "not-selected"));
        var manifest = Manifest("test", "TestMod", "modding-api-60", package) with
        {
            FlatFiles = ["runtime/mod.dll"]
        };

        await manager.InstallFromFileAsync(manifest, package);

        var modsRoot = Path.Combine(InstanceRoot, "hollow_knight_Data", "Managed", "Mods");
        Assert.Equal("selected", await File.ReadAllTextAsync(Path.Combine(modsRoot, "mod.dll")));
        Assert.False(File.Exists(Path.Combine(modsRoot, "readme.txt")));
    }

    [Fact]
    public async Task Flat_layout_rejects_a_manifest_without_an_explicit_file_inventory()
    {
        var manager = CreateManager();
        var package = CreateZip(("mod.dll", "mod"));
        var manifest = Manifest("test", "TestMod", "modding-api-60", package) with { FlatFiles = [] };

        await Assert.ThrowsAsync<InvalidDataException>(() => manager.InstallFromFileAsync(manifest, package));
        Assert.Empty(await manager.GetInstalledAsync());
    }

    [Fact]
    public async Task InstallWithDependencies_keeps_completed_dependencies_when_a_later_package_fails()
    {
        var manager = CreateManager();
        var basePackage = CreateZip(("base.dll", "base"));
        var featurePackage = CreateZip(("feature.dll", "feature"));
        var baseManifest = Manifest("base", "Base", "modding-api-77", basePackage);
        var featureManifest = Manifest(
            "feature", "Feature", "modding-api-77", featurePackage, "base") with
        {
            Sha256 = new string('0', 64)
        };

        await Assert.ThrowsAsync<InvalidDataException>(() => manager.InstallWithDependenciesFromFilesAsync(
            [featureManifest, baseManifest],
            ["feature"],
            new Dictionary<string, string>
            {
                ["feature"] = featurePackage,
                ["base"] = basePackage
            }));

        Assert.Equal("base", Assert.Single(await manager.GetInstalledAsync()).Id);
        Assert.True(File.Exists(Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "Mods", "Base", "base.dll")));
        Assert.False(Directory.Exists(Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "Mods", "Feature")));
    }

    [Fact]
    public async Task InstallFromFile_allows_a_manifest_without_package_size()
    {
        var manager = CreateManager();
        var package = CreateZip(("mod.dll", "mod"));
        var manifest = Manifest("test", "TestMod", "modding-api-77", package) with
        {
            SizeBytes = null
        };

        var receipt = await manager.InstallFromFileAsync(manifest, package);

        Assert.Equal("test", receipt.Id);
        Assert.True(File.Exists(Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "Mods", "TestMod", "mod.dll")));
    }

    [Fact]
    public async Task InstallFromUri_allows_unknown_size_and_reuses_verified_cache()
    {
        var package = CreateZip(("mod.dll", "mod"));
        var packageBytes = await File.ReadAllBytesAsync(package);
        var handler = new SingleResponseHandler(packageBytes);
        using var httpClient = new HttpClient(handler);
        var cacheRoot = Path.Combine(_root, "package-cache");
        var manifest = Manifest("test", "TestMod", "modding-api-77", package) with
        {
            SizeBytes = null
        };
        var first = CreateManager(cacheRoot, httpClient);

        await first.InstallFromUriAsync(manifest);

        var secondInstance = Path.Combine(_root, "instance-cache-second");
        Directory.CreateDirectory(secondInstance);
        var second = new ModManager(
            secondInstance,
            Path.Combine(_root, "transactions-cache-second"),
            Path.Combine(_root, "state-cache-second", "mods"),
            cacheRoot,
            httpClient);
        var receipt = await second.InstallFromUriAsync(manifest);

        Assert.Equal(1, handler.RequestCount);
        Assert.Single(Directory.EnumerateFiles(cacheRoot, "*.zip", SearchOption.AllDirectories));
        Assert.True(File.Exists(Path.Combine(
            secondInstance, "hollow_knight_Data", "Managed", "Mods", "TestMod", "mod.dll")));
        Assert.Equal("test", receipt.Id);
    }

    [Fact]
    public async Task InstallFromUri_forwards_package_transfer_progress()
    {
        var package = CreateZip(("mod.dll", new string('x', 32_000)));
        var packageBytes = await File.ReadAllBytesAsync(package);
        using var httpClient = new HttpClient(new SingleResponseHandler(packageBytes));
        var manager = CreateManager(Path.Combine(_root, "package-progress-cache"), httpClient);
        var reports = new List<Crystalfly.Core.Packages.PackageTransferProgress>();

        await manager.InstallFromUriAsync(
            Manifest("progress", "Progress", "modding-api-77", package),
            new Progress<Crystalfly.Core.Packages.PackageTransferProgress>(reports.Add));

        Assert.NotEmpty(reports);
        Assert.Equal(packageBytes.Length, reports[^1].CompletedBytes);
    }

    private string InstanceRoot => Path.Combine(_root, "instance");

    private string ReceiptPath(string id)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)));
        return Path.Combine(_root, "state", "mods", $"{hash}.json");
    }

    private ModManager CreateManager(string? packageCacheRoot = null, HttpClient? httpClient = null)
    {
        Directory.CreateDirectory(InstanceRoot);
        return new ModManager(
            InstanceRoot,
            Path.Combine(_root, "transactions"),
            Path.Combine(_root, "state", "mods"),
            packageCacheRoot,
            httpClient);
    }

    private string CreateZip(params (string Name, string Content)[] entries)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, $"{Guid.NewGuid():N}.zip");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var item in entries)
        {
            var entry = archive.CreateEntry(item.Name);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8, leaveOpen: false);
            writer.Write(item.Content);
        }
        return path;
    }

    private static ModManifest Manifest(
        string id,
        string name,
        string loaderId,
        string package,
        params string[] dependencies) => new()
        {
            Id = id,
            Name = name,
            Version = "1.0",
            DownloadUrl = "https://example.invalid/mod.zip",
            SizeBytes = new FileInfo(package).Length,
            Sha256 = FileSha256(package),
            LoaderId = loaderId,
            SupportedBuildIds = ["test"],
            Dependencies = dependencies,
            FlatFiles = IsFlatLoader(loaderId) ? PackageFiles(package) : []
        };

    private static bool IsFlatLoader(string loaderId) =>
        loaderId.Equals("modding-api-37", StringComparison.OrdinalIgnoreCase)
        || loaderId.Equals("modding-api-60", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> PackageFiles(string package)
    {
        using var archive = ZipFile.OpenRead(package);
        return archive.Entries
            .Where(entry => entry.Name.Length != 0)
            .Select(entry => entry.FullName)
            .ToArray();
    }

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
                throw new HttpRequestException("The verified cache should prevent a second request.");
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
            });
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

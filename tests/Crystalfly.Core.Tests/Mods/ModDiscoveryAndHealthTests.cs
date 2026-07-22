using System.Security.Cryptography;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;

namespace Crystalfly.Core.Tests.Mods;

public sealed class ModDiscoveryAndHealthTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(), $"crystalfly-mod-discovery-{Guid.NewGuid():N}");

    [Fact]
    public async Task Discovery_scans_modding_api_flat_directory_subdirectories_and_bepinex_plugins()
    {
        var instanceRoot = Path.Combine(root, "instance");
        var receiptsRoot = Path.Combine(root, "state", "mods");
        var managedFile = await WriteAsync(
            instanceRoot, "hollow_knight_Data/Managed/Mods/Managed/Managed.dll", "managed");
        var managed = Receipt(
            "managed", "Managed", "modding-api-77",
            "hollow_knight_Data/Managed/Mods/Managed",
            "hollow_knight_Data/Managed/Mods/Managed/Managed.dll",
            await HashAsync(managedFile));
        await WriteReceiptAsync(receiptsRoot, managed);
        await WriteAsync(instanceRoot, "hollow_knight_Data/Managed/Mods/Loose.dll", "loose");
        await WriteAsync(
            instanceRoot, "hollow_knight_Data/Managed/Mods/ThirdParty/ThirdParty.dll", "third-party");
        await WriteAsync(
            instanceRoot, "hollow_knight_Data/Managed/Mods/Disabled/Off/Off.dll", "off");
        await WriteAsync(instanceRoot, "BepInEx/plugins/BepPlugin/plugin.dll", "bep");

        var result = await new ModDiscoveryService(instanceRoot, receiptsRoot).DiscoverAsync("modding-api-60");

        Assert.Equal("managed", Assert.Single(result.InstalledReceipts).Id);
        Assert.Equal(4, result.ExternalMods.Count);
        Assert.All(result.ExternalMods, mod =>
        {
            Assert.Equal(ModOwnership.External, mod.Ownership);
            Assert.True(mod.IsReadOnly);
            Assert.NotEmpty(mod.EntryFiles);
        });
        Assert.True(result.ExternalMods.Single(mod => mod.Name == "Loose").Enabled);
        Assert.True(result.ExternalMods.Single(mod => mod.Name == "ThirdParty").Enabled);
        Assert.False(result.ExternalMods.Single(mod => mod.Name == "Off").Enabled);
        Assert.Equal(
            "bepinex-external",
            result.ExternalMods.Single(mod => mod.Name == "BepPlugin").LoaderId);
    }

    [Fact]
    public async Task Discovery_groups_flat_files_with_the_same_base_name_into_one_mod()
    {
        var instanceRoot = Path.Combine(root, "instance-flat-group");
        var receiptsRoot = Path.Combine(root, "state-flat-group", "mods");
        await WriteAsync(instanceRoot, "hollow_knight_Data/Managed/Mods/Foo.dll", "assembly");
        await WriteAsync(instanceRoot, "hollow_knight_Data/Managed/Mods/Foo.pdb", "symbols");
        await WriteAsync(instanceRoot, "hollow_knight_Data/Managed/Mods/Foo.xml", "docs");

        var result = await new ModDiscoveryService(instanceRoot, receiptsRoot)
            .DiscoverAsync("modding-api-60");

        var external = Assert.Single(result.ExternalMods);
        Assert.Equal("Foo", external.Name);
        Assert.Equal(3, external.Files.Count);
        Assert.Single(external.EntryFiles);
        Assert.Equal(result.Mods.Count, result.Mods.Select(mod => mod.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task Takeover_hashes_external_files_preserves_disabled_placement_and_blocks_automatic_update()
    {
        var instanceRoot = Path.Combine(root, "instance");
        var receiptsRoot = Path.Combine(root, "state", "mods");
        await WriteAsync(
            instanceRoot, "hollow_knight_Data/Managed/Mods/Disabled/Local/Local.dll", "local");
        var manager = CreateManager(instanceRoot, receiptsRoot);
        var discovery = await manager.DiscoverAsync("modding-api-77");
        var external = Assert.Single(discovery.ExternalMods);

        var receipt = await manager.TakeOverAsync("local", external);

        Assert.Equal(ModOwnership.LocalTakenOver, receipt.Ownership);
        Assert.True(receipt.IsLocal);
        Assert.False(receipt.Enabled);
        Assert.Equal(external.InstallRoot, receipt.InstallRoot);
        Assert.Equal(external.EntryFiles, receipt.EntryFiles);
        Assert.Equal(await HashAsync(Path.Combine(instanceRoot, external.Files[0])), receipt.Files[0].Sha256);
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.UpdateFromFileAsync(
            Manifest("local", "Local", "modding-api-77"),
            Path.Combine(root, "missing.zip")));
    }

    [Fact]
    public async Task Takeover_rejects_files_outside_recognized_mod_roots()
    {
        var instanceRoot = Path.Combine(root, "instance");
        var receiptsRoot = Path.Combine(root, "state", "mods");
        await WriteAsync(instanceRoot, "hollow_knight_Data/Managed/not-a-mod.dll", "outside");
        var external = new ModDiscoveryEntry
        {
            Id = "outside",
            Name = "Outside",
            LoaderId = "modding-api-77",
            InstallRoot = "hollow_knight_Data/Managed",
            Enabled = true,
            Ownership = ModOwnership.External,
            Files = ["hollow_knight_Data/Managed/not-a-mod.dll"],
            EntryFiles = ["hollow_knight_Data/Managed/not-a-mod.dll"]
        };

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            CreateManager(instanceRoot, receiptsRoot).TakeOverAsync("outside", external));
    }

    [Fact]
    public async Task Takeover_rejects_dot_segments_that_resolve_outside_recognized_mod_roots()
    {
        var instanceRoot = Path.Combine(root, "instance");
        var receiptsRoot = Path.Combine(root, "state", "mods");
        await WriteAsync(instanceRoot, "hollow_knight_Data/globalgamemanagers", "game");
        var external = new ModDiscoveryEntry
        {
            Id = "dot-segments",
            Name = "DotSegments",
            LoaderId = "modding-api-77",
            InstallRoot = "hollow_knight_Data/Managed/Mods",
            Enabled = true,
            Ownership = ModOwnership.External,
            Files = ["hollow_knight_Data/Managed/Mods/../../globalgamemanagers"],
            EntryFiles = []
        };

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            CreateManager(instanceRoot, receiptsRoot).TakeOverAsync("dot-segments", external));
        Assert.Empty(Directory.Exists(receiptsRoot)
            ? Directory.EnumerateFiles(receiptsRoot, "*.json")
            : []);
    }

    [Fact]
    public async Task Takeover_rejects_a_recognized_path_that_traverses_a_reparse_point()
    {
        var instanceRoot = Path.Combine(root, "instance");
        var receiptsRoot = Path.Combine(root, "state", "mods");
        var outsideRoot = Path.Combine(root, "outside");
        Directory.CreateDirectory(outsideRoot);
        await File.WriteAllTextAsync(Path.Combine(outsideRoot, "linked.dll"), "outside");
        var modsRoot = Path.Combine(instanceRoot, "hollow_knight_Data", "Managed", "Mods");
        Directory.CreateDirectory(modsRoot);
        Directory.CreateSymbolicLink(Path.Combine(modsRoot, "Linked"), outsideRoot);
        var external = new ModDiscoveryEntry
        {
            Id = "linked",
            Name = "Linked",
            LoaderId = "modding-api-77",
            InstallRoot = "hollow_knight_Data/Managed/Mods/Linked",
            Enabled = true,
            Ownership = ModOwnership.External,
            Files = ["hollow_knight_Data/Managed/Mods/Linked/linked.dll"],
            EntryFiles = ["hollow_knight_Data/Managed/Mods/Linked/linked.dll"]
        };

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            CreateManager(instanceRoot, receiptsRoot).TakeOverAsync("linked", external));
    }

    [Fact]
    public async Task Health_reports_missing_modified_extra_healthy_external_and_indeterminate_states()
    {
        var instanceRoot = Path.Combine(root, "instance");
        var filePath = await WriteAsync(
            instanceRoot, "hollow_knight_Data/Managed/Mods/Test/Test.dll", "healthy");
        var receipt = Receipt(
            "test", "Test", "modding-api-77",
            "hollow_knight_Data/Managed/Mods/Test",
            "hollow_knight_Data/Managed/Mods/Test/Test.dll",
            await HashAsync(filePath));
        var service = new ModHealthService(instanceRoot);

        Assert.Equal(ModHealthStatus.Healthy, (await service.AssessAsync(receipt, [receipt])).Status);

        await File.WriteAllTextAsync(filePath, "modified");
        Assert.Equal(ModHealthStatus.ModifiedFile, (await service.AssessAsync(receipt, [receipt])).Status);

        File.Delete(filePath);
        Assert.Equal(
            ModHealthStatus.CriticalFileMissing,
            (await service.AssessAsync(receipt, [receipt])).Status);

        await File.WriteAllTextAsync(filePath, "healthy");
        await WriteAsync(
            instanceRoot, "hollow_knight_Data/Managed/Mods/Test/extra.txt", "extra");
        Assert.Equal(ModHealthStatus.ExtraFile, (await service.AssessAsync(receipt, [receipt])).Status);

        await WriteAsync(
            instanceRoot,
            "hollow_knight_Data/Managed/Mods/External/External.dll",
            "external");
        var external = new ModDiscoveryEntry
        {
            Id = "external",
            Name = "External",
            LoaderId = "modding-api-77",
            InstallRoot = "hollow_knight_Data/Managed/Mods/External",
            Enabled = true,
            Ownership = ModOwnership.External,
            Files = ["hollow_knight_Data/Managed/Mods/External/External.dll"],
            EntryFiles = ["hollow_knight_Data/Managed/Mods/External/External.dll"]
        };
        var externalHealth = await service.AssessExternalAsync(external);
        Assert.Equal(ModHealthStatus.UnmanagedExternal, externalHealth.Status);
        Assert.Single(externalHealth.CurrentFileSha256ByPath);

        var empty = receipt with { Id = "empty", Files = [], EntryFiles = [] };
        Assert.Equal(ModHealthStatus.Indeterminate, (await service.AssessAsync(empty, [empty])).Status);
    }

    [Fact]
    public async Task Health_collects_missing_modified_and_extra_files_in_one_scan()
    {
        var instanceRoot = Path.Combine(root, "instance-combined-health");
        var installRoot = "hollow_knight_Data/Managed/Mods/Test";
        var modifiedPath = await WriteAsync(instanceRoot, $"{installRoot}/Modified.dll", "current");
        var receipt = new InstalledModReceipt
        {
            Id = "combined",
            Name = "Combined",
            Version = "1",
            LoaderId = "modding-api-77",
            InstallRoot = installRoot,
            Files =
            [
                new InstalledFileReceipt
                {
                    RelativePath = $"{installRoot}/Missing.dll",
                    Sha256 = new string('A', 64)
                },
                new InstalledFileReceipt
                {
                    RelativePath = $"{installRoot}/Modified.dll",
                    Sha256 = new string('B', 64)
                }
            ],
            EntryFiles = [$"{installRoot}/Missing.dll", $"{installRoot}/Modified.dll"]
        };
        await WriteAsync(instanceRoot, $"{installRoot}/Extra.txt", "extra");

        var report = await new ModHealthService(instanceRoot).AssessAsync(receipt, [receipt]);

        Assert.Equal(ModHealthStatus.CriticalFileMissing, report.Status);
        Assert.Equal([$"{installRoot}/Missing.dll"], report.MissingFiles);
        Assert.Equal([$"{installRoot}/Modified.dll"], report.ModifiedFiles);
        Assert.Equal([$"{installRoot}/Extra.txt"], report.ExtraFiles);
        Assert.Equal(await HashAsync(modifiedPath), report.CurrentFileSha256ByPath[$"{installRoot}/Modified.dll"]);
    }

    [Fact]
    public async Task Flat_layout_health_leaves_unowned_shared_root_files_to_external_discovery()
    {
        var instanceRoot = Path.Combine(root, "instance");
        var firstPath = await WriteAsync(
            instanceRoot, "hollow_knight_Data/Managed/Mods/first.dll", "first");
        var secondPath = await WriteAsync(
            instanceRoot, "hollow_knight_Data/Managed/Mods/second.dll", "second");
        await WriteAsync(instanceRoot, "hollow_knight_Data/Managed/Mods/loose.dll", "loose");
        var installRoot = "hollow_knight_Data/Managed/Mods";
        var first = Receipt(
            "first", "First", "modding-api-60", installRoot,
            "hollow_knight_Data/Managed/Mods/first.dll", await HashAsync(firstPath));
        var second = Receipt(
            "second", "Second", "modding-api-60", installRoot,
            "hollow_knight_Data/Managed/Mods/second.dll", await HashAsync(secondPath));
        var service = new ModHealthService(instanceRoot);

        var firstHealth = await service.AssessAsync(first, [first, second]);
        var secondHealth = await service.AssessAsync(second, [first, second]);

        Assert.Equal(ModHealthStatus.Healthy, firstHealth.Status);
        Assert.Equal(ModHealthStatus.Healthy, secondHealth.Status);
    }

    [Fact]
    public async Task Direct_bepinex_plugin_health_leaves_other_shared_root_files_to_discovery()
    {
        var instanceRoot = Path.Combine(root, "instance");
        var pluginPath = await WriteAsync(instanceRoot, "BepInEx/plugins/plugin.dll", "plugin");
        await WriteAsync(instanceRoot, "BepInEx/plugins/other.dll", "other");
        var receipt = Receipt(
            "plugin", "Plugin", "bepinex-5.4.23.4", "BepInEx/plugins",
            "BepInEx/plugins/plugin.dll", await HashAsync(pluginPath));

        var report = await new ModHealthService(instanceRoot).AssessAsync(receipt, [receipt]);

        Assert.Equal(ModHealthStatus.Healthy, report.Status);
    }

    [Fact]
    public async Task Discovery_skips_nested_reparse_points()
    {
        var instanceRoot = Path.Combine(root, "instance");
        var receiptsRoot = Path.Combine(root, "state", "mods");
        var container = Path.Combine(
            instanceRoot, "hollow_knight_Data", "Managed", "Mods", "Container");
        Directory.CreateDirectory(container);
        await File.WriteAllTextAsync(Path.Combine(container, "real.dll"), "real");
        var outside = Path.Combine(root, "outside-discovery");
        Directory.CreateDirectory(outside);
        await File.WriteAllTextAsync(Path.Combine(outside, "linked.dll"), "linked");
        Directory.CreateSymbolicLink(Path.Combine(container, "Linked"), outside);

        var result = await new ModDiscoveryService(instanceRoot, receiptsRoot)
            .DiscoverAsync("modding-api-77");

        var external = Assert.Single(result.ExternalMods);
        Assert.Equal(["hollow_knight_Data/Managed/Mods/Container/real.dll"], external.Files);
    }

    [Fact]
    public async Task Health_is_indeterminate_when_owned_root_is_a_reparse_point()
    {
        var instanceRoot = Path.Combine(root, "instance");
        var outside = Path.Combine(root, "outside-health-root");
        Directory.CreateDirectory(outside);
        var outsideFile = Path.Combine(outside, "linked.dll");
        await File.WriteAllTextAsync(outsideFile, "linked");
        var modsRoot = Path.Combine(instanceRoot, "hollow_knight_Data", "Managed", "Mods");
        Directory.CreateDirectory(modsRoot);
        Directory.CreateSymbolicLink(Path.Combine(modsRoot, "Linked"), outside);
        var receipt = Receipt(
            "linked", "Linked", "modding-api-77",
            "hollow_knight_Data/Managed/Mods/Linked",
            "hollow_knight_Data/Managed/Mods/Linked/linked.dll",
            await HashAsync(outsideFile));

        var report = await new ModHealthService(instanceRoot).AssessAsync(receipt, [receipt]);

        Assert.Equal(ModHealthStatus.Indeterminate, report.Status);
    }

    [Fact]
    public async Task Health_is_indeterminate_when_receipt_file_traverses_a_nested_reparse_point()
    {
        var instanceRoot = Path.Combine(root, "instance");
        var installRoot = Path.Combine(
            instanceRoot, "hollow_knight_Data", "Managed", "Mods", "Test");
        Directory.CreateDirectory(installRoot);
        var outside = Path.Combine(root, "outside-health-file");
        Directory.CreateDirectory(outside);
        var outsideFile = Path.Combine(outside, "linked.dll");
        await File.WriteAllTextAsync(outsideFile, "linked");
        Directory.CreateSymbolicLink(Path.Combine(installRoot, "Linked"), outside);
        var receipt = Receipt(
            "test", "Test", "modding-api-77",
            "hollow_knight_Data/Managed/Mods/Test",
            "hollow_knight_Data/Managed/Mods/Test/Linked/linked.dll",
            await HashAsync(outsideFile));

        var report = await new ModHealthService(instanceRoot).AssessAsync(receipt, [receipt]);

        Assert.Equal(ModHealthStatus.Indeterminate, report.Status);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private ModManager CreateManager(string instanceRoot, string receiptsRoot)
    {
        Directory.CreateDirectory(instanceRoot);
        return new ModManager(instanceRoot, Path.Combine(root, "transactions"), receiptsRoot);
    }

    private static InstalledModReceipt Receipt(
        string id,
        string name,
        string loaderId,
        string installRoot,
        string relativePath,
        string sha256) => new()
        {
            Id = id,
            Name = name,
            Version = "1.0",
            LoaderId = loaderId,
            InstallRoot = installRoot,
            Ownership = ModOwnership.Managed,
            Files = [new InstalledFileReceipt { RelativePath = relativePath, Sha256 = sha256 }],
            EntryFiles = [relativePath]
        };

    private static ModManifest Manifest(string id, string name, string loaderId) => new()
    {
        Id = id,
        Name = name,
        Version = "2.0",
        LoaderId = loaderId,
        DownloadUrl = "https://example.invalid/mod.zip",
        Sha256 = new string('A', 64)
    };

    private static async Task<string> WriteAsync(string instanceRoot, string relativePath, string content)
    {
        var path = Path.Combine(instanceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
        return path;
    }

    private static async Task WriteReceiptAsync(string receiptsRoot, InstalledModReceipt receipt)
    {
        Directory.CreateDirectory(receiptsRoot);
        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(receipt.Id)));
        await Crystalfly.Core.Serialization.AtomicJsonStore.WriteAsync(
            Path.Combine(receiptsRoot, $"{hash}.json"), receipt);
    }

    private static async Task<string> HashAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream));
    }
}

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;

namespace Crystalfly.Core.Tests.Mods;

public sealed class ModManagerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Crystalfly.Tests", Guid.NewGuid().ToString("N"));

    [Theory]
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
        var dll = Path.Combine(_root, "single.dll");
        await File.WriteAllTextAsync(dll, "dll");

        var dllReceipt = await manager.ImportLocalDllAsync(
            "local-dll", "LocalDll", "bepinex-5.4.23.4", dll);

        Assert.True(zipReceipt.IsLocal);
        Assert.True(dllReceipt.IsLocal);
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

    private string InstanceRoot => Path.Combine(_root, "instance");

    private ModManager CreateManager()
    {
        Directory.CreateDirectory(InstanceRoot);
        return new ModManager(
            InstanceRoot,
            Path.Combine(_root, "transactions"),
            Path.Combine(_root, "state", "mods"));
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
            Dependencies = dependencies
        };

    private static string FileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

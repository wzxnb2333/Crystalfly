using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Crystalfly.Core.Loaders;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Tests.Loaders;

public sealed class LoaderManagerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Crystalfly.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Install_and_uninstall_persist_receipt_and_restore_vanilla_state()
    {
        var manager = CreateManager();
        var assemblyPath = Path.Combine(InstanceRoot, "hollow_knight_Data", "Managed", "Assembly-CSharp.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath)!);
        await File.WriteAllTextAsync(assemblyPath, "vanilla");
        var package = CreateZip(
            ("Assembly-CSharp.dll", "patched"),
            ("MMHOOK_Assembly-CSharp.dll", "api"));
        var manifest = Manifest("modding-api-77", package);

        var receipt = await manager.InstallFromFileAsync(manifest, package);

        Assert.Equal(LoaderState.ModdingApi, receipt.LoaderState);
        Assert.True(File.Exists(Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "MMHOOK_Assembly-CSharp.dll")));
        Assert.True(File.Exists(ReceiptPath));
        Assert.Equal(LoaderState.ModdingApi, await manager.GetStateAsync());

        await manager.UninstallAsync();

        Assert.Equal(LoaderState.Vanilla, await manager.GetStateAsync());
        Assert.False(File.Exists(ReceiptPath));
        Assert.Equal("vanilla", await File.ReadAllTextAsync(assemblyPath));
    }

    [Fact]
    public async Task Switch_replaces_verified_loader_in_one_operation()
    {
        var manager = CreateManager();
        var apiPackage = CreateZip(("MMHOOK_Assembly-CSharp.dll", "api"));
        await manager.InstallFromFileAsync(Manifest("modding-api-77", apiPackage), apiPackage);
        var bepinexPackage = CreateZip(("BepInEx/core/BepInEx.dll", "bep"));

        var receipt = await manager.SwitchFromFileAsync(Manifest("bepinex-5.4.23.4", bepinexPackage), bepinexPackage);

        Assert.Equal(LoaderState.BepInEx, receipt.LoaderState);
        Assert.False(File.Exists(Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "MMHOOK_Assembly-CSharp.dll")));
        Assert.True(File.Exists(Path.Combine(InstanceRoot, "BepInEx", "core", "BepInEx.dll")));
        Assert.Equal(LoaderState.BepInEx, await manager.GetStateAsync());
    }

    [Fact]
    public async Task Drift_blocks_switch_and_uninstall_but_explicit_repair_restores_receipt_hashes()
    {
        var manager = CreateManager();
        var package = CreateZip(("MMHOOK_Assembly-CSharp.dll", "api"));
        var manifest = Manifest("modding-api-77", package);
        await manager.InstallFromFileAsync(manifest, package);
        var installedPath = Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "MMHOOK_Assembly-CSharp.dll");
        await File.WriteAllTextAsync(installedPath, "changed");

        Assert.Equal(LoaderState.Drifted, await manager.GetStateAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.UninstallAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.SwitchFromFileAsync(Manifest("bepinex-5.4.23.4", package), package));

        await manager.RepairFromFileAsync(manifest, package);

        Assert.Equal("api", await File.ReadAllTextAsync(installedPath));
        Assert.Equal(LoaderState.ModdingApi, await manager.GetStateAsync());
    }

    [Fact]
    public async Task Conflict_stops_loader_install_before_files_are_overwritten()
    {
        var manager = CreateManager();
        Directory.CreateDirectory(Path.Combine(InstanceRoot, "BepInEx"));
        Directory.CreateDirectory(Path.Combine(InstanceRoot, "hollow_knight_Data", "Managed", "Mods"));
        var package = CreateZip(("MMHOOK_Assembly-CSharp.dll", "api"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.InstallFromFileAsync(Manifest("modding-api-77", package), package));

        Assert.False(File.Exists(Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "MMHOOK_Assembly-CSharp.dll")));
    }

    [Fact]
    public async Task Uninstall_rejects_receipt_backup_root_outside_state_directory()
    {
        var manager = CreateManager();
        var installed = Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "MMHOOK_Assembly-CSharp.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(installed)!);
        await File.WriteAllTextAsync(installed, "api");
        var outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outside);
        await AtomicJsonStore.WriteAsync(ReceiptPath, new InstalledPackageReceipt
        {
            PackageId = "modding-api-77",
            LoaderState = LoaderState.ModdingApi,
            BackupRoot = outside,
            Files =
            [
                new InstalledFileReceipt
                {
                    RelativePath = "hollow_knight_Data/Managed/MMHOOK_Assembly-CSharp.dll",
                    Sha256 = FileSha256(installed)
                }
            ]
        });

        await Assert.ThrowsAsync<InvalidDataException>(() => manager.UninstallAsync());

        Assert.True(Directory.Exists(outside));
        Assert.True(File.Exists(installed));
    }

    [Fact]
    public async Task Receipt_write_failure_keeps_original_file_backup_for_manual_recovery()
    {
        var manager = CreateManager();
        var assemblyPath = Path.Combine(InstanceRoot, "hollow_knight_Data", "Managed", "Assembly-CSharp.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath)!);
        await File.WriteAllTextAsync(assemblyPath, "vanilla");
        Directory.CreateDirectory(ReceiptPath);
        var package = CreateZip(
            ("Assembly-CSharp.dll", "patched"),
            ("MMHOOK_Assembly-CSharp.dll", "api"));

        await Assert.ThrowsAnyAsync<IOException>(() =>
            manager.InstallFromFileAsync(Manifest("modding-api-77", package), package));

        var backups = Directory.GetFiles(
            Path.Combine(_root, "state", "loader-backups"),
            "Assembly-CSharp.dll",
            SearchOption.AllDirectories);
        Assert.Single(backups);
        Assert.Equal("vanilla", await File.ReadAllTextAsync(backups[0]));
    }

    private string InstanceRoot => Path.Combine(_root, "instance");

    private string ReceiptPath => Path.Combine(_root, "state", "loader.json");

    private LoaderManager CreateManager()
    {
        Directory.CreateDirectory(InstanceRoot);
        return new LoaderManager(InstanceRoot, Path.Combine(_root, "transactions"), ReceiptPath);
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

    private static LoaderManifest Manifest(string id, string package) => new()
    {
        Id = id,
        Name = id.StartsWith("bepinex", StringComparison.OrdinalIgnoreCase) ? "BepInEx" : "Modding API",
        Version = "1.0",
        DownloadUrl = "https://example.invalid/loader.zip",
        SizeBytes = new FileInfo(package).Length,
        Sha256 = FileSha256(package),
        SupportedBuildIds = ["test"]
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

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
    public async Task Local_loader_install_persists_unverified_status_and_declared_loader_state()
    {
        var manager = CreateManager();
        var packagePath = CreateZip(("BepInEx/core/BepInEx.dll", "local"));
        var package = new LocalLoaderPackage(
            Manifest("community-loader", packagePath),
            packagePath,
            LoaderState.BepInEx);

        var receipt = await manager.InstallLocalFromFileAsync(package);

        Assert.False(receipt.IsVerified);
        Assert.Equal(LoaderState.BepInEx, receipt.LoaderState);
        Assert.False((await manager.GetReceiptAsync())!.IsVerified);
        Assert.Equal(LoaderState.BepInEx, await manager.GetStateAsync());
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
    public async Task Receipt_write_failure_rolls_back_files_and_backup_metadata()
    {
        var manager = CreateManager();
        var assemblyPath = Path.Combine(InstanceRoot, "hollow_knight_Data", "Managed", "Assembly-CSharp.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath)!);
        await File.WriteAllTextAsync(assemblyPath, "vanilla");
        Directory.CreateDirectory(ReceiptPath);
        await File.WriteAllTextAsync(Path.Combine(ReceiptPath, "blocker.txt"), "block");
        var package = CreateZip(
            ("Assembly-CSharp.dll", "patched"),
            ("MMHOOK_Assembly-CSharp.dll", "api"));

        await Assert.ThrowsAnyAsync<IOException>(() =>
            manager.InstallFromFileAsync(Manifest("modding-api-77", package), package));

        Assert.Equal("vanilla", await File.ReadAllTextAsync(assemblyPath));
        Assert.False(File.Exists(Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "MMHOOK_Assembly-CSharp.dll")));
        Assert.Empty(Directory.Exists(Path.Combine(_root, "state", "loader-backups"))
            ? Directory.EnumerateFiles(Path.Combine(_root, "state", "loader-backups"), "*", SearchOption.AllDirectories)
            : []);
    }

    [Fact]
    public async Task Install_from_file_accepts_manifest_without_declared_size()
    {
        var manager = CreateManager();
        var package = CreateZip(("MMHOOK_Assembly-CSharp.dll", "api"));
        var manifest = Manifest("modding-api-77", package) with { SizeBytes = null };

        await manager.InstallFromFileAsync(manifest, package);

        Assert.Equal(LoaderState.ModdingApi, await manager.GetStateAsync());
    }

    [Fact]
    public async Task Remote_install_uses_content_length_and_reuses_package_cache()
    {
        var package = CreateZip(("MMHOOK_Assembly-CSharp.dll", "api"));
        var handler = new CountingHandler(await File.ReadAllBytesAsync(package));
        using var client = new HttpClient(handler);
        var manager = CreateManager(Path.Combine(_root, "packages"), client);
        var manifest = Manifest("modding-api-77", package) with { SizeBytes = null };

        await manager.InstallFromUriAsync(manifest);
        await manager.UninstallAsync();
        await manager.InstallFromUriAsync(manifest);

        Assert.Equal(1, handler.RequestCount);
        Assert.Single(Directory.EnumerateFiles(Path.Combine(_root, "packages"), "*.zip"));
        Assert.Equal(LoaderState.ModdingApi, await manager.GetStateAsync());
    }

    [Fact]
    public async Task Switch_receipt_failure_restores_previous_verified_loader()
    {
        var manager = CreateManager();
        var assemblyPath = Path.Combine(InstanceRoot, "hollow_knight_Data", "Managed", "Assembly-CSharp.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath)!);
        await File.WriteAllTextAsync(assemblyPath, "vanilla");
        var apiPackage = CreateZip(
            ("Assembly-CSharp.dll", "patched"),
            ("MMHOOK_Assembly-CSharp.dll", "api"));
        await manager.InstallFromFileAsync(Manifest("modding-api-77", apiPackage), apiPackage);
        var originalReceipt = await manager.GetReceiptAsync();
        var bepinexPackage = CreateZip(("BepInEx/core/BepInEx.dll", "bep"));

        File.SetAttributes(ReceiptPath, FileAttributes.ReadOnly);
        try
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => manager.SwitchFromFileAsync(
                Manifest("bepinex-5.4.23.4", bepinexPackage), bepinexPackage));
        }
        finally
        {
            ClearReadOnlyAttributes();
        }

        Assert.Equal(LoaderState.ModdingApi, await manager.GetStateAsync());
        Assert.True(File.Exists(Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "MMHOOK_Assembly-CSharp.dll")));
        Assert.False(File.Exists(Path.Combine(InstanceRoot, "BepInEx", "core", "BepInEx.dll")));
        Assert.Equal("vanilla", await File.ReadAllTextAsync(Path.Combine(
            originalReceipt!.BackupRoot, "hollow_knight_Data", "Managed", "Assembly-CSharp.dll")));
    }

    [Fact]
    public async Task Uninstall_receipt_failure_restores_installed_loader()
    {
        var manager = CreateManager();
        var package = CreateZip(("MMHOOK_Assembly-CSharp.dll", "api"));
        await manager.InstallFromFileAsync(Manifest("modding-api-77", package), package);

        File.SetAttributes(ReceiptPath, FileAttributes.ReadOnly);
        try
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => manager.UninstallAsync());
        }
        finally
        {
            ClearReadOnlyAttributes();
        }

        Assert.Equal(LoaderState.ModdingApi, await manager.GetStateAsync());
        Assert.True(File.Exists(Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "MMHOOK_Assembly-CSharp.dll")));
    }

    [Fact]
    public async Task Uninstall_reports_failure_when_unmanaged_loader_files_prevent_clean_state()
    {
        var manager = CreateManager();
        var package = CreateZip(("BepInEx/core/BepInEx.dll", "loader"));
        await manager.InstallFromFileAsync(Manifest("bepinex-5.4.23.4", package), package);
        var unmanaged = Path.Combine(InstanceRoot, "BepInEx", "plugins", "manual.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(unmanaged)!);
        await File.WriteAllTextAsync(unmanaged, "manual");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.UninstallAsync());

        Assert.Contains("manual", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(LoaderState.Drifted, await manager.GetStateAsync());
        Assert.True(File.Exists(unmanaged));
    }

    [Fact]
    public async Task External_BepInEx_cannot_be_repaired_uninstalled_or_overwritten()
    {
        var core = Path.Combine(InstanceRoot, "BepInEx", "core", "BepInEx.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(core)!);
        File.Copy(typeof(LoaderManager).Assembly.Location, core);
        var manager = CreateManager();
        var package = CreateZip(("BepInEx/core/BepInEx.dll", "replacement"));
        var manifest = Manifest("bepinex-1.0.0.0", package);

        Assert.Equal(LoaderState.BepInEx, await manager.GetStateAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.UninstallAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.RepairFromFileAsync(manifest, package));
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.InstallFromFileAsync(manifest, package));

        Assert.Equal(File.ReadAllBytes(typeof(LoaderManager).Assembly.Location), File.ReadAllBytes(core));
    }

    [Fact]
    public async Task Repair_receipt_failure_restores_pre_repair_drift()
    {
        var manager = CreateManager();
        var package = CreateZip(("MMHOOK_Assembly-CSharp.dll", "api"));
        var manifest = Manifest("modding-api-77", package);
        await manager.InstallFromFileAsync(manifest, package);
        var installed = Path.Combine(
            InstanceRoot, "hollow_knight_Data", "Managed", "MMHOOK_Assembly-CSharp.dll");
        await File.WriteAllTextAsync(installed, "changed");

        File.SetAttributes(ReceiptPath, FileAttributes.ReadOnly);
        try
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => manager.RepairFromFileAsync(manifest, package));
        }
        finally
        {
            ClearReadOnlyAttributes();
        }

        Assert.Equal("changed", await File.ReadAllTextAsync(installed));
        Assert.Equal(LoaderState.Drifted, await manager.GetStateAsync());
    }

    private string InstanceRoot => Path.Combine(_root, "instance");

    private string ReceiptPath => Path.Combine(_root, "state", "loader.json");

    private LoaderManager CreateManager(string? packageCacheRoot = null, HttpClient? httpClient = null)
    {
        Directory.CreateDirectory(InstanceRoot);
        return new LoaderManager(
            InstanceRoot,
            Path.Combine(_root, "transactions"),
            ReceiptPath,
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

    private void ClearReadOnlyAttributes()
    {
        foreach (var path in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class CountingHandler(byte[] content) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
            });
        }
    }
}

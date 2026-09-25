using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Tests.Mods;

public sealed class InstalledModReceiptMigrationTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(), $"crystalfly-receipt-migration-{Guid.NewGuid():N}");

    [Theory]
    [InlineData(false, ModOwnership.Managed)]
    [InlineData(true, ModOwnership.LocalTakenOver)]
    public async Task GetInstalled_migrates_v1_receipt_atomically_and_preserves_legacy_semantics(
        bool isLocal,
        ModOwnership expectedOwnership)
    {
        var receiptsRoot = Path.Combine(root, "state", "mods");
        Directory.CreateDirectory(receiptsRoot);
        var receiptPath = ReceiptPath(receiptsRoot, "legacy");
        var originalJson = $$"""
            {
              "schemaVersion": 1,
              "id": "legacy",
              "name": "Legacy",
              "version": "local",
              "loaderId": "modding-api-77",
              "installRoot": "hollow_knight_Data/Managed/Mods/Legacy",
              "enabled": false,
              "isLocal": {{isLocal.ToString().ToLowerInvariant()}},
              "dependencies": ["satchel"],
              "files": [
                {
                  "relativePath": "hollow_knight_Data/Managed/Mods/Legacy/Legacy.dll",
                  "sha256": "{{new string('A', 64)}}"
                }
              ]
            }
            """;
        await File.WriteAllTextAsync(receiptPath, originalJson);
        var originalBytes = await File.ReadAllBytesAsync(receiptPath);
        var manager = CreateManager(receiptsRoot);

        var receipt = Assert.Single(await manager.GetInstalledAsync());

        Assert.Equal(InstalledModReceipt.CurrentSchemaVersion, receipt.SchemaVersion);
        Assert.Equal(expectedOwnership, receipt.Ownership);
        Assert.Equal(isLocal, receipt.IsLocal);
        Assert.False(receipt.Enabled);
        Assert.False(receipt.Pinned);
        Assert.Equal(["satchel"], receipt.Dependencies);
        Assert.Equal(
            ["hollow_knight_Data/Managed/Mods/Legacy/Legacy.dll"],
            receipt.EntryFiles);
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(receiptPath + ".bak"));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(receiptPath));
        Assert.Equal(
            InstalledModReceipt.CurrentSchemaVersion,
            document.RootElement.GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public async Task GetInstalled_rejects_receipt_from_a_newer_schema()
    {
        var receiptsRoot = Path.Combine(root, "state", "mods");
        Directory.CreateDirectory(receiptsRoot);
        await File.WriteAllTextAsync(ReceiptPath(receiptsRoot, "future"), """
            {
              "schemaVersion": 999,
              "id": "future",
              "name": "Future",
              "version": "1.0",
              "loaderId": "modding-api-77",
              "installRoot": "hollow_knight_Data/Managed/Mods/Future"
            }
            """);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            CreateManager(receiptsRoot).GetInstalledAsync());
    }

    [Fact]
    public async Task Discovery_uses_the_same_receipt_migration_and_backup_path()
    {
        var receiptsRoot = Path.Combine(root, "state", "mods");
        var instanceRoot = Path.Combine(root, "instance");
        Directory.CreateDirectory(receiptsRoot);
        Directory.CreateDirectory(instanceRoot);
        var receiptPath = ReceiptPath(receiptsRoot, "legacy-discovery");
        await File.WriteAllTextAsync(receiptPath, """
            {
              "schemaVersion": 1,
              "id": "legacy-discovery",
              "name": "Legacy Discovery",
              "version": "1.0",
              "loaderId": "modding-api-77",
              "installRoot": "hollow_knight_Data/Managed/Mods/Legacy",
              "isLocal": true,
              "files": []
            }
            """);

        var result = await new ModDiscoveryService(instanceRoot, receiptsRoot)
            .DiscoverAsync("modding-api-77");

        var receipt = Assert.Single(result.InstalledReceipts);
        Assert.Equal(InstalledModReceipt.CurrentSchemaVersion, receipt.SchemaVersion);
        Assert.Equal(ModOwnership.LocalTakenOver, receipt.Ownership);
        Assert.True(File.Exists(receiptPath + ".bak"));
    }

    [Fact]
    public async Task GetInstalled_rejects_receipt_file_outside_recognized_mod_roots()
    {
        var receiptsRoot = Path.Combine(root, "state", "mods");
        Directory.CreateDirectory(receiptsRoot);
        var instanceRoot = Path.Combine(root, "instance");
        Directory.CreateDirectory(instanceRoot);
        var executablePath = Path.Combine(instanceRoot, "hollow_knight.exe");
        await File.WriteAllTextAsync(executablePath, "game");
        await File.WriteAllTextAsync(ReceiptPath(receiptsRoot, "malicious"), $$"""
            {
              "schemaVersion": 2,
              "id": "malicious",
              "name": "Malicious",
              "version": "1.0",
              "loaderId": "modding-api-77",
              "installRoot": "hollow_knight_Data/Managed/Mods/Malicious",
              "files": [
                {
                  "relativePath": "hollow_knight.exe",
                  "sha256": "{{new string('A', 64)}}"
                }
              ],
              "entryFiles": []
            }
            """);

        var manager = CreateManager(receiptsRoot);

        await Assert.ThrowsAsync<InvalidDataException>(() => manager.GetInstalledAsync());
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            manager.UninstallIgnoringDependentsAsync("malicious"));
        Assert.Equal("game", await File.ReadAllTextAsync(executablePath));
    }

    [Fact]
    public async Task GetInstalled_rejects_duplicate_file_ownership_and_invalid_hashes()
    {
        var instanceRoot = Path.Combine(root, "instance");
        Directory.CreateDirectory(instanceRoot);
        var sharedFile = "hollow_knight_Data/Managed/Mods/shared.dll";
        var valid = Receipt("first", sharedFile, new string('A', 64));
        var duplicate = Receipt("second", sharedFile, new string('B', 64));

        Assert.Throws<InvalidDataException>(() => InstalledModReceiptStore.ValidateAll(
            instanceRoot,
            [valid, duplicate]));
        Assert.Throws<InvalidDataException>(() => InstalledModReceiptStore.ValidateAll(
            instanceRoot,
            [Receipt("bad-hash", "hollow_knight_Data/Managed/Mods/bad.dll", "not-a-hash")]));
    }

    [Fact]
    public async Task Missing_primary_receipt_is_recovered_from_backup_without_becoming_external()
    {
        var receiptsRoot = Path.Combine(root, "state", "mods");
        var modFile = "hollow_knight_Data/Managed/Mods/Backup.dll";
        var receipt = Receipt("backup-mod", modFile, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("mod"))));
        var fullModPath = Path.Combine(root, "instance", modFile);
        Directory.CreateDirectory(Path.GetDirectoryName(fullModPath)!);
        await File.WriteAllTextAsync(fullModPath, "mod");
        var path = ReceiptPath(receiptsRoot, receipt.Id);
        await AtomicJsonStore.WriteAsync(path, receipt);
        File.Move(path, path + ".bak");
        var manager = CreateManager(receiptsRoot);

        Assert.Equal(receipt.Id, Assert.Single(await manager.GetInstalledAsync()).Id);
        Assert.Equal(ModOwnership.Managed, Assert.Single((await manager.DiscoverAsync("modding-api-77")).Mods).Ownership);
        await manager.UninstallIgnoringDependentsAsync(receipt.Id);
        Assert.Empty(await manager.GetInstalledAsync());
        Assert.False(File.Exists(path + ".bak"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Invalid_legacy_file_list_is_reported_without_null_reference_failure(bool nullEntry)
    {
        var receiptsRoot = Path.Combine(root, "state", "mods");
        var receipt = Receipt("legacy-broken", "hollow_knight_Data/Managed/Mods/Legacy.dll", new string('A', 64)) with
        {
            SchemaVersion = 1, Files = nullEntry ? [null!] : null!
        };
        await AtomicJsonStore.WriteAsync(ReceiptPath(receiptsRoot, receipt.Id), receipt);

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateManager(receiptsRoot).GetInstalledAsync());
    }

    [Fact]
    public async Task Legacy_relink_backup_is_archived_without_hiding_the_current_receipt()
    {
        var receiptsRoot = Path.Combine(root, "state", "mods");
        var original = Receipt("old-local-id", "hollow_knight_Data/Managed/Mods/Local.dll", new string('A', 64)) with
        {
            IsLocal = true, Ownership = ModOwnership.LocalTakenOver
        };
        var oldPath = ReceiptPath(receiptsRoot, original.Id);
        await AtomicJsonStore.WriteAsync(oldPath + ".bak", original);
        var preserved = await File.ReadAllBytesAsync(oldPath + ".bak");
        var current = original with { Id = "catalog-id", Name = "Catalog Mod", Version = "2" };
        var currentPath = ReceiptPath(receiptsRoot, current.Id);
        await AtomicJsonStore.WriteAsync(currentPath, current);
        var manager = CreateManager(receiptsRoot);

        Assert.Equal(current.Id, Assert.Single(await manager.GetInstalledAsync()).Id);

        Assert.False(File.Exists(oldPath + ".bak"));
        var archive = Assert.Single(Directory.GetFiles(receiptsRoot, "*.relinked-*.bak"));
        Assert.Equal(preserved, await File.ReadAllBytesAsync(archive));
        File.Delete(currentPath);
        Assert.Empty(await manager.GetInstalledAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Relinking_keeps_one_valid_receipt_and_cleans_old_backups(bool obstructDestination)
    {
        var receiptsRoot = Path.Combine(root, "state", "mods");
        var receipt = Receipt("taken-over", "hollow_knight_Data/Managed/Mods/Local.dll", new string('A', 64)) with
        {
            IsLocal = true, Ownership = ModOwnership.LocalTakenOver
        };
        var oldPath = ReceiptPath(receiptsRoot, receipt.Id);
        await AtomicJsonStore.WriteAsync(oldPath, receipt);
        if (obstructDestination)
        {
            Directory.CreateDirectory(ReceiptPath(receiptsRoot, "catalog-mod"));
        }
        else
        {
            await AtomicJsonStore.WriteAsync(oldPath, receipt);
        }
        var manager = CreateManager(receiptsRoot);
        var manifest = new ModManifest
        {
            Id = "catalog-mod", Name = "Catalog Mod", Version = "1", LoaderId = receipt.LoaderId,
            DownloadUrl = "https://example.invalid/mod.zip", Sha256 = new string('B', 64)
        };
        Exception? failure = null;
        try
        {
            await manager.RelinkReceiptToCatalogAsync(receipt.Id, manifest);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            failure = exception;
        }

        var retained = Assert.Single(await manager.GetInstalledAsync());
        Assert.Equal(failure is null ? manifest.Id : receipt.Id, retained.Id);
        Assert.Equal(receipt.Files[0].Sha256, retained.Files[0].Sha256);
        if (failure is null)
        {
            Assert.False(File.Exists(oldPath));
            Assert.False(File.Exists(oldPath + ".bak"));
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private ModManager CreateManager(string receiptsRoot)
    {
        var instanceRoot = Path.Combine(root, "instance");
        Directory.CreateDirectory(instanceRoot);
        return new ModManager(
            instanceRoot,
            Path.Combine(root, "transactions"),
            receiptsRoot);
    }

    private static string ReceiptPath(string receiptsRoot, string id)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)));
        return Path.Combine(receiptsRoot, $"{hash}.json");
    }

    private static InstalledModReceipt Receipt(string id, string relativePath, string sha256) => new()
    {
        Id = id,
        Name = id,
        Version = "1.0",
        LoaderId = "modding-api-77",
        InstallRoot = "hollow_knight_Data/Managed/Mods",
        Files = [new InstalledFileReceipt { RelativePath = relativePath, Sha256 = sha256 }],
        EntryFiles = [relativePath]
    };
}

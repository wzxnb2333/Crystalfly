using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;

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
}

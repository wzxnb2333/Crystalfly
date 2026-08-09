using System.Security.Cryptography;
using System.Text;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;

namespace Crystalfly.Core.Tests.Instances;

public sealed class InstanceImportServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"crystalfly-import-{Guid.NewGuid():N}");

    [Fact]
    public async Task Discover_imports_direct_game_directories_and_marks_unknown_builds()
    {
        var knownRoot = await CreateGameAsync("known", "known");
        var unknownRoot = await CreateGameAsync("unknown", "unknown");
        var catalog = new GameCatalog
        {
            Builds =
            [
                new GameBuild
                {
                    Id = "known-build",
                    DisplayVersion = "Known",
                    ManifestId = "1",
                    ExecutableSha256 = Hash("known-exe"),
                    UnityPlayerSha256 = Hash("known-unity"),
                    GlobalGameManagersSha256 = Hash("known-global")
                }
            ]
        };

        var instances = await InstanceImportService.DiscoverAsync(root, catalog);

        Assert.Equal(2, instances.Count);
        Assert.Equal("known-build", instances.Single(instance => instance.RootPath == knownRoot).BuildId);
        Assert.Equal("unknown", instances.Single(instance => instance.RootPath == unknownRoot).BuildId);
        Assert.All(instances, instance => Assert.True(File.Exists(InstanceSidecar.GetMarkerPath(instance.RootPath))));
    }

    [Fact]
    public async Task Discover_upgrades_a_custom_manifest_identity_after_catalog_verification()
    {
        var instanceRoot = await CreateGameAsync("historical", "verified");
        var record = new InstanceRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "historical",
            RootPath = instanceRoot,
            BuildId = "steam-manifest-42",
            ProvisioningMode = InstanceProvisioningMode.Downloaded,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await InstanceSidecar.SaveAsync(record);
        var catalog = new GameCatalog
        {
            Builds =
            [
                new GameBuild
                {
                    Id = "verified-build",
                    DisplayVersion = "Verified",
                    ManifestId = "42",
                    ExecutableSha256 = Hash("verified-exe"),
                    UnityPlayerSha256 = Hash("verified-unity"),
                    GlobalGameManagersSha256 = Hash("verified-global")
                }
            ]
        };

        var discovered = await InstanceImportService.DiscoverAsync(root, catalog);

        Assert.Equal("verified-build", Assert.Single(discovered).BuildId);
        Assert.Equal("verified-build", (await InstanceSidecar.LoadAsync(instanceRoot))!.BuildId);
    }

    [Fact]
    public async Task Discover_keeps_custom_manifest_identity_when_catalog_fingerprint_does_not_match()
    {
        var instanceRoot = await CreateGameAsync("historical-mismatch", "actual");
        var record = new InstanceRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "historical-mismatch",
            RootPath = instanceRoot,
            BuildId = "steam-manifest-42",
            ProvisioningMode = InstanceProvisioningMode.Downloaded,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await InstanceSidecar.SaveAsync(record);
        var catalog = new GameCatalog
        {
            Builds =
            [
                new GameBuild
                {
                    Id = "verified-build",
                    DisplayVersion = "Verified",
                    ManifestId = "42",
                    ExecutableSha256 = Hash("different-exe"),
                    UnityPlayerSha256 = Hash("different-unity"),
                    GlobalGameManagersSha256 = Hash("different-global")
                }
            ]
        };

        var discovered = await InstanceImportService.DiscoverAsync(root, catalog);

        Assert.Equal("steam-manifest-42", Assert.Single(discovered).BuildId);
        Assert.Equal("steam-manifest-42", (await InstanceSidecar.LoadAsync(instanceRoot))!.BuildId);
    }

    [Fact]
    public async Task Discover_recreates_metadata_when_marker_exists_without_metadata()
    {
        var instanceRoot = await CreateGameAsync("orphaned", "orphaned");
        var record = new InstanceRecord
        {
            Id = "orphaned-instance",
            Name = "orphaned",
            RootPath = instanceRoot,
            BuildId = "unknown",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await InstanceSidecar.SaveAsync(record);
        Directory.Delete(
            Path.GetDirectoryName(InstanceSidecar.GetMetadataPath(instanceRoot, record.Id))!,
            recursive: true);

        var discovered = await InstanceImportService.DiscoverAsync(root, new GameCatalog { Builds = [] });

        var recreated = Assert.Single(discovered);
        Assert.Equal("orphaned-instance", recreated.Id);
        Assert.Equal(record.BuildId, (await InstanceSidecar.LoadAsync(instanceRoot))!.BuildId);
    }

    private async Task<string> CreateGameAsync(string directory, string content)
    {
        var gameRoot = Directory.CreateDirectory(Path.Combine(root, directory)).FullName;
        Directory.CreateDirectory(Path.Combine(gameRoot, "hollow_knight_Data"));
        await File.WriteAllTextAsync(Path.Combine(gameRoot, "hollow_knight.exe"), content + "-exe");
        await File.WriteAllTextAsync(Path.Combine(gameRoot, "UnityPlayer.dll"), content + "-unity");
        await File.WriteAllTextAsync(
            Path.Combine(gameRoot, "hollow_knight_Data", "globalgamemanagers"),
            content + "-global");
        return gameRoot;
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

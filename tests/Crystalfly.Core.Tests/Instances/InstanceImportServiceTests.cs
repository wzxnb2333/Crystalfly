using System.Security.Cryptography;
using System.Text;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;
using Crystalfly.Core.Runtime;

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

    [Fact]
    public async Task Discover_assigns_a_new_identity_to_a_manually_copied_game_without_changing_original()
    {
        var originalRoot = await CreateGameAsync("z-original", "game");
        var copyRoot = await CreateGameAsync("a-copy", "game");
        var original = new InstanceRecord
        {
            Id = "original-id",
            Name = "Original",
            RootPath = originalRoot,
            BuildId = "unknown",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await InstanceSidecar.SaveAsync(original);
        File.Copy(InstanceSidecar.GetMarkerPath(originalRoot), InstanceSidecar.GetMarkerPath(copyRoot));

        var discovered = await InstanceImportService.DiscoverAsync(root, new GameCatalog { Builds = [] });

        Assert.Equal(2, discovered.Select(instance => instance.Id).Distinct().Count());
        Assert.Equal(original.Id, discovered.Single(instance => instance.RootPath == originalRoot).Id);
        Assert.NotEqual(original.Id, (await InstanceSidecar.LoadAsync(copyRoot))!.Id);
        Assert.Equal(original, await InstanceSidecar.LoadAsync(originalRoot));
        var copyIdentity = discovered.Single(instance => instance.RootPath == copyRoot).Id;
        File.Delete(InstanceSidecar.GetMarkerPath(copyRoot));

        var recovered = await InstanceImportService.DiscoverAsync(root, new GameCatalog());

        Assert.Equal(copyIdentity, recovered.Single(instance => instance.RootPath == copyRoot).Id);
        Assert.Equal(original.Id, recovered.Single(instance => instance.RootPath == originalRoot).Id);
    }

    [Fact]
    public async Task Discover_keeps_healthy_games_when_another_marker_is_corrupt()
    {
        var brokenRoot = await CreateGameAsync("a-broken", "broken");
        var healthyRoot = await CreateGameAsync("z-healthy", "healthy");
        await File.WriteAllTextAsync(InstanceSidecar.GetMarkerPath(brokenRoot), "{bad json");

        var discovered = await InstanceImportService.DiscoverAsync(root, new GameCatalog { Builds = [] });

        Assert.Contains(discovered, instance => instance.RootPath == healthyRoot);
        Assert.Equal("{bad json", await File.ReadAllTextAsync(InstanceSidecar.GetMarkerPath(brokenRoot)));
    }

    [Fact]
    public async Task Discover_does_not_register_a_game_through_a_symbolic_link()
    {
        var actual = await CreateGameAsync("actual", "game");
        var linked = Path.Combine(root, "linked");
        Directory.CreateSymbolicLink(linked, actual);

        var discovered = await InstanceImportService.DiscoverAsync(root, new GameCatalog { Builds = [] });

        Assert.Equal(actual, Assert.Single(discovered).RootPath);
    }

    [Theory]
    [InlineData("unknown", "known", "known-build")]
    [InlineData("known-build", "changed", "unknown")]
    public async Task Discover_revalidates_disk_content_instead_of_trusting_an_old_build_id(
        string previousBuildId, string diskContent, string expectedBuildId)
    {
        var game = await CreateGameAsync("game", diskContent);
        await InstanceSidecar.SaveAsync(new InstanceRecord
        {
            Id = "instance", Name = "game", RootPath = game, BuildId = previousBuildId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        var catalog = new GameCatalog
        {
            Builds = [new GameBuild
            {
                Id = "known-build", DisplayVersion = "Known", ManifestId = "1",
                ExecutableSha256 = Hash("known-exe"),
                UnityPlayerSha256 = Hash("known-unity"),
                GlobalGameManagersSha256 = Hash("known-global")
            }]
        };

        var discovered = await InstanceImportService.DiscoverAsync(root, catalog);

        Assert.Equal(expectedBuildId, Assert.Single(discovered).BuildId);
        Assert.Equal(expectedBuildId, (await InstanceSidecar.LoadAsync(game))!.BuildId);
    }

    [Fact]
    public async Task Discover_does_not_erase_a_known_identity_when_the_catalog_is_unavailable()
    {
        var game = await CreateGameAsync("game", "known");
        var record = new InstanceRecord
        {
            Id = "instance", Name = "game", RootPath = game, BuildId = "known-build",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await InstanceSidecar.SaveAsync(record);

        var discovered = await InstanceImportService.DiscoverAsync(root, new GameCatalog());

        Assert.Equal(record.BuildId, Assert.Single(discovered).BuildId);
        Assert.Equal(record, await InstanceSidecar.LoadAsync(game));
    }

    [Fact]
    public async Task Concurrent_discovery_creates_only_one_identity_for_each_game()
    {
        var game = await CreateGameAsync("game", "same");

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            InstanceImportService.DiscoverAsync(root, new GameCatalog { Builds = [] })));

        Assert.All(results, result => Assert.Single(result));
        var identity = Assert.Single(results.SelectMany(result => result).Select(record => record.Id).Distinct());
        Assert.Equal(identity, (await InstanceSidecar.LoadAsync(game))!.Id);
        Assert.Single(Directory.EnumerateDirectories(Path.Combine(root, ".crystalfly", "instances")));
    }

    [Fact]
    public async Task Deleting_an_imported_manual_copy_preserves_original_game_and_saves()
    {
        var originalRoot = await CreateGameAsync("original", "game");
        var original = Assert.Single(await InstanceImportService.DiscoverAsync(root, new GameCatalog()));
        var originalState = Path.GetDirectoryName(InstanceSidecar.GetMetadataPath(originalRoot, original.Id))!;
        var saves = Directory.CreateDirectory(Path.Combine(originalState, "local-low")).FullName;
        await File.WriteAllTextAsync(Path.Combine(saves, "user1.dat"), "keep original save");
        var copy = await CreateGameAsync("copy", "game");
        File.Copy(InstanceSidecar.GetMarkerPath(originalRoot), InstanceSidecar.GetMarkerPath(copy));
        var records = await InstanceImportService.DiscoverAsync(root, new GameCatalog());
        var copyRecord = records.Single(record => record.RootPath == copy);

        var result = await new InstanceDeletionService(root, new StoppedGameProbe()).DeleteAsync(
            copyRecord, new InstanceDeletionConditions { TransactionsHealthy = true });

        Assert.True(result.CleanupCompleted);
        Assert.False(Directory.Exists(copy));
        Assert.Equal(original.Id, (await InstanceSidecar.LoadAsync(originalRoot))!.Id);
        Assert.Equal("keep original save", await File.ReadAllTextAsync(Path.Combine(saves, "user1.dat")));
    }

    [Fact]
    public async Task Discover_propagates_cancellation_without_registering_games()
    {
        var game = await CreateGameAsync("game", "game");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            InstanceImportService.DiscoverAsync(root, new GameCatalog(), cancellation.Token));

        Assert.False(File.Exists(InstanceSidecar.GetMarkerPath(game)));
    }

    private sealed class StoppedGameProbe : IHollowKnightProcessProbe
    {
        public bool IsRunning() => false;
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

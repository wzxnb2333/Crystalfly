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

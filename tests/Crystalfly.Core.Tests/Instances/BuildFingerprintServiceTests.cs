using System.Text;
using Crystalfly.Core.Catalog;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;

namespace Crystalfly.Core.Tests.Instances;

public sealed class BuildFingerprintServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"crystalfly-fingerprint-{Guid.NewGuid():N}");

    [Fact]
    public async Task Silksong_declared_version_is_read_without_marking_the_build_verified()
    {
        var game = Path.Combine(root, "silksong");
        var managed = Path.Combine(game, GameDefinition.Silksong.DataDirectoryName, "Managed");
        Directory.CreateDirectory(managed);
        File.Copy(typeof(BuildFingerprintServiceTests).Assembly.Location, Path.Combine(managed, "Assembly-CSharp.dll"));
        await File.WriteAllTextAsync(Path.Combine(game, GameDefinition.Silksong.ExecutableName), "game");
        await File.WriteAllTextAsync(Path.Combine(game, GameDefinition.Silksong.GlobalGameManagersPath), "data");

        var instance = Assert.Single(await InstanceImportService.DiscoverAsync(root, new GameCatalog()));
        Assert.Equal("1.0.28324", instance.DeclaredVersion);
        Assert.Equal("unknown", instance.BuildId);
        Assert.Equal(instance, await InstanceSidecar.LoadAsync(game));

        await File.WriteAllTextAsync(Path.Combine(managed, "Assembly-CSharp.dll"), "broken assembly");
        var refreshed = Assert.Single(await InstanceImportService.DiscoverAsync(root, new GameCatalog()));
        Assert.Null(refreshed.DeclaredVersion);
        Assert.Equal(instance.Id, refreshed.Id);
        Assert.Equal("unknown", refreshed.BuildId);
    }

    [Fact]
    public async Task Launch_fingerprint_changes_for_game_code_loader_files_and_doorstop_configuration()
    {
        Directory.CreateDirectory(Path.Combine(root, GameDefinition.Silksong.DataDirectoryName, "Managed"));
        Directory.CreateDirectory(Path.Combine(root, "BepInEx", "core"));
        await File.WriteAllTextAsync(Path.Combine(root, GameDefinition.Silksong.ExecutableName), "game");
        await File.WriteAllTextAsync(Path.Combine(root, GameDefinition.Silksong.GlobalGameManagersPath), "data");
        var fingerprint = await BuildFingerprintService.CalculateAsync(root, GameId.Silksong);
        var previous = await BuildFingerprintService.CalculateLaunchFingerprintAsync(root, fingerprint);
        foreach (var relative in new[] { "Hollow Knight Silksong_Data/Managed/Assembly-CSharp.dll", "BepInEx/core/BepInEx.dll", "doorstop_config.ini" })
        {
            await File.WriteAllTextAsync(Path.Combine(root, relative), "changed");
            var changed = await BuildFingerprintService.CalculateLaunchFingerprintAsync(root, fingerprint);
            Assert.NotEqual(previous, changed);
            previous = changed;
        }
    }

    [Fact]
    public void FindBuild_distinguishes_verified_12459_from_12620_despite_shared_player_binaries()
    {
        var catalog = EmbeddedCatalog.Load();
        var fingerprint = new BuildFingerprint
        {
            ExecutableSha256 = "7961BBA7243CE322163AD6B0DDFD6EF701413020B92AD0D98502526A396171B0",
            UnityPlayerSha256 = "8F2D601F8D3C7F4D29D80BA786C0BE873102BB7E6041EB03964A90B99724D90B",
            GlobalGameManagersSha256 = "C9720F4B34131058C97766A9A92A1C4FFCE3B407647DE192C6D4DF46253EAFA9"
        };

        Assert.Equal("1.5.12459", BuildFingerprintService.FindBuild(catalog.Builds, fingerprint)?.Id);
        var laterBuild = catalog.Builds.Single(build => build.Id == "1.5.12620.0");
        Assert.Equal(fingerprint.ExecutableSha256, laterBuild.ExecutableSha256);
        Assert.Equal(fingerprint.UnityPlayerSha256, laterBuild.UnityPlayerSha256);
        Assert.Equal("1.5.12620.0", BuildFingerprintService.FindBuild(catalog.Builds,
            fingerprint with { GlobalGameManagersSha256 = laterBuild.GlobalGameManagersSha256 })?.Id);
        Assert.Null(BuildFingerprintService.FindBuild(catalog.Builds,
            fingerprint with { GlobalGameManagersSha256 = new string('0', 64) }));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

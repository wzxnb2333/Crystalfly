using Crystalfly.Core.Catalog;

namespace Crystalfly.Core.Tests.Catalog;

public sealed class EmbeddedCatalogTests
{
    [Fact]
    public void Load_returns_verified_official_fallback_data()
    {
        var catalog = EmbeddedCatalog.Load();

        var build1221 = catalog.Builds.Single(build => build.Id == "1.2.2.1");
        Assert.Equal("648876203478229944", build1221.ManifestId);
        Assert.Equal("1434454FCB5A1F4FFF329EA56182A7C7DA1581DC0F4B6DCEF8585E739F416217", build1221.ExecutableSha256);
        Assert.Null(build1221.UnityPlayerSha256);
        Assert.Equal("58BC88B74D6F05B9E00D7E1F2BC9B3BA6E9FC51F75C6915DF10BF10B90CDD749", build1221.GlobalGameManagersSha256);

        Assert.Equal(
            "3AE7AD0A4658406A056D4D0C75E6787A553ABE3D0ADBCA510BFE8BBEC3111C67",
            catalog.Builds.Single(build => build.Id == "1.4.3.2").UnityPlayerSha256);
        Assert.Equal(
            "D97E92A7640B10580B4E60139EACF01828F74BAAEF53F75E08B9FDD6193FBE5E",
            catalog.Builds.Single(build => build.Id == "1.5.78.11833").UnityPlayerSha256);

        var latest = catalog.Builds.Single(build => build.Id == "1.5.12620.0");
        Assert.Equal("1.5.12620.0", latest.DisplayVersion);
        Assert.Equal("257781644874438846", latest.ManifestId);
        Assert.Equal("8F2D601F8D3C7F4D29D80BA786C0BE873102BB7E6041EB03964A90B99724D90B", latest.UnityPlayerSha256);
        Assert.Equal("1.5.12620.0", catalog.Channels.Single(channel => channel.Name == "latest").BuildId);

        var bepinex = catalog.Loaders.Single(loader => loader.Id == "bepinex-5.4.23.4");
        Assert.Equal(638940, bepinex.SizeBytes);
        Assert.Equal("F881201B79DA03E513BF97CDF39607FFA7F9E0D31A519B1AEECA8EB60F8309E7", bepinex.Sha256);
        Assert.Equal(["1.5.12620.0"], bepinex.SupportedBuildIds);

        var moddingApi37 = catalog.Loaders.Single(loader => loader.Id == "modding-api-37");
        Assert.Equal(932719, moddingApi37.SizeBytes);
        Assert.Equal("ECFF6C73C40194E9D8118C14B9ADE6862DB91E3949D8150027A86FD83BF290F7", moddingApi37.Sha256);

        var moddingApi77 = catalog.Loaders.Single(loader => loader.Id == "modding-api-77");
        Assert.Equal("BC9F0DB3D0916B05CD5A2420BB602FB1B239CE3FF6C289FD84BFFB682FB8F1D6", moddingApi77.Sha256);

        var moddingApi78 = catalog.Loaders.Single(loader => loader.Id == "modding-api-78");
        Assert.Equal(4982043, moddingApi78.SizeBytes);
        Assert.Equal("5B5EBDDA651171E3C5EA6F13FB68FFE2D1B5F8B97A9D6FDE0EED3EA529418748", moddingApi78.Sha256);
        Assert.Equal(["1.5.12620.0"], moddingApi78.SupportedBuildIds);

        var screenShake = catalog.SpeedrunAssets.Single(asset => asset.Id == "screen-shake-modifier-1221");
        Assert.Equal(2832384, screenShake.SizeBytes);
        Assert.Equal("EF25E8E55765230B9D12554355BB069189DD0AA0AEDAB684084DAD297D5391FA", screenShake.Sha256);

        var race1221 = catalog.SpeedrunTemplates.Single(template => template.Id == "race-1221");
        Assert.False(race1221.LoadNormaliserAvailable);
        Assert.Equal(["screen-shake-modifier-1221"], race1221.RequiredAssetIds);

        var single1221 = catalog.SpeedrunTemplates.Single(template => template.Id == "single-run-1221");
        Assert.Equal(["screen-shake-modifier-1221"], single1221.RequiredAssetIds);

        var single1578 = catalog.SpeedrunTemplates.Single(template => template.Id == "single-run-1578");
        Assert.Empty(single1578.RequiredAssetIds);
        Assert.False(single1578.RequiresLoadNormaliserSelection);

        var race1578 = catalog.SpeedrunTemplates.Single(template => template.Id == "race-1578");
        Assert.Equal(["load-normaliser-1.1"], race1578.RequiredAssetIds);
        Assert.True(race1578.RequiresLoadNormaliserSelection);
        Assert.Equal([1, 2, 3, 5], race1578.AllowedLoadNormaliserSeconds);
        Assert.Equal(4, catalog.SpeedrunTemplates.Count);
    }
}

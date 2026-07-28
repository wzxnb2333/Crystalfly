using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;

namespace Crystalfly.Core.Tests.Mods;

public sealed class ModCatalogCompatibilityTests
{
    [Fact]
    public void ProjectForBuild_maps_official_1578_mods_to_api78_on_latest_build()
    {
        var legacy = Manifest("HK ModLinks", "modding-api-77", "1.5.78.11833");

        var projected = ModCatalogCompatibility.ProjectForBuild(
            [legacy],
            "1.5.12620.0");

        var mod = Assert.Single(projected);
        Assert.Equal("modding-api-78", mod.LoaderId);
        Assert.Equal(["1.5.12620.0"], mod.SupportedBuildIds);
        Assert.Equal(legacy.DownloadUrl, mod.DownloadUrl);
        Assert.Equal(legacy.Sha256, mod.Sha256);
    }

    [Fact]
    public void ProjectForBuild_preserves_legacy_and_non_official_bindings()
    {
        var official = Manifest("HK ModLinks", "modding-api-77", "1.5.78.11833");
        var custom = Manifest("Custom", "modding-api-77", "1.5.78.11833") with { Id = "custom:test" };

        Assert.Same(
            official,
            Assert.Single(ModCatalogCompatibility.ProjectForBuild([official], "1.5.78.11833")));
        Assert.Same(
            custom,
            Assert.Single(ModCatalogCompatibility.ProjectForBuild([custom], "1.5.12620.0")));
    }

    [Fact]
    public void Supported_bindings_include_legacy_and_latest_api_pairs()
    {
        var official = Manifest("HK ModLinks", "modding-api-77", "1.5.78.11833");

        Assert.Equal(
            ["1.5.78.11833", "1.5.12620.0"],
            ModCatalogCompatibility.GetSupportedBuildIds(official));
        Assert.Equal(
            ["modding-api-77", "modding-api-78"],
            ModCatalogCompatibility.GetSupportedLoaderIds(official));
        Assert.True(ModCatalogCompatibility.Supports(
            official,
            "1.5.12620.0",
            "modding-api-78"));
        Assert.False(ModCatalogCompatibility.Supports(
            official,
            "1.5.78.11833",
            "modding-api-78"));
    }

    private static ModManifest Manifest(string source, string loader, string build) => new()
    {
        Id = "hkmod:Test",
        Name = "Test",
        SourceName = source,
        Version = "1.0",
        DownloadUrl = "https://example.invalid/test.zip",
        Sha256 = new string('A', 64),
        LoaderId = loader,
        SupportedBuildIds = [build]
    };
}

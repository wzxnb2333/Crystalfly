using Crystalfly.Core.Catalog;
using Crystalfly.Core.Models;

namespace Crystalfly.Core.Tests.Catalog;

public sealed class CustomCatalogSourceTests
{
    [Fact]
    public void Namespace_allows_mods_without_granting_official_catalog_authority()
    {
        var source = new GameCatalog
        {
            Builds = [Build("override")],
            Mods =
            [
                Mod("base"),
                Mod("feature", "base", "hkmod:Satchel")
            ],
            SpeedrunTemplates =
            [
                new SpeedrunTemplate
                {
                    Id = "official-looking",
                    Name = "Untrusted",
                    BuildId = "1.5.78.11833"
                }
            ]
        };

        var namespaced = CustomCatalogSource.Namespace("community", source);

        Assert.Empty(namespaced.Builds);
        Assert.Empty(namespaced.SpeedrunTemplates);
        Assert.Equal(["custom:community:base", "custom:community:feature"], namespaced.Mods.Select(mod => mod.Id));
        Assert.Equal(
            ["custom:community:base", "hkmod:Satchel"],
            namespaced.Mods.Single(mod => mod.Id.EndsWith(":feature", StringComparison.Ordinal)).Dependencies);
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad namespace")]
    [InlineData("../escape")]
    public void Namespace_rejects_invalid_names(string value) =>
        Assert.Throws<ArgumentException>(() => CustomCatalogSource.Namespace(value, new GameCatalog()));

    private static ModManifest Mod(string id, params string[] dependencies) => new()
    {
        Id = id,
        Name = id,
        Version = "1",
        DownloadUrl = "https://example.invalid/mod.zip",
        Sha256 = new string('A', 64),
        LoaderId = "modding-api-77",
        SupportedBuildIds = ["1.5.78.11833"],
        Dependencies = dependencies
    };

    private static GameBuild Build(string id) => new()
    {
        Id = id,
        DisplayVersion = id,
        ManifestId = "1",
        ExecutableSha256 = new string('A', 64),
        GlobalGameManagersSha256 = new string('B', 64)
    };
}

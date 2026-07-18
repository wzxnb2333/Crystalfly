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
        Assert.All(namespaced.Mods, mod => Assert.Equal("community", mod.SourceName));
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

    [Theory]
    [InlineData("null-dependencies")]
    [InlineData("null-authors")]
    [InlineData("null-tags")]
    [InlineData("null-integrations")]
    [InlineData("duplicate-dependencies")]
    [InlineData("duplicate-authors")]
    [InlineData("duplicate-tags")]
    [InlineData("duplicate-integrations")]
    [InlineData("insecure-repository")]
    [InlineData("insecure-issues")]
    public void Namespace_rejects_invalid_mod_metadata_before_rewriting_ids(string invalidKind)
    {
        var mod = Mod("broken");
        mod = invalidKind switch
        {
            "null-dependencies" => mod with { Dependencies = null! },
            "null-authors" => mod with { Authors = null! },
            "null-tags" => mod with { Tags = null! },
            "null-integrations" => mod with { Integrations = null! },
            "duplicate-dependencies" => mod with { Dependencies = ["shared", "SHARED"] },
            "duplicate-authors" => mod with { Authors = ["Author", "author"] },
            "duplicate-tags" => mod with { Tags = ["Utility", "utility"] },
            "duplicate-integrations" => mod with { Integrations = ["Menu", "menu"] },
            "insecure-repository" => mod with { RepositoryUrl = "http://example.invalid/repository" },
            "insecure-issues" => mod with { IssuesUrl = "ftp://example.invalid/issues" },
            _ => throw new ArgumentOutOfRangeException(nameof(invalidKind))
        };

        var exception = Assert.Throws<InvalidDataException>(() =>
            CustomCatalogSource.Namespace("community", new GameCatalog { Mods = [mod] }));

        Assert.Contains("broken", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

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

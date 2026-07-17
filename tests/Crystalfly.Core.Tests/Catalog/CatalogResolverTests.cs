using Crystalfly.Core.Catalog;
using Crystalfly.Core.Models;

namespace Crystalfly.Core.Tests.Catalog;

public sealed class CatalogResolverTests
{
    [Fact]
    public void Latest_resolution_uses_current_catalog_channel_mapping()
    {
        var older = CreateCatalog("older");
        var newer = CreateCatalog("newer");

        Assert.Equal("older", CatalogResolver.ResolveBuild(older, "latest").Id);
        Assert.Equal("newer", CatalogResolver.ResolveBuild(newer, "latest").Id);
    }

    private static GameCatalog CreateCatalog(string buildId) => new()
    {
        Builds =
        [
            new GameBuild
            {
                Id = buildId,
                DisplayVersion = buildId,
                DepotId = 367521,
                ManifestId = buildId,
                ExecutableSha256 = new string('A', 64),
                GlobalGameManagersSha256 = new string('B', 64)
            }
        ],
        Channels = [new GameChannel { Name = "latest", BuildId = buildId }]
    };
}

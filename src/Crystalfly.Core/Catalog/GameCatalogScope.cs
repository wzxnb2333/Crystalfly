using Crystalfly.Core.Models;

namespace Crystalfly.Core.Catalog;

public static class GameCatalogScope
{
    public static GameCatalog ForGame(GameCatalog catalog, GameId gameId) => catalog with
    {
        Builds = catalog.Builds.Where(item => item.GameId == gameId).ToArray(),
        Channels = catalog.Channels.Where(item => item.GameId == gameId).ToArray(),
        Loaders = catalog.Loaders.Where(item => item.GameId == gameId).ToArray(),
        Mods = catalog.Mods.Where(item => item.GameId == gameId).ToArray(),
        SpeedrunAssets = gameId == GameId.HollowKnight ? catalog.SpeedrunAssets : [],
        SpeedrunTemplates = gameId == GameId.HollowKnight ? catalog.SpeedrunTemplates : [],
        SpeedrunFileManifests = gameId == GameId.HollowKnight ? catalog.SpeedrunFileManifests : []
    };
}

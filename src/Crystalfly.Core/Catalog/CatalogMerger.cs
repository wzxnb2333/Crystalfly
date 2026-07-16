using Crystalfly.Core.Models;

namespace Crystalfly.Core.Catalog;

public static class CatalogMerger
{
    public static GameCatalog Merge(
        GameCatalog embedded,
        GameCatalog? cache,
        GameCatalog? remote,
        IEnumerable<GameCatalog>? customCatalogs = null)
    {
        var trustedCatalogs = new[] { embedded, cache, remote }.OfType<GameCatalog>();
        var customs = customCatalogs ?? [];

        return new GameCatalog
        {
            Builds = MergeEntries(trustedCatalogs, customs, catalog => catalog.Builds, build => build.Id),
            Channels = MergeEntries(trustedCatalogs, customs, catalog => catalog.Channels, channel => channel.Name),
            Loaders = MergeEntries(trustedCatalogs, customs, catalog => catalog.Loaders, loader => loader.Id),
            Mods = MergeEntries(trustedCatalogs, customs, catalog => catalog.Mods, mod => mod.Id),
            SpeedrunTemplates = MergeEntries(
                trustedCatalogs,
                customs,
                catalog => catalog.SpeedrunTemplates,
                template => template.Id),
            SpeedrunAssets = MergeEntries(
                trustedCatalogs,
                customs,
                catalog => catalog.SpeedrunAssets,
                asset => asset.Id)
        };
    }

    private static IReadOnlyList<T> MergeEntries<T>(
        IEnumerable<GameCatalog> trustedCatalogs,
        IEnumerable<GameCatalog> customCatalogs,
        Func<GameCatalog, IEnumerable<T>> entrySelector,
        Func<T, string> keySelector)
    {
        var entries = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var catalog in trustedCatalogs)
        {
            foreach (var entry in entrySelector(catalog))
            {
                entries[keySelector(entry)] = entry;
            }
        }

        foreach (var catalog in customCatalogs)
        {
            foreach (var entry in entrySelector(catalog))
            {
                entries.TryAdd(keySelector(entry), entry);
            }
        }

        return entries.Values.ToArray();
    }
}

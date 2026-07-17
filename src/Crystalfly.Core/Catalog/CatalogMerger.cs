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
        var trustedCatalogs = new[] { embedded, remote }.OfType<GameCatalog>().ToArray();
        var trustedBuildIds = trustedCatalogs
            .SelectMany(catalog => catalog.Builds)
            .Select(build => build.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        GameCatalog[] fallbackCatalogs = remote is null && cache is not null
            ? [new GameCatalog
            {
                Channels = cache.Channels
                    .Where(channel => trustedBuildIds.Contains(channel.BuildId))
                    .ToArray()
            }]
            : [];
        var untrustedCatalogs = fallbackCatalogs.Concat(customCatalogs ?? []);

        return new GameCatalog
        {
            Builds = MergeEntries(trustedCatalogs, untrustedCatalogs, catalog => catalog.Builds, build => build.Id),
            Channels = MergeEntries(trustedCatalogs, untrustedCatalogs, catalog => catalog.Channels, channel => channel.Name),
            Loaders = MergeEntries(trustedCatalogs, untrustedCatalogs, catalog => catalog.Loaders, loader => loader.Id),
            Mods = MergeEntries(trustedCatalogs, untrustedCatalogs, catalog => catalog.Mods, mod => mod.Id),
            SpeedrunTemplates = MergeEntries(
                trustedCatalogs,
                untrustedCatalogs,
                catalog => catalog.SpeedrunTemplates,
                template => template.Id),
            SpeedrunAssets = MergeEntries(
                trustedCatalogs,
                untrustedCatalogs,
                catalog => catalog.SpeedrunAssets,
                asset => asset.Id),
            SpeedrunFileManifests = MergeEntries(
                trustedCatalogs,
                untrustedCatalogs,
                catalog => catalog.SpeedrunFileManifests,
                manifest => manifest.Id)
        };
    }

    private static IReadOnlyList<T> MergeEntries<T>(
        IEnumerable<GameCatalog> trustedCatalogs,
        IEnumerable<GameCatalog> untrustedCatalogs,
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

        foreach (var catalog in untrustedCatalogs)
        {
            foreach (var entry in entrySelector(catalog))
            {
                entries.TryAdd(keySelector(entry), entry);
            }
        }

        return entries.Values.ToArray();
    }
}

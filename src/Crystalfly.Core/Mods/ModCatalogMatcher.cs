using Crystalfly.Core.Models;

namespace Crystalfly.Core.Mods;

public static class ModCatalogMatcher
{
    /// <summary>
    /// Suggests catalog mods that plausibly correspond to a discovered external
    /// mod on disk. The catalog stores only package-level hashes, so matching is
    /// heuristic: a mod name or one of its flat files must match the external
    /// entry, filtered by the loader family and the instance build.
    /// </summary>
    public static IReadOnlyList<ModManifest> Match(
        ModDiscoveryEntry external,
        string buildId,
        IReadOnlyList<ModManifest> catalogMods)
    {
        ArgumentNullException.ThrowIfNull(external);
        ArgumentNullException.ThrowIfNull(catalogMods);
        var externalNames = new[] { external.Name }
            .Concat(external.EntryFiles)
            .Concat(external.Files)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(FileName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var candidates = new List<ModManifest>();
        foreach (var manifest in catalogMods)
        {
            if (!NameMatches(manifest, externalNames))
            {
                continue;
            }
            if (!SupportsLoader(manifest, external.LoaderId))
            {
                continue;
            }
            if (!string.IsNullOrWhiteSpace(buildId) && !SupportsBuild(manifest, buildId))
            {
                continue;
            }
            candidates.Add(manifest);
        }

        return candidates
            .OrderBy(manifest => manifest.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(manifest => manifest.Version, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool NameMatches(ModManifest manifest, IReadOnlyList<string> externalNames)
    {
        if (externalNames.Contains(manifest.Name, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }
        var manifestFileNames = manifest.FlatFiles
            .Select(FileName)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray();
        return manifestFileNames.Length != 0
            && externalNames.Any(name => manifestFileNames.Contains(name, StringComparer.OrdinalIgnoreCase));
    }

    private static string FileName(string path) =>
        Path.GetFileName(path.TrimEnd('/', '\\'));

    private static bool SupportsLoader(ModManifest manifest, string loaderId)
    {
        var supported = ModCatalogCompatibility.GetSupportedLoaderIds(manifest);
        if (supported.Contains(loaderId, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }
        // External discovery reports the generic "modding-api-external" or
        // "bepinex-external" family, while the catalog lists a concrete version.
        // Match the family prefix in that case.
        return loaderId.EndsWith("-external", StringComparison.OrdinalIgnoreCase)
            && supported.Any(supported => supported.StartsWith(
                loaderId[..^"-external".Length] + "-",
                StringComparison.OrdinalIgnoreCase));
    }

    private static bool SupportsBuild(ModManifest manifest, string buildId) =>
        ModCatalogCompatibility.GetSupportedBuildIds(manifest)
            .Contains(buildId, StringComparer.OrdinalIgnoreCase);
}

using Crystalfly.Core.Models;

namespace Crystalfly.Core.Mods;

public static class ModCatalogCompatibility
{
    public const string LegacyBuildId = "1.5.78.11833";
    public const string LatestBuildId = "1.5.12620.0";
    public const string LegacyLoaderId = "modding-api-77";
    public const string LatestLoaderId = "modding-api-78";

    public static GameCatalog ProjectForBuild(GameCatalog catalog, string buildId)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return catalog with { Mods = ProjectForBuild(catalog.Mods, buildId) };
    }

    public static IReadOnlyList<ModManifest> ProjectForBuild(
        IReadOnlyList<ModManifest> mods,
        string buildId)
    {
        ArgumentNullException.ThrowIfNull(mods);
        return string.IsNullOrWhiteSpace(buildId)
            ? mods
            : mods.Select(mod => ProjectForBuild(mod, buildId)).ToArray();
    }

    public static ModManifest ProjectForBuild(ModManifest mod, string buildId)
    {
        ArgumentNullException.ThrowIfNull(mod);
        if (string.IsNullOrWhiteSpace(buildId))
        {
            return mod;
        }
        return IsLegacyOfficialMod(mod)
            && string.Equals(buildId, LatestBuildId, StringComparison.OrdinalIgnoreCase)
                ? mod with
                {
                    LoaderId = LatestLoaderId,
                    SupportedBuildIds = [LatestBuildId]
                }
                : mod;
    }

    public static IReadOnlyList<string> GetSupportedBuildIds(ModManifest mod)
    {
        ArgumentNullException.ThrowIfNull(mod);
        return IsLegacyOfficialMod(mod)
            ? [LegacyBuildId, LatestBuildId]
            : mod.SupportedBuildIds;
    }

    public static IReadOnlyList<string> GetSupportedLoaderIds(ModManifest mod)
    {
        ArgumentNullException.ThrowIfNull(mod);
        return IsLegacyOfficialMod(mod)
            ? [LegacyLoaderId, LatestLoaderId]
            : [mod.LoaderId];
    }

    public static bool Supports(ModManifest mod, string buildId, string loaderId)
    {
        var projected = ProjectForBuild(mod, buildId);
        return projected.SupportedBuildIds.Contains(buildId, StringComparer.OrdinalIgnoreCase)
            && string.Equals(projected.LoaderId, loaderId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLegacyOfficialMod(ModManifest mod) =>
        string.Equals(mod.SourceName, "HK ModLinks", StringComparison.OrdinalIgnoreCase)
        && mod.Id.StartsWith("hkmod:", StringComparison.OrdinalIgnoreCase)
        && string.Equals(mod.LoaderId, LegacyLoaderId, StringComparison.OrdinalIgnoreCase)
        && mod.SupportedBuildIds.Contains(LegacyBuildId, StringComparer.OrdinalIgnoreCase);
}

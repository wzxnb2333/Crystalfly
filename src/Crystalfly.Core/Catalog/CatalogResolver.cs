using Crystalfly.Core.Models;

namespace Crystalfly.Core.Catalog;

public static class CatalogResolver
{
    public static GameBuild ResolveBuild(GameCatalog catalog, string buildOrChannel)
    {
        var channel = catalog.Channels.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, buildOrChannel, StringComparison.OrdinalIgnoreCase));
        var buildId = channel?.BuildId ?? buildOrChannel;

        return catalog.Builds.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, buildId, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Build or channel '{buildOrChannel}' was not found.");
    }
}

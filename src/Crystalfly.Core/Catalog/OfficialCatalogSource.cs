using Crystalfly.Core.Models;

namespace Crystalfly.Core.Catalog;

public static class OfficialCatalogSource
{
    private static readonly Uri ModLinksUri = new(
        "https://raw.githubusercontent.com/hk-modding/modlinks/main/ModLinks.xml");
    private static readonly Uri ApiLinksUri = new(
        "https://raw.githubusercontent.com/hk-modding/modlinks/main/ApiLinks.xml");

    public static async Task<GameCatalog> LoadAsync(
        HttpClient httpClient,
        CancellationToken cancellationToken = default)
    {
        var modLinks = await httpClient.GetStringAsync(ModLinksUri, cancellationToken);
        var apiLinks = await httpClient.GetStringAsync(ApiLinksUri, cancellationToken);
        return new GameCatalog
        {
            Loaders = [OfficialXmlCatalogParser.ParseApi(apiLinks, "1.5.78.11833")],
            Mods = OfficialXmlCatalogParser.ParseMods(
                modLinks,
                "modding-api-77",
                "1.5.78.11833")
        };
    }
}

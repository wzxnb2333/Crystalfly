using System.Text.Json;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Catalog;

public static class CatalogProvider
{
    public static async Task<GameCatalog> LoadAsync(
        Uri remoteCatalogUrl,
        string cachePath,
        HttpClient httpClient,
        IEnumerable<GameCatalog>? customCatalogs = null,
        CancellationToken cancellationToken = default)
    {
        GameCatalog? cache = null;
        if (File.Exists(cachePath))
        {
            try
            {
                cache = await AtomicJsonStore.ReadAsync<GameCatalog>(cachePath, cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or JsonException)
            {
            }
        }

        GameCatalog? remote = null;
        try
        {
            using var response = await httpClient.GetAsync(remoteCatalogUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            remote = await JsonSerializer.DeserializeAsync<GameCatalog>(
                stream,
                CrystalflyJson.Options,
                cancellationToken)
                ?? throw new JsonException("Remote catalog did not contain a catalog value.");
            await AtomicJsonStore.WriteAsync(cachePath, remote, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException)
        {
        }

        return CatalogMerger.Merge(EmbeddedCatalog.Load(), cache, remote, customCatalogs);
    }
}

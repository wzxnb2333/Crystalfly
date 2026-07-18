using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;
using System.Text.Json;
using System.Xml;

namespace Crystalfly.Core.Catalog;

public static class OfficialCatalogSource
{
    private static readonly Uri ModLinksUri = new(
        "https://raw.githubusercontent.com/hk-modding/modlinks/main/ModLinks.xml");
    private static readonly Uri ApiLinksUri = new(
        "https://raw.githubusercontent.com/hk-modding/modlinks/main/ApiLinks.xml");

    public static async Task<GameCatalog> LoadAsync(
        HttpClient httpClient,
        CancellationToken cancellationToken = default) =>
        await FetchAsync(httpClient, cancellationToken);

    public static async Task<OfficialCatalogLoadResult> LoadAsync(
        HttpClient httpClient,
        string cachePath,
        CancellationToken cancellationToken = default)
    {
        Exception remoteFailure;
        try
        {
            var catalog = await FetchAsync(httpClient, cancellationToken);
            await AtomicJsonStore.WriteAsync(cachePath, catalog, cancellationToken);
            return Result(OfficialCatalogLoadStatus.Remote, catalog, null);
        }
        catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
        {
            remoteFailure = exception;
        }

        try
        {
            var catalog = await AtomicJsonStore.ReadAsync<GameCatalog>(cachePath, cancellationToken);
            Validate(catalog);
            return Result(OfficialCatalogLoadStatus.Cached, catalog, remoteFailure.Message);
        }
        catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
        {
            return Result(
                OfficialCatalogLoadStatus.Failed,
                new GameCatalog(),
                $"Remote: {remoteFailure.Message} Cache: {exception.Message}");
        }
    }

    private static async Task<GameCatalog> FetchAsync(
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        var modLinks = await httpClient.GetStringAsync(ModLinksUri, cancellationToken);
        var apiLinks = await httpClient.GetStringAsync(ApiLinksUri, cancellationToken);
        var loader = OfficialXmlCatalogParser.ParseApi(apiLinks, "1.5.78.11833");
        return Validate(new GameCatalog
        {
            Loaders = [loader],
            Mods = OfficialXmlCatalogParser.ParseMods(
                modLinks,
                loader.Id,
                "1.5.78.11833")
        });
    }

    private static GameCatalog Validate(GameCatalog catalog)
    {
        CatalogProvider.ValidateSource(catalog);
        CatalogProvider.ValidateResolved(
            CatalogMerger.Merge(EmbeddedCatalog.Load(), null, null, [catalog]));
        return catalog;
    }

    private static OfficialCatalogLoadResult Result(
        OfficialCatalogLoadStatus status,
        GameCatalog catalog,
        string? reason)
    {
        if (catalog.Loaders is null || catalog.Mods is null)
        {
            throw new InvalidDataException("Official catalog cache is incomplete.");
        }
        return new(
            status,
            catalog,
            catalog.Loaders.FirstOrDefault(loader =>
                loader.Id.StartsWith("modding-api-", StringComparison.OrdinalIgnoreCase))?.Version,
            catalog.Mods.Count,
            reason);
    }

    private static bool IsRecoverable(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException
            or IOException
            or JsonException
            or InvalidDataException
            or XmlException
        || exception is OperationCanceledException && !cancellationToken.IsCancellationRequested;
}

public enum OfficialCatalogLoadStatus
{
    Remote,
    Cached,
    Failed
}

public sealed record OfficialCatalogLoadResult(
    OfficialCatalogLoadStatus Status,
    GameCatalog Catalog,
    string? ApiVersion,
    int ModCount,
    string? Reason);

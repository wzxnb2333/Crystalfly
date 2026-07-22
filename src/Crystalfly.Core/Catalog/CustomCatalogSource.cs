using System.Text.Json;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Catalog;

public static class CustomCatalogSource
{
    public static async Task<GameCatalog> LoadAsync(
        string sourceNamespace,
        Uri uri,
        HttpClient httpClient,
        CancellationToken cancellationToken = default)
    {
        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Custom catalog URL must use HTTPS.", nameof(uri));
        }

        var source = await LoadRemoteSourceAsync(uri, httpClient, cancellationToken);
        return Namespace(sourceNamespace, source);
    }

    public static async Task<CustomCatalogLoadResult> LoadWithCacheAsync(
        string sourceNamespace,
        Uri uri,
        string cachePath,
        HttpClient httpClient,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cachePath);
        _ = Namespace(sourceNamespace, new GameCatalog());
        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Custom catalog URL must use HTTPS.", nameof(uri));
        }

        GameCatalog? cachedCatalog = null;
        if (File.Exists(cachePath))
        {
            try
            {
                var cachedSource = await AtomicJsonStore.ReadAsync<GameCatalog>(cachePath, cancellationToken);
                cachedCatalog = Namespace(sourceNamespace, cachedSource);
            }
            catch (Exception exception) when (exception is IOException
                or JsonException
                or InvalidDataException
                or ArgumentException)
            {
            }
        }

        try
        {
            var remoteSource = await LoadRemoteSourceAsync(uri, httpClient, cancellationToken);
            var remoteCatalog = Namespace(sourceNamespace, remoteSource);
            await AtomicJsonStore.WriteAsync(cachePath, remoteSource, cancellationToken);
            return new CustomCatalogLoadResult(
                remoteCatalog,
                CustomCatalogLoadStatus.Remote,
                null);
        }
        catch (Exception exception) when (IsRecoverableRemoteFailure(exception, cancellationToken))
        {
            if (cachedCatalog is not null)
            {
                return new CustomCatalogLoadResult(
                    cachedCatalog,
                    CustomCatalogLoadStatus.Cached,
                    exception.Message);
            }

            throw new InvalidDataException(
                $"Custom catalog '{sourceNamespace}' is unavailable and has no valid cache.",
                exception);
        }
    }

    private static async Task<GameCatalog> LoadRemoteSourceAsync(
        Uri uri,
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<GameCatalog>(
            stream,
            CrystalflyJson.Options,
            cancellationToken) ?? throw new JsonException("Custom catalog was empty.");
    }

    private static bool IsRecoverableRemoteFailure(
        Exception exception,
        CancellationToken cancellationToken) => exception is HttpRequestException
        or IOException
        or JsonException
        or InvalidDataException
        || exception is OperationCanceledException && !cancellationToken.IsCancellationRequested;

    public static GameCatalog Namespace(string sourceNamespace, GameCatalog source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceNamespace);
        if (sourceNamespace.Any(character =>
            !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '_' and not '-'))
        {
            throw new ArgumentException("Custom catalog namespace contains unsupported characters.", nameof(sourceNamespace));
        }

        CatalogProvider.ValidateSource(source);
        var prefix = $"custom:{sourceNamespace}:";
        var localIds = source.Mods.Select(mod => mod.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new GameCatalog
        {
            Mods = source.Mods.Select(mod => mod with
            {
                Id = prefix + mod.Id,
                SourceName = sourceNamespace,
                Dependencies = mod.Dependencies
                    .Select(dependency => localIds.Contains(dependency) ? prefix + dependency : dependency)
                    .ToArray()
            }).ToArray()
        };
    }
}

public enum CustomCatalogLoadStatus
{
    Remote,
    Cached
}

public sealed record CustomCatalogLoadResult(
    GameCatalog Catalog,
    CustomCatalogLoadStatus Status,
    string? Reason);

using System.Text.Json;
using System.Xml;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Catalog;

public static class CustomModLinksSource
{
    public static async Task<CustomModLinksLoadResult> LoadAsync(
        CustomModLinksDefinition definition,
        HttpClient httpClient,
        string cachePath,
        CancellationToken cancellationToken = default)
    {
        ValidateDefinition(definition);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(cachePath);

        GameCatalog? cached = null;
        try
        {
            var candidate = await AtomicJsonStore.ReadAsync<CustomModLinksCache>(
                cachePath,
                cancellationToken);
            if (candidate.Definition is not null
                && candidate.Catalog is not null
                && SameIdentity(candidate.Definition, definition))
            {
                cached = ValidateCatalog(candidate.Catalog, definition);
            }
        }
        catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
        {
        }

        try
        {
            using var response = await httpClient.GetAsync(
                new Uri(definition.Url),
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            var xml = await response.Content.ReadAsStringAsync(cancellationToken);
            var catalog = CreateCatalog(xml, definition);
            await AtomicJsonStore.WriteAsync(
                cachePath,
                new CustomModLinksCache
                {
                    Definition = definition,
                    Catalog = catalog
                },
                cancellationToken);
            return new CustomModLinksLoadResult(
                CustomModLinksLoadStatus.Remote,
                catalog,
                IsVerified: false,
                Reason: null);
        }
        catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
        {
            if (cached is not null)
            {
                return new CustomModLinksLoadResult(
                    CustomModLinksLoadStatus.Cached,
                    cached,
                    IsVerified: false,
                    exception.Message);
            }
            throw new InvalidDataException(
                "Custom ModLinks is unavailable and has no cache for the configured build and Loader.",
                exception);
        }
    }

    private static GameCatalog CreateCatalog(string xml, CustomModLinksDefinition definition)
    {
        var catalog = new GameCatalog
        {
            Mods = OfficialXmlCatalogParser.ParseMods(
                    xml,
                    definition.LoaderId,
                    definition.BuildId)
                .Select(mod => mod with { SourceName = "Custom ModLinks" })
                .ToArray()
        };
        return ValidateCatalog(catalog, definition);
    }

    private static GameCatalog ValidateCatalog(
        GameCatalog catalog,
        CustomModLinksDefinition definition)
    {
        CatalogProvider.ValidateSource(catalog);
        foreach (var mod in catalog.Mods)
        {
            if (!string.Equals(mod.SourceName, "Custom ModLinks", StringComparison.Ordinal)
                || !string.Equals(mod.LoaderId, definition.LoaderId, StringComparison.OrdinalIgnoreCase)
                || mod.SupportedBuildIds.Count != 1
                || !string.Equals(
                    mod.SupportedBuildIds[0],
                    definition.BuildId,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Custom ModLinks cache does not match its configured build and Loader.");
            }
        }
        return catalog;
    }

    private static void ValidateDefinition(CustomModLinksDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!Uri.TryCreate(definition.Url, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Custom ModLinks URL must use HTTPS.", nameof(definition));
        }
        if (string.IsNullOrWhiteSpace(definition.BuildId)
            || string.IsNullOrWhiteSpace(definition.LoaderId)
            || !definition.LoaderId.StartsWith("modding-api-", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Custom ModLinks requires an exact game build and Modding API Loader.",
                nameof(definition));
        }
    }

    private static bool SameIdentity(
        CustomModLinksDefinition left,
        CustomModLinksDefinition right) =>
        string.Equals(left.Url, right.Url, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.BuildId, right.BuildId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.LoaderId, right.LoaderId, StringComparison.OrdinalIgnoreCase);

    private static bool IsRecoverable(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException
            or IOException
            or UnauthorizedAccessException
            or JsonException
            or InvalidDataException
            or XmlException
        || exception is OperationCanceledException && !cancellationToken.IsCancellationRequested;

    private sealed record CustomModLinksCache
    {
        public CustomModLinksDefinition? Definition { get; init; }

        public GameCatalog? Catalog { get; init; }
    }
}

public enum CustomModLinksLoadStatus
{
    Remote,
    Cached
}

public sealed record CustomModLinksLoadResult(
    CustomModLinksLoadStatus Status,
    GameCatalog Catalog,
    bool IsVerified,
    string? Reason);

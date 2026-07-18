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

        using var response = await httpClient.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var source = await JsonSerializer.DeserializeAsync<GameCatalog>(
            stream,
            CrystalflyJson.Options,
            cancellationToken) ?? throw new JsonException("Custom catalog was empty.");
        return Namespace(sourceNamespace, source);
    }

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

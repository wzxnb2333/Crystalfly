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
        var embedded = EmbeddedCatalog.Load();
        GameCatalog? cache = null;
        if (File.Exists(cachePath))
        {
            try
            {
                var candidate = await AtomicJsonStore.ReadAsync<GameCatalog>(cachePath, cancellationToken);
                ValidateSource(candidate);
                ValidateCatalog(candidate, CatalogMerger.Merge(embedded, candidate, null));
                cache = candidate;
            }
            catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
            {
            }
        }

        GameCatalog? remote = null;
        try
        {
            using var response = await httpClient.GetAsync(remoteCatalogUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var candidate = await JsonSerializer.DeserializeAsync<GameCatalog>(
                stream,
                CrystalflyJson.Options,
                cancellationToken)
                ?? throw new JsonException("Remote catalog did not contain a catalog value.");
            ValidateSource(candidate);
            ValidateCatalog(candidate, CatalogMerger.Merge(embedded, null, candidate));
            await AtomicJsonStore.WriteAsync(cachePath, candidate, cancellationToken);
            remote = candidate;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException or InvalidDataException)
        {
        }

        return CatalogMerger.Merge(embedded, cache, remote, customCatalogs);
    }

    private static void ValidateCatalog(GameCatalog source, GameCatalog resolved)
    {
        Version(source.SchemaVersion, nameof(GameCatalog), "catalog");

        var builds = Index(resolved.Builds, build => build.Id, nameof(GameBuild));
        foreach (var build in builds.Values)
        {
            Version(build.SchemaVersion, nameof(GameBuild), build.Id);
            Text(build.DisplayVersion, $"Build '{build.Id}' display version");
            Text(build.ManifestId, $"Build '{build.Id}' manifest ID");
            if (build.DepotId == 0)
            {
                throw new InvalidDataException($"Build '{build.Id}' has no depot ID.");
            }
            Sha256(build.ExecutableSha256, $"Build '{build.Id}' executable");
            if (build.UnityPlayerSha256 is not null)
            {
                Sha256(build.UnityPlayerSha256, $"Build '{build.Id}' Unity player");
            }
            Sha256(build.GlobalGameManagersSha256, $"Build '{build.Id}' globalgamemanagers");
        }

        var buildIds = builds.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var channels = Index(resolved.Channels, channel => channel.Name, nameof(GameChannel));
        foreach (var channel in channels.Values)
        {
            Version(channel.SchemaVersion, nameof(GameChannel), channel.Name);
            Reference(channel.BuildId, buildIds, $"Channel '{channel.Name}' build");
        }

        var loaders = Index(resolved.Loaders, loader => loader.Id, nameof(LoaderManifest));
        foreach (var loader in loaders.Values)
        {
            Version(loader.SchemaVersion, nameof(LoaderManifest), loader.Id);
            Text(loader.Name, $"Loader '{loader.Id}' name");
            Text(loader.Version, $"Loader '{loader.Id}' version");
            Package(loader.DownloadUrl, loader.SizeBytes, loader.Sha256, $"Loader '{loader.Id}'");
            References(loader.SupportedBuildIds, buildIds, $"Loader '{loader.Id}' build");
            UniqueText(loader.ManagedFiles, $"Loader '{loader.Id}' managed file");
        }

        var loaderIds = loaders.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var mods = Index(resolved.Mods, mod => mod.Id, nameof(ModManifest));
        var modIds = mods.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in mods.Values)
        {
            Version(mod.SchemaVersion, nameof(ModManifest), mod.Id);
            Text(mod.Name, $"Mod '{mod.Id}' name");
            Text(mod.Version, $"Mod '{mod.Id}' version");
            Package(mod.DownloadUrl, mod.SizeBytes, mod.Sha256, $"Mod '{mod.Id}'");
            Reference(mod.LoaderId, loaderIds, $"Mod '{mod.Id}' loader");
            References(mod.SupportedBuildIds, buildIds, $"Mod '{mod.Id}' build");
            UniqueText(mod.Dependencies, $"Mod '{mod.Id}' dependency");
            foreach (var dependency in mod.Dependencies)
            {
                if (!modIds.Contains(dependency)
                    && !dependency.StartsWith("hkmod:", StringComparison.OrdinalIgnoreCase)
                    && !dependency.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"Mod '{mod.Id}' dependency references missing ID '{dependency}'.");
                }
            }
            UniqueText(mod.FlatFiles, $"Mod '{mod.Id}' flat file");
        }

        var assets = Index(resolved.SpeedrunAssets, asset => asset.Id, nameof(SpeedrunAsset));
        foreach (var asset in assets.Values)
        {
            Version(asset.SchemaVersion, nameof(SpeedrunAsset), asset.Id);
            Text(asset.Name, $"Speedrun asset '{asset.Id}' name");
            Text(asset.Version, $"Speedrun asset '{asset.Id}' version");
            Package(asset.DownloadUrl, asset.SizeBytes, asset.Sha256, $"Speedrun asset '{asset.Id}'");
            References(asset.SupportedBuildIds, buildIds, $"Speedrun asset '{asset.Id}' build");
        }

        var assetIds = assets.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var fileManifests = Index(
            resolved.SpeedrunFileManifests,
            manifest => manifest.Id,
            nameof(SpeedrunFileManifest));
        foreach (var manifest in fileManifests.Values)
        {
            Version(manifest.SchemaVersion, nameof(SpeedrunFileManifest), manifest.Id);
            Reference(manifest.BuildId, buildIds, $"Speedrun file manifest '{manifest.Id}' build");
            Text(manifest.RulesRevision, $"Speedrun file manifest '{manifest.Id}' rules revision");
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in manifest.Files
                ?? throw new InvalidDataException($"Speedrun file manifest '{manifest.Id}' files are required."))
            {
                if (file is null)
                {
                    throw new InvalidDataException($"Speedrun file manifest '{manifest.Id}' contains a null file rule.");
                }
                var path = Text(file.RelativePath, $"Speedrun file manifest '{manifest.Id}' path");
                if (!paths.Add(path))
                {
                    throw new InvalidDataException($"Speedrun file manifest '{manifest.Id}' contains duplicate path '{path}'.");
                }
                Sha256(file.Sha256, $"Speedrun file manifest '{manifest.Id}' file '{path}'");
                if (file.AssetId is not null)
                {
                    Reference(file.AssetId, assetIds, $"Speedrun file manifest '{manifest.Id}' asset");
                    Text(file.AssetVersion, $"Speedrun file manifest '{manifest.Id}' asset version");
                }
                else if (file.AssetVersion is not null || file.Kind == SpeedrunFileKind.Tool)
                {
                    throw new InvalidDataException($"Speedrun file manifest '{manifest.Id}' has incomplete tool metadata.");
                }
            }
        }

        var fileManifestIds = fileManifests.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var templates = Index(resolved.SpeedrunTemplates, template => template.Id, nameof(SpeedrunTemplate));
        foreach (var template in templates.Values)
        {
            Version(template.SchemaVersion, nameof(SpeedrunTemplate), template.Id);
            Text(template.Name, $"Speedrun template '{template.Id}' name");
            Reference(template.BuildId, buildIds, $"Speedrun template '{template.Id}' build");
            if (template.LoaderId is not null)
            {
                Reference(template.LoaderId, loaderIds, $"Speedrun template '{template.Id}' loader");
            }
            References(template.RequiredAssetIds, assetIds, $"Speedrun template '{template.Id}' asset");
            var seconds = template.AllowedLoadNormaliserSeconds
                ?? throw new InvalidDataException($"Speedrun template '{template.Id}' load normaliser values are required.");
            if (seconds.Any(value => value < 0) || seconds.Distinct().Count() != seconds.Count)
            {
                throw new InvalidDataException($"Speedrun template '{template.Id}' has invalid load normaliser values.");
            }
            if (template.IsOfficial)
            {
                Text(template.RulesRevision, $"Speedrun template '{template.Id}' rules revision");
                Reference(template.FileManifestId, fileManifestIds, $"Speedrun template '{template.Id}' file manifest");
            }
            else if (!string.IsNullOrWhiteSpace(template.FileManifestId))
            {
                Reference(template.FileManifestId, fileManifestIds, $"Speedrun template '{template.Id}' file manifest");
            }
        }
    }

    private static void ValidateSource(GameCatalog source)
    {
        Version(source.SchemaVersion, nameof(GameCatalog), "catalog");
        _ = Index(source.Builds, build => build.Id, nameof(GameBuild));
        _ = Index(source.Channels, channel => channel.Name, nameof(GameChannel));
        _ = Index(source.Loaders, loader => loader.Id, nameof(LoaderManifest));
        _ = Index(source.Mods, mod => mod.Id, nameof(ModManifest));
        _ = Index(source.SpeedrunTemplates, template => template.Id, nameof(SpeedrunTemplate));
        _ = Index(source.SpeedrunAssets, asset => asset.Id, nameof(SpeedrunAsset));
        _ = Index(source.SpeedrunFileManifests, manifest => manifest.Id, nameof(SpeedrunFileManifest));
    }

    private static Dictionary<string, T> Index<T>(
        IEnumerable<T>? values,
        Func<T, string> idSelector,
        string type)
    {
        if (values is null)
        {
            throw new InvalidDataException($"Catalog {type} entries are required.");
        }
        var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            if (value is null)
            {
                throw new InvalidDataException($"Catalog contains a null {type} entry.");
            }
            var id = Text(idSelector(value), $"{type} ID");
            if (!result.TryAdd(id, value))
            {
                throw new InvalidDataException($"Catalog contains duplicate {type} ID '{id}'.");
            }
        }
        return result;
    }

    private static void Package(string url, long? size, string sha256, string description)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidDataException($"{description} download URL must use HTTPS.");
        }
        if (size is <= 0)
        {
            throw new InvalidDataException($"{description} package size must be positive.");
        }
        Sha256(sha256, description);
    }

    private static void Sha256(string? value, string description)
    {
        try
        {
            if (value?.Length != 64 || Convert.FromHexString(value).Length != 32)
            {
                throw new FormatException();
            }
        }
        catch (Exception exception) when (exception is FormatException or ArgumentNullException)
        {
            throw new InvalidDataException($"{description} SHA-256 is invalid.", exception);
        }
    }

    private static void References(
        IEnumerable<string>? values,
        IReadOnlySet<string> available,
        string description)
    {
        if (values is null)
        {
            throw new InvalidDataException($"{description} references are required.");
        }
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            var id = Text(value, description);
            if (!seen.Add(id))
            {
                throw new InvalidDataException($"{description} contains duplicate ID '{id}'.");
            }
            Reference(id, available, description);
        }
    }

    private static void Reference(string value, IReadOnlySet<string> available, string description)
    {
        var id = Text(value, description);
        if (!available.Contains(id))
        {
            throw new InvalidDataException($"{description} references missing ID '{id}'.");
        }
    }

    private static void UniqueText(IEnumerable<string>? values, string description)
    {
        if (values is null)
        {
            throw new InvalidDataException($"{description} values are required.");
        }
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            var text = Text(value, description);
            if (!seen.Add(text))
            {
                throw new InvalidDataException($"{description} contains duplicate value '{text}'.");
            }
        }
    }

    private static string Text(string? value, string description) =>
        !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidDataException($"{description} is required.");

    private static void Version(int version, string type, string id)
    {
        if (version != GameCatalog.CurrentSchemaVersion)
        {
            throw new InvalidDataException($"{type} '{id}' uses unsupported schema version {version}.");
        }
    }
}

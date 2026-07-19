using System.Net;
using System.Text.Json;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Catalog;

public static class ModTranslationSource
{
    private const int MaxEntries = 5000;
    private const int MaxTextLength = 16_384;
    private const int MaxNameLength = 160;
    private const int MaxTagLength = 64;
    private static readonly Uri DefaultRemoteUri = new(
        "https://raw.githubusercontent.com/wzxnb2333/Crystalfly/main/catalog/mod-translations.zh-CN.v1.json");

    public static Task<ModTranslationLoadResult> LoadAsync(
        HttpClient httpClient,
        string cachePath,
        CancellationToken cancellationToken = default) =>
        LoadAsync(
            httpClient,
            cachePath,
            EmbeddedModTranslationCatalog.Load(),
            DefaultRemoteUri,
            cancellationToken);

    internal static async Task<ModTranslationLoadResult> LoadAsync(
        HttpClient httpClient,
        string cachePath,
        ModTranslationCatalog embeddedCatalog,
        Uri remoteUri,
        CancellationToken cancellationToken = default)
    {
        var embedded = Validate(embeddedCatalog);
        ModTranslationCatalog? cached = null;
        Exception? cacheFailure = null;
        try
        {
            cached = Validate(await AtomicJsonStore.ReadAsync<ModTranslationCatalog>(cachePath, cancellationToken));
        }
        catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
        {
            cacheFailure = exception;
        }

        try
        {
            using var response = await httpClient.GetAsync(remoteUri, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var remote = Validate(
                await JsonSerializer.DeserializeAsync<ModTranslationCatalog>(
                    stream,
                    CrystalflyJson.Options,
                    cancellationToken)
                ?? throw new JsonException("Remote translation catalog did not contain a value."));
            string? cacheWriteReason = null;
            try
            {
                await AtomicJsonStore.WriteAsync(cachePath, remote, cancellationToken);
            }
            catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
            {
                cacheWriteReason = $"Cache write: {exception.Message}";
            }
            return Result(ModTranslationLoadStatus.Remote, Merge(embedded, remote), cacheWriteReason);
        }
        catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
        {
            var reason = $"Remote: {exception.Message}";
            if (cached is not null)
            {
                return Result(ModTranslationLoadStatus.Cached, Merge(embedded, cached), reason);
            }

            if (cacheFailure is not null)
            {
                reason += $" Cache: {cacheFailure.Message}";
            }
            return Result(ModTranslationLoadStatus.Embedded, embedded, reason);
        }
    }

    internal static ModTranslationCatalog Validate(ModTranslationCatalog catalog)
    {
        if (catalog.SchemaVersion != ModTranslationCatalog.CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Translation catalog uses unsupported schema version {catalog.SchemaVersion}.");
        }
        if (!string.Equals(catalog.Language, ModTranslationCatalog.SupportedLanguage, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Translation catalog language must be {ModTranslationCatalog.SupportedLanguage}.");
        }
        if (catalog.Mods is null || catalog.Mods.Count > MaxEntries)
        {
            throw new InvalidDataException("Translation catalog contains too many Mod entries.");
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in catalog.Mods)
        {
            if (mod is null
                || string.IsNullOrWhiteSpace(mod.Id)
                || !mod.Id.StartsWith("hkmod:", StringComparison.OrdinalIgnoreCase)
                || !ids.Add(mod.Id))
            {
                throw new InvalidDataException("Translation catalog contains an invalid or duplicate Mod ID.");
            }
            Text(mod.DisplayName, MaxNameLength, $"Mod '{mod.Id}' display name");
            Text(mod.Description, MaxTextLength, $"Mod '{mod.Id}' description");
        }

        var tags = catalog.TagNames
            ?? throw new InvalidDataException("Translation catalog tag names are required.");
        var tagKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in tags)
        {
            Text(tag.Key, MaxTagLength, "Translation tag key");
            Text(tag.Value, MaxTagLength, $"Translation tag '{tag.Key}' value");
            if (!tagKeys.Add(tag.Key))
            {
                throw new InvalidDataException($"Translation catalog contains duplicate tag '{tag.Key}'.");
            }
        }
        return catalog;
    }

    private static ModTranslationCatalog Merge(
        ModTranslationCatalog embedded,
        ModTranslationCatalog overlay)
    {
        var entries = embedded.Mods.ToDictionary(mod => mod.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var mod in overlay.Mods)
        {
            if (entries.TryGetValue(mod.Id, out var baseline))
            {
                entries[mod.Id] = baseline with
                {
                    DisplayName = mod.DisplayName ?? baseline.DisplayName,
                    Description = mod.Description ?? baseline.Description
                };
            }
            else
            {
                entries[mod.Id] = mod;
            }
        }

        var tags = new Dictionary<string, string>(embedded.TagNames, StringComparer.OrdinalIgnoreCase);
        foreach (var tag in overlay.TagNames)
        {
            tags[tag.Key] = tag.Value;
        }
        return embedded with
        {
            TagNames = tags,
            Mods = entries.Values.OrderBy(mod => mod.Id, StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    private static ModTranslationLoadResult Result(
        ModTranslationLoadStatus status,
        ModTranslationCatalog catalog,
        string? reason) =>
        new(status, catalog, catalog.Mods.Count, reason);

    private static bool IsRecoverable(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException
            or HttpListenerException
            or IOException
            or UnauthorizedAccessException
            or JsonException
            or InvalidDataException
            or NotSupportedException
        || exception is OperationCanceledException && !cancellationToken.IsCancellationRequested;

    private static string? Text(string? value, int maxLength, string description)
    {
        if (value is null)
        {
            return null;
        }
        var text = value.Trim();
        if (text.Length == 0 || text.Length > maxLength)
        {
            throw new InvalidDataException($"{description} is empty or too long.");
        }
        return text;
    }
}

public enum ModTranslationLoadStatus
{
    Remote,
    Cached,
    Embedded
}

public sealed record ModTranslationLoadResult(
    ModTranslationLoadStatus Status,
    ModTranslationCatalog Catalog,
    int ModCount,
    string? Reason);

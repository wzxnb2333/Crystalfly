using System.Text.Json;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Catalog;

public static class ModActivitySource
{
    private const int MaximumEntries = 5000;
    private const int MaximumIdentifierLength = 256;
    private const int MinimumSourceRevisionLength = 7;
    private const int MaximumSourceRevisionLength = 64;
    private static readonly Uri DefaultRemoteUri = new(
        "https://raw.githubusercontent.com/wzxnb2333/Crystalfly/main/catalog/mod-activity.v1.json");

    public static Task<ModActivityLoadResult> LoadAsync(
        HttpClient httpClient,
        string cachePath,
        CancellationToken cancellationToken = default) =>
        LoadAsync(
            httpClient,
            cachePath,
            EmbeddedModActivityCatalog.Load(),
            DefaultRemoteUri,
            cancellationToken);

    internal static async Task<ModActivityLoadResult> LoadAsync(
        HttpClient httpClient,
        string cachePath,
        ModActivityCatalog embedded,
        Uri remoteUri,
        CancellationToken cancellationToken)
    {
        embedded = Validate(embedded);
        ModActivityCatalog? cached = null;
        Exception? cacheFailure = null;
        try
        {
            cached = Validate(await AtomicJsonStore.ReadAsync<ModActivityCatalog>(
                cachePath,
                cancellationToken));
        }
        catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
        {
            cacheFailure = exception;
        }

        try
        {
            using var response = await httpClient.GetAsync(
                remoteUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var remote = Validate(
                await JsonSerializer.DeserializeAsync<ModActivityCatalog>(
                    stream,
                    CrystalflyJson.Options,
                    cancellationToken)
                ?? throw new JsonException("Remote Mod activity catalog did not contain a value."));
            await AtomicJsonStore.WriteAsync(cachePath, remote, cancellationToken);
            return new ModActivityLoadResult(ModActivityLoadStatus.Remote, remote, null);
        }
        catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
        {
            if (cached is not null)
            {
                return new ModActivityLoadResult(
                    ModActivityLoadStatus.Cached,
                    cached,
                    exception.Message);
            }
            return new ModActivityLoadResult(
                ModActivityLoadStatus.Embedded,
                embedded,
                cacheFailure is null
                    ? exception.Message
                    : $"Remote: {exception.Message} Cache: {cacheFailure.Message}");
        }
    }

    internal static ModActivityCatalog Validate(ModActivityCatalog catalog)
    {
        if (catalog.SchemaVersion != ModActivityCatalog.CurrentSchemaVersion
            || catalog.GeneratedAt == default
            || string.IsNullOrWhiteSpace(catalog.SourceRevision)
            || catalog.SourceRevision.Length is < MinimumSourceRevisionLength or > MaximumSourceRevisionLength
            || catalog.Entries is null
            || catalog.Entries.Count > MaximumEntries)
        {
            throw new InvalidDataException("Mod activity catalog metadata is invalid.");
        }
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in catalog.Entries)
        {
            if (entry is null
                || string.IsNullOrWhiteSpace(entry.Id)
                || !entry.Id.StartsWith("hkmod:", StringComparison.OrdinalIgnoreCase)
                || entry.Id.Length > MaximumIdentifierLength
                || !ids.Add(entry.Id)
                || entry.AddedAt == default
                || entry.UpdatedAt < entry.AddedAt
                || entry.UpdatedAt > catalog.GeneratedAt)
            {
                throw new InvalidDataException("Mod activity catalog contains an invalid entry.");
            }
        }
        return catalog;
    }

    private static bool IsRecoverable(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException
            or IOException
            or UnauthorizedAccessException
            or JsonException
            or InvalidDataException
        || exception is OperationCanceledException && !cancellationToken.IsCancellationRequested;
}

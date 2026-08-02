using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Crystalfly.Core.Networking;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Speedrun;

public sealed class SpeedrunComClient(
    HttpClient httpClient,
    string cacheRoot,
    INetworkPolicy networkPolicy,
    TimeProvider? timeProvider = null)
{
    private const int CacheSchemaVersion = 1;
    private const int MaximumCategories = 500;
    private const int MaximumRuns = 5;
    private static readonly Uri ApiRoot = new("https://www.speedrun.com/api/v1/");
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(15);
    private readonly HttpClient httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly string cacheRoot = Path.GetFullPath(cacheRoot ?? throw new ArgumentNullException(nameof(cacheRoot)));
    private readonly INetworkPolicy networkPolicy = networkPolicy ?? throw new ArgumentNullException(nameof(networkPolicy));
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    public Task<SpeedrunDataResult<IReadOnlyList<SpeedrunCategory>>> GetCategoriesAsync(
        SpeedrunGame game,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default) =>
        LoadAsync(
            CacheKey(game, "categories"),
            new Uri(ApiRoot, $"games/{SpeedrunGameCatalog.GetId(game)}/categories"),
            ParseCategories,
            forceRefresh,
            cancellationToken);

    public Task<SpeedrunDataResult<SpeedrunLeaderboard>> GetLeaderboardAsync(
        SpeedrunGame game,
        SpeedrunCategory category,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        ValidateCategory(category);
        string gameId = SpeedrunGameCatalog.GetId(game);
        string categoryId = Uri.EscapeDataString(category.Id);
        return LoadAsync(
            CacheKey(game, $"leaderboard-{Hash(category.Id)}"),
            new Uri(ApiRoot, $"leaderboards/{gameId}/category/{categoryId}?top=5&embed=players"),
            element => ParseLeaderboard(element, category),
            forceRefresh,
            cancellationToken);
    }

    public Task<SpeedrunDataResult<SpeedrunRecentRuns>> GetRecentRunsAsync(
        SpeedrunGame game,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default) =>
        LoadAsync(
            CacheKey(game, "recent"),
            new Uri(
                ApiRoot,
                $"runs?game={SpeedrunGameCatalog.GetId(game)}&status=verified&orderby=verify-date&direction=desc&max=5&embed=players,category"),
            ParseRecentRuns,
            forceRefresh,
            cancellationToken);

    private async Task<SpeedrunDataResult<T>> LoadAsync<T>(
        string cacheKey,
        Uri requestUri,
        Func<JsonElement, T> parse,
        bool forceRefresh,
        CancellationToken cancellationToken)
        where T : class
    {
        CachedDocument<T>? cached = await ReadCacheAsync<T>(cacheKey, cancellationToken);
        bool isStale = cached is not null && IsStale(cached.FetchedAt);
        if (!forceRefresh && cached is not null && !isStale)
        {
            return new SpeedrunDataResult<T>(
                SpeedrunDataStatus.Cached,
                cached.Data,
                false,
                cached.FetchedAt,
                null);
        }

        if (networkPolicy.IsOffline)
        {
            return cached is null
                ? new SpeedrunDataResult<T>(SpeedrunDataStatus.Offline, null, false, null, "Offline mode.")
                : new SpeedrunDataResult<T>(SpeedrunDataStatus.Cached, cached.Data, isStale, cached.FetchedAt, "Offline mode.");
        }

        try
        {
            T data = await FetchAsync(requestUri, parse, cancellationToken);
            var document = new CachedDocument<T>(CacheSchemaVersion, cacheKey, timeProvider.GetUtcNow(), data);
            await AtomicJsonStore.WriteAsync(CachePath(cacheKey), document, cancellationToken);
            return new SpeedrunDataResult<T>(
                SpeedrunDataStatus.Remote,
                data,
                false,
                document.FetchedAt,
                null);
        }
        catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
        {
            return cached is null
                ? new SpeedrunDataResult<T>(SpeedrunDataStatus.Unavailable, null, false, null, exception.Message)
                : new SpeedrunDataResult<T>(SpeedrunDataStatus.Cached, cached.Data, true, cached.FetchedAt, exception.Message);
        }
    }

    private async Task<T> FetchAsync<T>(
        Uri requestUri,
        Func<JsonElement, T> parse,
        CancellationToken cancellationToken)
        where T : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.UserAgent.ParseAdd("Crystalfly/0.9.1");
        request.Headers.Accept.ParseAdd("application/json");
        using HttpResponseMessage response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Speedrun.com returned HTTP {(int)response.StatusCode}.",
                null,
                response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return parse(document.RootElement);
    }

    private async Task<CachedDocument<T>?> ReadCacheAsync<T>(
        string cacheKey,
        CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            CachedDocument<T> cached = await AtomicJsonStore.ReadAsync<CachedDocument<T>>(
                CachePath(cacheKey),
                cancellationToken);
            if (cached.SchemaVersion != CacheSchemaVersion
                || !string.Equals(cached.Key, cacheKey, StringComparison.Ordinal)
                || cached.FetchedAt == default
                || cached.Data is null)
            {
                throw new InvalidDataException("Speedrun.com cache identity is invalid.");
            }
            return cached;
        }
        catch (Exception exception) when (IsCacheFailure(exception, cancellationToken))
        {
            return null;
        }
    }

    private static IReadOnlyList<SpeedrunCategory> ParseCategories(JsonElement root)
    {
        JsonElement data = GetData(root);
        if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() > MaximumCategories)
        {
            throw new JsonException("Speedrun.com categories response is invalid.");
        }

        var categories = new List<SpeedrunCategory>(data.GetArrayLength());
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement item in data.EnumerateArray())
        {
            string id = RequiredString(item, "id", "category");
            string name = RequiredString(item, "name", "category");
            if (!ids.Add(id))
            {
                throw new JsonException("Speedrun.com categories response contains duplicate IDs.");
            }
            categories.Add(new SpeedrunCategory(id, name, OfficialUrl(item, "weblink")));
        }
        return categories.OrderBy(category => category.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static SpeedrunLeaderboard ParseLeaderboard(JsonElement root, SpeedrunCategory category)
    {
        JsonElement data = GetData(root);
        JsonElement runs = RequiredArray(data, "runs", "leaderboard");
        if (runs.GetArrayLength() > MaximumRuns)
        {
            throw new JsonException("Speedrun.com leaderboard response exceeds the requested limit.");
        }

        IReadOnlyDictionary<string, string> players = ParsePlayers(data);
        var parsed = new List<SpeedrunRun>(runs.GetArrayLength());
        foreach (JsonElement item in runs.EnumerateArray())
        {
            if (!item.TryGetProperty("run", out JsonElement run))
            {
                throw new JsonException("Speedrun.com leaderboard run is missing.");
            }
            int? place = item.TryGetProperty("place", out JsonElement placeValue)
                && placeValue.TryGetInt32(out int value)
                    ? value
                    : null;
            parsed.Add(ParseRun(run, players, place, null));
        }
        return new SpeedrunLeaderboard(category, parsed);
    }

    private static SpeedrunRecentRuns ParseRecentRuns(JsonElement root)
    {
        JsonElement data = GetData(root);
        if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() > MaximumRuns)
        {
            throw new JsonException("Speedrun.com recent runs response is invalid.");
        }

        IReadOnlyDictionary<string, string> players = ParsePlayers(root);
        IReadOnlyDictionary<string, string> categories = ParseCategoriesById(root);
        var parsed = data.EnumerateArray()
            .Select(run => ParseRun(run, players, null, categories))
            .OrderByDescending(run => run.VerifiedAt ?? DateTimeOffset.MinValue)
            .ToArray();
        return new SpeedrunRecentRuns(parsed);
    }

    private static SpeedrunRun ParseRun(
        JsonElement run,
        IReadOnlyDictionary<string, string> players,
        int? place,
        IReadOnlyDictionary<string, string>? categories)
    {
        string id = RequiredString(run, "id", "run");
        string time = RequiredString(RequiredObject(run, "times", "run"), "primary", "run time");
        string playerName = ParsePlayerName(run, players);
        DateOnly? playedOn = TryDate(run, "date");
        DateTimeOffset? verifiedAt = TryDateTime(RequiredObject(run, "status", "run"), "verify-date");
        string? categoryName = null;
        if (run.TryGetProperty("category", out JsonElement category))
        {
            if (category.ValueKind == JsonValueKind.String && categories is not null)
            {
                categories.TryGetValue(category.GetString() ?? string.Empty, out categoryName);
            }
            else if (category.ValueKind == JsonValueKind.Object
                && category.TryGetProperty("data", out JsonElement categoryData)
                && categoryData.ValueKind == JsonValueKind.Object)
            {
                categoryName = OptionalString(categoryData, "name");
            }
        }
        return new SpeedrunRun(
            id,
            place,
            playerName,
            time,
            playedOn,
            verifiedAt,
            categoryName,
            OfficialUrl(run, "weblink"));
    }

    private static IReadOnlyDictionary<string, string> ParsePlayers(JsonElement container)
    {
        if (!container.TryGetProperty("players", out JsonElement players))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        IEnumerable<JsonElement> values = players.ValueKind switch
        {
            JsonValueKind.Array => players.EnumerateArray(),
            JsonValueKind.Object when players.TryGetProperty("data", out JsonElement data)
                && data.ValueKind == JsonValueKind.Array => data.EnumerateArray(),
            _ => []
        };
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (JsonElement player in values)
        {
            string id = RequiredString(player, "id", "player");
            string? name = null;
            if (player.TryGetProperty("names", out JsonElement names)
                && names.TryGetProperty("international", out JsonElement international)
                && international.ValueKind == JsonValueKind.String)
            {
                name = international.GetString();
            }
            name ??= OptionalString(player, "name");
            if (!string.IsNullOrWhiteSpace(name))
            {
                result[id] = name;
            }
        }
        return result;
    }

    private static IReadOnlyDictionary<string, string> ParseCategoriesById(JsonElement root)
    {
        if (!root.TryGetProperty("categories", out JsonElement categories))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
        IEnumerable<JsonElement> values = categories.ValueKind switch
        {
            JsonValueKind.Array => categories.EnumerateArray(),
            JsonValueKind.Object when categories.TryGetProperty("data", out JsonElement data)
                && data.ValueKind == JsonValueKind.Array => data.EnumerateArray(),
            _ => []
        };
        return values.ToDictionary(
            item => RequiredString(item, "id", "category"),
            item => RequiredString(item, "name", "category"),
            StringComparer.Ordinal);
    }

    private static string ParsePlayerName(JsonElement run, IReadOnlyDictionary<string, string> players)
    {
        if (!run.TryGetProperty("players", out JsonElement runPlayers))
        {
            throw new JsonException("Speedrun.com run players is invalid.");
        }

        IEnumerable<JsonElement> values = runPlayers.ValueKind switch
        {
            JsonValueKind.Array => runPlayers.EnumerateArray(),
            JsonValueKind.Object when runPlayers.TryGetProperty("data", out JsonElement data)
                && data.ValueKind == JsonValueKind.Array => data.EnumerateArray(),
            _ => throw new JsonException("Speedrun.com run players is invalid.")
        };
        var names = new List<string>();
        foreach (JsonElement player in values)
        {
            string? name = OptionalString(player, "name");
            if (string.IsNullOrWhiteSpace(name)
                && player.TryGetProperty("id", out JsonElement id))
            {
                players.TryGetValue(id.GetString() ?? string.Empty, out name);
            }
            if (!string.IsNullOrWhiteSpace(name))
            {
                names.Add(name);
            }
        }
        return names.Count == 0 ? "Unknown runner" : string.Join(", ", names);
    }

    private static JsonElement GetData(JsonElement root) =>
        root.TryGetProperty("data", out JsonElement data)
            ? data
            : throw new JsonException("Speedrun.com response did not contain data.");

    private static JsonElement RequiredArray(JsonElement value, string property, string context)
    {
        if (!value.TryGetProperty(property, out JsonElement result)
            || result.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException($"Speedrun.com {context} {property} is invalid.");
        }
        return result;
    }

    private static JsonElement RequiredObject(JsonElement value, string property, string context)
    {
        if (!value.TryGetProperty(property, out JsonElement result)
            || result.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"Speedrun.com {context} {property} is invalid.");
        }
        return result;
    }

    private static string RequiredString(JsonElement value, string property, string context)
    {
        string? result = OptionalString(value, property);
        if (string.IsNullOrWhiteSpace(result) || result.Length > 512)
        {
            throw new JsonException($"Speedrun.com {context} {property} is invalid.");
        }
        return result;
    }

    private static string? OptionalString(JsonElement value, string property) =>
        value.TryGetProperty(property, out JsonElement result)
        && result.ValueKind == JsonValueKind.String
            ? result.GetString()
            : null;

    private static DateOnly? TryDate(JsonElement value, string property) =>
        DateOnly.TryParse(OptionalString(value, property), out DateOnly result) ? result : null;

    private static DateTimeOffset? TryDateTime(JsonElement value, string property) =>
        DateTimeOffset.TryParse(OptionalString(value, property), out DateTimeOffset result) ? result : null;

    private static string? OfficialUrl(JsonElement value, string property)
    {
        string? candidate = OptionalString(value, property);
        return Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && (string.Equals(uri.Host, "speedrun.com", StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Host, "www.speedrun.com", StringComparison.OrdinalIgnoreCase))
            ? uri.AbsoluteUri
            : null;
    }

    private static void ValidateCategory(SpeedrunCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);
        if (string.IsNullOrWhiteSpace(category.Id) || category.Id.Length > 128
            || string.IsNullOrWhiteSpace(category.Name) || category.Name.Length > 512)
        {
            throw new ArgumentException("Speedrun.com category is invalid.", nameof(category));
        }
    }

    private string CachePath(string cacheKey) => Path.Combine(cacheRoot, $"{Hash(cacheKey)}.json");

    private static string CacheKey(SpeedrunGame game, string resource) =>
        $"{SpeedrunGameCatalog.GetId(game)}:{resource}";

    private bool IsStale(DateTimeOffset fetchedAt) => timeProvider.GetUtcNow() - fetchedAt >= CacheLifetime;

    private static string Hash(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static bool IsCacheFailure(Exception exception, CancellationToken cancellationToken) =>
        exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or InvalidDataException
        || exception is OperationCanceledException && !cancellationToken.IsCancellationRequested;

    private static bool IsRecoverable(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException
            or IOException
            or UnauthorizedAccessException
            or JsonException
            or InvalidDataException
            or OfflineModeException
            or OfflineTransitionException
        || exception is OperationCanceledException && !cancellationToken.IsCancellationRequested;

    private sealed record CachedDocument<T>(int SchemaVersion, string Key, DateTimeOffset FetchedAt, T Data)
        where T : class;
}

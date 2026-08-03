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
    private const int CacheSchemaVersion = 2;
    private const int MaximumCategories = 500;
    private const int MaximumRuns = 100;
    private static readonly Uri ApiRoot = new("https://www.speedrun.com/api/v1/");
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(15);
    private readonly HttpClient httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly string cacheRoot = Path.GetFullPath(cacheRoot ?? throw new ArgumentNullException(nameof(cacheRoot)));
    private readonly INetworkPolicy networkPolicy = networkPolicy ?? throw new ArgumentNullException(nameof(networkPolicy));
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();

    public async Task<SpeedrunDataResult<IReadOnlyList<SpeedrunBoardDescriptor>>> GetBoardsAsync(
        SpeedrunGame game,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        Task<SpeedrunDataResult<IReadOnlyList<BoardCategory>>> categoriesTask = LoadAsync(
            CacheKey(game, "board-categories"),
            new Uri(ApiRoot, $"games/{SpeedrunGameCatalog.GetId(game)}/categories?embed=variables"),
            ParseBoardCategories,
            forceRefresh,
            cancellationToken);
        Task<SpeedrunDataResult<IReadOnlyList<BoardLevel>>> levelsTask = LoadAsync(
            CacheKey(game, "levels"),
            new Uri(ApiRoot, $"games/{SpeedrunGameCatalog.GetId(game)}/levels"),
            ParseLevels,
            forceRefresh,
            cancellationToken);
        await Task.WhenAll(categoriesTask, levelsTask);
        var categories = await categoriesTask;
        var levels = await levelsTask;
        if (categories.Data is null || levels.Data is null)
        {
            return new(
                categories.Status == SpeedrunDataStatus.Offline || levels.Status == SpeedrunDataStatus.Offline
                    ? SpeedrunDataStatus.Offline
                    : SpeedrunDataStatus.Unavailable,
                null,
                categories.IsStale || levels.IsStale,
                Latest(categories.FetchedAt, levels.FetchedAt),
                categories.Reason ?? levels.Reason);
        }

        var boards = new List<SpeedrunBoardDescriptor>();
        foreach (BoardCategory category in categories.Data)
        {
            IEnumerable<BoardLevel?> boardLevels = category.IsPerLevel
                ? levels.Data.Cast<BoardLevel?>()
                : [null];
            foreach (BoardLevel? level in boardLevels)
            {
                IEnumerable<IReadOnlyList<SpeedrunBoardVariable>> variants = ExpandVariants(category.Variables);
                foreach (IReadOnlyList<SpeedrunBoardVariable> variant in variants)
                {
                    boards.Add(new(game, category.Id, category.Name, level?.Id, level?.Name, variant));
                }
            }
        }
        return new(
            AggregateStatus(categories.Status, levels.Status),
            boards,
            categories.IsStale || levels.IsStale,
            Latest(categories.FetchedAt, levels.FetchedAt),
            categories.Reason ?? levels.Reason);
    }

    private static IEnumerable<IReadOnlyList<SpeedrunBoardVariable>> ExpandVariants(
        IReadOnlyList<BoardVariable> variables)
    {
        IEnumerable<IReadOnlyList<SpeedrunBoardVariable>> variants = [[]];
        foreach (BoardVariable variable in variables)
        {
            variants = variants.SelectMany(existing => variable.Values.Select(value =>
                (IReadOnlyList<SpeedrunBoardVariable>)[.. existing,
                    new(variable.Id, variable.Name, value.Id, value.Name)]));
        }
        return variants;
    }

    public Task<SpeedrunDataResult<SpeedrunBoardSnapshot>> GetPodiumAsync(
        SpeedrunBoardDescriptor board,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(board);
        string gameId = SpeedrunGameCatalog.GetId(board.Game);
        string path = board.LevelId is null
            ? $"leaderboards/{gameId}/category/{Uri.EscapeDataString(board.CategoryId)}"
            : $"leaderboards/{gameId}/level/{Uri.EscapeDataString(board.LevelId)}/{Uri.EscapeDataString(board.CategoryId)}";
        string variables = string.Concat(board.Subcategories
            .OrderBy(item => item.VariableId, StringComparer.Ordinal)
            .Select(item => $"&var-{Uri.EscapeDataString(item.VariableId)}={Uri.EscapeDataString(item.ValueId)}"));
        return LoadAsync(
            CacheKey(board.Game, $"podium-{Hash(board.Key)}"),
            new Uri(ApiRoot, $"{path}?top=3&embed=players{variables}"),
            element => ParsePodium(element, board),
            forceRefresh,
            cancellationToken);
    }

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

    private static IReadOnlyList<BoardCategory> ParseBoardCategories(JsonElement root)
    {
        JsonElement data = GetData(root);
        if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() > MaximumCategories)
        {
            throw new JsonException("Speedrun.com categories response is invalid.");
        }
        return data.EnumerateArray().Select(item =>
        {
            var variables = new List<BoardVariable>();
            if (item.TryGetProperty("variables", out JsonElement embedded)
                && embedded.TryGetProperty("data", out JsonElement variableData)
                && variableData.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement variable in variableData.EnumerateArray())
                {
                    if (!variable.TryGetProperty("is-subcategory", out JsonElement isSubcategory)
                        || isSubcategory.ValueKind != JsonValueKind.True)
                    {
                        continue;
                    }
                    JsonElement values = RequiredObject(RequiredObject(variable, "values", "variable"), "values", "variable");
                    var parsedValues = values.EnumerateObject()
                        .Select(value => new BoardValue(value.Name, RequiredString(value.Value, "label", "variable value")))
                        .ToArray();
                    variables.Add(new(
                        RequiredString(variable, "id", "variable"),
                        RequiredString(variable, "name", "variable"),
                        parsedValues));
                }
            }
            return new BoardCategory(
                RequiredString(item, "id", "category"),
                RequiredString(item, "name", "category"),
                string.Equals(OptionalString(item, "type"), "per-level", StringComparison.Ordinal),
                variables);
        }).ToArray();
    }

    private static IReadOnlyList<BoardLevel> ParseLevels(JsonElement root)
    {
        JsonElement data = GetData(root);
        if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() > MaximumCategories)
        {
            throw new JsonException("Speedrun.com levels response is invalid.");
        }
        return data.EnumerateArray()
            .Select(item => new BoardLevel(
                RequiredString(item, "id", "level"),
                RequiredString(item, "name", "level")))
            .ToArray();
    }

    private static SpeedrunBoardSnapshot ParsePodium(JsonElement root, SpeedrunBoardDescriptor board)
    {
        JsonElement data = GetData(root);
        JsonElement runs = RequiredArray(data, "runs", "leaderboard");
        if (runs.GetArrayLength() > MaximumRuns)
        {
            throw new JsonException("Speedrun.com podium response is too large.");
        }
        IReadOnlyDictionary<string, string> players = ParsePlayers(data);
        var entries = new List<SpeedrunPodiumEntry>(runs.GetArrayLength());
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement item in runs.EnumerateArray())
        {
            int place = item.TryGetProperty("place", out JsonElement placeElement)
                && placeElement.TryGetInt32(out int parsedPlace)
                && parsedPlace is >= 1 and <= 3
                    ? parsedPlace
                    : throw new JsonException("Speedrun.com podium place is invalid.");
            JsonElement run = RequiredObject(item, "run", "podium");
            string id = RequiredString(run, "id", "run");
            if (!ids.Add(id))
            {
                throw new JsonException("Speedrun.com podium contains duplicate runs.");
            }
            JsonElement times = RequiredObject(run, "times", "run");
            double seconds = times.TryGetProperty("primary_t", out JsonElement secondsElement)
                && secondsElement.TryGetDouble(out double parsedSeconds)
                && parsedSeconds >= 0
                    ? parsedSeconds
                    : throw new JsonException("Speedrun.com run time is invalid.");
            entries.Add(new(
                id,
                place,
                ParsePlayerName(run, players),
                RequiredString(times, "primary", "run time"),
                seconds,
                TryDateTime(RequiredObject(run, "status", "run"), "verify-date"),
                OfficialUrl(run, "weblink")));
        }
        return new(board, entries);
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
            string? id = OptionalString(player, "id");
            string? name = null;
            if (player.TryGetProperty("names", out JsonElement names)
                && names.TryGetProperty("international", out JsonElement international)
                && international.ValueKind == JsonValueKind.String)
            {
                name = international.GetString();
            }
            name ??= OptionalString(player, "name");
            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
            {
                result[id] = name;
            }
        }
        return result;
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

    private string CachePath(string cacheKey) => Path.Combine(cacheRoot, $"{Hash(cacheKey)}.json");

    private static string CacheKey(SpeedrunGame game, string resource) =>
        $"{SpeedrunGameCatalog.GetId(game)}:{resource}";

    private bool IsStale(DateTimeOffset fetchedAt) => timeProvider.GetUtcNow() - fetchedAt >= CacheLifetime;

    private static DateTimeOffset? Latest(DateTimeOffset? left, DateTimeOffset? right) =>
        left is null ? right : right is null || left >= right ? left : right;

    private static SpeedrunDataStatus AggregateStatus(params SpeedrunDataStatus[] statuses) =>
        statuses.All(status => status == SpeedrunDataStatus.Remote)
            ? SpeedrunDataStatus.Remote
            : statuses.Any(status => status == SpeedrunDataStatus.Offline)
                ? SpeedrunDataStatus.Offline
                : SpeedrunDataStatus.Cached;

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

    private sealed record BoardCategory(
        string Id,
        string Name,
        bool IsPerLevel,
        IReadOnlyList<BoardVariable> Variables);
    private sealed record BoardVariable(string Id, string Name, IReadOnlyList<BoardValue> Values);
    private sealed record BoardValue(string Id, string Name);
    private sealed record BoardLevel(string Id, string Name);
}

using System.Net;
using System.Text;
using Crystalfly.Core.Networking;
using Crystalfly.Core.Speedrun;

namespace Crystalfly.Core.Tests.Speedrun;

public sealed class SpeedrunComClientTests : IDisposable
{
    private readonly string cacheRoot = Path.Combine(Path.GetTempPath(), $"crystalfly-speedrun-{Guid.NewGuid():N}");

    [Fact]
    public async Task GetBoardsAsync_enumerates_full_game_level_and_subcategory_boards()
    {
        using var client = CreateClient((request, _) => Task.FromResult(
            request.RequestUri!.AbsolutePath.EndsWith("/categories", StringComparison.Ordinal)
                ? JsonResponse("""
                { "data": [
                  { "id":"full", "name":"Any%", "type":"per-game", "variables":{"data":[
                    { "id":"rules", "name":"Rules", "is-subcategory":true,
                      "values":{"values":{"nmg":{"label":"NMG"},"ag":{"label":"All Glitches"}}}}
                  ]}},
                  { "id":"level-category", "name":"Level", "type":"per-level", "variables":{"data":[]} }
                ] }
                """)
                : JsonResponse("""{ "data": [{ "id":"level-1", "name":"Pantheon" }] }""")));
        var speedrun = new SpeedrunComClient(client, cacheRoot, new NetworkPolicy());

        var result = await speedrun.GetBoardsAsync(SpeedrunGame.HollowKnight, forceRefresh: true);

        Assert.Equal(SpeedrunDataStatus.Remote, result.Status);
        Assert.Equal(3, result.Data!.Count);
        Assert.Contains(result.Data, board => board.CategoryId == "full" && board.Subcategories[0].ValueId == "nmg");
        Assert.Contains(result.Data, board => board.LevelId == "level-1");
    }

    [Fact]
    public async Task GetBoardsAsync_expands_multiple_subcategory_variables_into_real_board_keys()
    {
        using var client = CreateClient((request, _) => Task.FromResult(
            request.RequestUri!.AbsolutePath.EndsWith("/categories", StringComparison.Ordinal)
                ? JsonResponse("""
                  {"data":[{"id":"c","name":"Category","type":"per-game","variables":{"data":[
                    {"id":"a","name":"A","is-subcategory":true,"values":{"values":{"1":{"label":"One"},"2":{"label":"Two"}}}},
                    {"id":"b","name":"B","is-subcategory":true,"values":{"values":{"x":{"label":"X"},"y":{"label":"Y"}}}}
                  ]}}]}
                  """)
                : JsonResponse("""{"data":[]}""")));
        var speedrun = new SpeedrunComClient(client, cacheRoot, new NetworkPolicy());

        var result = await speedrun.GetBoardsAsync(SpeedrunGame.HollowKnight, forceRefresh: true);

        Assert.Equal(4, result.Data!.Count);
        Assert.Equal(4, result.Data.Select(board => board.Key).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task GetPodiumAsync_keeps_ties_and_filters_subcategory()
    {
        using var client = CreateClient((request, _) =>
        {
            Assert.Contains("top=3", request.RequestUri!.Query, StringComparison.Ordinal);
            Assert.Contains("var-rules=nmg", request.RequestUri.Query, StringComparison.Ordinal);
            return Task.FromResult(JsonResponse("""
            { "data": { "runs": [
              { "place":1, "run":{"id":"a","weblink":"https://www.speedrun.com/hollowknight/runs/a","status":{"verify-date":"2026-08-03T00:00:00Z"},"times":{"primary":"PT10M","primary_t":600},"players":[{"rel":"guest","name":"A"}]}},
              { "place":1, "run":{"id":"b","weblink":"https://evil.example/run/b","status":{"verify-date":"2026-08-03T01:00:00Z"},"times":{"primary":"PT10M","primary_t":600},"players":[{"rel":"guest","name":"B"}]}}
            ], "players":{"data":[]} } }
            """));
        });
        var speedrun = new SpeedrunComClient(client, cacheRoot, new NetworkPolicy());
        var board = new SpeedrunBoardDescriptor(
            SpeedrunGame.HollowKnight, "full", "Any%", null, null,
            [new("rules", "Rules", "nmg", "NMG")]);

        var result = await speedrun.GetPodiumAsync(board, forceRefresh: true);

        Assert.Equal(["a", "b"], result.Data!.Entries.Select(run => run.RunId));
        Assert.Equal("https://www.speedrun.com/hollowknight/runs/a", result.Data.Entries[0].RunUrl);
        Assert.Null(result.Data.Entries[1].RunUrl);
    }

    [Fact]
    public async Task Fresh_cache_avoids_second_request_and_offline_never_requests()
    {
        var requests = 0;
        using var client = CreateClient((request, _) =>
        {
            requests++;
            return Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("/categories", StringComparison.Ordinal)
                ? JsonResponse("""{"data":[]}""")
                : JsonResponse("""{"data":[]}"""));
        });
        using var policy = new NetworkPolicy();
        var speedrun = new SpeedrunComClient(client, cacheRoot, policy);

        await speedrun.GetBoardsAsync(SpeedrunGame.Silksong);
        await speedrun.GetBoardsAsync(SpeedrunGame.Silksong);
        Assert.Equal(2, requests);

        policy.SetOffline(true);
        var offline = await speedrun.GetBoardsAsync(SpeedrunGame.HollowKnight);
        Assert.Equal(SpeedrunDataStatus.Offline, offline.Status);
        Assert.Equal(2, requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Podium_failure_uses_stale_cache_without_overwriting_it(HttpStatusCode failure)
    {
        var requests = 0;
        using var client = CreateClient((_, _) => Task.FromResult(++requests == 1
            ? JsonResponse("""{"data":{"runs":[],"players":{"data":[]}}}""")
            : new HttpResponseMessage(failure)));
        var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-08-03T00:00:00Z"));
        var speedrun = new SpeedrunComClient(client, cacheRoot, new NetworkPolicy(), time);
        var board = new SpeedrunBoardDescriptor(SpeedrunGame.HollowKnight, "c", "Any%", null, null, []);
        await speedrun.GetPodiumAsync(board, forceRefresh: true);
        time.Advance(TimeSpan.FromMinutes(16));

        var result = await speedrun.GetPodiumAsync(board);

        Assert.Equal(SpeedrunDataStatus.Cached, result.Status);
        Assert.True(result.IsStale);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task Podium_rejects_maliciously_large_response()
    {
        string runs = string.Join(',', Enumerable.Range(0, 101).Select(index =>
            "{\"place\":3,\"run\":{\"id\":\"" + index + "\",\"status\":{},\"times\":{\"primary\":\"PT1S\",\"primary_t\":1},\"players\":[]}}"));
        using var client = CreateClient((_, _) => Task.FromResult(JsonResponse("{\"data\":{\"runs\":[" + runs + "]}}")));
        var speedrun = new SpeedrunComClient(client, cacheRoot, new NetworkPolicy());
        var board = new SpeedrunBoardDescriptor(SpeedrunGame.HollowKnight, "c", "Any%", null, null, []);

        var result = await speedrun.GetPodiumAsync(board, forceRefresh: true);

        Assert.Equal(SpeedrunDataStatus.Unavailable, result.Status);
        Assert.Null(result.Data);
    }

    public void Dispose()
    {
        if (Directory.Exists(cacheRoot)) Directory.Delete(cacheRoot, recursive: true);
    }

    private static HttpClient CreateClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory) =>
        new(new StubHandler(responseFactory));

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> factory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            factory(request, cancellationToken);
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => utcNow;
        public void Advance(TimeSpan duration) => utcNow += duration;
    }
}

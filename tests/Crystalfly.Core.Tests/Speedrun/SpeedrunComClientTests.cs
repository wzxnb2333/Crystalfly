using System.Net;
using System.Text;
using Crystalfly.Core.Networking;
using Crystalfly.Core.Serialization;
using Crystalfly.Core.Speedrun;

namespace Crystalfly.Core.Tests.Speedrun;

public sealed class SpeedrunComClientTests : IDisposable
{
    private readonly string cacheRoot = Path.Combine(
        Path.GetTempPath(),
        $"crystalfly-speedrun-{Guid.NewGuid():N}");

    [Fact]
    public async Task GetLeaderboardAsync_parses_embedded_player()
    {
        using var client = CreateClient((request, _) =>
        {
            Assert.Equal("GET", request.Method.Method);
            Assert.Contains("top=5", request.RequestUri!.Query, StringComparison.Ordinal);
            Assert.Contains("embed=players", request.RequestUri.Query, StringComparison.Ordinal);
            return Task.FromResult(JsonResponse("""
            {
              "data": {
                "runs": [
                  {
                    "place": 1,
                    "run": {
                      "id": "run-1",
                      "weblink": "https://www.speedrun.com/hollowknight/runs/run-1",
                      "date": "2026-07-30",
                      "status": { "status": "verified", "verify-date": "2026-07-31T00:00:00Z" },
                      "times": { "primary": "PT33M12S" },
                      "players": [{ "rel": "user", "id": "player-1" }]
                    }
                  }
                ],
                "players": { "data": [{ "id": "player-1", "names": { "international": "Runner One" } }] }
              }
            }
            """));
        });
        var speedrun = new SpeedrunComClient(
            client,
            cacheRoot,
            new NetworkPolicy());

        var result = await speedrun.GetLeaderboardAsync(
            SpeedrunGame.HollowKnight,
            new SpeedrunCategory("category-1", "Any%", null),
            forceRefresh: true);

        Assert.Equal(SpeedrunDataStatus.Remote, result.Status);
        var run = Assert.Single(result.Data!.Runs);
        Assert.Equal(1, run.Place);
        Assert.Equal("Runner One", run.PlayerName);
        Assert.Equal("PT33M12S", run.PrimaryTime);
        Assert.Equal("33:12", run.DisplayTime);
        Assert.Equal("https://www.speedrun.com/hollowknight/runs/run-1", run.RunUrl);
    }

    [Fact]
    public async Task GetLeaderboardAsync_ignores_embedded_guest_without_an_id()
    {
        using var client = CreateClient((_, _) => Task.FromResult(JsonResponse("""
        {
          "data": {
            "runs": [
              {
                "place": 1,
                "run": {
                  "id": "guest-run",
                  "status": { "status": "verified", "verify-date": "2026-08-01T00:00:00Z" },
                  "times": { "primary": "PT34M" },
                  "players": [{ "rel": "guest", "name": "Guest Runner" }]
                }
              },
              {
                "place": 2,
                "run": {
                  "id": "user-run",
                  "status": { "status": "verified", "verify-date": "2026-08-01T00:00:00Z" },
                  "times": { "primary": "PT35M" },
                  "players": [{ "rel": "user", "id": "player-1" }]
                }
              }
            ],
            "players": {
              "data": [
                { "rel": "guest", "name": "Guest Runner" },
                { "rel": "user", "id": "player-1", "names": { "international": "Runner One" } }
              ]
            }
          }
        }
        """)));
        var speedrun = new SpeedrunComClient(client, cacheRoot, new NetworkPolicy());

        var result = await speedrun.GetLeaderboardAsync(
            SpeedrunGame.HollowKnight,
            new SpeedrunCategory("category-1", "Any%", null),
            forceRefresh: true);

        Assert.Equal(SpeedrunDataStatus.Remote, result.Status);
        Assert.Equal(["Guest Runner", "Runner One"], result.Data!.Runs.Select(run => run.PlayerName));
    }

    [Fact]
    public async Task GetCategoriesAsync_uses_fresh_cache_without_second_request()
    {
        var requests = 0;
        using var client = CreateClient((_, _) =>
        {
            requests++;
            return Task.FromResult(JsonResponse("""
            {
              "data": [
                { "id": "category-1", "name": "Any%", "weblink": "https://www.speedrun.com/hollowknight/category/Any" }
              ]
            }
            """));
        });
        var speedrun = new SpeedrunComClient(
            client,
            cacheRoot,
            new NetworkPolicy());

        var first = await speedrun.GetCategoriesAsync(SpeedrunGame.HollowKnight);
        var second = await speedrun.GetCategoriesAsync(SpeedrunGame.HollowKnight);

        Assert.Equal(SpeedrunDataStatus.Remote, first.Status);
        Assert.Equal(SpeedrunDataStatus.Cached, second.Status);
        Assert.Equal(1, requests);
        Assert.Equal("Any%", Assert.Single(second.Data!).Name);
    }

    [Fact]
    public async Task GetRecentRunsAsync_returns_newest_verified_runs_first()
    {
        using var client = CreateClient((request, _) =>
        {
            Assert.Contains("status=verified", request.RequestUri!.Query, StringComparison.Ordinal);
            Assert.Contains("orderby=verify-date", request.RequestUri.Query, StringComparison.Ordinal);
            return Task.FromResult(JsonResponse("""
            {
              "data": [
                {
                  "id": "run-old",
                  "weblink": "https://www.speedrun.com/hollowknight/runs/run-old",
                  "date": "2026-07-20",
                  "status": { "status": "verified", "verify-date": "2026-07-21T00:00:00Z" },
                  "times": { "primary": "PT40M" },
                  "players": { "data": [{ "rel": "guest", "name": "Old Runner" }] },
                  "category": { "data": { "id": "category-old", "name": "Any%" } }
                },
                {
                  "id": "run-new",
                  "weblink": "https://www.speedrun.com/hollowknight/runs/run-new",
                  "date": "2026-07-29",
                  "status": { "status": "verified", "verify-date": "2026-07-30T00:00:00Z" },
                  "times": { "primary": "PT35M" },
                  "players": { "data": [{ "rel": "guest", "name": "New Runner" }] },
                  "category": { "data": { "id": "category-new", "name": "Any%" } }
                }
              ]
            }
            """));
        });
        var speedrun = new SpeedrunComClient(
            client,
            cacheRoot,
            new NetworkPolicy());

        var result = await speedrun.GetRecentRunsAsync(
            SpeedrunGame.HollowKnight,
            forceRefresh: true);

        Assert.Equal(SpeedrunDataStatus.Remote, result.Status);
        Assert.Equal(["run-new", "run-old"], result.Data!.Runs.Select(run => run.Id));
        Assert.Equal("New Runner", result.Data.Runs[0].PlayerName);
        Assert.Equal("Any%", result.Data.Runs[0].CategoryName);
    }

    [Fact]
    public async Task GetLeaderboardAsync_uses_stale_cache_when_remote_fails()
    {
        var requests = 0;
        using var client = CreateClient((_, _) =>
        {
            requests++;
            if (requests > 1)
            {
                throw new HttpRequestException("offline");
            }
            return Task.FromResult(JsonResponse("""
            {
              "data": {
                "runs": [],
                "players": []
              }
            }
            """));
        });
        var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-07-30T00:00:00Z"));
        var speedrun = new SpeedrunComClient(client, cacheRoot, new NetworkPolicy(), time);
        var category = new SpeedrunCategory("category-1", "Any%", null);

        await speedrun.GetLeaderboardAsync(SpeedrunGame.HollowKnight, category, forceRefresh: true);
        time.Advance(TimeSpan.FromMinutes(16));
        var result = await speedrun.GetLeaderboardAsync(SpeedrunGame.HollowKnight, category);

        Assert.Equal(SpeedrunDataStatus.Cached, result.Status);
        Assert.True(result.IsStale);
        Assert.Contains("offline", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Offline_mode_does_not_issue_a_network_request()
    {
        var requests = 0;
        using var client = CreateClient((_, _) =>
        {
            requests++;
            return Task.FromResult(JsonResponse("{}"));
        });
        using var policy = new NetworkPolicy(isOffline: true);
        var speedrun = new SpeedrunComClient(client, cacheRoot, policy);

        var result = await speedrun.GetCategoriesAsync(SpeedrunGame.Silksong);

        Assert.Equal(SpeedrunDataStatus.Offline, result.Status);
        Assert.Equal(0, requests);
        Assert.Null(result.Data);
    }

    public void Dispose()
    {
        if (Directory.Exists(cacheRoot))
        {
            Directory.Delete(cacheRoot, recursive: true);
        }
    }

    private static HttpClient CreateClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory) =>
        new(new StubHandler(responseFactory));

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => responseFactory(request, cancellationToken);
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => utcNow;

        public void Advance(TimeSpan duration) => utcNow += duration;
    }
}

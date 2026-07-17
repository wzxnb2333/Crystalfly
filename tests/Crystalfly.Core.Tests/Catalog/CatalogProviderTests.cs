using System.Net;
using System.Text;
using Crystalfly.Core.Catalog;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Tests.Catalog;

public sealed class CatalogProviderTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"crystalfly-catalog-{Guid.NewGuid():N}");

    [Fact]
    public async Task Load_merges_remote_catalog_and_updates_cache()
    {
        var remote = CreateCatalog("remote-build", "1001");
        using var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(CrystalflyJson.Serialize(remote), Encoding.UTF8, "application/json")
        }));
        var cachePath = Path.Combine(directory, "catalog.json");

        var result = await CatalogProvider.LoadAsync(
            new Uri("https://example.invalid/catalog.json"),
            cachePath,
            client);

        Assert.Contains(result.Builds, build => build.Id == "1.2.2.1");
        Assert.Equal("1001", result.Builds.Single(build => build.Id == "remote-build").ManifestId);
        Assert.Equal("1001", (await AtomicJsonStore.ReadAsync<GameCatalog>(cachePath)).Builds.Single().ManifestId);
    }

    [Fact]
    public async Task Load_uses_safe_cache_metadata_and_embedded_fallback_when_remote_is_unavailable()
    {
        var cachePath = Path.Combine(directory, "catalog.json");
        var cached = CreateCatalog("cached-build", "1002") with
        {
            Channels =
            [
                new GameChannel { Name = "cached-channel", BuildId = "1.2.2.1" },
                new GameChannel { Name = "invalid-channel", BuildId = "cached-build" }
            ]
        };
        await AtomicJsonStore.WriteAsync(cachePath, cached);
        using var client = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        var result = await CatalogProvider.LoadAsync(
            new Uri("https://example.invalid/catalog.json"),
            cachePath,
            client);

        Assert.Contains(result.Builds, build => build.Id == "1.2.2.1");
        Assert.DoesNotContain(result.Builds, build => build.Id == "cached-build");
        Assert.Contains(result.Channels, channel => channel.Name == "cached-channel");
        Assert.DoesNotContain(result.Channels, channel => channel.Name == "invalid-channel");
    }

    [Fact]
    public async Task Load_does_not_merge_stale_cache_entries_when_remote_succeeds()
    {
        var cachePath = Path.Combine(directory, "catalog.json");
        await AtomicJsonStore.WriteAsync(cachePath, CreateCatalog("stale-build", "1003"));
        using var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                CrystalflyJson.Serialize(CreateCatalog("remote-build", "1004")),
                Encoding.UTF8,
                "application/json")
        }));

        var result = await CatalogProvider.LoadAsync(
            new Uri("https://example.invalid/catalog.json"),
            cachePath,
            client);

        Assert.DoesNotContain(result.Builds, build => build.Id == "stale-build");
        Assert.Contains(result.Builds, build => build.Id == "remote-build");
    }

    [Theory]
    [InlineData("schema-version")]
    [InlineData("dangling-channel")]
    [InlineData("insecure-loader")]
    [InlineData("invalid-hash")]
    [InlineData("null-hash")]
    [InlineData("nonpositive-size")]
    [InlineData("dangling-mod-loader")]
    [InlineData("dangling-mod-dependency")]
    [InlineData("duplicate-build")]
    [InlineData("null-builds")]
    public async Task Load_rejects_invalid_remote_catalog_without_overwriting_cache(string invalidKind)
    {
        var cachePath = Path.Combine(directory, "catalog.json");
        var cached = new GameCatalog
        {
            Channels = [new GameChannel { Name = "cached-channel", BuildId = "1.2.2.1" }]
        };
        await AtomicJsonStore.WriteAsync(cachePath, cached);
        var cacheBefore = await File.ReadAllBytesAsync(cachePath);
        var remote = InvalidCatalog(invalidKind);
        using var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(CrystalflyJson.Serialize(remote), Encoding.UTF8, "application/json")
        }));

        var result = await CatalogProvider.LoadAsync(
            new Uri("https://example.invalid/catalog.json"),
            cachePath,
            client);

        Assert.Contains(result.Channels, channel => channel.Name == "cached-channel");
        Assert.DoesNotContain(result.Builds, build => build.Id == "remote-build");
        Assert.Equal(cacheBefore, await File.ReadAllBytesAsync(cachePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static GameCatalog CreateCatalog(string id, string manifestId) => new()
    {
        Builds =
        [
            new GameBuild
            {
                Id = id,
                DisplayVersion = id,
                DepotId = 367521,
                ManifestId = manifestId,
                ExecutableSha256 = new string('A', 64),
                GlobalGameManagersSha256 = new string('B', 64)
            }
        ]
    };

    private static GameCatalog InvalidCatalog(string kind)
    {
        var catalog = CreateCatalog("remote-build", "1006");
        return kind switch
        {
            "schema-version" => catalog with { SchemaVersion = 2 },
            "dangling-channel" => catalog with
            {
                Channels = [new GameChannel { Name = "latest", BuildId = "missing-build" }]
            },
            "insecure-loader" => catalog with
            {
                Loaders = [Loader("http://example.invalid/loader.zip", new string('A', 64), 1)]
            },
            "invalid-hash" => catalog with
            {
                Loaders = [Loader("https://example.invalid/loader.zip", "invalid", 1)]
            },
            "null-hash" => catalog with
            {
                Loaders = [Loader("https://example.invalid/loader.zip", null!, 1)]
            },
            "nonpositive-size" => catalog with
            {
                Loaders = [Loader("https://example.invalid/loader.zip", new string('A', 64), 0)]
            },
            "dangling-mod-loader" => catalog with
            {
                Mods =
                [
                    new ModManifest
                    {
                        Id = "mod",
                        Name = "Mod",
                        Version = "1",
                        DownloadUrl = "https://example.invalid/mod.zip",
                        SizeBytes = 1,
                        Sha256 = new string('A', 64),
                        LoaderId = "missing-loader",
                        SupportedBuildIds = ["remote-build"]
                    }
                ]
            },
            "dangling-mod-dependency" => catalog with
            {
                Loaders = [Loader("https://example.invalid/loader.zip", new string('A', 64), 1)],
                Mods =
                [
                    new ModManifest
                    {
                        Id = "mod",
                        Name = "Mod",
                        Version = "1",
                        DownloadUrl = "https://example.invalid/mod.zip",
                        SizeBytes = 1,
                        Sha256 = new string('A', 64),
                        LoaderId = "loader",
                        SupportedBuildIds = ["remote-build"],
                        Dependencies = ["missing-mod"]
                    }
                ]
            },
            "duplicate-build" => catalog with
            {
                Builds = [catalog.Builds[0], catalog.Builds[0] with { ManifestId = "1007" }]
            },
            "null-builds" => catalog with { Builds = null! },
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static LoaderManifest Loader(string url, string sha256, long? sizeBytes) => new()
    {
        Id = "loader",
        Name = "Loader",
        Version = "1",
        DownloadUrl = url,
        SizeBytes = sizeBytes,
        Sha256 = sha256,
        SupportedBuildIds = ["remote-build"]
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }
}

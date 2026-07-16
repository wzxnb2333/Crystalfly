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
        var remote = CreateCatalog("remote-build", "remote-manifest");
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
        Assert.Equal("remote-manifest", result.Builds.Single(build => build.Id == "remote-build").ManifestId);
        Assert.Equal("remote-manifest", (await AtomicJsonStore.ReadAsync<GameCatalog>(cachePath)).Builds.Single().ManifestId);
    }

    [Fact]
    public async Task Load_uses_cache_and_embedded_fallback_when_remote_is_unavailable()
    {
        var cachePath = Path.Combine(directory, "catalog.json");
        await AtomicJsonStore.WriteAsync(cachePath, CreateCatalog("cached-build", "cached-manifest"));
        using var client = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        var result = await CatalogProvider.LoadAsync(
            new Uri("https://example.invalid/catalog.json"),
            cachePath,
            client);

        Assert.Contains(result.Builds, build => build.Id == "1.2.2.1");
        Assert.Equal("cached-manifest", result.Builds.Single(build => build.Id == "cached-build").ManifestId);
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

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }
}

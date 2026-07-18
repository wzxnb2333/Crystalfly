using System.Net;
using Crystalfly.Core.Catalog;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Tests.Catalog;

public sealed class OfficialCatalogSourceTests : IDisposable
{
    private const string Namespace = "https://github.com/HollowKnight-Modding/HollowKnight.ModLinks/HollowKnight.ModManager";
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"crystalfly-official-{Guid.NewGuid():N}");

    [Fact]
    public async Task Load_returns_remote_status_and_atomically_updates_cache()
    {
        using var client = ClientFor(ModsXml("Example"), ApiXml("81"));
        var cachePath = Path.Combine(directory, "official.json");

        var result = await OfficialCatalogSource.LoadAsync(client, cachePath);

        Assert.Equal(OfficialCatalogLoadStatus.Remote, result.Status);
        Assert.Equal("81", result.ApiVersion);
        Assert.Equal(1, result.ModCount);
        Assert.Null(result.Reason);
        Assert.Equal("modding-api-81", Assert.Single(result.Catalog.Mods).LoaderId);
        Assert.Equal("81", Assert.Single((await AtomicJsonStore.ReadAsync<GameCatalog>(cachePath)).Loaders).Version);
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    [Fact]
    public async Task Load_returns_remote_catalog_when_cache_write_fails()
    {
        using var client = ClientFor(ModsXml("Remote Mod"), ApiXml("81"));
        var cachePath = Directory.CreateDirectory(Path.Combine(directory, "official.json")).FullName;

        var result = await OfficialCatalogSource.LoadAsync(client, cachePath);

        Assert.Equal(OfficialCatalogLoadStatus.Remote, result.Status);
        Assert.Equal("Remote Mod", Assert.Single(result.Catalog.Mods).Name);
        Assert.Contains("Cache", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Load_returns_cached_status_when_remote_fails()
    {
        var cachePath = Path.Combine(directory, "official.json");
        await AtomicJsonStore.WriteAsync(cachePath, CachedCatalog("80", "Cached Mod"));
        using var client = new HttpClient(new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))));

        var result = await OfficialCatalogSource.LoadAsync(client, cachePath);

        Assert.Equal(OfficialCatalogLoadStatus.Cached, result.Status);
        Assert.Equal("80", result.ApiVersion);
        Assert.Equal(1, result.ModCount);
        Assert.False(string.IsNullOrEmpty(result.Reason));
        Assert.Equal("Cached Mod", Assert.Single(result.Catalog.Mods).Name);
    }

    [Fact]
    public async Task Load_rejects_invalid_remote_before_overwriting_cache()
    {
        var cachePath = Path.Combine(directory, "official.json");
        await AtomicJsonStore.WriteAsync(cachePath, CachedCatalog("80", "Cached Mod"));
        var cacheBefore = await File.ReadAllBytesAsync(cachePath);
        using var client = ClientFor(ModsXml("Broken Mod"), ApiXml("60"));

        var result = await OfficialCatalogSource.LoadAsync(client, cachePath);

        Assert.Equal(OfficialCatalogLoadStatus.Cached, result.Status);
        Assert.Equal("80", result.ApiVersion);
        Assert.Contains("not supported", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(cacheBefore, await File.ReadAllBytesAsync(cachePath));
    }

    [Fact]
    public async Task Load_rejects_invalid_cache_and_returns_visible_failure()
    {
        var cachePath = Path.Combine(directory, "official.json");
        await AtomicJsonStore.WriteAsync(cachePath, CachedCatalog("60", "Broken Mod"));
        using var client = new HttpClient(new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))));

        var result = await OfficialCatalogSource.LoadAsync(client, cachePath);

        Assert.Equal(OfficialCatalogLoadStatus.Failed, result.Status);
        Assert.Empty(result.Catalog.Mods);
        Assert.Contains("not supported", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Load_returns_failed_status_when_remote_and_cache_fail()
    {
        var cachePath = Path.Combine(directory, "official.json");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(cachePath, "not json");
        using var client = new HttpClient(new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))));

        var result = await OfficialCatalogSource.LoadAsync(client, cachePath);

        Assert.Equal(OfficialCatalogLoadStatus.Failed, result.Status);
        Assert.Null(result.ApiVersion);
        Assert.Equal(0, result.ModCount);
        Assert.Empty(result.Catalog.Mods);
        Assert.Contains("Remote", result.Reason);
        Assert.Contains("cache", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Load_returns_failed_status_when_cache_read_is_unauthorized()
    {
        var cachePath = Directory.CreateDirectory(Path.Combine(directory, "official.json")).FullName;
        using var client = new HttpClient(new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))));

        var result = await OfficialCatalogSource.LoadAsync(client, cachePath);

        Assert.Equal(OfficialCatalogLoadStatus.Failed, result.Status);
        Assert.Empty(result.Catalog.Mods);
        Assert.Contains("Cache", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Load_propagates_caller_cancellation()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new HttpClient(new StubHandler(async (_, cancellationToken) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        using var cancellation = new CancellationTokenSource();
        var load = OfficialCatalogSource.LoadAsync(
            client,
            Path.Combine(directory, "official.json"),
            cancellation.Token);

        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => load.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static HttpClient ClientFor(string mods, string api) => new(new StubHandler((request, _) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(request.RequestUri!.AbsolutePath.EndsWith("ApiLinks.xml", StringComparison.Ordinal)
                ? api
                : mods)
        })));

    private static string ModsXml(string name) => $$"""
        <ModLinks xmlns="{{Namespace}}">
          <Manifest>
            <Name>{{name}}</Name>
            <Version>1.0.0.0</Version>
            <Link SHA256="{{new string('A', 64)}}">https://example.invalid/mod.zip</Link>
          </Manifest>
        </ModLinks>
        """;

    private static string ApiXml(string version) => $$"""
        <ApiLinks xmlns="{{Namespace}}">
          <Manifest>
            <Version>{{version}}</Version>
            <Links>
              <Windows SHA256="{{new string('B', 64)}}">https://example.invalid/api.zip</Windows>
            </Links>
          </Manifest>
        </ApiLinks>
        """;

    private static GameCatalog CachedCatalog(string apiVersion, string modName) => new()
    {
        Loaders =
        [
            new LoaderManifest
            {
                Id = $"modding-api-{apiVersion}",
                Name = "Modding API",
                Version = apiVersion,
                DownloadUrl = "https://example.invalid/api.zip",
                Sha256 = new string('B', 64),
                SupportedBuildIds = ["1.5.78.11833"]
            }
        ],
        Mods =
        [
            new ModManifest
            {
                Id = $"hkmod:{modName}",
                Name = modName,
                Version = "1",
                DownloadUrl = "https://example.invalid/mod.zip",
                Sha256 = new string('A', 64),
                LoaderId = $"modding-api-{apiVersion}",
                SupportedBuildIds = ["1.5.78.11833"]
            }
        ]
    };

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            responseFactory(request, cancellationToken);
    }
}

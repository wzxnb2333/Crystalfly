using System.Net;
using Crystalfly.Core.Catalog;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Tests.Catalog;

public sealed class ModTranslationSourceTests : IDisposable
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        $"crystalfly-translations-{Guid.NewGuid():N}");

    [Fact]
    public async Task Load_prefers_remote_and_merges_it_over_embedded_baseline()
    {
        var embedded = Catalog("Embedded", "Embedded description");
        var remote = Catalog("中文名称", null);
        using var client = ClientFor(remote);

        var result = await ModTranslationSource.LoadAsync(
            client,
            Path.Combine(directory, "mod-translations.json"),
            embedded,
            new Uri("https://example.test/translations.json"));

        Assert.Equal(ModTranslationLoadStatus.Remote, result.Status);
        var entry = Assert.Single(result.Catalog.Mods);
        Assert.Equal("中文名称", entry.DisplayName);
        Assert.Equal("Embedded description", entry.Description);
    }

    [Fact]
    public async Task Load_uses_cached_catalog_when_remote_fails()
    {
        var cachePath = Path.Combine(directory, "mod-translations.json");
        await AtomicJsonStore.WriteAsync(cachePath, Catalog("缓存名称", "缓存说明"));
        using var client = new HttpClient(new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))));

        var result = await ModTranslationSource.LoadAsync(
            client,
            cachePath,
            Catalog("内置名称", "内置说明"),
            new Uri("https://example.test/translations.json"));

        Assert.Equal(ModTranslationLoadStatus.Cached, result.Status);
        Assert.Equal("缓存名称", Assert.Single(result.Catalog.Mods).DisplayName);
        Assert.Contains("503", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Load_uses_embedded_catalog_when_remote_and_cache_are_invalid()
    {
        var cachePath = Path.Combine(directory, "mod-translations.json");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(cachePath, "not json");
        using var client = new HttpClient(new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway))));

        var result = await ModTranslationSource.LoadAsync(
            client,
            cachePath,
            Catalog("内置名称", "内置说明"),
            new Uri("https://example.test/translations.json"));

        Assert.Equal(ModTranslationLoadStatus.Embedded, result.Status);
        Assert.Equal("内置名称", Assert.Single(result.Catalog.Mods).DisplayName);
        Assert.Contains("cache", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Load_does_not_replace_cache_with_invalid_remote_content()
    {
        var cachePath = Path.Combine(directory, "mod-translations.json");
        await AtomicJsonStore.WriteAsync(cachePath, Catalog("缓存名称", "缓存说明"));
        var before = await File.ReadAllBytesAsync(cachePath);
        using var client = new HttpClient(new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"schemaVersion\":99}")
            })));

        var result = await ModTranslationSource.LoadAsync(
            client,
            cachePath,
            Catalog("内置名称", "内置说明"),
            new Uri("https://example.test/translations.json"));

        Assert.Equal(ModTranslationLoadStatus.Cached, result.Status);
        Assert.Equal(before, await File.ReadAllBytesAsync(cachePath));
    }

    [Fact]
    public async Task Load_uses_valid_remote_catalog_when_cache_write_fails()
    {
        Directory.CreateDirectory(directory);
        var blocker = Path.Combine(directory, "not-a-directory");
        await File.WriteAllTextAsync(blocker, "block");
        using var client = ClientFor(Catalog("远程名称", "远程说明"));

        var result = await ModTranslationSource.LoadAsync(
            client,
            Path.Combine(blocker, "mod-translations.json"),
            Catalog("内置名称", "内置说明"),
            new Uri("https://example.test/translations.json"));

        Assert.Equal(ModTranslationLoadStatus.Remote, result.Status);
        Assert.Equal("远程名称", Assert.Single(result.Catalog.Mods).DisplayName);
        Assert.Contains("cache write", result.Reason, StringComparison.OrdinalIgnoreCase);
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
        var load = ModTranslationSource.LoadAsync(
            client,
            Path.Combine(directory, "mod-translations.json"),
            Catalog("内置名称", "内置说明"),
            new Uri("https://example.test/translations.json"),
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

    private static ModTranslationCatalog Catalog(string displayName, string? description) => new()
    {
        TagNames = new Dictionary<string, string> { ["Gameplay"] = "玩法" },
        Mods =
        [
            new ModTranslationEntry
            {
                Id = "hkmod:Example",
                DisplayName = displayName,
                Description = description
            }
        ]
    };

    private static HttpClient ClientFor(ModTranslationCatalog catalog) =>
        new(new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CrystalflyJson.Serialize(catalog))
            })));

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            responseFactory(request, cancellationToken);
    }
}

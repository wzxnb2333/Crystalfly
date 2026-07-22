using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Crystalfly.Core.Catalog;
using Crystalfly.Core.Models;

namespace Crystalfly.Core.Tests.Catalog;

public sealed class ModContentSourceTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        $"crystalfly-mod-content-{Guid.NewGuid():N}");

    [Fact]
    public void Sanitizer_removes_active_content_images_and_unsafe_links()
    {
        var source = """
            # Title
            <script>alert('x')</script>
            ![tracker](https://example.test/tracker.png)
            [safe](https://example.test/page)
            [unsafe](javascript:alert(1))
            <iframe src="https://example.test"></iframe>
            """;

        var sanitized = MarkdownSanitizer.Sanitize(source);

        Assert.Contains("# Title", sanitized, StringComparison.Ordinal);
        Assert.Contains("tracker", sanitized, StringComparison.Ordinal);
        Assert.Contains("[safe](https://example.test/page)", sanitized, StringComparison.Ordinal);
        Assert.Contains("unsafe", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("script", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("iframe", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("![", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Load_fetches_sanitizes_and_atomically_caches_GitHub_content()
    {
        var handler = new QueueHandler(
            Response("# README\n<img src=x>\n[bad](javascript:run)", "\"readme-v1\""),
            Response("{\"body\":\"## Version 1\\n<script>x</script>\"}", "\"release-v1\""));
        using var client = new HttpClient(handler);
        var source = new ModContentSource(client, Path.Combine(root, "cache"));

        var result = await source.LoadAsync(Manifest());

        Assert.Equal(ModContentLoadStatus.Remote, result.Status);
        var document = Assert.IsType<ModContentDocument>(result.Document);
        Assert.Equal("\"readme-v1\"", document.ReadmeETag);
        Assert.Equal("\"release-v1\"", document.ReleaseETag);
        Assert.DoesNotContain("<img", document.ReadmeMarkdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", document.ReadmeMarkdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("script", document.ReleaseNotesMarkdown, StringComparison.OrdinalIgnoreCase);
        Assert.Single(Directory.EnumerateFiles(Path.Combine(root, "cache"), "*.json"));
    }

    [Fact]
    public async Task Load_reuses_cached_content_with_ETag_and_falls_back_after_network_failure()
    {
        var handler = new QueueHandler(
            Response("# README", "\"readme-v1\""),
            Response("{\"body\":\"## Release\"}", "\"release-v1\""),
            new HttpResponseMessage(HttpStatusCode.NotModified),
            new HttpResponseMessage(HttpStatusCode.NotModified));
        using var client = new HttpClient(handler);
        var cacheRoot = Path.Combine(root, "etag-cache");
        var source = new ModContentSource(client, cacheRoot);
        var first = await source.LoadAsync(Manifest());

        var second = await source.LoadAsync(Manifest());

        Assert.Equal(ModContentLoadStatus.Remote, second.Status);
        Assert.Equal(first.Document!.ReadmeMarkdown, second.Document!.ReadmeMarkdown);
        Assert.Equal("\"readme-v1\"", handler.Requests[2].IfNoneMatch);
        Assert.Equal("\"release-v1\"", handler.Requests[3].IfNoneMatch);

        using var offlineClient = new HttpClient(new ThrowingHandler());
        var cached = await new ModContentSource(offlineClient, cacheRoot).LoadAsync(Manifest());
        Assert.Equal(ModContentLoadStatus.Cached, cached.Status);
        Assert.Equal("# README", cached.Document!.ReadmeMarkdown);
    }

    [Fact]
    public async Task Load_propagates_real_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var client = new HttpClient(new ThrowingHandler());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ModContentSource(client, Path.Combine(root, "cancel-cache"))
                .LoadAsync(Manifest(), cancellation.Token));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static ModManifest Manifest() => new()
    {
        Id = "hkmod:Test",
        Name = "Test",
        Version = "1",
        DownloadUrl = "https://example.test/mod.zip",
        Sha256 = new string('A', 64),
        LoaderId = "modding-api-77",
        RepositoryUrl = "https://github.com/example/test"
    };

    private static HttpResponseMessage Response(string content, string etag)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "text/plain")
        };
        response.Headers.ETag = EntityTagHeaderValue.Parse(etag);
        return response;
    }

    private sealed class QueueHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses = new(responses);

        public List<RequestSnapshot> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(new RequestSnapshot(
                request.RequestUri!,
                request.Headers.IfNoneMatch.FirstOrDefault()?.ToString()));
            return Task.FromResult(responses.Dequeue());
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new HttpRequestException("offline");
        }
    }

    private sealed record RequestSnapshot(Uri Uri, string? IfNoneMatch);
}

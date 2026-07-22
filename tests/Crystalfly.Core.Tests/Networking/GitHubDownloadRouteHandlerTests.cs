using Crystalfly.Core.Configuration;
using Crystalfly.Core.Networking;

namespace Crystalfly.Core.Tests.Networking;

public sealed class GitHubDownloadRouteHandlerTests
{
    [Theory]
    [InlineData("https://github.com/owner/repo/releases/download/v1/package.zip")]
    [InlineData("https://raw.githubusercontent.com/owner/repo/main/catalog.json")]
    public void Rewrite_mirrors_supported_GitHub_urls(string value)
    {
        var result = GitHubDownloadRouteHandler.Rewrite(new Uri(value), GitHubDownloadRoute.Mirror);

        Assert.Equal($"{GitHubDownloadRouteHandler.MirrorPrefix}{value}", result.AbsoluteUri);
    }

    [Theory]
    [InlineData("https://example.com/package.zip")]
    [InlineData("https://github.example.com/package.zip")]
    public void Rewrite_keeps_non_GitHub_urls_unchanged(string value)
    {
        var original = new Uri(value);

        Assert.Same(original, GitHubDownloadRouteHandler.Rewrite(original, GitHubDownloadRoute.Mirror));
        Assert.Same(original, GitHubDownloadRouteHandler.Rewrite(original, GitHubDownloadRoute.Direct));
    }

    [Fact]
    public async Task Handler_reads_current_route_for_each_request()
    {
        var route = GitHubDownloadRoute.Direct;
        var capture = new CaptureHandler();
        using var client = new HttpClient(new GitHubDownloadRouteHandler(() => route, capture));

        await client.GetAsync("https://github.com/owner/repo/releases/download/v1/package.zip");
        route = GitHubDownloadRoute.Mirror;
        await client.GetAsync("https://raw.githubusercontent.com/owner/repo/main/catalog.json");

        Assert.Equal("https://github.com/owner/repo/releases/download/v1/package.zip", capture.Requests[0]);
        Assert.Equal(
            "https://gh-proxy.com/https://raw.githubusercontent.com/owner/repo/main/catalog.json",
            capture.Requests[1]);
    }

    [Fact]
    public async Task Handler_with_network_policy_blocks_offline_requests_before_transport()
    {
        var policy = new NetworkPolicy(isOffline: true);
        var capture = new CaptureHandler();
        using var client = new HttpClient(new GitHubDownloadRouteHandler(
            () => GitHubDownloadRoute.Direct,
            policy,
            capture));

        await Assert.ThrowsAsync<OfflineModeException>(() =>
            client.GetAsync("https://github.com/owner/repo/releases/download/v1/package.zip"));

        Assert.Empty(capture.Requests);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}

using Crystalfly.Core.Configuration;

namespace Crystalfly.Core.Networking;

public sealed class GitHubDownloadRouteHandler(
    Func<GitHubDownloadRoute> route,
    HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
{
    public const string MirrorPrefix = "https://gh-proxy.com/";

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri is { } uri)
        {
            request.RequestUri = Rewrite(uri, route());
        }

        return base.SendAsync(request, cancellationToken);
    }

    public static Uri Rewrite(Uri uri, GitHubDownloadRoute route)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (route != GitHubDownloadRoute.Mirror || !uri.IsAbsoluteUri || !IsGitHubHost(uri.Host))
        {
            return uri;
        }

        return new Uri(MirrorPrefix + uri.AbsoluteUri, UriKind.Absolute);
    }

    private static bool IsGitHubHost(string host) =>
        host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase);
}
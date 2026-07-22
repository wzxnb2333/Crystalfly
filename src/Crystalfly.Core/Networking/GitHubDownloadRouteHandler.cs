using Crystalfly.Core.Configuration;

namespace Crystalfly.Core.Networking;

public sealed class GitHubDownloadRouteHandler : DelegatingHandler
{
    public const string MirrorPrefix = "https://gh-proxy.com/";

    private readonly Func<GitHubDownloadRoute> route;

    public GitHubDownloadRouteHandler(
        Func<GitHubDownloadRoute> route,
        HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
        this.route = route ?? throw new ArgumentNullException(nameof(route));
    }

    public GitHubDownloadRouteHandler(
        Func<GitHubDownloadRoute> route,
        INetworkPolicy networkPolicy,
        HttpMessageHandler innerHandler)
        : this(route, new NetworkPolicyHandler(networkPolicy, innerHandler))
    {
    }

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

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

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri is not { } originalUri)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        GitHubDownloadRoute selectedRoute = route();
        request.RequestUri = Rewrite(originalUri, selectedRoute);
        bool mirrorRequest = selectedRoute == GitHubDownloadRoute.Mirror
            && request.RequestUri != originalUri;

        if (!mirrorRequest || !CanRetryWithoutMirror(request))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        try
        {
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
            if (!ShouldFallbackToDirect(response.StatusCode))
            {
                return response;
            }

            response.Dispose();
        }
        catch (HttpRequestException)
        {
            // A reverse proxy can fail while creating the HTTPS tunnel and never
            // produce an HTTP response. Retry the original endpoint below.
        }

        return await base.SendAsync(CloneWithoutMirror(request, originalUri), cancellationToken);
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

    private static bool CanRetryWithoutMirror(HttpRequestMessage request) =>
        (request.Method == HttpMethod.Get || request.Method == HttpMethod.Head)
        && request.Content is null;

    private static HttpRequestMessage CloneWithoutMirror(HttpRequestMessage request, Uri uri)
    {
        var clone = new HttpRequestMessage(request.Method, uri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };
        foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }

    private static bool ShouldFallbackToDirect(System.Net.HttpStatusCode statusCode) =>
        statusCode is
            System.Net.HttpStatusCode.BadRequest or
            System.Net.HttpStatusCode.NotFound or
            System.Net.HttpStatusCode.BadGateway or
            System.Net.HttpStatusCode.ServiceUnavailable or
            System.Net.HttpStatusCode.GatewayTimeout;
}

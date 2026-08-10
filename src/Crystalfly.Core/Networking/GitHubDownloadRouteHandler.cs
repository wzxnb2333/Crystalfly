using Crystalfly.Core.Configuration;

namespace Crystalfly.Core.Networking;

public sealed class GitHubDownloadRouteHandler : DelegatingHandler
{
    public const string MirrorPrefix = "https://gh-proxy.com/";
    public const string GhProxyOrgPrefix = "https://gh-proxy.org/";
    public const string GhProxyNetPrefix = "https://ghproxy.net/";
    public const string GhFastTopPrefix = "https://ghfast.top/";

    private static readonly GitHubDownloadRoute[] AutoRoutes =
    [
        GitHubDownloadRoute.GhProxyOrg,
        GitHubDownloadRoute.GhProxyNet,
        GitHubDownloadRoute.GhFastTop,
        GitHubDownloadRoute.Mirror,
        GitHubDownloadRoute.Direct
    ];

    private readonly Func<GitHubDownloadRoute> route;
    private readonly GitHubRoutePreference? routePreference;
    private GitHubDownloadRoute preferredAutoRoute = GitHubDownloadRoute.GhProxyOrg;

    public GitHubDownloadRouteHandler(
        Func<GitHubDownloadRoute> route,
        HttpMessageHandler innerHandler,
        GitHubRoutePreference? routePreference = null)
        : base(innerHandler)
    {
        this.route = route ?? throw new ArgumentNullException(nameof(route));
        this.routePreference = routePreference;
    }

    public GitHubDownloadRouteHandler(
        Func<GitHubDownloadRoute> route,
        INetworkPolicy networkPolicy,
        HttpMessageHandler innerHandler,
        GitHubRoutePreference? routePreference = null)
        : this(route, new NetworkPolicyHandler(networkPolicy, innerHandler), routePreference)
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
        if (selectedRoute == GitHubDownloadRoute.Auto)
        {
            return await SendAutoAsync(request, originalUri, cancellationToken);
        }

        request.RequestUri = Rewrite(originalUri, selectedRoute);
        bool mirrorRequest = selectedRoute != GitHubDownloadRoute.Direct
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

        return await base.SendAsync(CloneWithUri(request, originalUri), cancellationToken);
    }

    public static Uri Rewrite(Uri uri, GitHubDownloadRoute route)
    {
        ArgumentNullException.ThrowIfNull(uri);

        string? prefix = GetMirrorPrefix(route);
        if (prefix is null || !uri.IsAbsoluteUri || !IsGitHubHost(uri.Host))
        {
            return uri;
        }

        return new Uri(prefix + uri.AbsoluteUri, UriKind.Absolute);
    }

    private static bool IsGitHubHost(string host) =>
        host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase);

    private static bool CanRetryWithoutMirror(HttpRequestMessage request) =>
        (request.Method == HttpMethod.Get || request.Method == HttpMethod.Head)
        && request.Content is null;

    private async Task<HttpResponseMessage> SendAutoAsync(
        HttpRequestMessage request,
        Uri originalUri,
        CancellationToken cancellationToken)
    {
        if (!CanRetryWithoutMirror(request) || !IsGitHubHost(originalUri.Host))
        {
            request.RequestUri = originalUri;
            return await base.SendAsync(request, cancellationToken);
        }

        HttpRequestException? lastException = null;
        HttpResponseMessage? lastFallbackResponse = null;
        foreach (GitHubDownloadRoute candidate in GetAutoRoutes())
        {
            Uri candidateUri = Rewrite(originalUri, candidate);
            try
            {
                HttpResponseMessage response = await base.SendAsync(
                    CloneWithUri(request, candidateUri),
                    cancellationToken);
                if (!ShouldFallbackToDirect(response.StatusCode))
                {
                    lastFallbackResponse?.Dispose();
                    if (response.IsSuccessStatusCode)
                    {
                        preferredAutoRoute = candidate;
                        routePreference?.SetPreferred(candidate);
                    }
                    return response;
                }

                lastFallbackResponse?.Dispose();
                lastFallbackResponse = response;
            }
            catch (HttpRequestException exception)
            {
                lastException = exception;
            }
        }

        if (lastFallbackResponse is not null)
        {
            return lastFallbackResponse;
        }

        throw lastException ?? new HttpRequestException("All GitHub download routes failed.");
    }

    private IEnumerable<GitHubDownloadRoute> GetAutoRoutes()
    {
        GitHubDownloadRoute preferred = routePreference?.PreferredRoute ?? preferredAutoRoute;
        yield return preferred;
        foreach (GitHubDownloadRoute candidate in AutoRoutes)
        {
            if (candidate != preferred)
            {
                yield return candidate;
            }
        }
    }

    private static HttpRequestMessage CloneWithUri(HttpRequestMessage request, Uri uri)
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

    private static string? GetMirrorPrefix(GitHubDownloadRoute route) => route switch
    {
        GitHubDownloadRoute.Mirror => MirrorPrefix,
        GitHubDownloadRoute.GhProxyOrg => GhProxyOrgPrefix,
        GitHubDownloadRoute.GhProxyNet => GhProxyNetPrefix,
        GitHubDownloadRoute.GhFastTop => GhFastTopPrefix,
        _ => null
    };

    private static bool ShouldFallbackToDirect(System.Net.HttpStatusCode statusCode) =>
        statusCode is
            System.Net.HttpStatusCode.BadRequest or
            System.Net.HttpStatusCode.Forbidden or
            System.Net.HttpStatusCode.NotFound or
            System.Net.HttpStatusCode.RequestTimeout or
            System.Net.HttpStatusCode.TooManyRequests or
            System.Net.HttpStatusCode.InternalServerError or
            System.Net.HttpStatusCode.BadGateway or
            System.Net.HttpStatusCode.ServiceUnavailable or
            System.Net.HttpStatusCode.GatewayTimeout;
}

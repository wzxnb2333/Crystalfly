using Crystalfly.Core.Configuration;

namespace Crystalfly.Core.Networking;

public enum GitHubRouteLatencyStatus
{
    Success,
    Timeout,
    Unavailable
}

public sealed record GitHubRouteLatencyResult(
    GitHubDownloadRoute Route,
    GitHubRouteLatencyStatus Status,
    TimeSpan? Latency);

public sealed record GitHubRouteLatencyTestResult(
    GitHubRouteLatencyResult Direct,
    GitHubRouteLatencyResult Mirror)
{
    public IReadOnlyList<GitHubRouteLatencyResult> Routes { get; init; } = [Direct, Mirror];

    public GitHubRouteLatencyTestResult(IReadOnlyList<GitHubRouteLatencyResult> routes)
        : this(
            routes.First(result => result.Route == GitHubDownloadRoute.Direct),
            routes.First(result => result.Route == GitHubDownloadRoute.Mirror))
    {
        Routes = routes;
    }
}

public sealed class GitHubRouteLatencyService : IDisposable
{
    public static readonly Uri ProbeUri = new(
        "https://raw.githubusercontent.com/wzxnb2333/Crystalfly/main/catalog/catalog.v1.json");

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
    private readonly HttpClient httpClient;
    private readonly TimeProvider timeProvider;
    private readonly TimeSpan timeout;
    private readonly GitHubRoutePreference? routePreference;

    public GitHubRouteLatencyService(
        HttpMessageHandler handler,
        TimeProvider? timeProvider = null,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(handler);

        this.timeout = timeout ?? DefaultTimeout;
        if (this.timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        httpClient = new HttpClient(handler);
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public GitHubRouteLatencyService(
        HttpMessageHandler handler,
        TimeProvider? timeProvider,
        TimeSpan? timeout,
        GitHubRoutePreference routePreference)
        : this(handler, timeProvider, timeout)
    {
        this.routePreference = routePreference ?? throw new ArgumentNullException(nameof(routePreference));
    }

    public GitHubRouteLatencyService(
        INetworkPolicy networkPolicy,
        HttpMessageHandler handler,
        TimeProvider? timeProvider = null,
        TimeSpan? timeout = null)
        : this(new NetworkPolicyHandler(networkPolicy, handler), timeProvider, timeout)
    {
    }

    public GitHubRouteLatencyService(
        INetworkPolicy networkPolicy,
        HttpMessageHandler handler,
        TimeProvider? timeProvider,
        TimeSpan? timeout,
        GitHubRoutePreference routePreference)
        : this(new NetworkPolicyHandler(networkPolicy, handler), timeProvider, timeout, routePreference)
    {
    }

    public async Task<GitHubRouteLatencyTestResult> TestAsync(
        CancellationToken cancellationToken = default)
    {
        return await TestAsync(
            [GitHubDownloadRoute.Direct, GitHubDownloadRoute.Mirror],
            cancellationToken);
    }

    public async Task<GitHubRouteLatencyTestResult> TestAsync(
        IReadOnlyList<GitHubDownloadRoute> routes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(routes);
        if (routes.Count == 0 || routes.Distinct().Count() != routes.Count)
        {
            throw new ArgumentException("At least one unique GitHub route is required.", nameof(routes));
        }

        if (!routes.Contains(GitHubDownloadRoute.Direct) || !routes.Contains(GitHubDownloadRoute.Mirror))
        {
            throw new ArgumentException("The latency result must include the legacy direct and mirror routes.", nameof(routes));
        }

        GitHubRouteLatencyResult[] results = await Task.WhenAll(
            routes.Select(route => ProbeAsync(route, cancellationToken)));
        routePreference?.UpdateFromLatency(results);
        return new GitHubRouteLatencyTestResult(results);
    }

    public void Dispose() => httpClient.Dispose();

    private async Task<GitHubRouteLatencyResult> ProbeAsync(
        GitHubDownloadRoute route,
        CancellationToken cancellationToken)
    {
        Uri requestUri = GitHubDownloadRouteHandler.Rewrite(ProbeUri, route);
        long startedAt = timeProvider.GetTimestamp();
        using var timeoutCancellation = new CancellationTokenSource(timeout, timeProvider);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCancellation.Token);

        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync(
                requestUri,
                HttpCompletionOption.ResponseHeadersRead,
                linkedCancellation.Token);
            if (!response.IsSuccessStatusCode)
            {
                return new GitHubRouteLatencyResult(
                    route,
                    GitHubRouteLatencyStatus.Unavailable,
                    null);
            }

            return new GitHubRouteLatencyResult(
                route,
                GitHubRouteLatencyStatus.Success,
                timeProvider.GetElapsedTime(startedAt));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OfflineTransitionException)
        {
            return new GitHubRouteLatencyResult(route, GitHubRouteLatencyStatus.Unavailable, null);
        }
        catch (OperationCanceledException) when (timeoutCancellation.IsCancellationRequested)
        {
            return new GitHubRouteLatencyResult(route, GitHubRouteLatencyStatus.Timeout, null);
        }
        catch (HttpRequestException)
        {
            return new GitHubRouteLatencyResult(route, GitHubRouteLatencyStatus.Unavailable, null);
        }
    }
}

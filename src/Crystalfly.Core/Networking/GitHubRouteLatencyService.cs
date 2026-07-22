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
    GitHubRouteLatencyResult Mirror);

public sealed class GitHubRouteLatencyService : IDisposable
{
    public static readonly Uri ProbeUri = new(
        "https://raw.githubusercontent.com/wzxnb2333/Crystalfly/main/catalog/catalog.v1.json");

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
    private readonly HttpClient httpClient;
    private readonly TimeProvider timeProvider;
    private readonly TimeSpan timeout;

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
        INetworkPolicy networkPolicy,
        HttpMessageHandler handler,
        TimeProvider? timeProvider = null,
        TimeSpan? timeout = null)
        : this(new NetworkPolicyHandler(networkPolicy, handler), timeProvider, timeout)
    {
    }

    public async Task<GitHubRouteLatencyTestResult> TestAsync(
        CancellationToken cancellationToken = default)
    {
        Task<GitHubRouteLatencyResult> direct = ProbeAsync(
            GitHubDownloadRoute.Direct,
            cancellationToken);
        Task<GitHubRouteLatencyResult> mirror = ProbeAsync(
            GitHubDownloadRoute.Mirror,
            cancellationToken);

        await Task.WhenAll(direct, mirror);
        return new GitHubRouteLatencyTestResult(await direct, await mirror);
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

using System.Net;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Networking;

namespace Crystalfly.Core.Tests.Networking;

public sealed class GitHubRouteLatencyServiceTests
{
    [Fact]
    public async Task TestAsync_probes_direct_and_mirror_in_parallel()
    {
        var bothStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = new List<string>();
        var sync = new object();
        var started = 0;
        var handler = new StubHandler(async (request, cancellationToken) =>
        {
            lock (sync)
            {
                requests.Add(request.RequestUri!.AbsoluteUri);
            }

            if (Interlocked.Increment(ref started) == 2)
            {
                bothStarted.TrySetResult();
            }

            await release.Task.WaitAsync(cancellationToken);
            return Success();
        });
        using var service = new GitHubRouteLatencyService(handler);

        Task<GitHubRouteLatencyTestResult> test = service.TestAsync();
        await bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(2, Volatile.Read(ref started));
        Assert.Contains(GitHubRouteLatencyService.ProbeUri.AbsoluteUri, requests);
        Assert.Contains(
            GitHubDownloadRouteHandler.Rewrite(
                GitHubRouteLatencyService.ProbeUri,
                GitHubDownloadRoute.Mirror).AbsoluteUri,
            requests);

        release.TrySetResult();
        GitHubRouteLatencyTestResult result = await test;
        Assert.Equal(GitHubRouteLatencyStatus.Success, result.Direct.Status);
        Assert.Equal(GitHubRouteLatencyStatus.Success, result.Mirror.Status);
    }

    [Fact]
    public async Task TestAsync_measures_time_to_headers_without_reading_body()
    {
        var bothStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var time = new ManualTimeProvider();
        var started = 0;
        var handler = new StubHandler(async (_, cancellationToken) =>
        {
            if (Interlocked.Increment(ref started) == 2)
            {
                time.Advance(TimeSpan.FromMilliseconds(37));
                bothStarted.TrySetResult();
            }

            await release.Task.WaitAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ThrowIfReadContent()
            };
        });
        using var service = new GitHubRouteLatencyService(handler, time);

        Task<GitHubRouteLatencyTestResult> test = service.TestAsync();
        await bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        release.TrySetResult();
        GitHubRouteLatencyTestResult result = await test;

        Assert.Equal(TimeSpan.FromMilliseconds(37), result.Direct.Latency);
        Assert.Equal(TimeSpan.FromMilliseconds(37), result.Mirror.Latency);
    }

    [Fact]
    public async Task TestAsync_times_out_only_the_slow_route()
    {
        var handler = new StubHandler(async (request, cancellationToken) =>
        {
            if (request.RequestUri!.AbsoluteUri.StartsWith(
                    GitHubDownloadRouteHandler.MirrorPrefix,
                    StringComparison.Ordinal))
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return Success();
        });
        using var service = new GitHubRouteLatencyService(
            handler,
            timeout: TimeSpan.FromMilliseconds(25));

        GitHubRouteLatencyTestResult result = await service.TestAsync().WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(GitHubRouteLatencyStatus.Success, result.Direct.Status);
        Assert.Equal(GitHubRouteLatencyStatus.Timeout, result.Mirror.Status);
        Assert.Null(result.Mirror.Latency);
    }

    [Fact]
    public async Task TestAsync_reports_http_and_network_failures_as_unavailable()
    {
        var handler = new StubHandler((request, _) =>
        {
            if (request.RequestUri!.AbsoluteUri.StartsWith(
                    GitHubDownloadRouteHandler.MirrorPrefix,
                    StringComparison.Ordinal))
            {
                throw new HttpRequestException("offline");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        });
        using var service = new GitHubRouteLatencyService(handler);

        GitHubRouteLatencyTestResult result = await service.TestAsync();

        Assert.Equal(GitHubRouteLatencyStatus.Unavailable, result.Direct.Status);
        Assert.Equal(GitHubRouteLatencyStatus.Unavailable, result.Mirror.Status);
        Assert.Null(result.Direct.Latency);
        Assert.Null(result.Mirror.Latency);
    }

    [Fact]
    public async Task TestAsync_propagates_caller_cancellation()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHandler(async (_, cancellationToken) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Success();
        });
        using var service = new GitHubRouteLatencyService(handler);
        using var cancellation = new CancellationTokenSource();

        Task<GitHubRouteLatencyTestResult> test = service.TestAsync(cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => test.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    private static HttpResponseMessage Success() => new(HttpStatusCode.OK);

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => response(request, cancellationToken);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Interlocked.Read(ref timestamp);

        public void Advance(TimeSpan elapsed) => Interlocked.Add(ref timestamp, elapsed.Ticks);
    }

    private sealed class ThrowIfReadContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context) => throw new InvalidOperationException("Body was read.");

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}

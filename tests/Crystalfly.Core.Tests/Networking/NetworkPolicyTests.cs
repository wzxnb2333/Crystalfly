using System.Net;
using Crystalfly.Core.Networking;

namespace Crystalfly.Core.Tests.Networking;

public sealed class NetworkPolicyTests
{
    [Fact]
    public async Task Handler_blocks_requests_while_offline_without_invoking_inner_handler()
    {
        var policy = new NetworkPolicy(isOffline: true);
        var inner = new CountingHandler();
        using var client = new HttpClient(new NetworkPolicyHandler(policy, inner));

        await Assert.ThrowsAsync<OfflineModeException>(() =>
            client.GetAsync("https://example.test/content"));

        Assert.Equal(0, inner.RequestCount);
    }

    [Fact]
    public async Task Handler_cancels_active_request_with_offline_transition_exception()
    {
        var policy = new NetworkPolicy();
        var inner = new BlockingHandler();
        using var client = new HttpClient(new NetworkPolicyHandler(policy, inner));
        Task<HttpResponseMessage> request = client.GetAsync("https://example.test/content");
        await inner.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        policy.SetOffline(true);

        await Assert.ThrowsAsync<OfflineTransitionException>(() =>
            request.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task Handler_allows_new_requests_after_returning_online()
    {
        var policy = new NetworkPolicy(isOffline: true);
        var inner = new CountingHandler();
        using var client = new HttpClient(new NetworkPolicyHandler(policy, inner));
        policy.SetOffline(false);

        using var response = await client.GetAsync("https://example.test/content");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, inner.RequestCount);
    }

    [Fact]
    public void Changed_is_raised_once_for_each_actual_transition()
    {
        var policy = new NetworkPolicy();
        var states = new List<bool>();
        policy.Changed += (_, args) => states.Add(args.IsOffline);

        policy.SetOffline(false);
        policy.SetOffline(true);
        policy.SetOffline(true);
        policy.SetOffline(false);

        Assert.Equal([true, false], states);
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class BlockingHandler : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}

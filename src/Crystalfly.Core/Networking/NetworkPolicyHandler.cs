namespace Crystalfly.Core.Networking;

public sealed class NetworkPolicyHandler(
    INetworkPolicy policy,
    HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(policy);
        CancellationToken onlineCancellation = policy.GetOnlineCancellationToken();
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            onlineCancellation);
        try
        {
            return await base.SendAsync(request, linkedCancellation.Token);
        }
        catch (OperationCanceledException exception) when (
            onlineCancellation.IsCancellationRequested
            && !cancellationToken.IsCancellationRequested)
        {
            throw new OfflineTransitionException(exception, onlineCancellation);
        }
    }
}

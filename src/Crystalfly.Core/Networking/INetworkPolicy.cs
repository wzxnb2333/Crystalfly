namespace Crystalfly.Core.Networking;

public interface INetworkPolicy
{
    bool IsOffline { get; }

    event EventHandler<NetworkPolicyChangedEventArgs>? Changed;

    CancellationToken GetOnlineCancellationToken();
}

public sealed class NetworkPolicyChangedEventArgs(bool isOffline) : EventArgs
{
    public bool IsOffline { get; } = isOffline;
}

namespace Crystalfly.Core.Networking;

public sealed class NetworkPolicy : INetworkPolicy, IDisposable
{
    private readonly Lock sync = new();
    private CancellationTokenSource onlineCancellation = new();
    private bool isOffline;
    private bool disposed;

    public NetworkPolicy(bool isOffline = false)
    {
        this.isOffline = isOffline;
        if (isOffline)
        {
            onlineCancellation.Cancel();
        }
    }

    public bool IsOffline
    {
        get
        {
            lock (sync)
            {
                return isOffline;
            }
        }
    }

    public event EventHandler<NetworkPolicyChangedEventArgs>? Changed;

    public CancellationToken GetOnlineCancellationToken()
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (isOffline)
            {
                throw new OfflineModeException();
            }
            return onlineCancellation.Token;
        }
    }

    public void SetOffline(bool value)
    {
        CancellationTokenSource? cancel = null;
        CancellationTokenSource? dispose = null;
        EventHandler<NetworkPolicyChangedEventArgs>? changed;
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (isOffline == value)
            {
                return;
            }

            isOffline = value;
            if (value)
            {
                cancel = onlineCancellation;
            }
            else
            {
                dispose = onlineCancellation;
                onlineCancellation = new CancellationTokenSource();
            }
            changed = Changed;
        }

        cancel?.Cancel();
        dispose?.Dispose();
        changed?.Invoke(this, new NetworkPolicyChangedEventArgs(value));
    }

    public void Dispose()
    {
        CancellationTokenSource cancellation;
        lock (sync)
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            cancellation = onlineCancellation;
            Changed = null;
        }
        cancellation.Cancel();
        cancellation.Dispose();
    }
}

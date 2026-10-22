using Sentry.Extensibility;

namespace Crystalfly.App.Diagnostics;

internal sealed class ReportingNetworkGate : INetworkStatusListener, IDisposable
{
    private readonly object sync = new();
    private bool allowed;
    private DateTimeOffset retryAt;
    private CancellationTokenSource requests = new();
    private TaskCompletionSource changed = NewSignal();

    public bool Online { get { lock (sync) return allowed && DateTimeOffset.UtcNow >= retryAt; } }
    public CancellationToken RequestCancellation { get { lock (sync) return requests.Token; } }

    public void SetAllowed(bool value)
    {
        lock (sync)
        {
            allowed = value;
            if (!value) requests.Cancel();
            else if (requests.IsCancellationRequested)
            {
                requests.Dispose();
                requests = new CancellationTokenSource();
            }
            changed.TrySetResult();
            changed = NewSignal();
        }
    }

    public void Defer(TimeSpan delay)
    {
        lock (sync)
        {
            var until = DateTimeOffset.UtcNow + delay;
            if (until > retryAt) retryAt = until;
            changed.TrySetResult();
            changed = NewSignal();
        }
    }

    public async Task WaitForNetworkOnlineAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Task signal;
            TimeSpan delay;
            lock (sync)
            {
                if (allowed && DateTimeOffset.UtcNow >= retryAt) return;
                signal = changed.Task;
                delay = allowed ? retryAt - DateTimeOffset.UtcNow : Timeout.InfiniteTimeSpan;
            }
            if (delay != Timeout.InfiniteTimeSpan && delay <= TimeSpan.Zero) continue;
            try { await signal.WaitAsync(delay, cancellationToken).ConfigureAwait(false); }
            catch (TimeoutException) { }
        }
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void Dispose() { SetAllowed(false); requests.Dispose(); }
}

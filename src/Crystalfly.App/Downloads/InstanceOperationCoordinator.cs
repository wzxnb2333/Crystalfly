namespace Crystalfly.App.Downloads;

public sealed class InstanceOperationCoordinator
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly AsyncLocal<int> heldDepth = new();

    public async Task RunAsync(
        string instanceId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentNullException.ThrowIfNull(operation);
        if (heldDepth.Value > 0)
        {
            await operation(cancellationToken);
            return;
        }
        await gate.WaitAsync(cancellationToken);
        heldDepth.Value++;
        try
        {
            await operation(cancellationToken);
        }
        finally
        {
            heldDepth.Value--;
            gate.Release();
        }
    }
}

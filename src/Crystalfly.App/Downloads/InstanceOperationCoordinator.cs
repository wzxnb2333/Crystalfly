namespace Crystalfly.App.Downloads;

public sealed class InstanceOperationCoordinator
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task RunAsync(
        string instanceId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentNullException.ThrowIfNull(operation);
        await gate.WaitAsync(cancellationToken);
        try
        {
            await operation(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }
}

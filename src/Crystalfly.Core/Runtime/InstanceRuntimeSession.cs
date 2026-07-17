using Crystalfly.Core.LocalLow;
using Crystalfly.Core.Models;

namespace Crystalfly.Core.Runtime;

public sealed class InstanceRuntimeSession : IAsyncDisposable
{
    private readonly SemaphoreSlim completionLock = new(1, 1);
    private readonly LocalLowIsolationService isolation;
    private readonly HollowKnightProcessGuard guard;
    private readonly IHollowKnightProcessProbe processProbe;
    private bool completed;

    private InstanceRuntimeSession(
        LocalLowIsolationService isolation,
        HollowKnightProcessGuard guard,
        IHollowKnightProcessProbe processProbe,
        LocalLowSessionJournal journal)
    {
        this.isolation = isolation;
        this.guard = guard;
        this.processProbe = processProbe;
        Journal = journal;
    }

    public LocalLowSessionJournal Journal { get; }

    public static async Task<InstanceRuntimeSession> StartAsync(
        LocalLowIsolationService isolation,
        string instanceId,
        string mutexName = HollowKnightProcessGuard.DefaultMutexName,
        IHollowKnightProcessProbe? processProbe = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(isolation);
        processProbe ??= new SystemHollowKnightProcessProbe();
        var guard = HollowKnightProcessGuard.Acquire(mutexName, processProbe);
        try
        {
            var journal = await isolation.SwitchInAsync(instanceId, cancellationToken);
            return new InstanceRuntimeSession(isolation, guard, processProbe, journal);
        }
        catch
        {
            guard.Dispose();
            throw;
        }
    }

    public async Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        await completionLock.WaitAsync(cancellationToken);
        try
        {
            if (completed)
            {
                return;
            }
            if (processProbe.IsRunning())
            {
                throw new InvalidOperationException(
                    "The hollow_knight process must exit before LocalLow data is written back.");
            }

            await isolation.SwitchOutAsync(Journal.Id, cancellationToken);
            completed = true;
            guard.Dispose();
        }
        finally
        {
            completionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CompleteAsync();
        GC.SuppressFinalize(this);
    }

}

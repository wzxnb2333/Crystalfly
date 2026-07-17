using System.Diagnostics;

namespace Crystalfly.Core.Runtime;

public interface IHollowKnightProcessProbe
{
    bool IsRunning();
}

public sealed class SystemHollowKnightProcessProbe : IHollowKnightProcessProbe
{
    public bool IsRunning()
    {
        var processes = Process.GetProcessesByName("hollow_knight");
        try
        {
            return processes.Length != 0;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }
}

public sealed class HollowKnightProcessGuard : IDisposable
{
    public const string DefaultMutexName = @"Global\Crystalfly.HollowKnight.Runtime";
    private Mutex? mutex;

    private HollowKnightProcessGuard(Mutex mutex)
    {
        this.mutex = mutex;
    }

    public static HollowKnightProcessGuard Acquire(
        string mutexName = DefaultMutexName,
        IHollowKnightProcessProbe? processProbe = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mutexName);
        var mutex = new Mutex(initiallyOwned: false, mutexName, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            throw new InvalidOperationException("Another Crystalfly runtime already holds the named mutex.");
        }

        try
        {
            if ((processProbe ?? new SystemHollowKnightProcessProbe()).IsRunning())
            {
                throw new InvalidOperationException(
                    "A hollow_knight process is already running; LocalLow cannot be switched safely.");
            }
            return new HollowKnightProcessGuard(mutex);
        }
        catch
        {
            mutex.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref mutex, null)?.Dispose();
    }
}

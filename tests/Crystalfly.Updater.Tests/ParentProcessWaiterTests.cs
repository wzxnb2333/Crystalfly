using System.Diagnostics;

namespace Crystalfly.Updater.Tests;

public sealed class ParentProcessWaiterTests
{
    [Fact]
    public async Task WaitForExitAsync_returns_when_process_does_not_exist()
    {
        await ParentProcessWaiter.WaitForExitAsync(int.MaxValue, TimeSpan.FromSeconds(1), CancellationToken.None);
    }

    [Fact]
    public async Task WaitForExitAsync_fails_clearly_when_timeout_expires()
    {
        TimeoutException exception = await Assert.ThrowsAsync<TimeoutException>(() =>
            ParentProcessWaiter.WaitForExitAsync(
                Environment.ProcessId,
                TimeSpan.FromMilliseconds(50),
                CancellationToken.None));

        Assert.Contains(Environment.ProcessId.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains("timeout", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}

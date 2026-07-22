using Crystalfly.App.Downloads;

namespace Crystalfly.App.Tests.Downloads;

public sealed class InstanceOperationCoordinatorTests
{
    [Fact]
    public async Task Same_instance_operations_are_serialized()
    {
        var coordinator = new InstanceOperationCoordinator();
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = coordinator.RunAsync("instance", async cancellationToken =>
        {
            firstEntered.SetResult();
            await releaseFirst.Task.WaitAsync(cancellationToken);
        });
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var second = coordinator.RunAsync("instance", _ =>
        {
            secondEntered.SetResult();
            return Task.CompletedTask;
        });
        await Task.Delay(50);

        Assert.False(secondEntered.Task.IsCompleted);
        releaseFirst.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(secondEntered.Task.IsCompleted);
    }

    [Fact]
    public async Task Different_instance_operations_share_the_transaction_root_gate()
    {
        var coordinator = new InstanceOperationCoordinator();
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = coordinator.RunAsync("first", async cancellationToken =>
        {
            firstEntered.SetResult();
            await releaseFirst.Task.WaitAsync(cancellationToken);
        });
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var second = coordinator.RunAsync("second", _ =>
        {
            secondEntered.SetResult();
            return Task.CompletedTask;
        });
        await Task.Delay(50);

        Assert.False(secondEntered.Task.IsCompleted);
        releaseFirst.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(secondEntered.Task.IsCompleted);
    }

    [Fact]
    public async Task Nested_operation_for_the_held_instance_is_reentrant()
    {
        var coordinator = new InstanceOperationCoordinator();
        var nestedEntered = false;

        await coordinator.RunAsync("instance", async cancellationToken =>
        {
            await coordinator.RunAsync("instance", _ =>
            {
                nestedEntered = true;
                return Task.CompletedTask;
            }, cancellationToken);
        }).WaitAsync(TimeSpan.FromSeconds(1));

        Assert.True(nestedEntered);
    }
}

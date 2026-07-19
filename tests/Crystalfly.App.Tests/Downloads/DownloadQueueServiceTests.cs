using System.Collections.Concurrent;
using System.Net;
using Crystalfly.App.Downloads;
using Crystalfly.Core.Packages;

namespace Crystalfly.App.Tests.Downloads;

public sealed class DownloadQueueServiceTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(), "Crystalfly.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Independent_groups_use_at_most_three_concurrent_network_transfers()
    {
        var executor = new ControlledExecutor(blockTransfers: true);
        await using var queue = CreateQueue(executor);
        await queue.InitializeAsync();

        for (var index = 0; index < 4; index++)
        {
            await queue.EnqueueAsync(Group($"group-{index}", $"mod-{index}"));
        }

        await executor.WaitForStartedAsync(3);
        await Task.Delay(100);
        Assert.Equal(3, executor.StartedTransfers);
        Assert.Equal(3, executor.MaxConcurrentTransfers);

        executor.ReleaseTransfers();
        await queue.WaitForIdleAsync();

        Assert.Equal(4, executor.StartedTransfers);
        Assert.Equal(3, executor.MaxConcurrentTransfers);
    }

    [Fact]
    public async Task Steam_login_waiters_do_not_starve_catalog_transfers()
    {
        var catalogExecutor = new ControlledExecutor();
        var executor = new SteamDownloadQueueExecutor(
            catalogExecutor,
            (_, _, _) => throw new InvalidOperationException("Steam download started before login."),
            _ => null,
            static () => false,
            TimeSpan.FromMilliseconds(10));
        await using var queue = CreateQueue(executor);
        await queue.InitializeAsync();
        var steamGroups = Enumerable.Range(0, 3)
            .Select(index => SteamDownloadQueueGroupFactory.Create(
                "public",
                "Steam public",
                null,
                Path.Combine(root, "versions"),
                $"steam-{index}"))
            .ToArray();
        foreach (var group in steamGroups)
        {
            await queue.EnqueueAsync(group);
        }
        await WaitUntilAsync(() => steamGroups.All(group => queue.Groups
            .Single(candidate => candidate.Id == group.Id)
            .Items.Single().State == DownloadQueueItemState.Transferring));

        await queue.EnqueueAsync(Group("catalog", "feature"));

        await catalogExecutor.WaitForStartedAsync(1);
        await WaitUntilAsync(() => queue.Groups.Single(group => group.Id == "catalog").State
            == DownloadQueueGroupState.Completed);
        Assert.Equal(DownloadQueueGroupState.Completed,
            queue.Groups.Single(group => group.Id == "catalog").State);

        foreach (var group in steamGroups)
        {
            await queue.CancelAsync(group.Id);
        }
        await queue.WaitForIdleAsync();
    }

    [Fact]
    public async Task Items_in_one_group_run_in_dependency_order()
    {
        var executor = new ControlledExecutor();
        await using var queue = CreateQueue(executor);
        await queue.InitializeAsync();
        var group = Group(
            "feature-group",
            "feature",
            Item("loader", DownloadQueueItemKind.Loader),
            Item("library", DownloadQueueItemKind.Dependency),
            Item("feature", DownloadQueueItemKind.Mod));

        await queue.EnqueueAsync(group);
        await queue.WaitForIdleAsync();

        Assert.Equal(
            [
                "transfer:loader", "install:loader",
                "transfer:library", "install:library",
                "transfer:feature", "install:feature"
            ],
            executor.Events);
    }

    [Fact]
    public async Task Duplicate_instance_and_mod_request_reuses_existing_group()
    {
        var executor = new ControlledExecutor(blockTransfers: true);
        await using var queue = CreateQueue(executor);
        await queue.InitializeAsync();
        var group = Group("first", "feature");

        var first = await queue.EnqueueAsync(group);
        var duplicate = await queue.EnqueueAsync(group with { Id = "second" });

        Assert.True(first.Added);
        Assert.False(duplicate.Added);
        Assert.Equal("first", duplicate.Group.Id);

        executor.ReleaseTransfers();
        await queue.WaitForIdleAsync();
    }

    [Fact]
    public async Task Failed_request_does_not_block_a_new_catalog_plan()
    {
        var executor = new ControlledExecutor(
            transferFailure: _ => new InvalidDataException("bad package"));
        await using var queue = CreateQueue(executor);
        await queue.InitializeAsync();
        await queue.EnqueueAsync(Group("failed", "feature"));
        await queue.WaitForIdleAsync();

        var duplicate = await queue.EnqueueAsync(Group("duplicate", "feature"));

        Assert.True(duplicate.Added);
        Assert.Equal("duplicate", duplicate.Group.Id);
        Assert.Equal(2, queue.Groups.Count);
        await queue.WaitForIdleAsync();
    }

    [Fact]
    public async Task Transient_transfer_failure_retries_three_times()
    {
        var executor = new ControlledExecutor(
            transferFailure: _ => new HttpRequestException(
                "temporary", null, HttpStatusCode.ServiceUnavailable));
        await using var queue = CreateQueue(executor);
        await queue.InitializeAsync();

        await queue.EnqueueAsync(Group("retry", "feature"));
        await queue.WaitForIdleAsync();

        var group = Assert.Single(queue.Groups);
        var item = Assert.Single(group.Items);
        Assert.Equal(4, executor.StartedTransfers);
        Assert.Equal(3, item.RetryCount);
        Assert.Equal(DownloadQueueItemState.Failed, item.State);
    }

    [Fact]
    public async Task Deterministic_failure_is_not_retried_and_blocks_later_items()
    {
        var executor = new ControlledExecutor(
            transferFailure: _ => new InvalidDataException("bad hash"));
        await using var queue = CreateQueue(executor);
        await queue.InitializeAsync();

        await queue.EnqueueAsync(Group(
            "blocked",
            "feature",
            Item("library", DownloadQueueItemKind.Dependency),
            Item("feature", DownloadQueueItemKind.Mod)));
        await queue.WaitForIdleAsync();

        var items = Assert.Single(queue.Groups).Items;
        Assert.Equal(1, executor.StartedTransfers);
        Assert.Equal(DownloadQueueItemState.Failed, items[0].State);
        Assert.Equal(DownloadQueueItemState.Blocked, items[1].State);
    }

    [Fact]
    public async Task Cancel_group_stops_current_transfer_and_does_not_start_later_items()
    {
        var executor = new ControlledExecutor(blockTransfers: true);
        await using var queue = CreateQueue(executor);
        await queue.InitializeAsync();
        await queue.EnqueueAsync(Group(
            "cancel",
            "feature",
            Item("library", DownloadQueueItemKind.Dependency),
            Item("feature", DownloadQueueItemKind.Mod)));
        await executor.WaitForStartedAsync(1);

        await queue.CancelAsync("cancel");
        await queue.WaitForIdleAsync();

        var items = Assert.Single(queue.Groups).Items;
        Assert.Equal(1, executor.StartedTransfers);
        Assert.All(items, item => Assert.Equal(DownloadQueueItemState.Canceled, item.State));
        Assert.Empty(await ReadStoredGroupsAsync());
    }

    [Fact]
    public async Task Cancel_after_transfer_returns_does_not_install_or_complete_group()
    {
        var installPersistenceStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseInstallPersistence = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var executor = new ControlledExecutor();
        await using var queue = CreateQueue(
            executor,
            persistOverride: async (groups, _) =>
            {
                if (groups.SingleOrDefault()?.Items.Single().State
                    == DownloadQueueItemState.Installing)
                {
                    installPersistenceStarted.TrySetResult();
                    await releaseInstallPersistence.Task;
                }
            });
        await queue.InitializeAsync();
        await queue.EnqueueAsync(Group("cancel-transfer-boundary", "feature"));
        await installPersistenceStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var cancellation = queue.CancelAsync("cancel-transfer-boundary");
        await WaitUntilAsync(() => Assert.Single(queue.Groups).State
            == DownloadQueueGroupState.Canceled);
        releaseInstallPersistence.TrySetResult();
        await cancellation;
        await queue.WaitForIdleAsync();

        var group = Assert.Single(queue.Groups);
        Assert.Equal(0, executor.StartedInstalls);
        Assert.Equal(DownloadQueueGroupState.Canceled, group.State);
        Assert.Equal(DownloadQueueItemState.Canceled, Assert.Single(group.Items).State);
    }

    [Fact]
    public async Task Failed_group_can_be_retried()
    {
        var executor = new ControlledExecutor(
            transferFailure: attempt => attempt == 1 ? new InvalidDataException("bad package") : null);
        await using var queue = CreateQueue(executor);
        await queue.InitializeAsync();
        await queue.EnqueueAsync(Group("retry-group", "feature"));
        await queue.WaitForIdleAsync();
        Assert.Equal(DownloadQueueGroupState.Failed, Assert.Single(queue.Groups).State);

        await queue.RetryAsync("retry-group");
        await queue.WaitForIdleAsync();

        Assert.Equal(DownloadQueueGroupState.Completed, Assert.Single(queue.Groups).State);
        Assert.Equal(2, executor.StartedTransfers);
    }

    [Fact]
    public async Task Failed_group_can_be_canceled_and_removed_from_persistence()
    {
        var executor = new ControlledExecutor(
            transferFailure: _ => new InvalidDataException("bad package"));
        await using var queue = CreateQueue(executor);
        await queue.InitializeAsync();
        await queue.EnqueueAsync(Group("failed-cancel", "feature"));
        await queue.WaitForIdleAsync();

        await queue.CancelAsync("failed-cancel");

        Assert.Equal(DownloadQueueGroupState.Canceled, Assert.Single(queue.Groups).State);
        Assert.Empty(await ReadStoredGroupsAsync());
    }

    [Fact]
    public async Task Failed_group_can_be_retried_as_soon_as_failure_is_published()
    {
        var executor = new ControlledExecutor(
            transferFailure: attempt => attempt == 1 ? new InvalidDataException("bad package") : null);
        await using var queue = CreateQueue(executor);
        Task? retry = null;
        queue.QueueChanged += groups =>
        {
            if (groups.SingleOrDefault()?.State == DownloadQueueGroupState.Failed)
            {
                retry ??= queue.RetryAsync("retry-now");
            }
        };
        await queue.InitializeAsync();

        await queue.EnqueueAsync(Group("retry-now", "feature"));
        await WaitUntilAsync(() => retry is not null);
        await retry!;
        await queue.WaitForIdleAsync();

        Assert.Equal(DownloadQueueGroupState.Completed, Assert.Single(queue.Groups).State);
        Assert.Equal(2, executor.StartedTransfers);
    }

    [Fact]
    public async Task Retry_rolls_back_when_persistence_fails()
    {
        var failWrites = false;
        var executor = new ControlledExecutor(
            transferFailure: attempt => attempt == 1 ? new InvalidDataException("bad package") : null);
        await using var queue = CreateQueue(
            executor,
            persistOverride: (groups, cancellationToken) => failWrites
                ? Task.FromException(new IOException("disk failed"))
                : WriteStoredGroupsAsync(groups, cancellationToken));
        await queue.InitializeAsync();
        await queue.EnqueueAsync(Group("retry-persist", "feature"));
        await queue.WaitForIdleAsync();

        failWrites = true;
        await Assert.ThrowsAsync<IOException>(() => queue.RetryAsync("retry-persist"));
        await queue.WaitForIdleAsync();

        var group = Assert.Single(queue.Groups);
        Assert.Equal(DownloadQueueGroupState.Failed, group.State);
        Assert.Equal(DownloadQueueItemState.Failed, Assert.Single(group.Items).State);
        failWrites = false;
    }

    [Fact]
    public async Task Completed_groups_remain_in_memory_but_are_not_persisted()
    {
        var executor = new ControlledExecutor();
        await using var queue = CreateQueue(executor);
        await queue.InitializeAsync();

        await queue.EnqueueAsync(Group("complete", "feature"));
        await queue.WaitForIdleAsync();

        Assert.Equal(DownloadQueueGroupState.Completed, Assert.Single(queue.Groups).State);
        Assert.Empty(await ReadStoredGroupsAsync());
    }

    [Fact]
    public async Task Completed_groups_are_removed_from_active_task_tracking()
    {
        await using var queue = CreateQueue(new ControlledExecutor());
        await queue.InitializeAsync();

        await queue.EnqueueAsync(Group("tracked", "feature"));
        await queue.WaitForIdleAsync();
        await Task.Delay(50);

        var tasks = Assert.IsAssignableFrom<ICollection<Task>>(typeof(DownloadQueueService)
            .GetField("groupTasks", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(queue));
        Assert.Empty(tasks);
    }

    [Fact]
    public async Task Install_waits_for_game_exit_without_holding_transfer_slot()
    {
        var gameRunning = true;
        var executor = new ControlledExecutor(requiresGameExit: true);
        await using var queue = CreateQueue(executor, () => gameRunning);
        await queue.InitializeAsync();

        await queue.EnqueueAsync(Group("wait", "feature"));
        await executor.WaitForStartedAsync(1);
        await WaitUntilAsync(() => Assert.Single(queue.Groups).Items[0].State
            == DownloadQueueItemState.WaitingForGameExit);
        Assert.Equal(0, executor.StartedInstalls);

        gameRunning = false;
        await queue.WaitForIdleAsync();

        Assert.Equal(1, executor.StartedInstalls);
    }

    [Fact]
    public async Task Interrupted_group_resumes_after_restart()
    {
        var firstExecutor = new ControlledExecutor(blockTransfers: true);
        var firstQueue = CreateQueue(firstExecutor);
        await firstQueue.InitializeAsync();
        await firstQueue.EnqueueAsync(Group("resume", "feature"));
        await firstExecutor.WaitForStartedAsync(1);
        await firstQueue.DisposeAsync();

        var secondExecutor = new ControlledExecutor();
        await using var secondQueue = CreateQueue(secondExecutor);
        await secondQueue.InitializeAsync();
        await secondQueue.WaitForIdleAsync();

        Assert.Equal(DownloadQueueGroupState.Completed, Assert.Single(secondQueue.Groups).State);
        Assert.Equal(1, secondExecutor.StartedTransfers);
    }

    [Fact]
    public async Task Groups_returns_a_deep_snapshot()
    {
        var executor = new ControlledExecutor(blockTransfers: true);
        await using var queue = CreateQueue(executor);
        await queue.InitializeAsync();
        await queue.EnqueueAsync(Group("snapshot", "feature"));
        await executor.WaitForStartedAsync(1);

        var snapshot = Assert.Single(queue.Groups);
        snapshot.State = DownloadQueueGroupState.Canceled;
        snapshot.Items[0].State = DownloadQueueItemState.Canceled;

        Assert.NotEqual(DownloadQueueGroupState.Canceled, Assert.Single(queue.Groups).State);
        Assert.NotEqual(DownloadQueueItemState.Canceled, Assert.Single(queue.Groups).Items[0].State);
        await queue.CancelAsync("snapshot");
        await queue.WaitForIdleAsync();
    }

    [Fact]
    public async Task QueueChanged_publishes_progress_and_terminal_state_snapshots()
    {
        var executor = new ControlledExecutor();
        await using var queue = CreateQueue(executor);
        var states = new ConcurrentQueue<DownloadQueueGroupState>();
        queue.QueueChanged += groups =>
        {
            if (groups.Count > 0)
            {
                states.Enqueue(groups[0].State);
            }
        };
        await queue.InitializeAsync();

        await queue.EnqueueAsync(Group("notify", "feature"));
        await queue.WaitForIdleAsync();

        Assert.Contains(DownloadQueueGroupState.Running, states);
        Assert.Contains(DownloadQueueGroupState.Completed, states);
    }

    [Fact]
    public async Task Concurrent_dispose_calls_wait_for_the_same_shutdown()
    {
        var executor = new ControlledExecutor(blockTransfers: true, ignoreTransferCancellation: true);
        var queue = CreateQueue(executor);
        await queue.InitializeAsync();
        await queue.EnqueueAsync(Group("dispose", "feature"));
        await executor.WaitForStartedAsync(1);

        var first = queue.DisposeAsync().AsTask();
        var second = queue.DisposeAsync().AsTask();
        await Task.Delay(50);

        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);
        executor.ReleaseTransfers();
        await Task.WhenAll(first, second);
    }

    [Fact]
    public async Task Cancellation_callback_reentering_dispose_receives_same_shutdown_task()
    {
        DownloadQueueService? queue = null;
        Task? reentrant = null;
        var callbackRan = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executor = new ControlledExecutor(
            blockTransfers: true,
            ignoreTransferCancellation: true,
            cancellationCallback: () =>
            {
                reentrant = queue!.DisposeAsync().AsTask();
                callbackRan.TrySetResult();
            });
        queue = CreateQueue(executor);
        await queue.InitializeAsync();
        await queue.EnqueueAsync(Group("dispose-reenter", "feature"));
        await executor.WaitForStartedAsync(1);

        var shutdown = queue.DisposeAsync().AsTask();
        await callbackRan.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Same(shutdown, reentrant);

        executor.ReleaseTransfers();
        await shutdown;
    }

    [Fact]
    public async Task User_cancel_remains_canceled_when_shutdown_starts_concurrently()
    {
        var executor = new ControlledExecutor(blockTransfers: true, ignoreTransferCancellation: true);
        var queue = CreateQueue(executor);
        await queue.InitializeAsync();
        await queue.EnqueueAsync(Group("cancel-close", "feature"));
        await executor.WaitForStartedAsync(1);

        await queue.CancelAsync("cancel-close");
        var shutdown = queue.DisposeAsync().AsTask();
        executor.ReleaseTransfers();
        await shutdown;

        var stored = await ReadStoredGroupsAsync();
        Assert.Empty(stored);
    }

    [Fact]
    public async Task Cancel_notification_can_reenter_queue_without_deadlock()
    {
        var executor = new ControlledExecutor(blockTransfers: true);
        await using var queue = CreateQueue(executor);
        Task<DownloadQueueEnqueueResult>? reentry = null;
        queue.QueueChanged += groups =>
        {
            if (groups.Any(group => group.Id == "cancel-reenter"
                && group.State == DownloadQueueGroupState.Canceled))
            {
                reentry ??= queue.EnqueueAsync(Group("replacement", "other"));
            }
        };
        await queue.InitializeAsync();
        await queue.EnqueueAsync(Group("cancel-reenter", "feature"));
        await executor.WaitForStartedAsync(1);

        await queue.CancelAsync("cancel-reenter").WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True((await reentry!.WaitAsync(TimeSpan.FromSeconds(5))).Added);

        executor.ReleaseTransfers();
        await queue.WaitForIdleAsync();
    }

    [Fact]
    public async Task Cancellation_callback_failure_still_persists_canceled_state()
    {
        var executor = new ControlledExecutor(
            blockTransfers: true,
            cancellationCallback: () => throw new InvalidOperationException("callback failed"));
        await using var queue = CreateQueue(executor);
        var canceledPublished = false;
        queue.QueueChanged += groups => canceledPublished |= groups.Any(group =>
            group.Id == "cancel-callback" && group.State == DownloadQueueGroupState.Canceled);
        await queue.InitializeAsync();
        await queue.EnqueueAsync(Group("cancel-callback", "feature"));
        await executor.WaitForStartedAsync(1);
        await executor.WaitForCancellationRegistrationAsync();

        await Assert.ThrowsAsync<AggregateException>(() => queue.CancelAsync("cancel-callback"));
        await queue.WaitForIdleAsync();

        Assert.True(canceledPublished);
        Assert.Empty(await ReadStoredGroupsAsync());
    }

    [Fact]
    public async Task Duplicate_group_id_is_rejected_even_with_a_different_deduplication_key()
    {
        var executor = new ControlledExecutor(blockTransfers: true);
        await using var queue = CreateQueue(executor);
        await queue.InitializeAsync();
        await queue.EnqueueAsync(Group("same-id", "feature"));

        var duplicateId = Group("same-id", "other") with { DeduplicationKey = "instance:other" };
        await Assert.ThrowsAsync<ArgumentException>(() => queue.EnqueueAsync(duplicateId));

        await queue.CancelAsync("same-id");
        await queue.WaitForIdleAsync();
    }

    [Fact]
    public async Task Empty_group_id_is_rejected()
    {
        await using var queue = CreateQueue(new ControlledExecutor());
        await queue.InitializeAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            queue.EnqueueAsync(Group(" ", "feature")));
    }

    [Fact]
    public async Task QueueChanged_subscriber_failure_does_not_fail_download()
    {
        var executor = new ControlledExecutor();
        await using var queue = CreateQueue(executor);
        queue.QueueChanged += _ => throw new InvalidOperationException("subscriber failed");
        await queue.InitializeAsync();

        await queue.EnqueueAsync(Group("event", "feature"));
        await queue.WaitForIdleAsync();

        Assert.Equal(DownloadQueueGroupState.Completed, Assert.Single(queue.Groups).State);
    }

    [Fact]
    public async Task Final_persistence_failure_faults_idle_and_shutdown()
    {
        var emptyWrites = 0;
        var executor = new ControlledExecutor();
        var queue = CreateQueue(
            executor,
            persistOverride: (groups, cancellationToken) =>
            {
                if (groups.Count == 0 && Interlocked.Increment(ref emptyWrites) == 2)
                {
                    return Task.FromException(new IOException("final persistence failed"));
                }
                return WriteStoredGroupsAsync(groups, cancellationToken);
            });
        await queue.InitializeAsync();
        await queue.EnqueueAsync(Group("persist-final", "feature"));

        await Assert.ThrowsAsync<IOException>(() => queue.WaitForIdleAsync());
        await Assert.ThrowsAsync<IOException>(() => queue.DisposeAsync().AsTask());
    }

    private DownloadQueueService CreateQueue(
        IDownloadQueueExecutor executor,
        Func<bool>? isGameRunning = null,
        Func<IReadOnlyList<DownloadQueueGroup>, CancellationToken, Task>? persistOverride = null) => new(
        Path.Combine(root, "download-queue.json"),
        executor,
        isGameRunning ?? (static () => false),
        gameExitPollInterval: TimeSpan.FromMilliseconds(10),
        persistOverride);

    private Task WriteStoredGroupsAsync(
        IReadOnlyList<DownloadQueueGroup> groups,
        CancellationToken cancellationToken) =>
        Crystalfly.Core.Serialization.AtomicJsonStore.WriteAsync(
            Path.Combine(root, "download-queue.json"),
            groups,
            cancellationToken);

    private async Task<IReadOnlyList<DownloadQueueGroup>> ReadStoredGroupsAsync() =>
        await Crystalfly.Core.Serialization.AtomicJsonStore.ReadAsync<DownloadQueueGroup[]>(
            Path.Combine(root, "download-queue.json"));

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private static DownloadQueueGroup Group(
        string id,
        string modId,
        params DownloadQueueItem[] items) => new()
        {
            Id = id,
            DeduplicationKey = $"instance:{modId}",
            Kind = DownloadQueueGroupKind.ModInstall,
            Name = modId,
            TargetInstanceId = "instance",
            TargetInstanceName = "Instance",
            TargetInstanceRoot = "C:\\game",
            CreatedAt = DateTimeOffset.UtcNow,
            Items = items.Length == 0 ? [Item(modId, DownloadQueueItemKind.Mod)] : items
        };

    private static DownloadQueueItem Item(string id, DownloadQueueItemKind kind) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Kind = kind,
        PackageId = id,
        Name = id,
        Version = "1.0",
        LoaderId = "modding-api-77",
        State = DownloadQueueItemState.Pending
    };

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class ControlledExecutor : IDownloadQueueExecutor
    {
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource cancellationRegistered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly bool blockTransfers;
        private readonly bool requiresGameExit;
        private readonly bool ignoreTransferCancellation;
        private readonly Action? cancellationCallback;
        private readonly Func<int, Exception?>? transferFailure;
        private int activeTransfers;
        private int maxConcurrentTransfers;
        private int startedTransfers;
        private int startedInstalls;

        public ControlledExecutor(
            bool blockTransfers = false,
            bool requiresGameExit = false,
            bool ignoreTransferCancellation = false,
            Action? cancellationCallback = null,
            Func<int, Exception?>? transferFailure = null)
        {
            this.blockTransfers = blockTransfers;
            this.requiresGameExit = requiresGameExit;
            this.ignoreTransferCancellation = ignoreTransferCancellation;
            this.cancellationCallback = cancellationCallback;
            this.transferFailure = transferFailure;
        }

        public ConcurrentQueue<string> Events { get; } = new();

        public int StartedTransfers => Volatile.Read(ref startedTransfers);

        public int MaxConcurrentTransfers => Volatile.Read(ref maxConcurrentTransfers);

        public int StartedInstalls => Volatile.Read(ref startedInstalls);

        public bool RequiresGameExit(DownloadQueueItem item) => requiresGameExit;

        public bool IsTransient(Exception exception) => exception is HttpRequestException;

        public async Task TransferAsync(
            DownloadQueueGroup group,
            DownloadQueueItem item,
            IProgress<PackageTransferProgress> progress,
            SemaphoreSlim networkGate,
            CancellationToken cancellationToken)
        {
            await networkGate.WaitAsync(cancellationToken);
            var attempt = Interlocked.Increment(ref startedTransfers);
            var active = Interlocked.Increment(ref activeTransfers);
            InterlockedExtensions.Max(ref maxConcurrentTransfers, active);
            Events.Enqueue($"transfer:{item.PackageId}");
            Task? blockedTransfer = blockTransfers
                ? ignoreTransferCancellation
                    ? release.Task
                    : release.Task.WaitAsync(cancellationToken)
                : null;
            using var registration = cancellationCallback is null
                ? default
                : cancellationToken.Register(cancellationCallback);
            if (cancellationCallback is not null)
            {
                cancellationRegistered.TrySetResult();
            }
            try
            {
                if (blockTransfers)
                {
                    await blockedTransfer!;
                }
                if (transferFailure?.Invoke(attempt) is { } exception)
                {
                    throw exception;
                }
            }
            finally
            {
                Interlocked.Decrement(ref activeTransfers);
                networkGate.Release();
            }
        }

        public Task InstallAsync(
            DownloadQueueGroup group,
            DownloadQueueItem item,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref startedInstalls);
            Events.Enqueue($"install:{item.PackageId}");
            return Task.CompletedTask;
        }

        public async Task WaitForStartedAsync(int count)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (StartedTransfers < count)
            {
                await Task.Delay(10, timeout.Token);
            }
        }

        public void ReleaseTransfers() => release.TrySetResult();

        public Task WaitForCancellationRegistrationAsync() =>
            cancellationRegistered.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static class InterlockedExtensions
    {
        public static void Max(ref int target, int value)
        {
            int current;
            while ((current = Volatile.Read(ref target)) < value
                && Interlocked.CompareExchange(ref target, value, current) != current)
            {
            }
        }
    }
}

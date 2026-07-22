using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Runtime.ExceptionServices;
using Crystalfly.Core.Networking;
using Crystalfly.Core.Packages;
using Crystalfly.Core.Serialization;

namespace Crystalfly.App.Downloads;

public sealed class DownloadQueueService : IAsyncDisposable
{
    private const int MaxTransferRetries = 3;
    private readonly string storePath;
    private readonly IDownloadQueueExecutor executor;
    private readonly Func<bool> isGameRunning;
    private readonly Func<IReadOnlyList<DownloadQueueGroup>, CancellationToken, Task>? persistOverride;
    private readonly TimeSpan gameExitPollInterval;
    private readonly INetworkPolicy? networkPolicy;
    private readonly InstanceOperationCoordinator? operationCoordinator;
    private readonly Channel<DownloadQueueGroup> channel = Channel.CreateUnbounded<DownloadQueueGroup>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly SemaphoreSlim networkGate = new(3, 3);
    private readonly SemaphoreSlim persistenceGate = new(1, 1);
    private readonly SemaphoreSlim mutationGate = new(1, 1);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> presetInstanceGates =
        new(StringComparer.Ordinal);
    private readonly CancellationTokenSource lifetime = new();
    private readonly object disposeSync = new();
    private readonly Lock sync = new();
    private readonly List<DownloadQueueGroup> groups = [];
    private readonly List<Task> groupTasks = [];
    private readonly Dictionary<string, Task> activeGroupTasks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CancellationTokenSource> groupCancellations =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> userCanceledGroups = new(StringComparer.Ordinal);
    private readonly HashSet<string> networkWaitingGroups = new(StringComparer.Ordinal);
    private readonly object networkStateSync = new();
    private TaskCompletionSource idle = CompletedSource();
    private Task? dispatcher;
    private int pendingGroups;
    private Task? disposeTask;
    private Task networkStateTask = Task.CompletedTask;
    private Exception? idleFailure;
    private Exception? backgroundFailure;
    private bool initialized;
    private bool shuttingDown;
    private bool disposed;

    public DownloadQueueService(
        string storePath,
        IDownloadQueueExecutor executor,
        Func<bool> isGameRunning,
        TimeSpan gameExitPollInterval,
        INetworkPolicy? networkPolicy = null,
        InstanceOperationCoordinator? operationCoordinator = null)
        : this(
            storePath,
            executor,
            isGameRunning,
            gameExitPollInterval,
            null,
            networkPolicy,
            operationCoordinator)
    {
    }

    internal DownloadQueueService(
        string storePath,
        IDownloadQueueExecutor executor,
        Func<bool> isGameRunning,
        TimeSpan gameExitPollInterval,
        Func<IReadOnlyList<DownloadQueueGroup>, CancellationToken, Task>? persistOverride,
        INetworkPolicy? networkPolicy = null,
        InstanceOperationCoordinator? operationCoordinator = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storePath);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(isGameRunning);
        if (gameExitPollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(gameExitPollInterval));
        }
        this.storePath = storePath;
        this.executor = executor;
        this.isGameRunning = isGameRunning;
        this.gameExitPollInterval = gameExitPollInterval;
        this.persistOverride = persistOverride;
        this.networkPolicy = networkPolicy;
        this.operationCoordinator = operationCoordinator;
        if (networkPolicy is not null)
        {
            networkPolicy.Changed += NetworkPolicyChanged;
        }
    }

    public IReadOnlyList<DownloadQueueGroup> Groups
    {
        get
        {
            lock (sync)
            {
                return groups.Select(CloneGroup).ToArray();
            }
        }
    }

    public event Action<IReadOnlyList<DownloadQueueGroup>>? QueueChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var notify = false;
        await mutationGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            if (initialized)
            {
                return;
            }

            var loaded = File.Exists(storePath)
                ? await AtomicJsonStore.ReadAsync<DownloadQueueGroup[]>(storePath, cancellationToken)
                    : File.Exists(storePath + ".bak")
                    ? await AtomicJsonStore.ReadAsync<DownloadQueueGroup[]>(storePath + ".bak", cancellationToken)
                    : [];
            ValidateLoadedGroups(loaded);
            var resumable = loaded.Where(NormalizeLoadedGroup).ToArray();
            DownloadQueueGroup[] scheduled;
            lock (sync)
            {
                groups.AddRange(loaded);
                var isOffline = networkPolicy?.IsOffline == true;
                if (!isOffline && resumable.Length > 0)
                {
                    pendingGroups = resumable.Length;
                    idle = NewSource();
                }
                initialized = true;
                dispatcher = DispatchAsync(lifetime.Token);
                if (isOffline)
                {
                    foreach (var group in resumable)
                    {
                        PauseForNetwork(group);
                        networkWaitingGroups.Add(group.Id);
                    }
                    scheduled = [];
                }
                else
                {
                    scheduled = resumable;
                }
            }
            if (loaded.Length > 0)
            {
                await AtomicJsonStore.WriteAsync(
                    storePath,
                    CreatePersistableSnapshot(loaded),
                    cancellationToken);
            }
            foreach (var group in scheduled)
            {
                lock (sync)
                {
                    groupCancellations[group.Id] = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                }
                channel.Writer.TryWrite(group);
            }
            notify = true;
        }
        finally
        {
            mutationGate.Release();
        }
        if (notify)
        {
            NotifyChanged();
        }
    }

    public async Task<DownloadQueueEnqueueResult> EnqueueAsync(
        DownloadQueueGroup group,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);
        if (string.IsNullOrWhiteSpace(group.Id)
            || string.IsNullOrWhiteSpace(group.DeduplicationKey)
            || group.Items.Count == 0)
        {
            throw new ArgumentException(
                "A queue group needs an ID, a deduplication key, and at least one item.",
                nameof(group));
        }
        ValidatePresetGroupStructure(group, persisted: false);

        DownloadQueueEnqueueResult result;
        var notify = false;
        await mutationGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfNotReady();
            var queued = CloneGroup(group);
            lock (sync)
            {
                if (groups.Any(candidate => string.Equals(candidate.Id, group.Id, StringComparison.Ordinal)))
                {
                    throw new ArgumentException($"Download group ID '{group.Id}' already exists.", nameof(group));
                }
                var duplicate = groups.FirstOrDefault(candidate =>
                    candidate.State is DownloadQueueGroupState.Pending
                        or DownloadQueueGroupState.Running
                        or DownloadQueueGroupState.WaitingForNetwork
                    && string.Equals(candidate.DeduplicationKey, group.DeduplicationKey,
                        StringComparison.OrdinalIgnoreCase));
                if (duplicate is not null)
                {
                    result = new(false, CloneGroup(duplicate));
                    return result;
                }
                var waitForNetwork = networkPolicy?.IsOffline == true;
                queued.State = waitForNetwork
                    ? DownloadQueueGroupState.WaitingForNetwork
                    : DownloadQueueGroupState.Pending;
                queued.CompletedAt = null;
                queued.Error = null;
                if (waitForNetwork)
                {
                    PauseForNetwork(queued);
                }
                groups.Add(queued);
                if (waitForNetwork)
                {
                    networkWaitingGroups.Add(queued.Id);
                }
                else
                {
                    groupCancellations[queued.Id] =
                        CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                    if (pendingGroups++ == 0)
                    {
                        idle = NewSource();
                        idleFailure = null;
                    }
                }
            }

            try
            {
                await PersistAsync(cancellationToken);
                if (queued.State != DownloadQueueGroupState.WaitingForNetwork
                    && !channel.Writer.TryWrite(queued))
                {
                    throw new InvalidOperationException("Download queue is closed.");
                }
            }
            catch
            {
                lock (sync)
                {
                    groups.Remove(queued);
                    networkWaitingGroups.Remove(queued.Id);
                    groupCancellations.Remove(queued.Id, out var source);
                    source?.Dispose();
                    if (queued.State != DownloadQueueGroupState.WaitingForNetwork)
                    {
                        CompletePendingGroup();
                    }
                }
                throw;
            }
            result = new(true, CloneGroup(queued));
            notify = true;
        }
        finally
        {
            mutationGate.Release();
        }
        if (notify)
        {
            NotifyChanged();
        }
        return result;
    }

    public async Task CancelAsync(string groupId, CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? source = null;
        var notify = false;
        await mutationGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfNotReady();
            lock (sync)
            {
                var group = FindGroup(groupId);
                if (group.State is DownloadQueueGroupState.Completed or DownloadQueueGroupState.Canceled)
                {
                    return;
                }
                groupCancellations.TryGetValue(group.Id, out source);
                if (source is not null)
                {
                    userCanceledGroups.Add(group.Id);
                }
                CancelGroup(group);
                networkWaitingGroups.Remove(group.Id);
                notify = true;
            }
        }
        finally
        {
            mutationGate.Release();
        }
        Exception? cancellationFailure = null;
        try
        {
            source?.Cancel();
        }
        catch (Exception exception)
        {
            cancellationFailure = exception;
        }
        Exception? persistenceFailure = null;
        if (notify)
        {
            try
            {
                await PersistAsync(CancellationToken.None);
            }
            catch (Exception exception)
            {
                persistenceFailure = exception;
            }
            NotifyChanged();
        }
        if (cancellationFailure is not null && persistenceFailure is not null)
        {
            throw new AggregateException(cancellationFailure, persistenceFailure);
        }
        if (cancellationFailure is not null)
        {
            ExceptionDispatchInfo.Capture(cancellationFailure).Throw();
        }
        if (persistenceFailure is not null)
        {
            ExceptionDispatchInfo.Capture(persistenceFailure).Throw();
        }
    }

    public async Task RetryAsync(string groupId, CancellationToken cancellationToken = default)
    {
        var notify = false;
        await mutationGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfNotReady();
            DownloadQueueGroup group;
            Task? activeTask;
            lock (sync)
            {
                group = FindGroup(groupId);
                if (group.State != DownloadQueueGroupState.Failed)
                {
                    throw new InvalidOperationException("Only failed download groups can be retried.");
                }
                activeGroupTasks.TryGetValue(group.Id, out activeTask);
            }
            if (activeTask is not null)
            {
                await activeTask.WaitAsync(cancellationToken);
            }

            DownloadQueueGroup snapshot;
            CancellationTokenSource? retryCancellation;
            lock (sync)
            {
                group = FindGroup(groupId);
                if (group.State != DownloadQueueGroupState.Failed)
                {
                    throw new InvalidOperationException("Only failed download groups can be retried.");
                }
                snapshot = CloneGroup(group);
                if (group.Kind == DownloadQueueGroupKind.ModDependencyRepair)
                {
                    ResetRepairItems(group);
                    UpdateGroupProgress(group);
                }
                else
                {
                    ResetPresetPreparationIfNoChangesApplied(group);
                    foreach (var item in group.Items.Where(item => item.State is
                        DownloadQueueItemState.Failed or DownloadQueueItemState.Blocked))
                    {
                        item.State = DownloadQueueItemState.Pending;
                        item.Stage = "Pending";
                        item.Error = null;
                        item.RetryCount = 0;
                        item.BytesPerSecond = 0;
                        item.CompletedAt = null;
                    }
                }
                var waitForNetwork = networkPolicy?.IsOffline == true;
                group.State = waitForNetwork
                    ? DownloadQueueGroupState.WaitingForNetwork
                    : DownloadQueueGroupState.Pending;
                group.Stage = waitForNetwork ? "Waiting for network" : "Pending";
                group.Error = null;
                group.CompletedAt = null;
                groupCancellations.Remove(group.Id, out var previous);
                previous?.Dispose();
                if (waitForNetwork)
                {
                    retryCancellation = null;
                    networkWaitingGroups.Add(group.Id);
                }
                else
                {
                    retryCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                    groupCancellations[group.Id] = retryCancellation;
                }
                userCanceledGroups.Remove(group.Id);
                if (!waitForNetwork && pendingGroups++ == 0)
                {
                    idle = NewSource();
                    idleFailure = null;
                }
            }
            try
            {
                await PersistAsync(cancellationToken);
                if (group.State != DownloadQueueGroupState.WaitingForNetwork
                    && !channel.Writer.TryWrite(group))
                {
                    throw new InvalidOperationException("Download queue is closed.");
                }
            }
            catch
            {
                lock (sync)
                {
                    var index = groups.IndexOf(group);
                    if (index >= 0)
                    {
                        groups[index] = snapshot;
                    }
                    if (groupCancellations.TryGetValue(group.Id, out var current)
                        && ReferenceEquals(current, retryCancellation))
                    {
                        groupCancellations.Remove(group.Id);
                    }
                    networkWaitingGroups.Remove(group.Id);
                    if (group.State != DownloadQueueGroupState.WaitingForNetwork)
                    {
                        CompletePendingGroup();
                    }
                }
                retryCancellation?.Dispose();
                throw;
            }
            notify = true;
        }
        finally
        {
            mutationGate.Release();
        }
        if (notify)
        {
            NotifyChanged();
        }
    }

    public Task WaitForIdleAsync(CancellationToken cancellationToken = default)
    {
        lock (sync)
        {
            ThrowIfNotReady();
            return idle.Task.WaitAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (disposeSync)
        {
            return new ValueTask(disposeTask ??= DisposeCoreDeferredAsync());
        }
    }

    private async Task DisposeCoreDeferredAsync()
    {
        await Task.Yield();
        await DisposeCoreAsync();
    }

    private async Task DisposeCoreAsync()
    {
        Task pendingNetworkState;
        lock (sync)
        {
            shuttingDown = true;
        }
        if (networkPolicy is not null)
        {
            networkPolicy.Changed -= NetworkPolicyChanged;
        }
        lock (networkStateSync)
        {
            pendingNetworkState = networkStateTask;
        }
        await pendingNetworkState;

        await mutationGate.WaitAsync();
        Task? dispatchTask;
        try
        {
            lock (sync)
            {
                disposed = true;
                channel.Writer.TryComplete();
                idle.TrySetCanceled();
                dispatchTask = dispatcher;
            }
        }
        finally
        {
            mutationGate.Release();
        }
        Exception? shutdownFailure = null;
        try
        {
            try
            {
                lifetime.Cancel();
            }
            catch (Exception exception)
            {
                shutdownFailure = exception;
            }
            if (dispatchTask is not null)
            {
                await dispatchTask;
            }
            Task[] tasks;
            lock (sync)
            {
                tasks = groupTasks.ToArray();
            }
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception exception)
            {
                shutdownFailure = exception;
            }
            lock (sync)
            {
                shutdownFailure ??= backgroundFailure;
            }
            if (initialized)
            {
                try
                {
                    await PersistAsync(CancellationToken.None);
                }
                catch (Exception exception)
                {
                    shutdownFailure ??= exception;
                }
            }
        }
        finally
        {
            lock (sync)
            {
                foreach (var source in groupCancellations.Values)
                {
                    source.Dispose();
                }
                groupCancellations.Clear();
            }
            lifetime.Dispose();
            networkGate.Dispose();
            persistenceGate.Dispose();
            mutationGate.Dispose();
            foreach (var gate in presetInstanceGates.Values)
            {
                gate.Dispose();
            }
            presetInstanceGates.Clear();
        }
        if (shutdownFailure is not null)
        {
            ExceptionDispatchInfo.Capture(shutdownFailure).Throw();
        }
    }

    private async Task DispatchAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var group in channel.Reader.ReadAllAsync(cancellationToken))
            {
                var task = RunGroupAsync(group, cancellationToken);
                lock (sync)
                {
                    groupTasks.Add(task);
                    activeGroupTasks[group.Id] = task;
                }
                _ = task.ContinueWith(
                    completed =>
                    {
                        lock (sync)
                        {
                            groupTasks.Remove(completed);
                            if (activeGroupTasks.TryGetValue(group.Id, out var active)
                                && ReferenceEquals(active, completed))
                            {
                                activeGroupTasks.Remove(group.Id);
                            }
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task RunGroupAsync(DownloadQueueGroup group, CancellationToken cancellationToken)
    {
        CancellationTokenSource groupCancellation;
        lock (sync)
        {
            groupCancellation = groupCancellations[group.Id];
        }
        var pausedForNetwork = false;
        SemaphoreSlim? presetInstanceGate = null;
        var presetInstanceGateHeld = false;
        try
        {
            if (group.Kind == DownloadQueueGroupKind.ModPresetApply
                && operationCoordinator is not null)
            {
                await operationCoordinator.RunAsync(
                    group.TargetInstanceId,
                    token => ProcessGroupAsync(group, token),
                    groupCancellation.Token);
            }
            else
            {
                if (group.Kind == DownloadQueueGroupKind.ModPresetApply)
                {
                    presetInstanceGate = presetInstanceGates.GetOrAdd(
                        group.TargetInstanceId,
                        static _ => new SemaphoreSlim(1, 1));
                    await presetInstanceGate.WaitAsync(groupCancellation.Token);
                    presetInstanceGateHeld = true;
                }
                await ProcessGroupAsync(group, groupCancellation.Token);
            }
        }
        catch (Exception exception) when (exception is OfflineModeException or OfflineTransitionException)
        {
            lock (sync)
            {
                if (userCanceledGroups.Contains(group.Id))
                {
                    CancelGroup(group);
                }
                else
                {
                    PauseForNetwork(group);
                    pausedForNetwork = true;
                }
            }
            NotifyChanged();
        }
        catch (OperationCanceledException) when (groupCancellation.IsCancellationRequested)
        {
            lock (sync)
            {
                if (userCanceledGroups.Contains(group.Id))
                {
                    CancelGroup(group);
                }
                else if (cancellationToken.IsCancellationRequested)
                {
                    ResetInterruptedGroup(group);
                }
                else
                {
                    CancelGroup(group);
                }
            }
            NotifyChanged();
        }
        catch (Exception exception)
        {
            lock (sync)
            {
                FailGroup(group, exception);
            }
            NotifyChanged();
        }
        finally
        {
            Exception? persistenceFailure = null;
            try
            {
                await PersistAsync(CancellationToken.None);
            }
            catch (Exception exception)
            {
                persistenceFailure = exception;
                throw;
            }
            finally
            {
                CancellationTokenSource? completedCancellation;
                lock (sync)
                {
                    if (groupCancellations.TryGetValue(group.Id, out var current)
                        && ReferenceEquals(current, groupCancellation))
                    {
                        groupCancellations.Remove(group.Id);
                        completedCancellation = current;
                    }
                    else
                    {
                        completedCancellation = null;
                    }
                    userCanceledGroups.Remove(group.Id);
                    CompletePendingGroup(persistenceFailure);
                    if (pausedForNetwork)
                    {
                        networkWaitingGroups.Add(group.Id);
                    }
                }
                completedCancellation?.Dispose();
                if (presetInstanceGateHeld)
                {
                    presetInstanceGate!.Release();
                }
            }
        }
        if (pausedForNetwork && networkPolicy?.IsOffline == false)
        {
            ScheduleNetworkStateUpdate();
        }
    }

    private async Task ProcessGroupAsync(DownloadQueueGroup group, CancellationToken cancellationToken)
    {
        _ = networkPolicy?.GetOnlineCancellationToken();
        Update(group, null, () =>
        {
            group.State = DownloadQueueGroupState.Running;
            group.StartedAt ??= DateTimeOffset.UtcNow;
            group.Stage = "Starting";
        });
        await PersistAsync(cancellationToken);

        for (var index = 0; index < group.Items.Count; index++)
        {
            var item = group.Items[index];
            if (item.State == DownloadQueueItemState.Completed)
            {
                continue;
            }
            var succeeded = await TransferAsync(group, item, cancellationToken);
            if (succeeded)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsUserCanceled(group))
                {
                    return;
                }
                succeeded = await InstallAsync(group, item, cancellationToken);
            }
            if (!succeeded)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsUserCanceled(group))
                {
                    return;
                }
                lock (sync)
                {
                    BlockFollowingItems(group, index + 1, item);
                    group.State = DownloadQueueGroupState.Failed;
                    group.CompletedAt = DateTimeOffset.UtcNow;
                    group.Stage = "Failed";
                    group.Error = item.Error;
                    UpdateGroupProgress(group);
                }
                await PersistAsync(cancellationToken);
                NotifyChanged();
                return;
            }
        }

        lock (sync)
        {
            if (userCanceledGroups.Contains(group.Id)
                || group.State == DownloadQueueGroupState.Canceled)
            {
                return;
            }
            cancellationToken.ThrowIfCancellationRequested();
            group.State = DownloadQueueGroupState.Completed;
            group.CompletedAt = DateTimeOffset.UtcNow;
            group.Stage = "Completed";
            group.Error = null;
            UpdateGroupProgress(group);
        }
        await PersistAsync(cancellationToken);
        NotifyChanged();
    }

    private async Task<bool> TransferAsync(
        DownloadQueueGroup group,
        DownloadQueueItem item,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                _ = networkPolicy?.GetOnlineCancellationToken();
                Update(group, item, () =>
                {
                    item.State = DownloadQueueItemState.Transferring;
                    item.StartedAt ??= DateTimeOffset.UtcNow;
                    item.Stage = item.RetryCount == 0 ? "Downloading" : "Retrying";
                    item.Error = null;
                });
                await PersistAsync(cancellationToken);
                await executor.TransferAsync(group, item,
                    new InlineProgress<PackageTransferProgress>(
                        value => ReportProgress(group, item, value)),
                    networkGate,
                    cancellationToken);
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is OfflineModeException or OfflineTransitionException)
            {
                throw;
            }
            catch (Exception exception)
            {
                if (executor.IsTransient(exception) && item.RetryCount < MaxTransferRetries)
                {
                    Update(group, item, () =>
                    {
                        item.RetryCount++;
                        item.Stage = "Retrying";
                        item.Error = exception.Message;
                    });
                    await PersistAsync(cancellationToken);
                    continue;
                }
                Update(group, item, () => FailItem(item, exception));
                return false;
            }
        }
    }

    private async Task<bool> InstallAsync(
        DownloadQueueGroup group,
        DownloadQueueItem item,
        CancellationToken cancellationToken)
    {
        try
        {
            if (executor.RequiresGameExit(item))
            {
                Update(group, item, () =>
                {
                    item.State = DownloadQueueItemState.WaitingForGameExit;
                    item.Stage = "Waiting for game exit";
                    item.BytesPerSecond = 0;
                });
                await PersistAsync(cancellationToken);
                while (isGameRunning())
                {
                    await Task.Delay(gameExitPollInterval, cancellationToken);
                }
            }
            Update(group, item, () =>
            {
                item.State = DownloadQueueItemState.Installing;
                item.Stage = "Installing";
                item.BytesPerSecond = 0;
            });
            await PersistAsync(cancellationToken);
            if (IsUserCanceled(group))
            {
                return false;
            }
            cancellationToken.ThrowIfCancellationRequested();
            await executor.InstallAsync(group, item, cancellationToken);
            lock (sync)
            {
                if (userCanceledGroups.Contains(group.Id)
                    || group.State == DownloadQueueGroupState.Canceled)
                {
                    return false;
                }
                cancellationToken.ThrowIfCancellationRequested();
                item.State = DownloadQueueItemState.Completed;
                item.CompletedAt = DateTimeOffset.UtcNow;
                item.Stage = "Completed";
                item.BytesPerSecond = 0;
                item.Error = null;
                group.Stage = item.Stage;
                UpdateGroupProgress(group);
            }
            NotifyChanged();
            await PersistAsync(cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Update(group, item, () => FailItem(item, exception));
            return false;
        }
    }

    private bool IsUserCanceled(DownloadQueueGroup group)
    {
        lock (sync)
        {
            return userCanceledGroups.Contains(group.Id)
                || group.State == DownloadQueueGroupState.Canceled;
        }
    }

    private void ReportProgress(
        DownloadQueueGroup group,
        DownloadQueueItem item,
        PackageTransferProgress progress) => Update(group, item, () =>
    {
        item.CompletedBytes = progress.CompletedBytes;
        item.TotalBytes = progress.TotalBytes;
        item.BytesPerSecond = progress.BytesPerSecond;
        item.Stage = progress.Stage;
    });

    private void Update(DownloadQueueGroup group, DownloadQueueItem? item, Action action)
    {
        lock (sync)
        {
            action();
            group.Stage = item?.Stage ?? group.Stage;
            UpdateGroupProgress(group);
        }
        NotifyChanged();
    }

    private async Task PersistAsync(CancellationToken cancellationToken)
    {
        await persistenceGate.WaitAsync(cancellationToken);
        try
        {
            DownloadQueueGroup[] snapshot;
            lock (sync)
            {
                snapshot = CreatePersistableSnapshot(groups);
            }
            if (persistOverride is not null)
            {
                await persistOverride(snapshot, cancellationToken);
            }
            else
            {
                await AtomicJsonStore.WriteAsync(storePath, snapshot, cancellationToken);
            }
        }
        finally
        {
            persistenceGate.Release();
        }
    }

    private static bool NormalizeLoadedGroup(DownloadQueueGroup group)
    {
        if (group.Kind == DownloadQueueGroupKind.ModDependencyRepair
            && group.State is DownloadQueueGroupState.Pending or DownloadQueueGroupState.Running
            && !group.Items.Any(item => item.State == DownloadQueueItemState.Failed))
        {
            ResetRepairItems(group);
            group.State = DownloadQueueGroupState.Pending;
            group.Stage = "Pending";
            group.CompletedAt = null;
            group.Error = null;
            UpdateGroupProgress(group);
            return true;
        }

        ResetPresetPreparationIfNoChangesApplied(group);

        for (var index = 0; index < group.Items.Count; index++)
        {
            var item = group.Items[index];
            if (item.State == DownloadQueueItemState.Failed)
            {
                BlockFollowingItems(group, index + 1, item);
                group.State = DownloadQueueGroupState.Failed;
                group.Stage = "Failed";
                group.Error = item.Error;
                group.CompletedAt ??= DateTimeOffset.UtcNow;
                UpdateGroupProgress(group);
                return false;
            }
            if (!IsTerminal(item.State))
            {
                item.State = DownloadQueueItemState.Pending;
                item.Stage = "Pending";
                item.BytesPerSecond = 0;
                item.Error = null;
            }
        }
        if (group.Items.All(item => item.State == DownloadQueueItemState.Completed))
        {
            group.State = DownloadQueueGroupState.Completed;
            group.Stage = "Completed";
            UpdateGroupProgress(group);
            return false;
        }
        group.State = DownloadQueueGroupState.Pending;
        group.Stage = "Pending";
        group.CompletedAt = null;
        group.Error = null;
        UpdateGroupProgress(group);
        return true;
    }

    private static void ValidateLoadedGroups(IReadOnlyList<DownloadQueueGroup> loaded)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in loaded)
        {
            if (string.IsNullOrWhiteSpace(group.Id) || !ids.Add(group.Id))
            {
                throw new InvalidDataException("The persisted download queue contains an empty or duplicate group ID.");
            }
            ValidatePresetGroupStructure(group, persisted: true);
        }
    }

    private static void ValidatePresetGroupStructure(DownloadQueueGroup group, bool persisted)
    {
        if (group.Kind != DownloadQueueGroupKind.ModPresetApply)
        {
            return;
        }

        var itemIds = new HashSet<string>(StringComparer.Ordinal);
        var packageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var valid = !string.IsNullOrWhiteSpace(group.TargetInstanceId)
            && !string.IsNullOrWhiteSpace(group.ExpectedBuildId)
            && !string.IsNullOrWhiteSpace(group.ExpectedLoaderId)
            && group.Items.Count >= 2
            && group.Items[0].Kind == DownloadQueueItemKind.PresetPrepare
            && group.Items.Count(item => item.Kind == DownloadQueueItemKind.PresetPrepare) == 1
            && group.Items.All(item =>
                !string.IsNullOrWhiteSpace(item.Id)
                && itemIds.Add(item.Id)
                && !string.IsNullOrWhiteSpace(item.PackageId)
                && !string.IsNullOrWhiteSpace(item.LoaderId)
                && string.Equals(item.LoaderId, group.ExpectedLoaderId, StringComparison.OrdinalIgnoreCase))
            && group.Items.Skip(1).All(item =>
                item.Kind is DownloadQueueItemKind.PresetInstall
                    or DownloadQueueItemKind.PresetEnable
                    or DownloadQueueItemKind.PresetDisable
                && packageIds.Add(item.PackageId));
        if (valid)
        {
            return;
        }

        const string message = "A preset queue group must start with exactly one prepare item and contain consistent, unique change items.";
        if (persisted)
        {
            throw new InvalidDataException(message);
        }
        throw new ArgumentException(message, nameof(group));
    }

    private static void ResetInterruptedGroup(DownloadQueueGroup group)
    {
        if (group.Kind == DownloadQueueGroupKind.ModDependencyRepair)
        {
            ResetRepairItems(group);
        }
        else
        {
            ResetPresetPreparationIfNoChangesApplied(group);
            foreach (var item in group.Items.Where(item => !IsTerminal(item.State)))
            {
                item.State = DownloadQueueItemState.Pending;
                item.Stage = "Pending";
                item.BytesPerSecond = 0;
                item.Error = null;
            }
        }
        group.State = DownloadQueueGroupState.Pending;
        group.Stage = "Pending";
        group.Error = null;
        group.CompletedAt = null;
        UpdateGroupProgress(group);
    }

    private static void ResetPresetPreparationIfNoChangesApplied(DownloadQueueGroup group)
    {
        if (group.Kind != DownloadQueueGroupKind.ModPresetApply
            || group.Items.FirstOrDefault()?.Kind != DownloadQueueItemKind.PresetPrepare
            || group.Items[0].State != DownloadQueueItemState.Completed
            || group.Items.Skip(1).Any(item => item.State is
                DownloadQueueItemState.Completed or DownloadQueueItemState.Installing))
        {
            return;
        }
        var prepare = group.Items[0];
        prepare.State = DownloadQueueItemState.Pending;
        prepare.Stage = "Pending";
        prepare.CompletedBytes = 0;
        prepare.BytesPerSecond = 0;
        prepare.Error = null;
        prepare.CompletedAt = null;
    }

    private static void PauseForNetwork(DownloadQueueGroup group)
    {
        ResetInterruptedGroup(group);
        foreach (var item in group.Items.Where(item => !IsTerminal(item.State)))
        {
            item.State = DownloadQueueItemState.WaitingForNetwork;
            item.Stage = "Waiting for network";
            item.BytesPerSecond = 0;
            item.Error = null;
        }
        group.State = DownloadQueueGroupState.WaitingForNetwork;
        group.Stage = "Waiting for network";
        group.Error = null;
        group.CompletedAt = null;
        UpdateGroupProgress(group);
    }

    private void NetworkPolicyChanged(object? sender, NetworkPolicyChangedEventArgs eventArgs) =>
        ScheduleNetworkStateUpdate();

    private void ScheduleNetworkStateUpdate()
    {
        lock (sync)
        {
            if (!initialized || shuttingDown || disposed)
            {
                return;
            }
        }
        lock (networkStateSync)
        {
            networkStateTask = networkStateTask.ContinueWith(
                static (_, state) => ((DownloadQueueService)state!).ApplyNetworkStateAsync(),
                this,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default).Unwrap();
        }
    }

    private async Task ApplyNetworkStateAsync()
    {
        var notify = false;
        var resume = new List<DownloadQueueGroup>();
        try
        {
            await mutationGate.WaitAsync();
            try
            {
                lock (sync)
                {
                    if (shuttingDown || disposed || networkPolicy is null)
                    {
                        return;
                    }

                    if (networkPolicy.IsOffline)
                    {
                        foreach (var group in groups.Where(group =>
                                     group.State == DownloadQueueGroupState.Pending))
                        {
                            PauseForNetwork(group);
                            notify = true;
                        }
                    }
                    else
                    {
                        foreach (var groupId in networkWaitingGroups.ToArray())
                        {
                            var group = FindGroup(groupId);
                            if (group.State != DownloadQueueGroupState.WaitingForNetwork)
                            {
                                networkWaitingGroups.Remove(groupId);
                                continue;
                            }
                            ResetInterruptedGroup(group);
                            groupCancellations[group.Id] =
                                CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                            networkWaitingGroups.Remove(group.Id);
                            if (pendingGroups++ == 0)
                            {
                                idle = NewSource();
                                idleFailure = null;
                            }
                            resume.Add(group);
                            notify = true;
                        }
                    }
                }

                if (notify)
                {
                    foreach (var group in resume)
                    {
                        if (!channel.Writer.TryWrite(group))
                        {
                            throw new InvalidOperationException("Download queue is closed.");
                        }
                    }
                    await PersistAsync(CancellationToken.None);
                }
            }
            finally
            {
                mutationGate.Release();
            }
        }
        catch (Exception exception)
        {
            lock (sync)
            {
                backgroundFailure ??= exception;
            }
        }
        if (notify)
        {
            NotifyChanged();
        }
    }

    private static void ResetRepairItems(DownloadQueueGroup group)
    {
        foreach (var item in group.Items)
        {
            item.State = DownloadQueueItemState.Pending;
            item.Stage = "Pending";
            item.Error = null;
            item.RetryCount = 0;
            item.CompletedBytes = 0;
            item.BytesPerSecond = 0;
            item.StartedAt = null;
            item.CompletedAt = null;
        }
    }

    private static void CancelGroup(DownloadQueueGroup group)
    {
        foreach (var item in group.Items.Where(item => item.State != DownloadQueueItemState.Completed))
        {
            item.State = DownloadQueueItemState.Canceled;
            item.Stage = "Canceled";
            item.BytesPerSecond = 0;
            item.Error = null;
            item.CompletedAt = DateTimeOffset.UtcNow;
        }
        group.State = DownloadQueueGroupState.Canceled;
        group.Stage = "Canceled";
        group.Error = null;
        group.CompletedAt = DateTimeOffset.UtcNow;
        UpdateGroupProgress(group);
    }

    private static void FailGroup(DownloadQueueGroup group, Exception exception)
    {
        for (var index = 0; index < group.Items.Count; index++)
        {
            var item = group.Items[index];
            if (IsTerminal(item.State))
            {
                continue;
            }
            FailItem(item, exception);
            BlockFollowingItems(group, index + 1, item);
            break;
        }
        group.State = DownloadQueueGroupState.Failed;
        group.Stage = "Failed";
        group.Error = exception.Message;
        group.CompletedAt = DateTimeOffset.UtcNow;
        UpdateGroupProgress(group);
    }

    private static void FailItem(DownloadQueueItem item, Exception exception)
    {
        item.State = DownloadQueueItemState.Failed;
        item.Stage = "Failed";
        item.Error = exception.Message;
        item.BytesPerSecond = 0;
        item.CompletedAt = DateTimeOffset.UtcNow;
    }

    private static void BlockFollowingItems(
        DownloadQueueGroup group,
        int startIndex,
        DownloadQueueItem failed)
    {
        for (var index = startIndex; index < group.Items.Count; index++)
        {
            var item = group.Items[index];
            if (item.State == DownloadQueueItemState.Completed)
            {
                continue;
            }
            item.State = DownloadQueueItemState.Blocked;
            item.Stage = "Blocked";
            item.Error = $"Blocked by failed item '{failed.Name}'.";
            item.BytesPerSecond = 0;
            item.CompletedAt = DateTimeOffset.UtcNow;
        }
    }

    private static void UpdateGroupProgress(DownloadQueueGroup group)
    {
        group.CompletedBytes = group.Items.Sum(item => item.CompletedBytes);
        group.TotalBytes = group.Items.Sum(item => item.TotalBytes);
        group.BytesPerSecond = group.Items
            .Where(item => item.State == DownloadQueueItemState.Transferring)
            .Sum(item => item.BytesPerSecond);
    }

    private void CompletePendingGroup(Exception? failure = null)
    {
        idleFailure ??= failure;
        backgroundFailure ??= failure;
        if (--pendingGroups == 0)
        {
            if (idleFailure is null)
            {
                idle.TrySetResult();
            }
            else
            {
                idle.TrySetException(idleFailure);
            }
        }
    }

    private static bool IsTerminal(DownloadQueueGroup group) =>
        group.Items.Count > 0 && group.Items.All(item => IsTerminal(item.State));

    private static bool IsTerminal(DownloadQueueItemState state) => state is
        DownloadQueueItemState.Completed or DownloadQueueItemState.Failed
        or DownloadQueueItemState.Blocked or DownloadQueueItemState.Canceled;

    private DownloadQueueGroup FindGroup(string groupId) =>
        groups.FirstOrDefault(group => string.Equals(group.Id, groupId, StringComparison.Ordinal))
        ?? throw new KeyNotFoundException($"Download group '{groupId}' was not found.");

    private void NotifyChanged()
    {
        var snapshot = Groups;
        foreach (var handler in QueueChanged?.GetInvocationList() ?? [])
        {
            try
            {
                ((Action<IReadOnlyList<DownloadQueueGroup>>)handler)(snapshot);
            }
            catch
            {
                // Queue state must not depend on UI or logging subscribers.
            }
        }
    }

    private static DownloadQueueGroup CloneGroup(DownloadQueueGroup group) => group with
    {
        Items = group.Items.Select(item => item with { }).ToArray()
    };

    private static DownloadQueueGroup[] CreatePersistableSnapshot(
        IEnumerable<DownloadQueueGroup> source) => source
        .Where(group => group.State is not (
            DownloadQueueGroupState.Completed or DownloadQueueGroupState.Canceled))
        .Select(CloneGroup)
        .ToArray();

    private void ThrowIfNotReady()
    {
        ThrowIfDisposed();
        if (!initialized)
        {
            throw new InvalidOperationException("Download queue is not initialized.");
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

    private static TaskCompletionSource CompletedSource()
    {
        var source = NewSource();
        source.SetResult();
        return source;
    }

    private static TaskCompletionSource NewSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}

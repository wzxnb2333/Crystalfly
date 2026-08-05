using System.Collections.Concurrent;
using Crystalfly.App.Downloads;
using Crystalfly.App.ViewModels;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Packages;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class DownloadCenterViewModelTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(), "Crystalfly.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Retry_all_retries_every_failed_group_and_updates_can_execute()
    {
        var executor = new FakeQueueExecutor
        {
            TransferFailure = (_, attempt) => attempt == 1
                ? new InvalidDataException("bad package")
                : null
        };
        await using var queue = CreateQueue(executor);
        var center = CreateCenter(queue);
        await queue.InitializeAsync();
        await center.EnqueueAsync(Group("fail-one", "One"));
        await center.EnqueueAsync(Group("fail-two", "Two"));
        await queue.WaitForIdleAsync();

        Assert.Equal(2, queue.Groups.Count(group => group.State == DownloadQueueGroupState.Failed));
        Assert.True(center.CanRetryAll);
        Assert.True(center.RetryAllCommand.CanExecute(null));

        await center.RetryAllCommand.ExecuteAsync(null);
        await queue.WaitForIdleAsync();

        Assert.All(queue.Groups, group => Assert.Equal(DownloadQueueGroupState.Completed, group.State));
        Assert.False(center.CanRetryAll);
        Assert.False(center.RetryAllCommand.CanExecute(null));
    }

    [Fact]
    public async Task Clear_completed_removes_completed_groups_and_keeps_unfinished()
    {
        var executor = new FakeQueueExecutor();
        await using var queue = CreateQueue(executor);
        var center = CreateCenter(queue);
        await queue.InitializeAsync();
        await center.EnqueueAsync(Group("done", "Done"));
        await queue.WaitForIdleAsync();
        Assert.Equal(DownloadQueueGroupState.Completed, Assert.Single(queue.Groups).State);
        Assert.True(center.CanClearCompleted);

        await center.ClearCompletedCommand.ExecuteAsync(null);

        Assert.Empty(queue.Groups);
        Assert.False(center.CanClearCompleted);
        Assert.False(center.ClearCompletedCommand.CanExecute(null));
        Assert.Empty(await Crystalfly.Core.Serialization.AtomicJsonStore.ReadAsync<DownloadQueueGroup[]>(
            Path.Combine(root, "download-queue.json")));
    }

    [Fact]
    public async Task Cancel_all_cancels_every_unfinished_group()
    {
        var executor = new FakeQueueExecutor();
        executor.Blocked.Add("cancel-me");
        await using var queue = CreateQueue(executor);
        var center = CreateCenter(queue);
        await queue.InitializeAsync();
        await center.EnqueueAsync(Group("cancel-me", "Cancel me"));
        await WaitUntilAsync(() => queue.Groups.Single().State == DownloadQueueGroupState.Running);
        Assert.True(center.CanCancelAll);

        await center.CancelAllCommand.ExecuteAsync(null);

        Assert.Equal(DownloadQueueGroupState.Canceled, Assert.Single(queue.Groups).State);
        Assert.False(center.CanCancelAll);
    }

    [Fact]
    public async Task Pause_all_and_resume_all_toggle_steam_groups()
    {
        var executor = new FakeQueueExecutor();
        executor.Blocked.Add("steam-one");
        await using var queue = CreateQueue(executor);
        var center = CreateCenter(queue);
        await queue.InitializeAsync();
        await center.EnqueueAsync(SteamGroup("steam-one"));
        await WaitUntilAsync(() => queue.Groups.Single().State == DownloadQueueGroupState.Running);
        Assert.True(center.CanPauseAll);
        Assert.False(center.CanResumeAll);

        await center.PauseAllCommand.ExecuteAsync(null);

        var paused = Assert.Single(queue.Groups);
        Assert.Equal(DownloadQueueGroupState.Pending, paused.State);
        Assert.Equal("Waiting for Steam login", paused.Stage);
        Assert.False(center.CanPauseAll);
        Assert.True(center.CanResumeAll);

        await center.ResumeAllCommand.ExecuteAsync(null);
        await queue.WaitForIdleAsync();

        Assert.Equal(DownloadQueueGroupState.Completed, Assert.Single(queue.Groups).State);
        Assert.False(center.CanResumeAll);
    }

    private DownloadQueueService CreateQueue(IDownloadQueueExecutor executor) => new(
        Path.Combine(root, "download-queue.json"),
        executor,
        static () => false,
        TimeSpan.FromMilliseconds(10));

    private DownloadCenterViewModel CreateCenter(
        DownloadQueueService queue,
        List<string>? toasts = null)
    {
        var localization = new LocalizationViewModel();
        localization.Apply(UiLanguage.English);
        return new DownloadCenterViewModel(new DownloadCenterDependencies(
            queue,
            localization,
            toasts is null ? null : toasts.Add,
            ErrorReported: null,
            IsBusy: static () => false,
            GetVersionRoot: static () => Path.GetTempPath(),
            GetSelectedInstanceId: static () => null,
            RestoreSelectedInstance: static _ => { },
            RefreshAsync: static () => Task.CompletedTask,
            LifetimeCancellation: CancellationToken.None));
    }

    private DownloadQueueGroup Group(string id, string name) => new()
    {
        Id = id,
        DeduplicationKey = $"instance:{id}",
        Kind = DownloadQueueGroupKind.ModInstall,
        Name = name,
        TargetInstanceId = "instance",
        TargetInstanceName = "Instance",
        TargetInstanceRoot = "C:\\game",
        CreatedAt = DateTimeOffset.UtcNow,
        Items =
        [
            new DownloadQueueItem
            {
                Id = $"{id}:item",
                Kind = DownloadQueueItemKind.Mod,
                PackageId = id,
                Name = name,
                State = DownloadQueueItemState.Pending
            }
        ]
    };

    private DownloadQueueGroup SteamGroup(string id) => SteamDownloadQueueGroupFactory.Create(
        "public",
        "Steam public",
        null,
        Path.Combine(root, "versions"),
        id) with { Id = id };

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class FakeQueueExecutor : IDownloadQueueExecutor
    {
        private readonly ConcurrentDictionary<string, int> attempts = new(StringComparer.Ordinal);
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public HashSet<string> Blocked { get; } = new(StringComparer.Ordinal);

        public Func<string, int, Exception?>? TransferFailure { get; init; }

        public bool RequiresGameExit(DownloadQueueItem item) => false;

        public bool IsTransient(Exception exception) => false;

        public async Task TransferAsync(
            DownloadQueueGroup group,
            DownloadQueueItem item,
            IProgress<PackageTransferProgress> progress,
            SemaphoreSlim networkGate,
            CancellationToken cancellationToken)
        {
            var attempt = attempts.AddOrUpdate(group.Id, 1, static (_, value) => value + 1);
            await networkGate.WaitAsync(cancellationToken);
            try
            {
                if (attempt == 1 && Blocked.Contains(group.Id))
                {
                    await release.Task.WaitAsync(cancellationToken);
                }
                if (TransferFailure?.Invoke(group.Id, attempt) is { } exception)
                {
                    throw exception;
                }
            }
            finally
            {
                networkGate.Release();
            }
        }

        public Task InstallAsync(
            DownloadQueueGroup group,
            DownloadQueueItem item,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

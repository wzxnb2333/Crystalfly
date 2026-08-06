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
    public void Group_and_item_eta_text_use_remaining_bytes_and_hide_without_speed()
    {
        var localization = new LocalizationViewModel();
        localization.Apply(UiLanguage.English);
        var group = new DownloadQueueGroup
        {
            Id = "eta",
            Name = "ETA",
            State = DownloadQueueGroupState.Running,
            Stage = "Downloading",
            CompletedBytes = 250,
            TotalBytes = 1000,
            BytesPerSecond = 50,
            Items =
            [
                new DownloadQueueItem
                {
                    Id = "eta-item",
                    Name = "Item",
                    State = DownloadQueueItemState.Transferring,
                    Stage = "Downloading",
                    CompletedBytes = 250,
                    TotalBytes = 1000,
                    BytesPerSecond = 50
                }
            ]
        };
        var viewModel = new DownloadQueueGroupItemViewModel(group, localization);

        Assert.Equal("00:00:15", viewModel.EtaText);
        Assert.Equal("00:00:15", viewModel.Items[0].EtaText);

        viewModel.Update(group with
        {
            BytesPerSecond = 0,
            Items = [group.Items[0] with { BytesPerSecond = 0 }]
        }, localization);

        Assert.Equal(string.Empty, viewModel.EtaText);
        Assert.Equal(string.Empty, viewModel.Items[0].EtaText);
    }

    [Fact]
    public async Task Overview_totals_sum_active_speed_eta_and_count()
    {
        var executor = new FakeQueueExecutor();
        executor.Blocked.Add("active-a");
        executor.Blocked.Add("active-b");
        executor.ProgressFor = id => id switch
        {
            "active-a" => new PackageTransferProgress(0, 3000, 100, "Downloading"),
            "active-b" => new PackageTransferProgress(500, 2000, 50, "Downloading"),
            _ => new PackageTransferProgress(0, 0, 0, "Downloading")
        };
        await using var queue = CreateQueue(executor);
        var center = CreateCenter(queue);
        await queue.InitializeAsync();
        await center.EnqueueAsync(Group("active-a", "Active A"));
        await center.EnqueueAsync(Group("active-b", "Active B"));
        await center.EnqueueAsync(Group("done", "Done"));
        await WaitUntilAsync(() => queue.Groups.Count(group =>
                group.State == DownloadQueueGroupState.Running) == 2
            && queue.Groups.Single(group => group.Id == "done").State
            == DownloadQueueGroupState.Completed);
        await WaitUntilAsync(() => queue.Groups
            .Where(group => group.Id != "done")
            .Sum(group => group.BytesPerSecond) > 0);

        Assert.Equal("150.0 B/s", center.TotalSpeedText);
        Assert.Equal("00:00:30", center.OverallEtaText);
        Assert.Equal("2 active downloads", center.ActiveCountText);
    }

    [Fact]
    public async Task Session_enqueued_group_completion_raises_toast_but_restored_groups_are_silent()
    {
        var executor = new FakeQueueExecutor();
        await using var queue = CreateQueue(executor);
        var toasts = new List<string>();
        var center = CreateCenter(queue, toasts);
        await queue.InitializeAsync();

        var sessionGroup = Group("session", "Session");
        await center.EnqueueAsync(sessionGroup);
        center.QueueDownloadQueueProjection([sessionGroup]);
        center.ApplyPendingDownloadQueueProjection();
        Assert.Empty(toasts);

        center.QueueDownloadQueueProjection(
            [sessionGroup with { State = DownloadQueueGroupState.Completed }]);
        center.ApplyPendingDownloadQueueProjection();

        Assert.Equal("Session finished downloading", Assert.Single(toasts));

        var restored = Group("restored", "Restored");
        center.QueueDownloadQueueProjection(
            [restored with { State = DownloadQueueGroupState.Running }]);
        center.ApplyPendingDownloadQueueProjection();
        center.QueueDownloadQueueProjection(
            [restored with { State = DownloadQueueGroupState.Completed }]);
        center.ApplyPendingDownloadQueueProjection();

        Assert.Single(toasts);
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
        executor.Blocked.Remove("steam-one");

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

    private DownloadQueueService CreateQueue(IDownloadQueueExecutor executor, string? storeRoot = null) => new(
        Path.Combine(storeRoot ?? root, "download-queue.json"),
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

        public Func<string, PackageTransferProgress>? ProgressFor { get; set; }

        public Func<string, int, Exception?>? TransferFailure { get; set; }

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
                if (ProgressFor?.Invoke(group.Id) is { } reported)
                {
                    progress.Report(reported);
                }
                if (Blocked.Contains(group.Id))
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

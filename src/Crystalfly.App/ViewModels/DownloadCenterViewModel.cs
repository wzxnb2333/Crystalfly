using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.App.Downloads;

namespace Crystalfly.App.ViewModels;

public sealed partial class DownloadCenterViewModel : ViewModelBase
{
    private readonly DownloadQueueService downloadQueue;
    private readonly Action<string>? toastRequested;
    private readonly Action<string>? errorReported;
    private readonly Func<bool> isBusy;
    private readonly Func<string> getVersionRoot;
    private readonly Func<string?> getSelectedInstanceId;
    private readonly Action<string?> restoreSelectedInstance;
    private readonly Func<Task> refreshAsync;
    private readonly CancellationToken lifetimeCancellation;
    private readonly object downloadQueueProjectionSync = new();
    private readonly HashSet<string> refreshedTerminalQueueGroups = new(StringComparer.Ordinal);
    private IReadOnlyList<DownloadQueueGroup>? pendingDownloadQueueSnapshot;
    private int downloadQueueProjectionScheduled;
    private int queueRefreshRequested;
    private int queueRefreshScheduled;
    private LocalizationViewModel loc;

    public DownloadCenterViewModel(DownloadCenterDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        downloadQueue = dependencies.DownloadQueue;
        loc = dependencies.Loc;
        toastRequested = dependencies.ToastRequested;
        errorReported = dependencies.ErrorReported;
        isBusy = dependencies.IsBusy;
        getVersionRoot = dependencies.GetVersionRoot;
        getSelectedInstanceId = dependencies.GetSelectedInstanceId;
        restoreSelectedInstance = dependencies.RestoreSelectedInstance;
        refreshAsync = dependencies.RefreshAsync;
        lifetimeCancellation = dependencies.LifetimeCancellation;
        downloadQueue.QueueChanged += OnDownloadQueueChanged;
    }

    public ObservableCollection<DownloadQueueGroupItemViewModel> DownloadQueueGroups { get; } = [];

    public int ActiveDownloadCount => DownloadQueueGroups.Count(group => group.State is
        DownloadQueueGroupState.Pending
            or DownloadQueueGroupState.Running
            or DownloadQueueGroupState.WaitingForNetwork);

    public bool HasActiveDownloads => ActiveDownloadCount > 0;

    public bool HasUnfinishedDownloads => DownloadQueueGroups.Any(group =>
        group.State is DownloadQueueGroupState.Pending
            or DownloadQueueGroupState.Running
            or DownloadQueueGroupState.WaitingForNetwork
            or DownloadQueueGroupState.Failed);

    public string ActiveDownloadSummary => DownloadQueueGroups.FirstOrDefault(group => group.State is
        DownloadQueueGroupState.Pending
            or DownloadQueueGroupState.Running
            or DownloadQueueGroupState.WaitingForNetwork) is { } group
        ? $"{group.Name} · {group.StageText}"
        : string.Empty;

    internal DownloadQueueService DownloadQueue => downloadQueue;

    internal void ApplyLanguage(LocalizationViewModel localization)
    {
        ArgumentNullException.ThrowIfNull(localization);
        loc = localization;
    }

    internal async Task<DownloadQueueEnqueueResult> EnqueueAsync(
        DownloadQueueGroup group,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);
        return await downloadQueue.EnqueueAsync(group, cancellationToken);
    }

    internal void Dispose() => downloadQueue.QueueChanged -= OnDownloadQueueChanged;

    private void OnDownloadQueueChanged(IReadOnlyList<DownloadQueueGroup> groups) =>
        QueueDownloadQueueProjection(groups);

    internal void QueueDownloadQueueProjection(IReadOnlyList<DownloadQueueGroup> groups)
    {
        var scheduleProjection = false;
        var scheduleRefresh = false;
        lock (downloadQueueProjectionSync)
        {
            foreach (var group in groups.Where(group => group.State is
                         DownloadQueueGroupState.Pending
                             or DownloadQueueGroupState.Running
                             or DownloadQueueGroupState.WaitingForNetwork))
            {
                refreshedTerminalQueueGroups.Remove(group.Id);
            }
            foreach (var group in groups.Where(group => group.State is
                         DownloadQueueGroupState.Completed
                             or DownloadQueueGroupState.Failed
                             or DownloadQueueGroupState.Canceled))
            {
                scheduleRefresh |= refreshedTerminalQueueGroups.Add(group.Id);
            }
            pendingDownloadQueueSnapshot = groups;
            if (downloadQueueProjectionScheduled == 0)
            {
                downloadQueueProjectionScheduled = 1;
                scheduleProjection = true;
            }
        }
        if (scheduleProjection)
        {
            Dispatcher.UIThread.Post(ApplyPendingDownloadQueueProjection);
        }
        if (scheduleRefresh)
        {
            ScheduleRefreshAfterQueueMutation();
        }
    }

    internal void ApplyPendingDownloadQueueProjection()
    {
        IReadOnlyList<DownloadQueueGroup>? snapshot;
        lock (downloadQueueProjectionSync)
        {
            snapshot = pendingDownloadQueueSnapshot;
            pendingDownloadQueueSnapshot = null;
            downloadQueueProjectionScheduled = 0;
        }
        if (snapshot is null || lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }

        var existing = DownloadQueueGroups.ToDictionary(group => group.Id, StringComparer.Ordinal);
        var ordered = new List<DownloadQueueGroupItemViewModel>(snapshot.Count);
        foreach (var group in snapshot.OrderByDescending(group => group.CreatedAt))
        {
            if (!existing.TryGetValue(group.Id, out var viewModel))
            {
                viewModel = new DownloadQueueGroupItemViewModel(group, loc);
            }
            else
            {
                viewModel.Update(group, loc);
            }
            ordered.Add(viewModel);
        }
        for (var index = DownloadQueueGroups.Count - 1; index >= 0; index--)
        {
            if (ordered.All(group => group.Id != DownloadQueueGroups[index].Id))
            {
                DownloadQueueGroups.RemoveAt(index);
            }
        }
        for (var index = 0; index < ordered.Count; index++)
        {
            var current = DownloadQueueGroups.IndexOf(ordered[index]);
            if (current < 0)
            {
                DownloadQueueGroups.Insert(index, ordered[index]);
            }
            else if (current != index)
            {
                DownloadQueueGroups.Move(current, index);
            }
        }
        NotifyDownloadQueueProperties();
    }

    private void ScheduleRefreshAfterQueueMutation()
    {
        Interlocked.Exchange(ref queueRefreshRequested, 1);
        if (Interlocked.Exchange(ref queueRefreshScheduled, 1) != 0)
        {
            return;
        }
        Dispatcher.UIThread.Post(() => _ = RefreshAfterQueueMutationAsync());
    }

    private async Task RefreshAfterQueueMutationAsync()
    {
        try
        {
            do
            {
                Interlocked.Exchange(ref queueRefreshRequested, 0);
                while (isBusy() && !lifetimeCancellation.IsCancellationRequested)
                {
                    await Task.Delay(100, lifetimeCancellation);
                }
                if (!Directory.Exists(getVersionRoot()) || lifetimeCancellation.IsCancellationRequested)
                {
                    return;
                }
                var selectedInstanceId = getSelectedInstanceId();
                await refreshAsync();
                if (selectedInstanceId is not null)
                {
                    restoreSelectedInstance(selectedInstanceId);
                }
            } while (Volatile.Read(ref queueRefreshRequested) != 0);
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            Interlocked.Exchange(ref queueRefreshScheduled, 0);
            if (Volatile.Read(ref queueRefreshRequested) != 0)
            {
                ScheduleRefreshAfterQueueMutation();
            }
        }
    }

    private void NotifyDownloadQueueProperties()
    {
        OnPropertyChanged(nameof(ActiveDownloadCount));
        OnPropertyChanged(nameof(HasActiveDownloads));
        OnPropertyChanged(nameof(HasUnfinishedDownloads));
        OnPropertyChanged(nameof(ActiveDownloadSummary));
    }

    [RelayCommand]
    private async Task CancelQueuedDownloadAsync(string? groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return;
        }
        try
        {
            await downloadQueue.CancelAsync(groupId, lifetimeCancellation);
        }
        catch (Exception exception) when (exception is IOException
            or InvalidOperationException
            or KeyNotFoundException
            or AggregateException)
        {
            errorReported?.Invoke($"{loc["OperationFailed"]}: {exception.Message}");
        }
    }

    [RelayCommand]
    private async Task RetryQueuedDownloadAsync(string? groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return;
        }
        try
        {
            await downloadQueue.RetryAsync(groupId, lifetimeCancellation);
        }
        catch (Exception exception) when (exception is IOException
            or InvalidOperationException
            or KeyNotFoundException)
        {
            errorReported?.Invoke($"{loc["OperationFailed"]}: {exception.Message}");
        }
    }
}

using System.Collections.ObjectModel;
using System.Globalization;
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
    private readonly HashSet<string> sessionEnqueuedGroupIds = new(StringComparer.Ordinal);
    private Dictionary<string, DownloadQueueGroup> lastAppliedGroups = new(StringComparer.Ordinal);
    private int projectionEpoch;
    private int lastAppliedProjectionEpoch;
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

    public string TotalSpeedText => QueueDisplayText.Speed(ActiveQueueGroups.Sum(group => group.BytesPerSecond));

    public string OverallEtaText => QueueDisplayText.Eta(
        ActiveQueueGroups.Sum(group => group.BytesPerSecond),
        ActiveQueueGroups.Sum(group => group.CompletedBytes),
        ActiveQueueGroups.Sum(group => group.TotalBytes));

    public string ActiveCountText => string.Format(
        CultureInfo.CurrentCulture,
        loc["ActiveDownloads"],
        ActiveQueueGroups.Count());

    internal DownloadQueueService DownloadQueue => downloadQueue;

    private static int StatePriority(DownloadQueueGroupState state) => state switch
    {
        DownloadQueueGroupState.Pending or DownloadQueueGroupState.Running
            or DownloadQueueGroupState.WaitingForNetwork => 0,
        DownloadQueueGroupState.Failed => 1,
        _ => 2
    };

    internal static DownloadErrorCategory ClassifyError(string? error, DownloadQueueGroupState state)
    {
        if (state == DownloadQueueGroupState.WaitingForNetwork)
        {
            return DownloadErrorCategory.Offline;
        }
        if (string.IsNullOrWhiteSpace(error))
        {
            return DownloadErrorCategory.Other;
        }
        if (error.Contains("offline", StringComparison.OrdinalIgnoreCase))
        {
            return DownloadErrorCategory.Offline;
        }
        if (error.Contains("SHA-256", StringComparison.OrdinalIgnoreCase)
            || error.Contains("hash", StringComparison.OrdinalIgnoreCase))
        {
            return DownloadErrorCategory.Verification;
        }
        if (error.Contains("HTTP", StringComparison.OrdinalIgnoreCase))
        {
            return DownloadErrorCategory.Network;
        }
        if (error.Contains("access", StringComparison.OrdinalIgnoreCase)
            || error.Contains("permission", StringComparison.OrdinalIgnoreCase))
        {
            return DownloadErrorCategory.Permission;
        }
        return DownloadErrorCategory.Other;
    }

    private IEnumerable<DownloadQueueGroup> ActiveQueueGroups => downloadQueue.Groups.Where(group =>
        group.State is DownloadQueueGroupState.Pending
            or DownloadQueueGroupState.Running
            or DownloadQueueGroupState.WaitingForNetwork);

    internal void ApplyLanguage(LocalizationViewModel localization)
    {
        ArgumentNullException.ThrowIfNull(localization);
        loc = localization;
        lock (downloadQueueProjectionSync)
        {
            // Localized text is derived from the same group data, so a language
            // change must force the projection to re-render existing groups.
            projectionEpoch++;
        }
    }

    internal async Task<DownloadQueueEnqueueResult> EnqueueAsync(
        DownloadQueueGroup group,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);
        var result = await downloadQueue.EnqueueAsync(group, cancellationToken);
        if (result.Added)
        {
            sessionEnqueuedGroupIds.Add(group.Id);
        }
        return result;
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
            if (SnapshotMatchesLastApplied(groups))
            {
                return;
            }
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
        Dictionary<string, DownloadQueueGroup> lastApplied;
        var forceReRender = false;
        lock (downloadQueueProjectionSync)
        {
            snapshot = pendingDownloadQueueSnapshot;
            pendingDownloadQueueSnapshot = null;
            downloadQueueProjectionScheduled = 0;
            if (snapshot is null || lifetimeCancellation.IsCancellationRequested)
            {
                return;
            }
            lastApplied = lastAppliedGroups;
            // A projection epoch bump (language change) must re-render every
            // group even when the underlying data is unchanged.
            forceReRender = projectionEpoch != lastAppliedProjectionEpoch;
            if (!forceReRender && GroupsMatchLastApplied(snapshot, lastApplied))
            {
                return;
            }
        }

        ApplyProjection(snapshot, lastApplied, forceReRender);

        lock (downloadQueueProjectionSync)
        {
            lastAppliedGroups = BuildLastApplied(snapshot);
            lastAppliedProjectionEpoch = projectionEpoch;
        }
        NotifyDownloadQueueProperties();
    }

    private void ApplyProjection(
        IReadOnlyList<DownloadQueueGroup> snapshot,
        Dictionary<string, DownloadQueueGroup> lastApplied,
        bool forceReRender)
    {
        var snapshotIds = new HashSet<string>(snapshot.Count, StringComparer.Ordinal);
        foreach (var group in snapshot)
        {
            snapshotIds.Add(group.Id);
        }

        var ordered = snapshot
            .OrderBy(group => StatePriority(group.State))
            .ThenByDescending(group => group.CreatedAt)
            .ToArray();

        var existing = new Dictionary<string, DownloadQueueGroupItemViewModel>(
            DownloadQueueGroups.Count,
            StringComparer.Ordinal);
        foreach (var viewModel in DownloadQueueGroups)
        {
            existing[viewModel.Id] = viewModel;
        }

        foreach (var group in ordered)
        {
            if (group.State == DownloadQueueGroupState.Completed
                && sessionEnqueuedGroupIds.Contains(group.Id)
                && lastApplied.TryGetValue(group.Id, out var previous)
                && previous.State is DownloadQueueGroupState.Pending or DownloadQueueGroupState.Running)
            {
                toastRequested?.Invoke(string.Format(
                    CultureInfo.CurrentCulture,
                    loc["QueueCompletedFormat"],
                    group.Name));
            }
            if (!existing.TryGetValue(group.Id, out var viewModel))
            {
                viewModel = new DownloadQueueGroupItemViewModel(group, loc);
                existing[group.Id] = viewModel;
                DownloadQueueGroups.Add(viewModel);
            }
            else if (forceReRender
                || !lastApplied.TryGetValue(group.Id, out var lastSeen)
                || !ProjectedGroupsEqual(lastSeen, group))
            {
                viewModel.Update(group, loc);
            }
        }

        for (var index = DownloadQueueGroups.Count - 1; index >= 0; index--)
        {
            if (!snapshotIds.Contains(DownloadQueueGroups[index].Id))
            {
                DownloadQueueGroups.RemoveAt(index);
            }
        }

        var orderedViewModels = new List<DownloadQueueGroupItemViewModel>(ordered.Length);
        foreach (var group in ordered)
        {
            orderedViewModels.Add(existing[group.Id]);
        }
        var reorderNeeded = false;
        for (var index = 0; index < orderedViewModels.Count; index++)
        {
            if (!ReferenceEquals(DownloadQueueGroups[index], orderedViewModels[index]))
            {
                reorderNeeded = true;
                break;
            }
        }
        if (reorderNeeded)
        {
            for (var index = 0; index < orderedViewModels.Count; index++)
            {
                if (ReferenceEquals(DownloadQueueGroups[index], orderedViewModels[index]))
                {
                    continue;
                }
                var current = DownloadQueueGroups.IndexOf(orderedViewModels[index]);
                DownloadQueueGroups.Move(current, index);
            }
        }
    }

    private bool SnapshotMatchesLastApplied(IReadOnlyList<DownloadQueueGroup> snapshot) =>
        projectionEpoch == lastAppliedProjectionEpoch
        && GroupsMatchLastApplied(snapshot, lastAppliedGroups);

    private static bool GroupsMatchLastApplied(
        IReadOnlyList<DownloadQueueGroup> snapshot,
        Dictionary<string, DownloadQueueGroup> lastApplied)
    {
        if (snapshot.Count != lastApplied.Count)
        {
            return false;
        }
        foreach (var group in snapshot)
        {
            if (!lastApplied.TryGetValue(group.Id, out var previous)
                || !ProjectedGroupsEqual(previous, group))
            {
                return false;
            }
        }
        return true;
    }

    private static Dictionary<string, DownloadQueueGroup> BuildLastApplied(
        IReadOnlyList<DownloadQueueGroup> snapshot)
    {
        var lastApplied = new Dictionary<string, DownloadQueueGroup>(snapshot.Count, StringComparer.Ordinal);
        foreach (var group in snapshot)
        {
            lastApplied[group.Id] = group;
        }
        return lastApplied;
    }

    private static bool ProjectedGroupsEqual(DownloadQueueGroup first, DownloadQueueGroup second)
    {
        if (ReferenceEquals(first, second))
        {
            return true;
        }
        return first.Name == second.Name
            && first.TargetInstanceName == second.TargetInstanceName
            && first.State == second.State
            && first.Stage == second.Stage
            && first.Error == second.Error
            && first.CompletedBytes == second.CompletedBytes
            && first.TotalBytes == second.TotalBytes
            && first.BytesPerSecond == second.BytesPerSecond
            && first.CreatedAt == second.CreatedAt
            && first.StartedAt == second.StartedAt
            && first.CompletedAt == second.CompletedAt
            && ProjectedItemsEqual(first.Items, second.Items);
    }

    private static bool ProjectedItemsEqual(
        IReadOnlyList<DownloadQueueItem> first,
        IReadOnlyList<DownloadQueueItem> second)
    {
        if (ReferenceEquals(first, second))
        {
            return true;
        }
        if (first.Count != second.Count)
        {
            return false;
        }
        for (var index = 0; index < first.Count; index++)
        {
            var firstItem = first[index];
            var secondItem = second[index];
            if (firstItem.Name != secondItem.Name
                || firstItem.Version != secondItem.Version
                || firstItem.State != secondItem.State
                || firstItem.Stage != secondItem.Stage
                || firstItem.Error != secondItem.Error
                || firstItem.RetryCount != secondItem.RetryCount
                || firstItem.CompletedBytes != secondItem.CompletedBytes
                || firstItem.TotalBytes != secondItem.TotalBytes
                || firstItem.BytesPerSecond != secondItem.BytesPerSecond
                || firstItem.StartedAt != secondItem.StartedAt
                || firstItem.CompletedAt != secondItem.CompletedAt)
            {
                return false;
            }
        }
        return true;
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
        OnPropertyChanged(nameof(CanRetryAll));
        OnPropertyChanged(nameof(CanPauseAll));
        OnPropertyChanged(nameof(CanResumeAll));
        OnPropertyChanged(nameof(CanCancelAll));
        OnPropertyChanged(nameof(CanClearCompleted));
        OnPropertyChanged(nameof(TotalSpeedText));
        OnPropertyChanged(nameof(OverallEtaText));
        OnPropertyChanged(nameof(ActiveCountText));
    }

    public bool CanRetryAll => downloadQueue.Groups.Any(group =>
        group.State == DownloadQueueGroupState.Failed);

    public bool CanPauseAll => downloadQueue.Groups.Any(group =>
        group.State is DownloadQueueGroupState.Pending or DownloadQueueGroupState.Running
        && group.Stage != "Waiting for Steam login"
        && group.Items.Any(SteamDownloadQueueGroupFactory.IsSteamItem));

    public bool CanResumeAll => downloadQueue.Groups.Any(group =>
        group.State == DownloadQueueGroupState.Pending
        && group.Stage == "Waiting for Steam login");

    public bool CanCancelAll => downloadQueue.Groups.Any(group =>
        group.State is not (DownloadQueueGroupState.Completed or DownloadQueueGroupState.Canceled));

    public bool CanClearCompleted => downloadQueue.Groups.Any(group =>
        group.State == DownloadQueueGroupState.Completed);

    [RelayCommand(CanExecute = nameof(CanRetryAll))]
    private async Task RetryAllAsync()
    {
        var failures = 0;
        string? firstFailure = null;
        foreach (var group in downloadQueue.Groups.Where(group =>
                     group.State == DownloadQueueGroupState.Failed))
        {
            try
            {
                await downloadQueue.RetryAsync(group.Id, lifetimeCancellation);
            }
            catch (Exception exception) when (exception is IOException
                or InvalidOperationException
                or KeyNotFoundException)
            {
                if (failures++ == 0)
                {
                    firstFailure = exception.Message;
                }
            }
        }
        if (failures > 0)
        {
            errorReported?.Invoke($"{loc["OperationFailed"]}: {firstFailure}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanPauseAll))]
    private async Task PauseAllAsync()
    {
        try
        {
            await downloadQueue.PauseSteamDownloadsAsync(lifetimeCancellation);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            errorReported?.Invoke($"{loc["OperationFailed"]}: {exception.Message}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanResumeAll))]
    private async Task ResumeAllAsync()
    {
        try
        {
            await downloadQueue.ResumeSteamDownloadsAsync(lifetimeCancellation);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            errorReported?.Invoke($"{loc["OperationFailed"]}: {exception.Message}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancelAll))]
    private async Task CancelAllAsync()
    {
        var failures = 0;
        string? firstFailure = null;
        foreach (var group in downloadQueue.Groups.Where(group =>
                     group.State is not (DownloadQueueGroupState.Completed or DownloadQueueGroupState.Canceled)))
        {
            try
            {
                await downloadQueue.CancelAsync(group.Id, lifetimeCancellation);
            }
            catch (Exception exception) when (exception is IOException
                or InvalidOperationException
                or KeyNotFoundException
                or AggregateException)
            {
                if (failures++ == 0)
                {
                    firstFailure = exception.Message;
                }
            }
        }
        if (failures > 0)
        {
            errorReported?.Invoke($"{loc["OperationFailed"]}: {firstFailure}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanClearCompleted))]
    private async Task ClearCompletedAsync()
    {
        try
        {
            await downloadQueue.ClearCompletedAsync(lifetimeCancellation);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            errorReported?.Invoke($"{loc["OperationFailed"]}: {exception.Message}");
        }
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

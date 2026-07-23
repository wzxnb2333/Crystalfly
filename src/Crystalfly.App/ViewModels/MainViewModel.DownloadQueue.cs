using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.App.Downloads;
using Crystalfly.Core.Mods;
using Crystalfly.Core.Runtime;
using Crystalfly.Steam.Downloads;

namespace Crystalfly.App.ViewModels;

public partial class MainViewModel
{
    private readonly InstanceOperationCoordinator instanceOperationCoordinator = new();
    private readonly DownloadQueueService downloadQueue;
    private readonly object downloadQueueProjectionSync = new();
    private readonly HashSet<string> refreshedTerminalQueueGroups = new(StringComparer.Ordinal);
    private IReadOnlyList<DownloadQueueGroup>? pendingDownloadQueueSnapshot;
    private int downloadQueueProjectionScheduled;
    private int queueRefreshRequested;
    private int queueRefreshScheduled;

    public ObservableCollection<DownloadQueueGroupItemViewModel> DownloadQueueGroups { get; } = [];

    public bool IsDownloadQueueSection => CurrentDownloadSection == "DownloadQueue";

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

    private DownloadQueueService CreateDownloadQueue()
    {
        var packageExecutor = new CatalogPackageQueueExecutor(
            () => catalog,
            packageHttpClient,
            instanceOperationCoordinator,
            isGameProcessRunning,
            networkPolicy: networkPolicy);
        var executor = new SteamDownloadQueueExecutor(
            packageExecutor,
            async (request, report, cancellationToken) =>
            {
                var session = steamSession;
                if (!IsSteamSessionLoggedOn() || session is null)
                {
                    throw new InvalidOperationException("Sign in to Steam before this queued download can continue.");
                }
                using var content = new SteamKitContentDeliveryClient(session.Client);
                return await new SteamDepotDownloadService(content, report)
                    .DownloadAsync(request, cancellationToken);
            },
            manifestId => catalog.Builds.FirstOrDefault(build =>
                string.Equals(
                    build.ManifestId,
                    manifestId.ToString(CultureInfo.InvariantCulture),
                    StringComparison.Ordinal))?.Id,
            IsSteamSessionLoggedOn,
            operationCoordinator: instanceOperationCoordinator,
            networkPolicy: networkPolicy);
        return new DownloadQueueService(
            Path.Combine(paths.ApplicationDataRoot, "download-queue.json"),
            executor,
            isGameProcessRunning,
            TimeSpan.FromMilliseconds(500),
            networkPolicy,
            instanceOperationCoordinator);
    }

    private async Task InitializeDownloadQueueAsync()
    {
        await downloadQueue.InitializeAsync(lifetimeCancellation.Token);
        QueueDownloadQueueProjection(downloadQueue.Groups);
    }

    private void OnDownloadQueueChanged(IReadOnlyList<DownloadQueueGroup> groups) =>
        QueueDownloadQueueProjection(groups);

    private void QueueDownloadQueueProjection(IReadOnlyList<DownloadQueueGroup> groups)
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

    private void ApplyPendingDownloadQueueProjection()
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
                viewModel = new DownloadQueueGroupItemViewModel(group, Loc);
            }
            else
            {
                viewModel.Update(group, Loc);
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
                while (IsBusy && !lifetimeCancellation.IsCancellationRequested)
                {
                    await Task.Delay(100, lifetimeCancellation.Token);
                }
                if (!Directory.Exists(VersionRoot) || lifetimeCancellation.IsCancellationRequested)
                {
                    return;
                }
                var selectedInstanceId = SelectedInstance?.Id;
                await RefreshAsync();
                if (selectedInstanceId is not null)
                {
                    SelectedInstance = Instances.FirstOrDefault(instance => instance.Id == selectedInstanceId)
                        ?? SelectedInstance;
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

    internal async Task<ModInstallPlan> CreateSelectedMarketInstallPlanAsync(
        MarketInstallTargetViewModel target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        var mod = SelectedMarketMod
            ?? throw new InvalidOperationException(Loc["SelectMod"]);
        return await CreateModInstallService(target.Instance.Record)
            .CreatePlanAsync(mod.Id, cancellationToken);
    }

    private async Task EnqueueSelectedMarketModAsync()
    {
        if (SelectedMarketMod is null
            || SelectedMarketInstallTarget is null
            || !SelectedMarketInstallTarget.IsAvailable)
        {
            ErrorMessage = Loc["NoInstallTargets"];
            return;
        }

        ErrorMessage = null;
        try
        {
            await downloadQueue.InitializeAsync(lifetimeCancellation.Token);
            var target = SelectedMarketInstallTarget;
            var plan = await CreateSelectedMarketInstallPlanAsync(target, lifetimeCancellation.Token);
            var group = ModInstallQueueGroupFactory.Create(plan, catalog, target.Instance.Record);
            var result = await downloadQueue.EnqueueAsync(group, lifetimeCancellation.Token);
            ToastRequested?.Invoke(result.Added
                ? Loc["AddedToDownloadQueue"]
                : Loc["QueueTaskAlreadyExists"]);
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or InvalidOperationException
            or UnauthorizedAccessException
            or HttpRequestException
            or KeyNotFoundException
            or ArgumentException
            or System.Text.Json.JsonException)
        {
            ErrorMessage = $"{Loc["OperationFailed"]}: {exception.Message}";
        }
    }


    private async Task EnqueueModDependencyRepairAsync(ModDependencyRepairPlan plan)
    {
        if (SelectedInstance is null)
        {
            ErrorMessage = Loc["NoInstance"];
            return;
        }

        ErrorMessage = null;
        try
        {
            await downloadQueue.InitializeAsync(lifetimeCancellation.Token);
            var group = ModDependencyRepairQueueGroupFactory.Create(plan, catalog, SelectedInstance.Record);
            var result = await downloadQueue.EnqueueAsync(group, lifetimeCancellation.Token);
            ToastRequested?.Invoke(result.Added
                ? Loc["AddedToDownloadQueue"]
                : Loc["QueueTaskAlreadyExists"]);
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or InvalidOperationException
            or UnauthorizedAccessException
            or HttpRequestException
            or KeyNotFoundException
            or ArgumentException
            or System.Text.Json.JsonException)
        {
            ErrorMessage = $"{Loc["OperationFailed"]}: {exception.Message}";
        }
    }
    private async Task EnqueueSteamBuildAsync()
    {
        if (!IsSteamSessionLoggedOn() || SelectedDownloadBuild is null)
        {
            ErrorMessage = "Sign in to Steam and select a build first.";
            return;
        }
        if (!Directory.Exists(VersionRoot))
        {
            ErrorMessage = Loc["ChooseRoot"];
            return;
        }

        var selected = SelectedDownloadBuild;
        await downloadQueue.InitializeAsync(lifetimeCancellation.Token);
        var instanceName = UniqueInstanceName(selected.DisplayName);
        var group = SteamDownloadQueueGroupFactory.Create(
            selected.BuildId,
            selected.DisplayName,
            selected.ManifestId,
            VersionRoot,
            instanceName);
        var result = await downloadQueue.EnqueueAsync(group, lifetimeCancellation.Token);
        DownloadStatus = result.Added
            ? Loc["AddedToDownloadQueue"]
            : Loc["QueueTaskAlreadyExists"];
        ToastRequested?.Invoke(DownloadStatus);
    }

    [RelayCommand]
    private void OpenDownloadQueue()
    {
        CurrentPage = "Downloads";
        CurrentDownloadSection = "DownloadQueue";
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
            await downloadQueue.CancelAsync(groupId, lifetimeCancellation.Token);
        }
        catch (Exception exception) when (exception is IOException
            or InvalidOperationException
            or KeyNotFoundException
            or AggregateException)
        {
            ErrorMessage = $"{Loc["OperationFailed"]}: {exception.Message}";
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
            await downloadQueue.RetryAsync(groupId, lifetimeCancellation.Token);
        }
        catch (Exception exception) when (exception is IOException
            or InvalidOperationException
            or KeyNotFoundException)
        {
            ErrorMessage = $"{Loc["OperationFailed"]}: {exception.Message}";
        }
    }
}

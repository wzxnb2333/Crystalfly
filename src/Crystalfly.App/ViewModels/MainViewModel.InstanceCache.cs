using CommunityToolkit.Mvvm.ComponentModel;
using Crystalfly.Core.Loaders;
using Crystalfly.Core.Models;
using Crystalfly.Core.Thunderstore;

namespace Crystalfly.App.ViewModels;

public partial class MainViewModel
{
    private sealed record InstanceScanInspection(LoaderInspection LoaderInspection,
        InstalledPackageReceipt? LoaderReceipt, ThunderstoreInstallState? ThunderstoreState);

    // Presentation only: launching and mutating an instance still inspect the current files.
    private readonly Dictionary<string, IReadOnlyList<(InstanceRecord Record, LoaderState LoaderState,
        InstalledPackageReceipt? LoaderReceipt, int ModCount)>> instanceProjectionCache = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? gameSwitchRefreshCancellation;
    private Task gameSwitchRefreshTask = Task.CompletedTask;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInstanceInspectionPending))]
    [NotifyPropertyChangedFor(nameof(LaunchReadinessTitle))]
    [NotifyPropertyChangedFor(nameof(LaunchReadinessHint))]
    public partial bool IsRefreshingGameInstances { get; private set; }

    public bool IsInstanceInspectionPending => IsRefreshingGameInstances || IsLoadingInstanceDetails;

    private string InstanceProjectionCacheKey => $"{SelectedGameId}:{VersionRoot}";

    private void RememberInstanceProjection()
    {
        if (lastInstanceProjection is not null)
            instanceProjectionCache[InstanceProjectionCacheKey] = lastInstanceProjection
                .Where(item => Instances.Instances.Any(current => current.Record == item.Record)).ToArray();
    }

    private void RestoreInstanceProjection()
    {
        instanceProjectionCache.TryGetValue(InstanceProjectionCacheKey, out var cached);
        ApplyInstanceProjection(cached ?? [], deferLoading: true);
        // A cached list cannot authorize a launch before the fresh inspection completes.
        LaunchPreflight = new(false, false, false, false);
    }

    private void RefreshGameInstancesInBackground()
    {
        gameSwitchRefreshCancellation?.Cancel();
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetimeCancellation.Token);
        gameSwitchRefreshCancellation = cancellation;
        IsRefreshingGameInstances = true;
        gameSwitchRefreshTask = RefreshAfterPreviousAsync(gameSwitchRefreshTask, cancellation);
    }

    private async Task RefreshAfterPreviousAsync(Task previous, CancellationTokenSource cancellation)
    {
        try
        {
            await previous;
            cancellation.Token.ThrowIfCancellationRequested();
            await RefreshInstancesAsync(showBusy: false, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(gameSwitchRefreshCancellation, cancellation))
            {
                gameSwitchRefreshCancellation = null;
                IsRefreshingGameInstances = false;
            }
            cancellation.Dispose();
        }
    }

    internal Task WaitForGameInstanceRefreshAsync() => gameSwitchRefreshTask;
}

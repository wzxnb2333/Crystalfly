using Crystalfly.App.Downloads;

namespace Crystalfly.App.ViewModels;

public sealed record DownloadCenterDependencies(
    DownloadQueueService DownloadQueue,
    LocalizationViewModel Loc,
    Action<string>? ToastRequested,
    Action<string>? ErrorReported,
    Func<bool> IsBusy,
    Func<string> GetVersionRoot,
    Func<string?> GetSelectedInstanceId,
    Action<string?> RestoreSelectedInstance,
    Func<Task> RefreshAsync,
    CancellationToken LifetimeCancellation);

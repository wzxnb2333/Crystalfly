using Crystalfly.Core.Packages;

namespace Crystalfly.App.Downloads;

public interface IDownloadQueueExecutor
{
    bool RequiresGameExit(DownloadQueueItem item);

    bool IsTransient(Exception exception);

    Task TransferAsync(
        DownloadQueueGroup group,
        DownloadQueueItem item,
        IProgress<PackageTransferProgress> progress,
        SemaphoreSlim networkGate,
        CancellationToken cancellationToken);

    Task InstallAsync(
        DownloadQueueGroup group,
        DownloadQueueItem item,
        CancellationToken cancellationToken);
}

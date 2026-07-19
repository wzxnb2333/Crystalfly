using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;
using Crystalfly.Core.Packages;
using Crystalfly.Core.Serialization;
using Crystalfly.Steam.Downloads;

namespace Crystalfly.App.Downloads;

public delegate Task<SteamDownloadResult> SteamQueueDownloadAsync(
    SteamDownloadRequest request,
    Action<SteamDownloadProgress> progress,
    CancellationToken cancellationToken);

public sealed class SteamDownloadQueueExecutor(
    IDownloadQueueExecutor fallback,
    SteamQueueDownloadAsync download,
    Func<ulong, string?> resolveBuildId,
    Func<bool>? isLoggedOn = null,
    TimeSpan? loginPollInterval = null,
    InstanceOperationCoordinator? operationCoordinator = null) : IDownloadQueueExecutor
{
    private readonly SemaphoreSlim steamDownloadGate = new(1, 1);
    private readonly Func<bool> isLoggedOn = isLoggedOn ?? (static () => true);
    private readonly TimeSpan loginPollInterval = loginPollInterval ?? TimeSpan.FromMilliseconds(500);
    private readonly InstanceOperationCoordinator operationCoordinator = operationCoordinator ?? new();

    public bool RequiresGameExit(DownloadQueueItem item) =>
        SteamDownloadQueueGroupFactory.IsSteamItem(item)
            ? false
            : fallback.RequiresGameExit(item);

    public bool IsTransient(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is TimeoutException or TaskCanceledException or SocketException
            || exception is HttpRequestException requestException
                && (requestException.StatusCode is null
                    or HttpStatusCode.RequestTimeout
                    or HttpStatusCode.TooManyRequests
                    || (int)requestException.StatusCode is >= 500 and <= 599)
            || fallback.IsTransient(exception);
    }

    public async Task TransferAsync(
        DownloadQueueGroup group,
        DownloadQueueItem item,
        IProgress<PackageTransferProgress> progress,
        SemaphoreSlim networkGate,
        CancellationToken cancellationToken)
    {
        if (!SteamDownloadQueueGroupFactory.IsSteamItem(item))
        {
            await fallback.TransferAsync(group, item, progress, networkGate, cancellationToken);
            return;
        }

        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(networkGate);
        ulong? requestedManifest = ParseManifestId(item.PackagePath);
        string staging = SteamDownloadQueueGroupFactory.GetStagingDirectory(group);
        while (!isLoggedOn())
        {
            progress.Report(new PackageTransferProgress(0, 0, 0, "Waiting for Steam login"));
            await Task.Delay(loginPollInterval, cancellationToken);
        }
        var steamGateTaken = false;
        Exception? failure = null;
        try
        {
            await steamDownloadGate.WaitAsync(cancellationToken);
            steamGateTaken = true;
            while (!isLoggedOn())
            {
                progress.Report(new PackageTransferProgress(0, 0, 0, "Waiting for Steam login"));
                await Task.Delay(loginPollInterval, cancellationToken);
            }
            DeleteDirectory(staging);
            SteamDownloadResult result;
            await networkGate.WaitAsync(cancellationToken);
            try
            {
                result = await download(
                    new SteamDownloadRequest(staging, requestedManifest),
                    value => progress.Report(new PackageTransferProgress(
                        value.CompletedBytes,
                        value.TotalBytes,
                        value.BytesPerSecond,
                        value.CurrentFile)),
                    cancellationToken);
            }
            finally
            {
                networkGate.Release();
            }
            ValidateResult(result, staging, requestedManifest);
            await AtomicJsonStore.WriteAsync(
                Path.Combine(staging, InstanceDirectory.PendingDownloadMarkerFileName),
                new SteamQueueTransferState
                {
                    ManifestId = result.ManifestId,
                    InstanceId = group.TargetInstanceId
                },
                cancellationToken);
        }
        catch (Exception exception)
        {
            failure = exception;
            throw;
        }
        finally
        {
            if (failure is not null)
            {
                TryDeleteDirectory(staging, failure, "Crystalfly.SteamStagingCleanupError");
            }
            if (steamGateTaken)
            {
                steamDownloadGate.Release();
            }
        }
    }

    public async Task InstallAsync(
        DownloadQueueGroup group,
        DownloadQueueItem item,
        CancellationToken cancellationToken)
    {
        if (!SteamDownloadQueueGroupFactory.IsSteamItem(item))
        {
            await fallback.InstallAsync(group, item, cancellationToken);
            return;
        }

        await operationCoordinator.RunAsync(
            group.TargetInstanceId,
            token => PublishAsync(group, item, token),
            cancellationToken);
    }

    private async Task PublishAsync(
        DownloadQueueGroup group,
        DownloadQueueItem item,
        CancellationToken cancellationToken)
    {
        string staging = SteamDownloadQueueGroupFactory.GetStagingDirectory(group);
        string destination = Path.GetFullPath(group.TargetInstanceRoot);
        var moved = false;
        Exception? failure = null;
        try
        {
            string statePath = Path.Combine(staging, InstanceDirectory.PendingDownloadMarkerFileName);
            SteamQueueTransferState state = await ReadTransferStateAsync(
                statePath, group, item, cancellationToken);
            string buildId = ResolveBuildId(item, state.ManifestId);
            if (File.Exists(destination))
            {
                throw new IOException($"Instance destination already exists: {destination}");
            }
            if (Directory.Exists(destination))
            {
                await RecoverExistingDestinationAsync(
                    group, item, state, buildId, staging, destination, cancellationToken);
                return;
            }

            Directory.Move(staging, destination);
            moved = true;
            await InstanceSidecar.SaveAsync(CreateInstanceRecord(group, destination, buildId), cancellationToken);
            File.Delete(Path.Combine(destination, InstanceDirectory.PendingDownloadMarkerFileName));
        }
        catch (Exception exception)
        {
            failure = exception;
            throw;
        }
        finally
        {
            if (failure is not null)
            {
                if (!moved)
                {
                    TryDeleteDirectory(staging, failure, "Crystalfly.SteamPublishCleanupError");
                }
            }
        }
    }

    private async Task RecoverExistingDestinationAsync(
        DownloadQueueGroup group,
        DownloadQueueItem item,
        SteamQueueTransferState stagedState,
        string buildId,
        string staging,
        string destination,
        CancellationToken cancellationToken)
    {
        if (File.Exists(InstanceSidecar.GetMarkerPath(destination)))
        {
            InstanceRecord existing = await InstanceSidecar.LoadAsync(destination, cancellationToken);
            if (!IsMatchingInstance(existing, group, destination, buildId))
            {
                throw new InvalidDataException("Existing Steam instance target does not match the queue request.");
            }
            File.Delete(Path.Combine(destination, InstanceDirectory.PendingDownloadMarkerFileName));
            DeleteDirectory(staging);
            return;
        }

        string destinationStatePath = Path.Combine(
            destination,
            InstanceDirectory.PendingDownloadMarkerFileName);
        if (!File.Exists(destinationStatePath))
        {
            throw new IOException($"Instance destination already exists: {destination}");
        }
        SteamQueueTransferState destinationState = await ReadTransferStateAsync(
            destinationStatePath, group, item, cancellationToken);
        if (destinationState.ManifestId != stagedState.ManifestId)
        {
            throw new InvalidDataException("Existing Steam destination manifest does not match the new download.");
        }

        await InstanceSidecar.SaveAsync(CreateInstanceRecord(group, destination, buildId), cancellationToken);
        File.Delete(destinationStatePath);
        DeleteDirectory(staging);
    }

    private static InstanceRecord CreateInstanceRecord(
        DownloadQueueGroup group,
        string destination,
        string buildId) => new()
    {
        Id = group.TargetInstanceId,
        Name = group.TargetInstanceName,
        RootPath = destination,
        BuildId = buildId,
        ProvisioningMode = InstanceProvisioningMode.Downloaded,
        CreatedAt = group.CreatedAt
    };

    private static bool IsMatchingInstance(
        InstanceRecord instance,
        DownloadQueueGroup group,
        string destination,
        string buildId) =>
        string.Equals(instance.Id, group.TargetInstanceId, StringComparison.Ordinal)
        && string.Equals(instance.Name, group.TargetInstanceName, StringComparison.Ordinal)
        && string.Equals(instance.BuildId, buildId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(
            Path.GetFullPath(instance.RootPath),
            destination,
            StringComparison.OrdinalIgnoreCase)
        && instance.ProvisioningMode == InstanceProvisioningMode.Downloaded;

    private static async Task<SteamQueueTransferState> ReadTransferStateAsync(
        string statePath,
        DownloadQueueGroup group,
        DownloadQueueItem item,
        CancellationToken cancellationToken)
    {
        SteamQueueTransferState state = await AtomicJsonStore.ReadAsync<SteamQueueTransferState>(
            statePath,
            cancellationToken);
        if (!string.Equals(state.InstanceId, group.TargetInstanceId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Downloaded Steam target does not match the queue request.");
        }
        ulong? requestedManifest = ParseManifestId(item.PackagePath);
        if (requestedManifest is not null && requestedManifest != state.ManifestId)
        {
            throw new InvalidDataException("Downloaded Steam manifest does not match the queue request.");
        }
        return state;
    }

    private string ResolveBuildId(DownloadQueueItem item, ulong manifestId)
    {
        string requestedBuildId = item.PackageId[SteamDownloadQueueGroupFactory.PackagePrefix.Length..];
        string? resolvedBuildId = resolveBuildId(manifestId);
        if (string.Equals(requestedBuildId, "public", StringComparison.Ordinal))
        {
            return resolvedBuildId
                ?? $"steam-public-{manifestId.ToString(CultureInfo.InvariantCulture)}";
        }
        if (string.IsNullOrWhiteSpace(requestedBuildId)
            || !string.Equals(requestedBuildId, resolvedBuildId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Requested build '{requestedBuildId}' does not match Steam manifest {manifestId}.");
        }
        return requestedBuildId;
    }

    private static ulong? ParseManifestId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        return ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong manifest)
            ? manifest
            : throw new InvalidDataException("Steam download item contains an invalid manifest ID.");
    }

    private static void ValidateResult(
        SteamDownloadResult result,
        string staging,
        ulong? requestedManifest)
    {
        if (result.AppId != SteamProduct.HollowKnightAppId
            || result.DepotId != SteamProduct.HollowKnightWindowsDepotId
            || !string.Equals(
                Path.GetFullPath(result.StagingDirectory),
                Path.GetFullPath(staging),
                StringComparison.OrdinalIgnoreCase)
            || requestedManifest is not null && requestedManifest != result.ManifestId
            || !Directory.Exists(staging))
        {
            throw new InvalidDataException("Steam downloader returned a result that does not match the queue request.");
        }
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void TryDeleteDirectory(string path, Exception failure, string dataKey)
    {
        try
        {
            DeleteDirectory(path);
        }
        catch (Exception cleanupException)
        {
            failure.Data[dataKey] = cleanupException;
        }
    }

    private sealed record SteamQueueTransferState
    {
        public required ulong ManifestId { get; init; }

        public required string InstanceId { get; init; }
    }
}

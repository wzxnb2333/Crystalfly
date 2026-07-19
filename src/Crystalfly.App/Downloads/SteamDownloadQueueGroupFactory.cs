using System.Globalization;
using Crystalfly.Core.Instances;

namespace Crystalfly.App.Downloads;

public static class SteamDownloadQueueGroupFactory
{
    internal const string PackagePrefix = "steam:";
    internal const string LoaderId = "steam-depot";

    public static DownloadQueueGroup Create(
        string buildId,
        string displayName,
        ulong? manifestId,
        string versionRoot,
        string instanceName,
        DateTimeOffset? createdAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(buildId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(versionRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);

        string fullVersionRoot = Path.GetFullPath(versionRoot);
        string targetRoot = InstanceDirectory.ResolveUnderRoot(fullVersionRoot, instanceName);
        string id = Guid.NewGuid().ToString("N");
        string manifest = manifestId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        return new DownloadQueueGroup
        {
            Id = id,
            DeduplicationKey = $"steam-instance:{targetRoot}",
            Kind = DownloadQueueGroupKind.AssetInstall,
            Name = displayName,
            TargetInstanceId = Guid.NewGuid().ToString("N"),
            TargetInstanceName = instanceName,
            TargetInstanceRoot = targetRoot,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            State = DownloadQueueGroupState.Pending,
            Stage = "Pending",
            Items =
            [
                new DownloadQueueItem
                {
                    Id = $"{id}:game",
                    Kind = DownloadQueueItemKind.Asset,
                    PackageId = PackagePrefix + buildId,
                    Name = displayName,
                    Version = displayName,
                    LoaderId = LoaderId,
                    PackagePath = manifest,
                    State = DownloadQueueItemState.Pending,
                    Stage = "Pending"
                }
            ]
        };
    }

    public static string GetStagingDirectory(DownloadQueueGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        if (!Guid.TryParseExact(group.Id, "N", out _))
        {
            throw new InvalidDataException("Steam download group ID must be a GUID in N format.");
        }
        string targetRoot = Path.GetFullPath(group.TargetInstanceRoot);
        string versionRoot = Directory.GetParent(targetRoot)?.FullName
            ?? throw new InvalidDataException("Steam download target must have a version root.");
        if (!string.Equals(
                InstanceDirectory.ResolveUnderRoot(versionRoot, group.TargetInstanceName),
                targetRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Steam download target does not match its instance name.");
        }
        return Path.Combine(versionRoot, ".crystalfly", "downloads", $"steam-{group.Id}");
    }

    internal static bool IsSteamItem(DownloadQueueItem item) =>
        item.Kind == DownloadQueueItemKind.Asset
        && string.Equals(item.LoaderId, LoaderId, StringComparison.Ordinal)
        && item.PackageId.StartsWith(PackagePrefix, StringComparison.Ordinal);
}

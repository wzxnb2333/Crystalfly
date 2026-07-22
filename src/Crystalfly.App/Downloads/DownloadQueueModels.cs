using System.Text.Json.Serialization;

namespace Crystalfly.App.Downloads;

public enum DownloadQueueGroupKind
{
    ModInstall = 0,
    LoaderInstall = 1,
    AssetInstall = 2,
    ModDependencyRepair = 3
}

public enum DownloadQueueGroupState
{
    Pending,
    Running,
    Completed,
    Failed,
    Canceled,
    WaitingForNetwork
}

public enum DownloadQueueItemKind
{
    Loader = 0,
    Dependency = 1,
    Mod = 2,
    Asset = 3,
    DependencyReEnable = 4
}

public enum DownloadQueueItemState
{
    Pending,
    Transferring,
    WaitingForGameExit,
    Installing,
    Completed,
    Failed,
    Blocked,
    Canceled,
    WaitingForNetwork
}

public sealed record DownloadQueueGroup
{
    public string Id { get; init; } = string.Empty;

    public string DeduplicationKey { get; init; } = string.Empty;

    public DownloadQueueGroupKind Kind { get; init; }

    public string Name { get; init; } = string.Empty;

    public string TargetInstanceId { get; init; } = string.Empty;

    public string TargetInstanceName { get; init; } = string.Empty;

    public string TargetInstanceRoot { get; init; } = string.Empty;

    public string ExpectedBuildId { get; init; } = string.Empty;

    public string ExpectedLoaderId { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DownloadQueueGroupState State { get; set; } = DownloadQueueGroupState.Pending;

    public long CompletedBytes { get; set; }

    public long TotalBytes { get; set; }

    public double BytesPerSecond { get; set; }

    public string Stage { get; set; } = string.Empty;

    public string? Error { get; set; }

    public IReadOnlyList<DownloadQueueItem> Items { get; init; } = [];

    [JsonIgnore]
    public double Progress => TotalBytes > 0
        ? Math.Clamp((double)CompletedBytes / TotalBytes, 0, 1)
        : State == DownloadQueueGroupState.Completed ? 1 : 0;
}

public sealed record DownloadQueueItem
{
    public string Id { get; init; } = string.Empty;

    public DownloadQueueItemKind Kind { get; init; }

    public string PackageId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string LoaderId { get; init; } = string.Empty;

    public string? DownloadUrl { get; init; }

    public long? SizeBytes { get; init; }

    public string? Sha256 { get; init; }

    public string? PackagePath { get; init; }

    public bool IsSatisfied { get; init; }

    public DownloadQueueItemState State { get; set; } = DownloadQueueItemState.Pending;

    public long CompletedBytes { get; set; }

    public long TotalBytes { get; set; }

    public double BytesPerSecond { get; set; }

    public string Stage { get; set; } = string.Empty;

    public int RetryCount { get; set; }

    public string? Error { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    [JsonIgnore]
    public double Progress => TotalBytes > 0
        ? Math.Clamp((double)CompletedBytes / TotalBytes, 0, 1)
        : State == DownloadQueueItemState.Completed ? 1 : 0;
}

public sealed record DownloadQueueEnqueueResult(bool Added, DownloadQueueGroup Group);

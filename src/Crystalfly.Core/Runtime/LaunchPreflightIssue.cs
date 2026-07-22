namespace Crystalfly.Core.Runtime;

public enum LaunchIssueSeverity
{
    Blocking,
    Forceable,
    Warning
}

public enum LaunchIssueCode
{
    ExecutableMissing,
    GameAlreadyRunning,
    LoaderConflict,
    LoaderDrifted,
    UnsupportedBuildLoaderCombination,
    TransactionUnhealthy,
    LocalLowNotReady,
    MissingDependency,
    DisabledDependency,
    ModCriticalFileMissing,
    ModModifiedFile,
    ModExtraFile,
    UnmanagedExternalMod,
    ModHealthIndeterminate
}

public sealed record LaunchPreflightIssue
{
    public required LaunchIssueCode Code { get; init; }

    public required LaunchIssueSeverity Severity { get; init; }

    public string? SubjectModId { get; init; }

    public string? RelativeFilePath { get; init; }

    public string? CurrentFileSha256 { get; init; }

    public IReadOnlyList<string> Arguments { get; init; } = [];

    public bool IsAcknowledged { get; init; }
}

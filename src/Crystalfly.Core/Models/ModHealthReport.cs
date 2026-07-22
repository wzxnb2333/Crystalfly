namespace Crystalfly.Core.Models;

public enum ModHealthStatus
{
    Healthy,
    CriticalFileMissing,
    ModifiedFile,
    ExtraFile,
    UnmanagedExternal,
    Indeterminate
}

public sealed record ModHealthReport
{
    public required string ModId { get; init; }

    public required ModHealthStatus Status { get; init; }

    public IReadOnlyList<string> MissingFiles { get; init; } = [];

    public IReadOnlyList<string> ModifiedFiles { get; init; } = [];

    public IReadOnlyList<string> ExtraFiles { get; init; } = [];

    public IReadOnlyDictionary<string, string> CurrentFileSha256ByPath { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public string? Detail { get; init; }
}

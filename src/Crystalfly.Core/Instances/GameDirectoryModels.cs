using Crystalfly.Core.Configuration;

namespace Crystalfly.Core.Instances;

public sealed record GameDirectoryCandidate
{
    public required string Path { get; init; }

    public required string DisplayName { get; init; }

    public GameDirectorySourceKind Source { get; init; } = GameDirectorySourceKind.Custom;
}

public sealed record GameDirectoryScanResult
{
    public required string RootPath { get; init; }

    public IReadOnlyList<GameDirectoryCandidate> Candidates { get; init; } = [];

    public IReadOnlyList<string> SkippedPaths { get; init; } = [];
}

public sealed record GameDirectoryIntegrityReport
{
    public required string RootPath { get; init; }

    public bool DirectoryExists { get; init; }

    public bool IsAccessible { get; init; } = true;

    public bool IsReparsePoint { get; init; }

    public bool HasPendingDownload { get; init; }

    public bool UnityPlayerExists { get; init; }

    public IReadOnlyList<string> MissingRequiredFiles { get; init; } = [];

    public bool IsValid => DirectoryExists
        && IsAccessible
        && !IsReparsePoint
        && !HasPendingDownload
        && MissingRequiredFiles.Count == 0;
}

public sealed record GameDirectoryMigrationResult(
    string SourcePath,
    string DestinationPath,
    bool SourceCleanupCompleted,
    string? SourceCleanupError);

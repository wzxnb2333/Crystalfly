namespace Crystalfly.Core.Models;

public sealed record SpeedrunVerificationReport
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string Id { get; init; }

    public required string InstanceId { get; init; }

    public required string TemplateId { get; init; }

    public SpeedrunTemplateSource TemplateSource { get; init; }

    public required string TemplateRulesRevision { get; init; }

    public required string CurrentRulesRevision { get; init; }

    public required string FileManifestId { get; init; }

    public required string ExpectedBuildId { get; init; }

    public required BuildFingerprint ActualBuildFingerprint { get; init; }

    public int? LoadNormaliserSeconds { get; init; }

    public DateTimeOffset GeneratedAt { get; init; }

    public bool IsReadyToLaunch { get; init; }

    public bool IsOfficiallyVerified { get; init; }

    public IReadOnlyList<SpeedrunVerifiedFile> Files { get; init; } = [];

    public IReadOnlyList<SpeedrunVerifiedTool> Tools { get; init; } = [];

    public IReadOnlyList<SpeedrunVerificationIssue> Issues { get; init; } = [];
}

public sealed record SpeedrunVerifiedFile
{
    public required string RelativePath { get; init; }

    public required string Sha256 { get; init; }

    public SpeedrunFileKind Kind { get; init; }

    public string? AssetId { get; init; }
}

public sealed record SpeedrunVerifiedTool
{
    public required string AssetId { get; init; }

    public required string Version { get; init; }

    public IReadOnlyList<SpeedrunVerifiedFile> Files { get; init; } = [];
}

public sealed record SpeedrunVerificationIssue
{
    public SpeedrunIssueCode Code { get; init; }

    public required string Message { get; init; }

    public string? RelativePath { get; init; }
}

public enum SpeedrunTemplateSource
{
    Custom,
    OfficialCatalog
}

public enum SpeedrunFileKind
{
    Unknown,
    Game,
    Tool
}

public enum SpeedrunIssueCode
{
    TemplateNotTrusted,
    UnsupportedOfficialTemplate,
    InstanceNotFound,
    InstanceNotDedicated,
    InstanceNotFullCopy,
    TemplateMismatch,
    BuildMismatch,
    RulesRevisionMismatch,
    MissingRequiredTool,
    InvalidToolSelection,
    InvalidFileManifest,
    ForbiddenFile,
    UnlistedFile,
    MissingFile,
    HashMismatch,
    GameFingerprintMismatch
}

namespace Crystalfly.Core.Models;

public enum LocalLowSessionPhase
{
    Prepared,
    ActivationStaged,
    SharedPreserved,
    InstanceActive,
    CaptureStaged,
    InstanceCaptured,
    SharedRestored,
    Completed,
    RolledBack
}

public sealed record LocalLowSessionJournal
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string Id { get; init; }

    public required string InstanceId { get; init; }

    public TransactionState State { get; init; }

    public LocalLowSessionPhase Phase { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public required string SharedPath { get; init; }

    public required string InstancePath { get; init; }

    public required string SharedPreservedPath { get; init; }

    public required string ActivationStagingPath { get; init; }

    public required string InstanceStagingPath { get; init; }

    public required string InstancePreviousPath { get; init; }

    public required string SharedSha256 { get; init; }

    public required string InstanceSha256 { get; init; }

    public string? CapturedSha256 { get; init; }

    public string? Error { get; init; }
}

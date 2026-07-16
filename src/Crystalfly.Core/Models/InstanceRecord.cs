namespace Crystalfly.Core.Models;

public sealed record InstanceRecord
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string RootPath { get; init; }

    public required string BuildId { get; init; }

    public string? LoaderId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

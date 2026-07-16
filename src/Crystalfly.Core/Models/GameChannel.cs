namespace Crystalfly.Core.Models;

public sealed record GameChannel
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string Name { get; init; }

    public required string BuildId { get; init; }
}

namespace Crystalfly.Core.Updates;

public enum UpdateAssetKind
{
    Installer,
    Portable
}

public sealed record UpdateAsset
{
    public required UpdateAssetKind Kind { get; init; }

    public required string Runtime { get; init; }

    public required string Url { get; init; }

    public required long Size { get; init; }

    public required string Sha256 { get; init; }
}

public sealed record UpdateManifest
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string Channel { get; init; }

    public required string Version { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }

    public required string NotesMarkdown { get; init; }

    public required IReadOnlyList<UpdateAsset> Assets { get; init; }
}

public sealed record SignedUpdateManifest
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string KeyId { get; init; }

    public required string Payload { get; init; }

    public required string Signature { get; init; }
}

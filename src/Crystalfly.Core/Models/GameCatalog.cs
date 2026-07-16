namespace Crystalfly.Core.Models;

public sealed record GameCatalog
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public IReadOnlyList<GameBuild> Builds { get; init; } = [];

    public IReadOnlyList<GameChannel> Channels { get; init; } = [];

    public IReadOnlyList<LoaderManifest> Loaders { get; init; } = [];

    public IReadOnlyList<ModManifest> Mods { get; init; } = [];

    public IReadOnlyList<SpeedrunTemplate> SpeedrunTemplates { get; init; } = [];

    public IReadOnlyList<SpeedrunAsset> SpeedrunAssets { get; init; } = [];
}

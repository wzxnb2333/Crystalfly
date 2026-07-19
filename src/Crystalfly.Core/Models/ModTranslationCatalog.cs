namespace Crystalfly.Core.Models;

public sealed record ModTranslationCatalog
{
    public const int CurrentSchemaVersion = 1;
    public const string SupportedLanguage = "zh-CN";

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public string Language { get; init; } = SupportedLanguage;

    public IReadOnlyDictionary<string, string> TagNames { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ModTranslationEntry> Mods { get; init; } = [];
}

public sealed record ModTranslationEntry
{
    public required string Id { get; init; }

    public string? DisplayName { get; init; }

    public string? Description { get; init; }
}

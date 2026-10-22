namespace Crystalfly.Core.Thunderstore;

public sealed record SilksongModTranslationCatalog
{
    public int SchemaVersion { get; init; } = 1;
    public string Language { get; init; } = "zh-CN";
    public string Community { get; init; } = ThunderstoreCatalogSource.Community;
    public DateTimeOffset UpdatedAt { get; init; }
    public IReadOnlyDictionary<string, string> CategoryNames { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<SilksongModTranslationEntry> Mods { get; init; } = [];
}

public sealed record SilksongModTranslationEntry
{
    public required string FullName { get; init; }
    public required string DisplayName { get; init; }
    public required string SourceUrl { get; init; }
    public IReadOnlyList<SilksongModVersionTranslation> Versions { get; init; } = [];
}

public sealed record SilksongModVersionTranslation
{
    public required string Version { get; init; }
    public string? Description { get; init; }
    public string? DescriptionSha256 { get; init; }
    public IReadOnlyDictionary<string, SilksongModDocumentTranslation> Documents { get; init; } =
        new Dictionary<string, SilksongModDocumentTranslation>();
}

public sealed record SilksongModDocumentTranslation
{
    public bool Missing { get; init; }
    public string? SourceSha256 { get; init; }
    public string? TranslationSha256 { get; init; }
}

public sealed record SilksongTranslatedDocument(string Original, string? Chinese, bool Missing);

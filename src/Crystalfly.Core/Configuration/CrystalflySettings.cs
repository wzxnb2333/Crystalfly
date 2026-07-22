using Crystalfly.Core.Runtime;

namespace Crystalfly.Core.Configuration;

public enum UiLanguage
{
    FollowSystem,
    SimplifiedChinese,
    English
}

public enum UiTheme
{
    System,
    Light,
    Dark
}

public enum GitHubDownloadRoute
{
    Direct,
    Mirror
}

public sealed record CrystalflySettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public string? VersionRoot { get; init; }

    public string? CurrentInstanceId { get; init; }

    public UiLanguage Language { get; init; } = UiLanguage.FollowSystem;

    public UiTheme Theme { get; init; } = UiTheme.System;

    public GitHubDownloadRoute GitHubDownloadRoute { get; init; } = GitHubDownloadRoute.Direct;

    public bool OfflineMode { get; init; }

    public IReadOnlyList<ModHealthAcknowledgement> ModHealthAcknowledgements { get; init; } = [];

    public IReadOnlyList<CustomCatalogDefinition> CustomCatalogs { get; init; } = [];
}

public sealed record CustomCatalogDefinition
{
    public required string Namespace { get; init; }

    public required string Url { get; init; }
}

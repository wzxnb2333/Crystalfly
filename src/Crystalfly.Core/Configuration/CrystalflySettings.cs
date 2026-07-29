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

public static class AccentColorPalette
{
    public const string DefaultColor = "#0F6CBD";

    public static IReadOnlyList<string> Presets { get; } =
    [
        DefaultColor,
        "#4338CA",
        "#7E22CE",
        "#BE185D",
        "#C2410C",
        "#15803D",
        "#0E7490"
    ];

    public static bool TryNormalize(string? value, out string normalized)
    {
        var candidate = value?.Trim() ?? string.Empty;
        if (candidate.StartsWith('#'))
        {
            candidate = candidate[1..];
        }

        if (candidate.Length == 6 && candidate.All(Uri.IsHexDigit))
        {
            normalized = $"#{candidate.ToUpperInvariant()}";
            return true;
        }

        normalized = DefaultColor;
        return false;
    }

    public static string Normalize(string? value) =>
        TryNormalize(value, out var normalized) ? normalized : DefaultColor;
}

public enum GitHubDownloadRoute
{
    Direct,
    Mirror
}

public sealed record BackgroundImageSettings
{
    public const int DefaultOpacityPercent = 35;

    public string FileName { get; init; } = string.Empty;

    public int OpacityPercent { get; init; } = DefaultOpacityPercent;

    public static BackgroundImageSettings? Normalize(BackgroundImageSettings? value)
    {
        if (value is null || !IsSafeFileName(value.FileName))
        {
            return null;
        }

        return value with
        {
            FileName = value.FileName.Trim(),
            OpacityPercent = Math.Clamp(value.OpacityPercent, 0, 100)
        };
    }

    public static bool IsSafeFileName(string? value)
    {
        var candidate = value?.Trim() ?? string.Empty;
        return candidate.Length > 0
            && candidate is not "." and not ".."
            && string.Equals(candidate, Path.GetFileName(candidate), StringComparison.Ordinal)
            && candidate.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }
}

public sealed record CrystalflySettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public string? VersionRoot { get; init; }

    public IReadOnlyList<GameDirectoryRegistration> GameDirectories { get; init; } = [];

    public bool GameDirectoryDiscoveryCompleted { get; init; }

    public string? CurrentInstanceId { get; init; }
    public IReadOnlyList<string> FavoriteInstanceIds { get; init; } = [];


    public UiLanguage Language { get; init; } = UiLanguage.FollowSystem;

    public UiTheme Theme { get; init; } = UiTheme.System;

    public string AccentColor { get; init; } = AccentColorPalette.DefaultColor;

    public BackgroundImageSettings? BackgroundImage { get; init; }

    public GitHubDownloadRoute GitHubDownloadRoute { get; init; } = GitHubDownloadRoute.Direct;

    public bool OfflineMode { get; init; }

    public bool CheckForUpdates { get; init; } = true;

    public DateTimeOffset? LastUpdateCheckAt { get; init; }

    public string? SkippedUpdateVersion { get; init; }

    public IReadOnlyList<ModHealthAcknowledgement> ModHealthAcknowledgements { get; init; } = [];

    public IReadOnlyList<CustomCatalogDefinition> CustomCatalogs { get; init; } = [];

    public CustomModLinksDefinition? CustomModLinks { get; init; }
}

public sealed record CustomCatalogDefinition
{
    public required string Namespace { get; init; }

    public required string Url { get; init; }
}

public sealed record CustomModLinksDefinition
{
    public required string Url { get; init; }

    public required string BuildId { get; init; }

    public required string LoaderId { get; init; }
}

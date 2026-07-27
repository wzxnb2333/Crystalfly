using Crystalfly.Core.Configuration;
using Crystalfly.Core.Runtime;

namespace Crystalfly.Core.Tests.Configuration;

public sealed class CrystalflySettingsStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"crystalfly-settings-{Guid.NewGuid():N}");

    [Fact]
    public async Task Load_returns_defaults_then_round_trips_saved_settings()
    {
        var path = Path.Combine(root, "settings.json");
        var defaults = await CrystalflySettingsStore.LoadAsync(path);
        Assert.Equal(UiLanguage.FollowSystem, defaults.Language);
        Assert.Equal(UiTheme.System, defaults.Theme);
        Assert.Equal(AccentColorPalette.DefaultColor, defaults.AccentColor);
        Assert.Equal(GitHubDownloadRoute.Direct, defaults.GitHubDownloadRoute);
        Assert.False(defaults.OfflineMode);
        Assert.Empty(defaults.GameDirectories);
        Assert.False(defaults.GameDirectoryDiscoveryCompleted);

        var expected = defaults with
        {
            VersionRoot = @"D:\HK_ver",
            CurrentInstanceId = "practice-1578",
            Language = UiLanguage.SimplifiedChinese,
            Theme = UiTheme.Dark,
            AccentColor = "7e22ce",
            GitHubDownloadRoute = GitHubDownloadRoute.Mirror,
            OfflineMode = true,
            GameDirectories =
            [
                new GameDirectoryRegistration
                {
                    Path = @"D:\HK_ver",
                    DisplayName = "HK versions",
                    Source = GameDirectorySourceKind.Managed
                }
            ],
            GameDirectoryDiscoveryCompleted = true,
            ModHealthAcknowledgements =
            [
                new ModHealthAcknowledgement { Fingerprint = new string('A', 64) }
            ],
            CustomCatalogs =
            [
                new CustomCatalogDefinition
                {
                    Namespace = "community",
                    Url = "https://example.invalid/catalog.json"
                }
            ],
            CustomModLinks = new CustomModLinksDefinition
            {
                Url = "https://example.invalid/ModLinks.xml",
                BuildId = "1.5.78.11833",
                LoaderId = "modding-api-77"
            }
        };
        await CrystalflySettingsStore.SaveAsync(path, expected);

        var actual = await CrystalflySettingsStore.LoadAsync(path);
        Assert.Equal(
            expected with
            {
                AccentColor = "#7E22CE",
                CustomCatalogs = [],
                GameDirectories = [],
                ModHealthAcknowledgements = []
            },
            actual with { CustomCatalogs = [], GameDirectories = [], ModHealthAcknowledgements = [] });
        Assert.Equal(expected.CustomCatalogs, actual.CustomCatalogs);
        Assert.Equal(expected.CustomModLinks, actual.CustomModLinks);
        Assert.Equal(expected.ModHealthAcknowledgements, actual.ModHealthAcknowledgements);
        Assert.Equal(expected.GameDirectories.ToArray(), actual.GameDirectories.ToArray());
        Assert.Equal("#7E22CE", actual.AccentColor);
    }

    [Fact]
    public async Task Load_migrates_existing_version_root_to_managed_game_directory()
    {
        var versionRoot = Directory.CreateDirectory(Path.Combine(root, "HK versions")).FullName;
        var path = Path.Combine(root, "legacy-version-root.json");
        await File.WriteAllTextAsync(
            path,
            $$"""
            {"schemaVersion":1,"versionRoot":{{System.Text.Json.JsonSerializer.Serialize(versionRoot)}},"gameDirectories":[]}
            """);

        var settings = await CrystalflySettingsStore.LoadAsync(path);

        var registration = Assert.Single(settings.GameDirectories);
        Assert.Equal(Path.GetFullPath(versionRoot), registration.Path);
        Assert.Equal("HK versions", registration.DisplayName);
        Assert.Equal(GameDirectorySourceKind.Managed, registration.Source);
        Assert.True(settings.GameDirectoryDiscoveryCompleted);
    }

    [Fact]
    public async Task Load_does_not_migrate_missing_legacy_version_root()
    {
        var versionRoot = Path.Combine(root, "missing");
        var path = Path.Combine(root, "legacy-missing-root.json");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            path,
            $$"""
            {"schemaVersion":1,"versionRoot":{{System.Text.Json.JsonSerializer.Serialize(versionRoot)}},"gameDirectories":[]}
            """);

        var settings = await CrystalflySettingsStore.LoadAsync(path);

        Assert.Empty(settings.GameDirectories);
        Assert.False(settings.GameDirectoryDiscoveryCompleted);
        Assert.Equal(versionRoot, settings.VersionRoot);
    }

    [Fact]
    public async Task Load_legacy_settings_without_route_uses_direct_GitHub()
    {
        var path = Path.Combine(root, "legacy-settings.json");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            path,
            """
            {"schemaVersion":1,"language":"english","theme":"dark","customCatalogs":[]}
            """);

        var settings = await CrystalflySettingsStore.LoadAsync(path);

        Assert.Equal(GitHubDownloadRoute.Direct, settings.GitHubDownloadRoute);
        Assert.False(settings.OfflineMode);
        Assert.Empty(settings.ModHealthAcknowledgements);
        Assert.Equal(AccentColorPalette.DefaultColor, settings.AccentColor);
    }

    [Fact]
    public async Task Load_invalid_accent_color_falls_back_to_default()
    {
        var path = Path.Combine(root, "invalid-accent-settings.json");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            path,
            """
            {"schemaVersion":1,"accentColor":"transparent","customCatalogs":[]}
            """);

        var settings = await CrystalflySettingsStore.LoadAsync(path);

        Assert.Equal(AccentColorPalette.DefaultColor, settings.AccentColor);
    }

    [Fact]
    public void Accent_palette_is_fixed_unique_and_normalizes_hex()
    {
        Assert.Equal(
            ["#0F6CBD", "#4338CA", "#7E22CE", "#BE185D", "#C2410C", "#15803D", "#0E7490"],
            AccentColorPalette.Presets);
        Assert.Equal(AccentColorPalette.Presets.Count, AccentColorPalette.Presets.Distinct().Count());
        Assert.True(AccentColorPalette.TryNormalize("be185d", out var normalized));
        Assert.Equal("#BE185D", normalized);
        Assert.False(AccentColorPalette.TryNormalize("#1234", out _));
        Assert.False(AccentColorPalette.TryNormalize("#00112233", out _));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

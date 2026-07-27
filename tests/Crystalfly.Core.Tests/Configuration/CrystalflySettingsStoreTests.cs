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
        Assert.Null(defaults.BackgroundImage);
        Assert.Equal(GitHubDownloadRoute.Direct, defaults.GitHubDownloadRoute);
        Assert.False(defaults.OfflineMode);

        var expected = defaults with
        {
            VersionRoot = @"D:\HK_ver",
            CurrentInstanceId = "practice-1578",
            Language = UiLanguage.SimplifiedChinese,
            Theme = UiTheme.Dark,
            AccentColor = "7e22ce",
            BackgroundImage = new BackgroundImageSettings
            {
                FileName = "ABCDEF.webp",
                OpacityPercent = 41
            },
            GitHubDownloadRoute = GitHubDownloadRoute.Mirror,
            OfflineMode = true,
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
                ModHealthAcknowledgements = []
            },
            actual with { CustomCatalogs = [], ModHealthAcknowledgements = [] });
        Assert.Equal(expected.CustomCatalogs, actual.CustomCatalogs);
        Assert.Equal(expected.CustomModLinks, actual.CustomModLinks);
        Assert.Equal(expected.ModHealthAcknowledgements, actual.ModHealthAcknowledgements);
        Assert.Equal("#7E22CE", actual.AccentColor);
        Assert.Equal(new BackgroundImageSettings
        {
            FileName = "ABCDEF.webp",
            OpacityPercent = 41
        }, actual.BackgroundImage);
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
        Assert.Null(settings.BackgroundImage);
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

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(35, 35)]
    [InlineData(101, 100)]
    public async Task Background_image_normalizes_opacity(int input, int expected)
    {
        var path = Path.Combine(root, $"opacity-{input}.json");

        await CrystalflySettingsStore.SaveAsync(path, new CrystalflySettings
        {
            BackgroundImage = new BackgroundImageSettings
            {
                FileName = "image.png",
                OpacityPercent = input
            }
        });

        var settings = await CrystalflySettingsStore.LoadAsync(path);

        Assert.Equal(expected, settings.BackgroundImage?.OpacityPercent);
    }

    [Theory]
    [InlineData("../image.png")]
    [InlineData("folder/image.png")]
    [InlineData("folder\\image.png")]
    [InlineData(".")]
    [InlineData("")]
    public async Task Background_image_rejects_unsafe_file_names(string fileName)
    {
        var path = Path.Combine(root, "unsafe.json");

        await CrystalflySettingsStore.SaveAsync(path, new CrystalflySettings
        {
            BackgroundImage = new BackgroundImageSettings
            {
                FileName = fileName,
                OpacityPercent = 35
            }
        });

        var settings = await CrystalflySettingsStore.LoadAsync(path);

        Assert.Null(settings.BackgroundImage);
    }


    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

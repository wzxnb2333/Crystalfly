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
        Assert.Equal(GitHubDownloadRoute.Direct, defaults.GitHubDownloadRoute);
        Assert.False(defaults.OfflineMode);

        var expected = defaults with
        {
            VersionRoot = @"D:\HK_ver",
            CurrentInstanceId = "practice-1578",
            Language = UiLanguage.SimplifiedChinese,
            Theme = UiTheme.Dark,
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
            expected with { CustomCatalogs = [], ModHealthAcknowledgements = [] },
            actual with { CustomCatalogs = [], ModHealthAcknowledgements = [] });
        Assert.Equal(expected.CustomCatalogs, actual.CustomCatalogs);
        Assert.Equal(expected.CustomModLinks, actual.CustomModLinks);
        Assert.Equal(expected.ModHealthAcknowledgements, actual.ModHealthAcknowledgements);
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
    }


    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

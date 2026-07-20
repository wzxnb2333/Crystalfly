using Crystalfly.Core.Configuration;

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

        var expected = defaults with
        {
            VersionRoot = @"D:\HK_ver",
            CurrentInstanceId = "practice-1578",
            Language = UiLanguage.SimplifiedChinese,
            Theme = UiTheme.Dark,
            GitHubDownloadRoute = GitHubDownloadRoute.Mirror,
            CustomCatalogs =
            [
                new CustomCatalogDefinition
                {
                    Namespace = "community",
                    Url = "https://example.invalid/catalog.json"
                }
            ]
        };
        await CrystalflySettingsStore.SaveAsync(path, expected);

        var actual = await CrystalflySettingsStore.LoadAsync(path);
        Assert.Equal(expected with { CustomCatalogs = [] }, actual with { CustomCatalogs = [] });
        Assert.Equal(expected.CustomCatalogs, actual.CustomCatalogs);
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
    }


    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

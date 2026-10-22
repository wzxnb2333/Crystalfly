using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Configuration;

public static class CrystalflySettingsStore
{
    public static Task SaveAsync(
        string path,
        CrystalflySettings settings,
        CancellationToken cancellationToken = default) =>
        AtomicJsonStore.WriteAsync(
            path,
            GameWorkspaceMigration.Capture(Normalize(settings)),
            cancellationToken);

    public static async Task<CrystalflySettings> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path) && !File.Exists(path + ".bak"))
        {
            return new CrystalflySettings();
        }

        var settings = await AtomicJsonStore.ReadAsync<CrystalflySettings>(path, cancellationToken);
        var migrated = MigrateLegacyVersionRoot(Normalize(settings));
        if (migrated.GameWorkspaces.Count == 0)
        {
            if (File.Exists(path) && !File.Exists(path + ".pre-multigame.bak"))
                File.Copy(path, path + ".pre-multigame.bak");
            migrated = GameWorkspaceMigration.Capture(migrated);
            await SaveAsync(path, migrated, cancellationToken);
        }
        return migrated;
    }

    private static CrystalflySettings MigrateLegacyVersionRoot(CrystalflySettings settings)
    {
        if (settings.GameDirectories.Count > 0
            || string.IsNullOrWhiteSpace(settings.VersionRoot))
        {
            return settings;
        }

        string root;
        try
        {
            root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(settings.VersionRoot));
            if (!Directory.Exists(root)
                || (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            {
                return settings;
            }
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            return settings;
        }

        var displayName = Path.GetFileName(root);
        return settings with
        {
            GameDirectories =
            [
                new GameDirectoryRegistration
                {
                    GameId = settings.SelectedGameId,
                    Path = root,
                    DisplayName = string.IsNullOrWhiteSpace(displayName) ? root : displayName,
                    Source = GameDirectorySourceKind.Managed
                }
            ],
            GameDirectoryDiscoveryCompleted = true
        };
    }

    private static CrystalflySettings Normalize(CrystalflySettings settings) => settings with
    {
        AccentColor = AccentColorPalette.Normalize(settings.AccentColor),
        BackgroundImage = BackgroundImageSettings.Normalize(settings.BackgroundImage),
        LiveSplitFavoritePaths = LiveSplitFavoritePathPolicy.NormalizeStoredPaths(settings.LiveSplitFavoritePaths),
        SpeedrunCommunityLinks = SpeedrunCommunityLinkDefinition.NormalizeStoredLinks(settings.SpeedrunCommunityLinks)
    };
}

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
            Normalize(settings),
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
        return Normalize(settings);
    }

    private static CrystalflySettings Normalize(CrystalflySettings settings) => settings with
    {
        AccentColor = AccentColorPalette.Normalize(settings.AccentColor),
        BackgroundImage = BackgroundImageSettings.Normalize(settings.BackgroundImage)
    };
}

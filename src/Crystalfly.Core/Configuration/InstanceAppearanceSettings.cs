using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Configuration;

public sealed record InstanceAppearanceSettings
{
    public BackgroundImageSettings? BackgroundImage { get; init; }
}

public static class InstanceAppearanceSettingsStore
{
    public static Task SaveAsync(
        string path,
        InstanceAppearanceSettings settings,
        CancellationToken cancellationToken = default) =>
        AtomicJsonStore.WriteAsync(path, Normalize(settings), cancellationToken);

    public static async Task<InstanceAppearanceSettings> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path) && !File.Exists(path + ".bak"))
        {
            return new InstanceAppearanceSettings();
        }

        return Normalize(await AtomicJsonStore.ReadAsync<InstanceAppearanceSettings>(path, cancellationToken));
    }

    private static InstanceAppearanceSettings Normalize(InstanceAppearanceSettings settings) => settings with
    {
        BackgroundImage = BackgroundImageSettings.Normalize(settings.BackgroundImage)
    };
}

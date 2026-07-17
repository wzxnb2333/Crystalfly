using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Configuration;

public static class CrystalflySettingsStore
{
    public static Task SaveAsync(
        string path,
        CrystalflySettings settings,
        CancellationToken cancellationToken = default) =>
        AtomicJsonStore.WriteAsync(path, settings, cancellationToken);

    public static async Task<CrystalflySettings> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path) && !File.Exists(path + ".bak"))
        {
            return new CrystalflySettings();
        }

        return await AtomicJsonStore.ReadAsync<CrystalflySettings>(path, cancellationToken);
    }
}

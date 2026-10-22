using Crystalfly.Core.Configuration;

namespace Crystalfly.Core.Tests.Configuration;

public sealed class InstanceAppearanceSettingsStoreTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        $"crystalfly-instance-appearance-{Guid.NewGuid():N}");

    [Fact]
    public async Task Load_normalizes_invalid_override_to_inherited_default()
    {
        var path = Path.Combine(root, "appearance.json");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            path,
            """
            {"backgroundImage":{"fileName":"..\\outside.png","opacityPercent":500}}
            """);

        var settings = await InstanceAppearanceSettingsStore.LoadAsync(path);

        Assert.Null(settings.BackgroundImage);
    }

    [Fact]
    public async Task Load_uses_backup_when_primary_appearance_file_is_corrupt()
    {
        var path = Path.Combine(root, "appearance.json");
        var recoverable = new InstanceAppearanceSettings
        {
            BackgroundImage = new BackgroundImageSettings
            {
                FileName = "RECOVERABLE.png",
                OpacityPercent = 44
            }
        };
        await InstanceAppearanceSettingsStore.SaveAsync(path, recoverable);
        await InstanceAppearanceSettingsStore.SaveAsync(
            path,
            new InstanceAppearanceSettings
            {
                BackgroundImage = new BackgroundImageSettings
                {
                    FileName = "NEWER.png",
                    OpacityPercent = 60
                }
            });
        await File.WriteAllTextAsync(path, "{broken-json");

        var settings = await InstanceAppearanceSettingsStore.LoadAsync(path);

        Assert.Equal(recoverable, settings);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

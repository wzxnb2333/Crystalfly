using Crystalfly.Core.Configuration;

namespace Crystalfly.Core.Tests.Configuration;

public sealed class AppConfigServiceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"crystalfly-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task Load_missing_file_returns_empty_document()
    {
        var path = Path.Combine(directory, "AppConfig.ini");

        var document = await AppConfigService.LoadAsync(path);

        Assert.Empty(document.Sections);
    }

    [Fact]
    public async Task Save_then_load_round_trips_values()
    {
        var path = Path.Combine(directory, "AppConfig.ini");
        var document = new IniDocument();
        AppConfigService.SetDouble(document, AppConfigService.AccessibilitySection, AppConfigService.ReducedCameraShakeKey, 0.2);
        AppConfigService.SetDouble(document, AppConfigService.AccessibilitySection, AppConfigService.ReducedControllerRumbleKey, 0.4);

        await AppConfigService.SaveAsync(path, document);
        var loaded = await AppConfigService.LoadAsync(path);

        Assert.Equal(0.2, AppConfigService.GetDouble(loaded, AppConfigService.AccessibilitySection, AppConfigService.ReducedCameraShakeKey, 0));
        Assert.Equal(0.4, AppConfigService.GetDouble(loaded, AppConfigService.AccessibilitySection, AppConfigService.ReducedControllerRumbleKey, 0));
    }

    [Fact]
    public async Task Overwrite_creates_backup_with_previous_content()
    {
        var path = Path.Combine(directory, "AppConfig.ini");
        var first = new IniDocument();
        first.SetValue(AppConfigService.AccessibilitySection, AppConfigService.ReducedCameraShakeKey, "0.1");
        await AppConfigService.SaveAsync(path, first);

        var second = new IniDocument();
        second.SetValue(AppConfigService.AccessibilitySection, AppConfigService.ReducedCameraShakeKey, "0.9");
        await AppConfigService.SaveAsync(path, second);

        var backup = await File.ReadAllTextAsync(path + ".bak");
        Assert.Contains("ReducedCameraShake=0.1", backup);
        var current = await AppConfigService.LoadAsync(path);
        Assert.Equal("0.9", current.GetValue(AppConfigService.AccessibilitySection, AppConfigService.ReducedCameraShakeKey));
    }

    [Fact]
    public async Task Save_preserves_unknown_sections_and_keys()
    {
        var path = Path.Combine(directory, "AppConfig.ini");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(path, """
            [Accessibility]
            ReducedCameraShake=0.2
            [Custom]
            Tweak=42
            """);

        var document = await AppConfigService.LoadAsync(path);
        document.SetValue(AppConfigService.AccessibilitySection, AppConfigService.ReducedCameraShakeKey, "0.7");
        await AppConfigService.SaveAsync(path, document);

        var reloaded = await AppConfigService.LoadAsync(path);
        Assert.Equal("0.7", reloaded.GetValue(AppConfigService.AccessibilitySection, AppConfigService.ReducedCameraShakeKey));
        Assert.Equal("42", reloaded.GetValue("Custom", "Tweak"));
    }

    [Fact]
    public void GetDouble_returns_default_for_missing_or_invalid_values()
    {
        var document = IniDocument.Parse("""
            [Accessibility]
            ReducedCameraShake=not-a-number
            """);

        Assert.Equal(0.5, AppConfigService.GetDouble(document, AppConfigService.AccessibilitySection, AppConfigService.ReducedCameraShakeKey, 0.5));
        Assert.Equal(0.25, AppConfigService.GetDouble(document, AppConfigService.AccessibilitySection, "Missing", 0.25));
    }

    [Fact]
    public void SetDouble_writes_invariant_culture()
    {
        var document = new IniDocument();

        AppConfigService.SetDouble(document, AppConfigService.AccessibilitySection, AppConfigService.ReducedCameraShakeKey, 0.35);

        Assert.Equal("0.35", document.GetValue(AppConfigService.AccessibilitySection, AppConfigService.ReducedCameraShakeKey));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

using Crystalfly.App.ViewModels;
using Crystalfly.Core.Configuration;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class GameConfigViewModelTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"crystalfly-tests-{Guid.NewGuid():N}");

    private string ConfigPath => Path.Combine(directory, "AppConfig.ini");

    [Fact]
    public async Task Load_missing_file_defaults_to_zero_sliders()
    {
        var viewModel = new GameConfigViewModel(ConfigPath);

        await viewModel.LoadAsync();

        Assert.Equal(0, viewModel.ReducedCameraShake);
        Assert.Equal(0, viewModel.ReducedControllerRumble);
        Assert.Empty(viewModel.Entries);
    }

    [Fact]
    public async Task Save_persists_changes_and_clears_dirty()
    {
        await WriteConfigAsync("""
            [Accessibility]
            ReducedCameraShake=0.2
            [Custom]
            Tweak=42
            """);
        var viewModel = new GameConfigViewModel(ConfigPath);
        await viewModel.LoadAsync();
        viewModel.ReducedCameraShake = 0.9;

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsDirty);
        var reloaded = await AppConfigService.LoadAsync(ConfigPath);
        Assert.Equal("0.9", reloaded.GetValue(AppConfigService.AccessibilitySection, AppConfigService.ReducedCameraShakeKey));
        Assert.Equal("42", reloaded.GetValue("Custom", "Tweak"));
    }

    [Fact]
    public async Task Add_and_remove_entry_round_trip_through_save()
    {
        await WriteConfigAsync("[Accessibility]\nReducedCameraShake=0.2");
        var viewModel = new GameConfigViewModel(ConfigPath);
        await viewModel.LoadAsync();

        viewModel.AddEntryCommand.Execute(null);
        var added = viewModel.Entries[^1];
        added.Section = "Video";
        added.Key = "Width";
        added.Value = "1920";
        await viewModel.SaveCommand.ExecuteAsync(null);

        var reloaded = await AppConfigService.LoadAsync(ConfigPath);
        Assert.Equal("1920", reloaded.GetValue("Video", "Width"));

        viewModel.RemoveEntryCommand.Execute(viewModel.Entries.Single(item => item.Key == "Width"));
        await viewModel.SaveCommand.ExecuteAsync(null);

        var afterRemove = await AppConfigService.LoadAsync(ConfigPath);
        Assert.Null(afterRemove.GetValue("Video", "Width"));
    }

    [Fact]
    public async Task Reset_discards_pending_changes()
    {
        await WriteConfigAsync("[Accessibility]\nReducedCameraShake=0.2");
        var viewModel = new GameConfigViewModel(ConfigPath);
        await viewModel.LoadAsync();
        viewModel.ReducedCameraShake = 0.9;
        Assert.True(viewModel.IsDirty);

        await viewModel.ResetCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsDirty);
        Assert.Equal(0.2, viewModel.ReducedCameraShake);
    }

    private async Task WriteConfigAsync(string content)
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(ConfigPath, content);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

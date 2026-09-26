using Crystalfly.App.ViewModels;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Models;
using Crystalfly.Core.Saves;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class EditorIntegrationViewModelTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "crystalfly-editor-integration",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Config_tab_loads_only_the_selected_instance_config()
    {
        var versionRoot = Directory.CreateDirectory(Path.Combine(root, "versions")).FullName;
        var selected = CreateInstance(versionRoot, "selected");
        var other = CreateInstance(versionRoot, "other");
        await WriteConfigAsync(versionRoot, selected.Id, "0.25");
        await WriteConfigAsync(versionRoot, other.Id, "0.75");
        await using var viewModel = new MainViewModel(Path.Combine(root, "app-data"))
        {
            VersionRoot = versionRoot,
            SelectedInstance = new InstanceItemViewModel(selected, selected.BuildId, "Vanilla", 0)
        };

        viewModel.SelectManageTabCommand.Execute("Config");
        await WaitUntilAsync(() => viewModel.GameConfig?.IsLoaded == true);

        Assert.NotNull(viewModel.GameConfig);
        Assert.Equal(0.25, viewModel.GameConfig.ReducedCameraShake);
        Assert.Contains(
            Path.Combine("instances", selected.Id, "local-low", AppConfigService.FileName),
            viewModel.GameConfig.ConfigPath,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Edit_current_save_lists_only_the_selected_instance_slots()
    {
        var versionRoot = Directory.CreateDirectory(Path.Combine(root, "versions")).FullName;
        var selected = CreateInstance(versionRoot, "selected");
        var other = CreateInstance(versionRoot, "other");
        await WriteSaveAsync(versionRoot, selected.Id, "user1.dat", """{"profile":"selected"}""");
        await WriteSaveAsync(versionRoot, other.Id, "user2.dat", """{"profile":"other"}""");
        await using var viewModel = new MainViewModel(Path.Combine(root, "app-data"))
        {
            VersionRoot = versionRoot,
            SelectedInstance = new InstanceItemViewModel(selected, selected.BuildId, "Vanilla", 0)
        };

        await viewModel.EditSaveCommand.ExecuteAsync("current");

        Assert.NotNull(viewModel.SaveEditor);
        Assert.Equal(["user1.dat"], viewModel.SaveEditor.Slots);
        Assert.DoesNotContain("user2.dat", viewModel.SaveEditor.Slots);
    }

    [Fact]
    public async Task Edit_current_save_without_slots_finishes_loading()
    {
        var versionRoot = Directory.CreateDirectory(Path.Combine(root, "versions")).FullName;
        var selected = CreateInstance(versionRoot, "empty");
        Directory.CreateDirectory(Path.Combine(
            versionRoot,
            ".crystalfly",
            "instances",
            selected.Id,
            "local-low"));
        await using var viewModel = new MainViewModel(Path.Combine(root, "app-data"))
        {
            VersionRoot = versionRoot,
            SelectedInstance = new InstanceItemViewModel(selected, selected.BuildId, "Vanilla", 0)
        };

        await viewModel.EditSaveCommand.ExecuteAsync("current");

        Assert.NotNull(viewModel.SaveEditor);
        Assert.True(viewModel.SaveEditor.IsLoaded);
        Assert.Empty(viewModel.SaveEditor.Slots);
        Assert.Empty(viewModel.SaveEditor.Entries);
    }

    [Theory]
    [InlineData("{\"health\":5}", "invalid number", "9", "{\"health\":9}")]
    [InlineData("{\"equipped\":false}", "invalid boolean", "true", "{\"equipped\":true}")]
    public async Task Invalid_save_value_reports_error_preserves_file_and_allows_retry(
        string original, string invalid, string corrected, string expected)
    {
        await using var viewModel = await CreateSaveEditorAsync(original);
        var editor = Assert.IsType<SaveEditorViewModel>(viewModel.SaveEditor);
        var entry = Assert.Single(editor.Entries);
        var path = Path.Combine(root, "versions", ".crystalfly", "instances", "selected", "local-low", "user1.dat");
        var originalBytes = await File.ReadAllBytesAsync(path);
        entry.Value = invalid;

        var exception = await Record.ExceptionAsync(() => editor.SaveCommand.ExecuteAsync(null));

        Assert.Null(exception);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(path));
        Assert.True(editor.IsDirty);
        Assert.True(editor.CanSave);
        entry.Value = corrected;
        await editor.SaveCommand.ExecuteAsync(null);
        Assert.Null(viewModel.ErrorMessage);
        Assert.False(editor.IsDirty);
        Assert.Equal(expected, await SaveFileCodec.DecryptAsync(path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Locked_save_reports_error_keeps_edits_and_allows_retry(bool reset)
    {
        const string original = "{\"health\":5}";
        await using var viewModel = await CreateSaveEditorAsync(original);
        var editor = Assert.IsType<SaveEditorViewModel>(viewModel.SaveEditor);
        Assert.Single(editor.Entries).Value = "9";
        var path = Path.Combine(root, "versions", ".crystalfly", "instances", "selected", "local-low", "user1.dat");
        var originalBytes = await File.ReadAllBytesAsync(path);
        var command = reset ? editor.ResetCommand : editor.SaveCommand;
        await using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var exception = await Record.ExceptionAsync(() => command.ExecuteAsync(null));
            Assert.Null(exception);
        }

        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(path));
        Assert.True(editor.IsLoaded);
        Assert.True(editor.IsDirty);
        Assert.Equal("9", Assert.Single(editor.Entries).Value);
        Assert.Equal("user1.dat", editor.SelectedSlot);
        await command.ExecuteAsync(null);
        Assert.Null(viewModel.ErrorMessage);
        Assert.False(editor.IsDirty);
        Assert.Equal(reset ? original : "{\"health\":9}", await SaveFileCodec.DecryptAsync(path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Locked_config_reports_error_keeps_edits_and_allows_retry(bool reset)
    {
        var versionRoot = Directory.CreateDirectory(Path.Combine(root, "versions")).FullName;
        var selected = CreateInstance(versionRoot, "selected");
        await WriteConfigAsync(versionRoot, selected.Id, "0.25");
        await using var viewModel = new MainViewModel(Path.Combine(root, "app-data"))
        {
            VersionRoot = versionRoot,
            SelectedInstance = new InstanceItemViewModel(selected, selected.BuildId, "Vanilla", 0)
        };
        viewModel.SelectManageTabCommand.Execute("Config");
        await WaitUntilAsync(() => viewModel.GameConfig?.IsLoaded == true);
        var editor = Assert.IsType<GameConfigViewModel>(viewModel.GameConfig);
        editor.ReducedCameraShake = 0.75;
        var originalBytes = await File.ReadAllBytesAsync(editor.ConfigPath);
        var command = reset ? editor.ResetCommand : editor.SaveCommand;
        await using (var locked = new FileStream(editor.ConfigPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var exception = await Record.ExceptionAsync(() => command.ExecuteAsync(null));
            Assert.Null(exception);
        }

        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(editor.ConfigPath));
        Assert.True(editor.IsLoaded);
        Assert.True(editor.IsDirty);
        Assert.Equal(0.75, editor.ReducedCameraShake);
        await command.ExecuteAsync(null);
        Assert.Null(viewModel.ErrorMessage);
        Assert.False(editor.IsDirty);
        var saved = await AppConfigService.LoadAsync(editor.ConfigPath);
        Assert.Equal(reset ? "0.25" : "0.75", saved.GetValue("Accessibility", "ReducedCameraShake"));
    }

    private async Task<MainViewModel> CreateSaveEditorAsync(string json)
    {
        var versionRoot = Directory.CreateDirectory(Path.Combine(root, "versions")).FullName;
        var selected = CreateInstance(versionRoot, "selected");
        await WriteSaveAsync(versionRoot, selected.Id, "user1.dat", json);
        var viewModel = new MainViewModel(Path.Combine(root, "app-data"))
        {
            VersionRoot = versionRoot,
            SelectedInstance = new InstanceItemViewModel(selected, selected.BuildId, "Vanilla", 0)
        };
        await viewModel.EditSaveCommand.ExecuteAsync("current");
        return viewModel;
    }

    private static InstanceRecord CreateInstance(string versionRoot, string id)
    {
        var instanceRoot = Directory.CreateDirectory(Path.Combine(versionRoot, id)).FullName;
        return new InstanceRecord
        {
            Id = id,
            Name = id,
            RootPath = instanceRoot,
            BuildId = "1.5.78.11833",
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static Task WriteConfigAsync(string versionRoot, string instanceId, string shake)
    {
        var path = Path.Combine(
            versionRoot,
            ".crystalfly",
            "instances",
            instanceId,
            "local-low",
            AppConfigService.FileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return File.WriteAllTextAsync(
            path,
            $"[Accessibility]{Environment.NewLine}ReducedCameraShake={shake}");
    }

    private static Task WriteSaveAsync(
        string versionRoot,
        string instanceId,
        string slot,
        string json)
    {
        var path = Path.Combine(
            versionRoot,
            ".crystalfly",
            "instances",
            instanceId,
            "local-low",
            slot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return SaveFileCodec.EncryptAsync(path, json);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var timeout = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow >= timeout)
            {
                throw new TimeoutException("The editor did not finish loading.");
            }

            await Task.Delay(10);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

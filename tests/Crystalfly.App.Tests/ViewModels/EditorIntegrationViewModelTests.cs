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

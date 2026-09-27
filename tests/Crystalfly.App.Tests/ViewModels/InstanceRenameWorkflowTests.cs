using System.Reflection;
using Crystalfly.App.ViewModels;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class InstanceRenameWorkflowTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Crystalfly.Tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(InstancePurpose.General, "Manage")]
    [InlineData(InstancePurpose.OfficialSpeedrun, "Speedrun")]
    public async Task Rename_refreshes_selection_and_keeps_the_matching_workspace(InstancePurpose purpose, string expectedPage)
    {
        var original = await CreateInstanceAsync(purpose);
        await using var viewModel = CreateViewModel(original);

        await viewModel.Instances.RenameInstanceCommand.ExecuteAsync("新环境名称");

        Assert.Null(viewModel.ErrorMessage);
        Assert.False(viewModel.IsBusy);
        Assert.Equal(expectedPage, viewModel.CurrentPage);
        var selected = Assert.IsType<InstanceItemViewModel>(viewModel.SelectedInstance);
        Assert.Equal(original.Id, selected.Id);
        Assert.Equal("新环境名称", selected.Name);
        Assert.Equal(Path.Combine(root, "versions", "新环境名称"), selected.RootPath);
        Assert.Equal(original with { Name = selected.Name, RootPath = selected.RootPath }, selected.Record);
        Assert.False(Directory.Exists(original.RootPath));
        if (purpose == InstancePurpose.OfficialSpeedrun)
        {
            Assert.Equal(selected, viewModel.SelectedSpeedrunInstance);
            Assert.Equal(selected, Assert.Single(viewModel.Instances.SpeedrunInstances));
            Assert.Null(viewModel.SpeedrunReportPath);
        }
    }

    [Fact]
    public async Task Rejected_speedrun_rename_preserves_selection_and_shows_the_error()
    {
        var original = await CreateInstanceAsync(InstancePurpose.OfficialSpeedrun);
        Directory.CreateDirectory(Path.Combine(root, "versions", "Existing"));
        await using var viewModel = CreateViewModel(original);

        await viewModel.Instances.RenameInstanceCommand.ExecuteAsync("Existing");

        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
        Assert.False(viewModel.IsBusy);
        Assert.Equal("Speedrun", viewModel.CurrentPage);
        Assert.Equal(original, viewModel.SelectedSpeedrunInstance?.Record);
        Assert.Equal(original, await InstanceSidecar.LoadAsync(original.RootPath));
    }

    private async Task<InstanceRecord> CreateInstanceAsync(InstancePurpose purpose)
    {
        string source = Directory.CreateDirectory(Path.Combine(root, "versions", "Original")).FullName;
        var record = new InstanceRecord
        {
            Id = "rename-workflow",
            Name = "Original",
            RootPath = source,
            BuildId = "1.5.78.11833",
            Purpose = purpose,
            SpeedrunTemplateId = purpose == InstancePurpose.OfficialSpeedrun ? "runtime-patches-1578" : null,
            SpeedrunRulesRevision = "test-rules",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await InstanceSidecar.SaveAsync(record);
        return record;
    }

    private MainViewModel CreateViewModel(InstanceRecord record)
    {
        var viewModel = new MainViewModel(Path.Combine(root, "app-data"));
        SetPrivateField(viewModel, "sharedLocalLowPathOverride", Path.Combine(root, "local-low"));
        SetPrivateField(viewModel, "instanceDiscovery",
            new Func<string, GameCatalog, CancellationToken, Task<IReadOnlyList<InstanceRecord>>>(
                async (versionRoot, _, cancellationToken) =>
                {
                    var records = new List<InstanceRecord>();
                    foreach (string directory in Directory.EnumerateDirectories(versionRoot))
                    {
                        if (await InstanceSidecar.LoadAsync(directory, cancellationToken) is { } discovered)
                        {
                            records.Add(discovered);
                        }
                    }
                    return records;
                }));
        viewModel.VersionRoot = Path.Combine(root, "versions");
        var item = new InstanceItemViewModel(record, record.BuildId, "Vanilla", 0);
        viewModel.Instances.Instances.Add(item);
        viewModel.SelectedInstance = item;
        if (record.Purpose == InstancePurpose.OfficialSpeedrun)
        {
            viewModel.Instances.SpeedrunInstances.Add(item);
            viewModel.SelectedSpeedrunInstance = item;
            viewModel.CurrentPage = "Speedrun";
            viewModel.SpeedrunReportPath = Path.Combine(record.RootPath, "old-report.json");
        }
        return viewModel;
    }

    private static void SetPrivateField(MainViewModel viewModel, string name, object value)
    {
        var field = typeof(MainViewModel).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(viewModel, value);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

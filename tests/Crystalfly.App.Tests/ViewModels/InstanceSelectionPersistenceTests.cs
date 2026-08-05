using Crystalfly.App.ViewModels;
using Crystalfly.Core.Catalog;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;
using System.Reflection;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class InstanceSelectionPersistenceTests
{
    [Fact]
    public async Task Restart_restores_last_regular_instance_without_being_overridden_by_speedrun_instances()
    {
        using var test = new TestDirectory();
        var appData = test.CreateDirectory("app-data");
        var versionRoot = test.CreateDirectory("versions");
        var first = Instance("first", test.CreateDirectory("versions", "first"));
        var second = Instance("second", test.CreateDirectory("versions", "second"));
        var speedrun = Instance("speedrun", test.CreateDirectory("versions", "speedrun")) with
        {
            Purpose = InstancePurpose.OfficialSpeedrun,
            SpeedrunTemplateId = "template-1"
        };

        // The user's last session selected the regular "second" instance.
        await CrystalflySettingsStore.SaveAsync(
            Path.Combine(appData, "settings.json"),
            new CrystalflySettings
            {
                VersionRoot = versionRoot,
                CurrentInstanceId = second.Id
            });

        // First session: initialize, confirm the selection, then dispose (persists).
        var firstViewModel = CreateViewModel(appData, first, second, speedrun);
        await firstViewModel.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(second.Id, firstViewModel.SelectedInstance?.Id);
        await firstViewModel.DisposeAsync();

        // Second session must restore the same regular instance, not fall through to a speedrun one.
        await using var secondViewModel = CreateViewModel(appData, first, second, speedrun);
        await secondViewModel.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(second.Id, secondViewModel.SelectedInstance?.Id);
        Assert.Contains(secondViewModel.Instances.SpeedrunInstances, instance => instance.Id == speedrun.Id);
    }

    [Fact]
    public async Task Restart_restores_last_speedrun_instance_when_it_was_selected()
    {
        using var test = new TestDirectory();
        var appData = test.CreateDirectory("app-data");
        var versionRoot = test.CreateDirectory("versions");
        var regular = Instance("regular", test.CreateDirectory("versions", "regular"));
        var speedrun = Instance("speedrun", test.CreateDirectory("versions", "speedrun")) with
        {
            Purpose = InstancePurpose.OfficialSpeedrun,
            SpeedrunTemplateId = "template-1"
        };
        await CrystalflySettingsStore.SaveAsync(
            Path.Combine(appData, "settings.json"),
            new CrystalflySettings
            {
                VersionRoot = versionRoot,
                CurrentInstanceId = speedrun.Id
            });

        await using var viewModel = CreateViewModel(appData, regular, speedrun);
        await viewModel.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(15));

        Assert.Equal(speedrun.Id, viewModel.SelectedSpeedrunInstance?.Id);
        Assert.Equal(speedrun.Id, viewModel.SelectedInstance?.Id);
    }

    private static MainViewModel CreateViewModel(
        string appData,
        params InstanceRecord[] records)
    {
        var viewModel = new MainViewModel(appData);
        SetPrivateField(
            viewModel,
            "catalogLoader",
            new Func<CancellationToken, Task<GameCatalog>>(_ => Task.FromResult(new GameCatalog())));
        SetPrivateField(viewModel, "steamReconnect", new Func<Task>(() => Task.CompletedTask));
        SetPrivateField(
            viewModel,
            "instanceDiscovery",
            new Func<string, GameCatalog, CancellationToken, Task<IReadOnlyList<InstanceRecord>>>(
                (_, _, _) => Task.FromResult<IReadOnlyList<InstanceRecord>>(records)));
        return viewModel;
    }

    private static InstanceRecord Instance(string id, string rootPath) => new()
    {
        Id = id,
        Name = id,
        RootPath = rootPath,
        BuildId = "build-1",
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static void SetPrivateField(MainViewModel viewModel, string name, object value)
    {
        var field = typeof(MainViewModel).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(viewModel, value);
    }

    private sealed class TestDirectory : IDisposable
    {
        private readonly string root = Path.Combine(
            Path.GetTempPath(),
            "Crystalfly.Tests",
            Guid.NewGuid().ToString("N"));

        public string CreateDirectory(params string[] parts)
        {
            var path = parts.Aggregate(root, Path.Combine);
            Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}

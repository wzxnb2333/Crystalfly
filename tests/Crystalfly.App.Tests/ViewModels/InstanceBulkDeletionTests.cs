using Crystalfly.App.ViewModels;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class InstanceBulkDeletionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "crystalfly-bulk-delete-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Selection_toggles_without_launching_and_filtering_clears_hidden_targets()
    {
        await using var model = new MainViewModel(Path.Combine(root, "app"));
        var items = AddInstances(model, GameId.HollowKnight);
        model.SelectedInstance = items[0];
        model.CurrentPage = "Versions";
        model.Instances.IsSelectingInstances = true;

        model.Instances.SelectInstanceForLaunchCommand.Execute(items[1]);
        Assert.Same(items[0], model.SelectedInstance);
        Assert.Equal("Versions", model.CurrentPage);
        Assert.Equal(new[] { items[1] }, model.Instances.SelectedInstances);
        model.Instances.SelectInstanceForLaunchCommand.Execute(items[1]);
        Assert.False(model.Instances.HasSelectedInstances);
        model.Instances.SelectAllInstancesCommand.Execute(null);
        Assert.Equal(3, model.Instances.SelectedInstanceCount);

        model.SearchText = "second";
        Assert.False(model.Instances.HasSelectedInstances);
        model.Instances.SelectAllInstancesCommand.Execute(null);
        Assert.Equal(new[] { items[1] }, model.Instances.SelectedInstances);
        model.Instances.IsSelectingInstances = false;
        Assert.False(model.Instances.HasSelectedInstances);
    }

    [Theory]
    [InlineData(GameId.HollowKnight)]
    [InlineData(GameId.Silksong)]
    public async Task Bulk_delete_checks_each_target_and_preserves_unselected_instances(GameId game)
    {
        var deleted = new List<string>();
        await using var model = new MainViewModel(Path.Combine(root, "app"),
            instanceDeletionOverride: async (record, evaluate, token) =>
            {
                var conditions = await evaluate(token);
                Assert.False(conditions.HasBlockingQueueTasks);
                Assert.True(conditions.TransactionsHealthy);
                deleted.Add(record.Id);
                return Result(record);
            });
        var items = AddInstances(model, game);
        model.SelectedInstance = items[0];
        model.CurrentPage = "Versions";
        model.Instances.IsSelectingInstances = true;
        model.Instances.SelectInstanceForLaunchCommand.Execute(items[0]);
        model.Instances.SelectInstanceForLaunchCommand.Execute(items[1]);

        await model.Instances.DeleteSelectedInstancesCommand.ExecuteAsync(items[..2]);

        Assert.Equal(new[] { "first", "second" }, deleted);
        Assert.Same(items[2], Assert.Single(model.Instances.Instances));
        Assert.Same(items[2], model.SelectedInstance);
        Assert.False(model.Instances.HasSelectedInstances);
        Assert.Equal("Versions", model.CurrentPage);
        Assert.False(model.IsBusy);
        Assert.Null(model.ErrorMessage);
    }

    [Fact]
    public async Task Failure_stops_the_batch_and_keeps_remaining_targets_selected()
    {
        var attempts = new List<string>();
        await using var model = new MainViewModel(Path.Combine(root, "app"),
            instanceDeletionOverride: async (record, evaluate, token) =>
            {
                await evaluate(token);
                attempts.Add(record.Id);
                if (record.Id == "second") throw new IOException("Locked instance");
                return Result(record);
            });
        var items = AddInstances(model, GameId.HollowKnight);
        model.CurrentPage = "Versions";
        model.Instances.IsSelectingInstances = true;
        model.Instances.SelectAllInstancesCommand.Execute(null);

        await model.Instances.DeleteSelectedInstancesCommand.ExecuteAsync(items);

        Assert.Equal(new[] { "first", "second" }, attempts);
        Assert.Equal(items[1..], model.Instances.Instances);
        Assert.Equal(items[1..], model.Instances.SelectedInstances);
        Assert.False(model.IsBusy);
        Assert.Equal("Versions", model.CurrentPage);
        Assert.NotNull(model.ErrorMessage);
    }

    [Fact]
    public async Task Stale_confirmation_and_busy_state_cannot_delete_instances()
    {
        var calls = 0;
        await using var model = new MainViewModel(Path.Combine(root, "app"),
            instanceDeletionOverride: (record, _, _) => { calls++; return Task.FromResult(Result(record)); });
        var items = AddInstances(model, GameId.HollowKnight);
        model.Instances.IsSelectingInstances = true;
        model.Instances.SelectAllInstancesCommand.Execute(null);
        model.IsBusy = true;
        await model.Instances.DeleteSelectedInstancesCommand.ExecuteAsync(items);
        model.IsBusy = false;
        model.Instances.ApplyInstanceFilter();
        await model.Instances.DeleteSelectedInstancesCommand.ExecuteAsync(items);

        Assert.Equal(0, calls);
        Assert.Equal(3, model.Instances.Instances.Count);
    }

    private InstanceItemViewModel[] AddInstances(MainViewModel model, GameId game)
    {
        model.VersionRoot = Path.Combine(root, "versions");
        Directory.CreateDirectory(model.VersionRoot);
        var items = new[] { "first", "second", "third" }.Select(id => new InstanceItemViewModel(new InstanceRecord
        {
            Id = id, Name = id, GameId = game, RootPath = Path.Combine(model.VersionRoot, id), BuildId = "1.0"
        }, "1.0", "Vanilla", 0)).ToArray();
        foreach (var item in items) model.Instances.Instances.Add(item);
        model.Instances.ApplyInstanceFilter();
        return items;
    }

    private static InstanceDeletionResult Result(InstanceRecord record) => new(record.Id, record.RootPath, "pending", true, null);

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}

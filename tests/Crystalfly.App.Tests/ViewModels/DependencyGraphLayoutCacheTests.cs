using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.DependencyGraph;
using static Crystalfly.App.Tests.ViewModels.DependencyGraphTestHelpers;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class DependencyGraphLayoutCacheTests
{
    [Fact]
    public async Task Rebuild_restores_positions_from_cache_when_the_layout_file_is_unavailable()
    {
        using var temp = TempDirectory.Create();
        var layoutPath = Path.Combine(temp.DirectoryPath, "layout.json");
        await DependencyGraphLayoutStore.WriteAsync(layoutPath, new DependencyGraphLayout
        {
            Positions = { ["a"] = new(120, 240) }
        });
        var viewModel = new DependencyGraphViewModel(Dependencies(
            getLayoutPath: _ => layoutPath,
            getSelectedInstanceId: () => "inst"));
        viewModel.Rebuild([Mod("a"), Mod("b")], selectedId: null, instanceId: "inst");
        await WaitUntilAsync(() => viewModel.Graph.Nodes.First(node => node.Id == "a").X == 120);

        // A content change triggers a rebuild; the saved positions must still be
        // restored without re-reading the (now deleted) layout file.
        File.Delete(layoutPath);
        viewModel.Rebuild([Mod("a"), Mod("b", dependencies: ["a"])], selectedId: null, instanceId: "inst");

        await WaitUntilAsync(() => viewModel.Graph.Nodes.First(node => node.Id == "a").X == 120);
        Assert.Equal(120, viewModel.Graph.Nodes.First(node => node.Id == "a").X);
    }

    [Fact]
    public async Task Rebuild_for_each_instance_restores_its_own_layout_positions()
    {
        using var temp = TempDirectory.Create();
        var pathA = Path.Combine(temp.DirectoryPath, "instance-a", "layout.json");
        var pathB = Path.Combine(temp.DirectoryPath, "instance-b", "layout.json");
        await DependencyGraphLayoutStore.WriteAsync(pathA, new DependencyGraphLayout
        {
            Positions = { ["a"] = new(120, 240) }
        });
        await DependencyGraphLayoutStore.WriteAsync(pathB, new DependencyGraphLayout
        {
            Positions = { ["a"] = new(360, 480) }
        });
        var selectedInstanceId = "instance-a";
        var viewModel = new DependencyGraphViewModel(Dependencies(
            getLayoutPath: instanceId => instanceId == "instance-a" ? pathA : pathB,
            getSelectedInstanceId: () => selectedInstanceId));

        viewModel.Rebuild([Mod("a")], selectedId: null, instanceId: "instance-a");
        await WaitUntilAsync(() => viewModel.Graph.Nodes.Single().X == 120);
        File.Delete(pathA);

        selectedInstanceId = "instance-b";
        viewModel.Rebuild([Mod("a")], selectedId: null, instanceId: "instance-b");
        await WaitUntilAsync(() => viewModel.Graph.Nodes.Single().X == 360);

        // Switching back restores instance-a's positions from the cache.
        selectedInstanceId = "instance-a";
        viewModel.Rebuild([Mod("a")], selectedId: null, instanceId: "instance-a");
        await WaitUntilAsync(() => viewModel.Graph.Nodes.Single().X == 120);
    }

    [Fact]
    public async Task Rebuild_prunes_expired_entries_from_the_layout_file()
    {
        using var temp = TempDirectory.Create();
        var layoutPath = Path.Combine(temp.DirectoryPath, "layout.json");
        await DependencyGraphLayoutStore.WriteAsync(layoutPath, new DependencyGraphLayout
        {
            Positions = { ["a"] = new(120, 240), ["ghost"] = new(500, 600) }
        });
        var viewModel = new DependencyGraphViewModel(Dependencies(
            getLayoutPath: _ => layoutPath,
            getSelectedInstanceId: () => "inst"));
        viewModel.Rebuild([Mod("a")], selectedId: null, instanceId: "inst");

        await WaitUntilAsync(() => !File.ReadAllText(layoutPath).Contains("ghost", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Commit_position_skips_writing_when_positions_are_unchanged()
    {
        using var temp = TempDirectory.Create();
        var layoutPath = Path.Combine(temp.DirectoryPath, "layout.json");
        await DependencyGraphLayoutStore.WriteAsync(layoutPath, new DependencyGraphLayout
        {
            Positions = { ["a"] = new(120, 240) }
        });
        var viewModel = new DependencyGraphViewModel(Dependencies(
            getLayoutPath: _ => layoutPath,
            getSelectedInstanceId: () => "inst"));
        viewModel.Rebuild([Mod("a")], selectedId: null, instanceId: "inst");
        await WaitUntilAsync(() => viewModel.Graph.Nodes.Single().X == 120);

        // Committing the unchanged positions must not recreate the deleted file.
        File.Delete(layoutPath);
        viewModel.Graph.CommitNodePosition();

        await Task.Delay(500);
        Assert.False(File.Exists(layoutPath));
    }
}

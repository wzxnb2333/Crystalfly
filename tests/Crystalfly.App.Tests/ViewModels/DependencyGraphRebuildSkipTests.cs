using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.DependencyGraph;
using static Crystalfly.App.Tests.ViewModels.DependencyGraphTestHelpers;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class DependencyGraphRebuildSkipTests
{
    [Fact]
    public void Rebuild_with_unchanged_content_keeps_the_existing_graph_instance()
    {
        var viewModel = new DependencyGraphViewModel(Dependencies());
        viewModel.Rebuild([Mod("a"), Mod("b", dependencies: ["a"])], selectedId: null, instanceId: null);
        var firstGraph = viewModel.Graph;

        // A mod refresh recreates the item view models with identical content.
        viewModel.Rebuild([Mod("a"), Mod("b", dependencies: ["a"])], selectedId: null, instanceId: null);

        Assert.Same(firstGraph, viewModel.Graph);
    }

    [Fact]
    public async Task Rebuild_with_unchanged_content_keeps_graph_and_positions_for_an_instance()
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
        var firstGraph = viewModel.Graph;
        await WaitUntilAsync(() => viewModel.Graph.Nodes.First(node => node.Id == "a").X == 120);

        viewModel.Rebuild([Mod("a"), Mod("b")], selectedId: null, instanceId: "inst");

        Assert.Same(firstGraph, viewModel.Graph);
        Assert.Equal(120, viewModel.Graph.Nodes.First(node => node.Id == "a").X);
    }

    [Fact]
    public void Rebuild_after_mod_state_change_creates_a_new_graph()
    {
        var viewModel = new DependencyGraphViewModel(Dependencies());
        viewModel.Rebuild([Mod("a")], selectedId: null, instanceId: null);
        var firstGraph = viewModel.Graph;

        viewModel.Rebuild([Mod("a", enabled: false)], selectedId: null, instanceId: null);

        Assert.NotSame(firstGraph, viewModel.Graph);
        Assert.Equal(DependencyGraphNodeState.Disabled, viewModel.Graph.Nodes.Single().State);
    }

    [Fact]
    public void Rebuild_after_selection_change_creates_a_new_graph()
    {
        var viewModel = new DependencyGraphViewModel(Dependencies());
        viewModel.Rebuild([Mod("a"), Mod("b")], selectedId: "a", instanceId: null);
        var firstGraph = viewModel.Graph;

        viewModel.Rebuild([Mod("a"), Mod("b")], selectedId: "b", instanceId: null);

        Assert.NotSame(firstGraph, viewModel.Graph);
        Assert.Equal("b", viewModel.Graph.Nodes.Single(node => node.IsSelected).Id);
    }

    [Fact]
    public async Task Rebuild_after_content_change_restores_positions_from_the_layout_file()
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

        viewModel.Rebuild([Mod("a"), Mod("b")], selectedId: null, instanceId: "inst");

        await WaitUntilAsync(() => viewModel.Graph.Nodes.First(node => node.Id == "a").X == 120);
        Assert.Equal(120, viewModel.Graph.Nodes.First(node => node.Id == "a").X);
    }
}

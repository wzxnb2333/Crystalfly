using Crystalfly.App.ViewModels.DependencyGraph;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class DependencyGraphModelTests
{
    [Fact]
    public void Graph_places_prerequisites_before_selected_mod_and_dependents_after_it()
    {
        var graph = DependencyGraphModel.Create(
            [
                new DependencyGraphNodeDefinition("core", "Core", "", "Enabled", DependencyGraphNodeState.Normal),
                new DependencyGraphNodeDefinition("feature", "Feature", "", "Enabled", DependencyGraphNodeState.Normal),
                new DependencyGraphNodeDefinition("addon", "Addon", "", "Enabled", DependencyGraphNodeState.Normal)
            ],
            [
                new DependencyGraphEdgeDefinition("core", "feature"),
                new DependencyGraphEdgeDefinition("feature", "addon")
            ],
            "feature");

        var core = Assert.Single(graph.Nodes, node => node.Id == "core");
        var feature = Assert.Single(graph.Nodes, node => node.Id == "feature");
        var addon = Assert.Single(graph.Nodes, node => node.Id == "addon");

        Assert.True(core.X < feature.X);
        Assert.True(feature.X < addon.X);
        Assert.All(graph.Nodes, node => Assert.True(node.IsRelated));
        Assert.True(feature.IsSelected);
    }

    [Fact]
    public void Graph_marks_cycles_and_keeps_their_nodes_selectable()
    {
        var graph = DependencyGraphModel.Create(
            [
                new DependencyGraphNodeDefinition("first", "First", "", "Enabled", DependencyGraphNodeState.Normal),
                new DependencyGraphNodeDefinition("second", "Second", "", "Enabled", DependencyGraphNodeState.Normal)
            ],
            [
                new DependencyGraphEdgeDefinition("first", "second"),
                new DependencyGraphEdgeDefinition("second", "first")
            ],
            "first");

        Assert.All(graph.Nodes, node => Assert.Equal(DependencyGraphNodeState.Cycle, node.State));
        Assert.All(graph.Edges, edge => Assert.True(edge.IsCycle));

        graph.Select("second");

        Assert.True(Assert.Single(graph.Nodes, node => node.Id == "second").IsSelected);
    }
}

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

    [Fact]
    public void Graph_applies_known_saved_positions_and_ignores_expired_nodes()
    {
        var graph = DependencyGraphModel.Create(
            [
                new DependencyGraphNodeDefinition("core", "Core", "", "Enabled", DependencyGraphNodeState.Normal),
                new DependencyGraphNodeDefinition("feature", "Feature", "", "Enabled", DependencyGraphNodeState.Normal)
            ],
            [new DependencyGraphEdgeDefinition("core", "feature")]);

        var applied = graph.ApplySavedPositions(new Dictionary<string, DependencyGraphNodePosition>
        {
            ["core"] = new(480, 320),
            ["removed-mod"] = new(32, 32)
        });

        var core = Assert.Single(graph.Nodes, node => node.Id == "core");
        Assert.True(applied.HadExpiredNodes);
        Assert.Equal(1, applied.AppliedCount);
        Assert.Equal(480, core.X);
        Assert.Equal(320, core.Y);
        Assert.True(graph.Width >= core.X + DependencyGraphModel.NodeWidth + 28);
        Assert.True(graph.Height >= core.Y + DependencyGraphModel.NodeHeight + 28);
    }

    [Fact]
    public void Graph_restores_automatic_layout_after_manual_move()
    {
        var graph = DependencyGraphModel.Create(
            [
                new DependencyGraphNodeDefinition("core", "Core", "", "Enabled", DependencyGraphNodeState.Normal),
                new DependencyGraphNodeDefinition("feature", "Feature", "", "Enabled", DependencyGraphNodeState.Normal)
            ],
            [new DependencyGraphEdgeDefinition("core", "feature")]);
        var core = Assert.Single(graph.Nodes, node => node.Id == "core");
        var original = new DependencyGraphNodePosition(core.X, core.Y);

        graph.MoveNode("core", 620, 420);
        graph.RestoreAutomaticLayout();

        Assert.Equal(original.X, core.X);
        Assert.Equal(original.Y, core.Y);
    }

    [Fact]
    public void Graph_routes_context_actions_only_for_manageable_nodes()
    {
        var graph = DependencyGraphModel.Create(
            [
                new DependencyGraphNodeDefinition(
                    "managed",
                    "Managed",
                    "",
                    "Enabled",
                    DependencyGraphNodeState.Normal,
                    CanToggle: true,
                    ToggleActionLabel: "Disable",
                    CanDelete: true),
                new DependencyGraphNodeDefinition("external", "External", "", "External", DependencyGraphNodeState.External)
            ],
            []);
        var actions = new List<string>();
        graph.NodeToggleRequested = id => actions.Add($"toggle:{id}");
        graph.NodeDeleteRequested = id => actions.Add($"delete:{id}");

        graph.RequestNodeToggle(Assert.Single(graph.Nodes, node => node.Id == "managed"));
        graph.RequestNodeDelete(Assert.Single(graph.Nodes, node => node.Id == "managed"));
        graph.RequestNodeToggle(Assert.Single(graph.Nodes, node => node.Id == "external"));
        graph.RequestNodeDelete(Assert.Single(graph.Nodes, node => node.Id == "external"));

        Assert.Equal(["toggle:managed", "delete:managed"], actions);
    }
}

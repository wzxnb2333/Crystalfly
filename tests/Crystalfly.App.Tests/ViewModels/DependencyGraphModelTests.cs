using Crystalfly.App.ViewModels.DependencyGraph;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class DependencyGraphModelTests
{
    [Fact]
    public void Graph_multiple_selection_preserves_focus_and_can_toggle_or_clear_nodes()
    {
        var graph = DependencyGraphModel.Create(
            [new("a", "A", "", "", DependencyGraphNodeState.Normal),
             new("b", "B", "", "", DependencyGraphNodeState.Normal),
             new("c", "C", "", "", DependencyGraphNodeState.Missing)], [new("a", "b")], "a");
        IReadOnlyList<string>? notified = null;
        graph.SelectionChanged = ids => notified = ids;
        graph.ToggleNodeSelection("b");
        Assert.Equal(2, graph.SelectedNodes.Count);
        Assert.Equal("b", graph.SelectedNode?.Id);
        Assert.Equal(2, notified?.Count);
        graph.FocusNode("a");
        Assert.Equal(2, graph.SelectedNodes.Count);
        graph.ToggleNodeSelection("b");
        Assert.Equal("a", Assert.Single(graph.SelectedNodes).Id);
        graph.SetSelection(["a", "c", "unknown"], notify: true);
        Assert.Equal(2, graph.SelectedNodes.Count);
        graph.SetSelection([], notify: true);
        Assert.Empty(graph.SelectedNodes);
        Assert.Null(graph.SelectedNode);
        Assert.All(graph.Nodes, node => Assert.False(node.IsDimmed));
    }

    [Fact]
    public void Graph_keeps_negative_world_coordinates_when_moving_and_restoring_layout()
    {
        var graph = DependencyGraphModel.Create(
            [new DependencyGraphNodeDefinition("mod", "Mod", "", "Enabled", DependencyGraphNodeState.Normal)], []);
        Assert.True(graph.MoveNode("mod", -320, -180));
        var position = graph.GetPositions()["mod"];
        Assert.Equal(-320, position.X);
        Assert.Equal(-180, position.Y);
        Assert.Equal(DependencyGraphModel.NodeWidth + 2 * DependencyGraphModel.CanvasPadding, graph.Width);
        Assert.Equal(DependencyGraphModel.NodeHeight + 2 * DependencyGraphModel.CanvasPadding, graph.Height);
        graph.RestoreAutomaticLayout();
        graph.ApplySavedPositions(new Dictionary<string, DependencyGraphNodePosition> { ["mod"] = position });
        Assert.Equal(position, graph.GetPositions()["mod"]);
    }
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
        Assert.True(graph.Left + graph.Width >= core.X + DependencyGraphModel.NodeWidth + 28);
        Assert.True(graph.Top + graph.Height >= core.Y + DependencyGraphModel.NodeHeight + 28);
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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Crystalfly.App.ViewModels.DependencyGraph;
using Crystalfly.App.Views.Controls;

namespace Crystalfly.App.Tests.Ui;

public sealed class DependencyGraphViewInteractionTests
{
    [AvaloniaFact]
    public void Graph_supports_modified_click_group_drag_marquee_and_selection_shortcuts()
    {
        var graph = DependencyGraphModel.Create(
            [new("a", "A", "", "", DependencyGraphNodeState.Normal), new("b", "B", "", "", DependencyGraphNodeState.Normal),
             new("c", "C", "", "", DependencyGraphNodeState.Missing)], [], "a");
        graph.ApplySavedPositions(new Dictionary<string, DependencyGraphNodePosition>
        { ["a"] = new(0, 80), ["b"] = new(300, 80), ["c"] = new(0, 280) });
        var view = new DependencyGraphView { DataContext = graph };
        var window = new Window { Width = 900, Height = 650, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        view.ResetViewport();
        try
        {
            Click("b", RawInputModifiers.Control);
            Assert.Equal(2, graph.SelectedNodes.Count);
            var initial = graph.GetPositions();
            var translation = view.Translation;
            var point = NodePoint("a");
            window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
            window.MouseMove(point + new Vector(-90, 40), RawInputModifiers.LeftMouseButton);
            window.MouseUp(point + new Vector(-90, 40), MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(initial["a"].X - 90, graph.GetPositions()["a"].X, 5);
            Assert.Equal(initial["b"].X - 90, graph.GetPositions()["b"].X, 5);
            Assert.Equal(initial["c"], graph.GetPositions()["c"]);
            Assert.Equal(translation, view.Translation);
            Click("b", RawInputModifiers.Control);
            Assert.Equal("a", Assert.Single(graph.SelectedNodes).Id);

            var notifications = 0;
            graph.SelectionChanged = _ => notifications++;
            window.MouseDown(new Point(20, 60), MouseButton.Left, RawInputModifiers.Shift);
            window.MouseMove(new Point(650, 450), RawInputModifiers.Shift | RawInputModifiers.LeftMouseButton);
            Assert.True(view.FindControl<Border>("SelectionRectangle")!.IsVisible);
            window.MouseUp(new Point(650, 450), MouseButton.Left, RawInputModifiers.Shift);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(3, graph.SelectedNodes.Count);
            Assert.Equal(1, notifications);
            Assert.False(view.FindControl<Border>("SelectionRectangle")!.IsVisible);
            Assert.Equal(translation, view.Translation);
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Assert.Empty(graph.SelectedNodes);
            window.KeyPress(Key.A, RawInputModifiers.Control, PhysicalKey.A, null);
            Assert.Equal(3, graph.SelectedNodes.Count);
            Click("b", RawInputModifiers.None);
            Assert.Equal("b", Assert.Single(graph.SelectedNodes).Id);
        }
        finally { window.Close(); }

        Point NodePoint(string id)
        {
            var node = graph.Nodes.Single(item => item.Id == id);
            return new(view.Translation.X + (node.X + 24) * view.Scale, view.Translation.Y + (node.Y + 24) * view.Scale);
        }
        void Click(string id, RawInputModifiers modifiers)
        {
            var point = NodePoint(id);
            window.MouseDown(point, MouseButton.Left, modifiers);
            window.MouseUp(point, MouseButton.Left, modifiers);
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void Graph_wheel_zooms_and_left_drag_pans_the_canvas()
    {
        var graph = DependencyGraphModel.Create(
            [new DependencyGraphNodeDefinition("mod", "Mod", string.Empty, "Enabled", DependencyGraphNodeState.Normal)],
            []);
        var view = new DependencyGraphView { DataContext = graph };
        var window = new Window { Width = 640, Height = 480, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            var initialScale = view.Scale;
            window.MouseWheel(new Point(320, 240), new Vector(0, 1), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.True(view.Scale > initialScale);

            var afterZoom = view.Translation;
            window.MouseDown(new Point(40, 40), MouseButton.Left, RawInputModifiers.None);
            window.MouseMove(new Point(88, 74), RawInputModifiers.LeftMouseButton);
            window.MouseUp(new Point(88, 74), MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.NotEqual(afterZoom, view.Translation);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Graph_left_drag_moves_a_card_without_panning_the_canvas()
    {
        var graph = DependencyGraphModel.Create(
            [new DependencyGraphNodeDefinition("mod", "Mod", string.Empty, "Enabled", DependencyGraphNodeState.Normal)],
            []);
        var node = Assert.Single(graph.Nodes);
        var view = new DependencyGraphView { DataContext = graph };
        var window = new Window { Width = 640, Height = 480, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            var nodeStart = new Point(
                view.Translation.X + (node.X + 24) * view.Scale,
                view.Translation.Y + (node.Y + 24) * view.Scale);
            var originalX = node.X;
            var originalY = node.Y;
            var translation = view.Translation;

            window.MouseDown(nodeStart, MouseButton.Left, RawInputModifiers.None);
            var nodeEnd = nodeStart + new Vector(72, 48);
            window.MouseMove(nodeEnd, RawInputModifiers.LeftMouseButton);
            window.MouseUp(nodeEnd, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.True(node.X > originalX);
            Assert.True(node.Y > originalY);
            Assert.Equal(translation, view.Translation);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Graph_dragging_a_node_beyond_the_top_left_edge_does_not_pan_the_canvas()
    {
        var graph = DependencyGraphModel.Create(
            [new DependencyGraphNodeDefinition("mod", "Mod", string.Empty, "Enabled", DependencyGraphNodeState.Normal)],
            []);
        var node = Assert.Single(graph.Nodes);
        var view = new DependencyGraphView { DataContext = graph };
        var window = new Window { Width = 640, Height = 480, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            var nodeStart = new Point(
                view.Translation.X + (node.X + 24) * view.Scale,
                view.Translation.Y + (node.Y + 24) * view.Scale);
            var initialTranslation = view.Translation;

            window.MouseDown(nodeStart, MouseButton.Left, RawInputModifiers.None);
            var nodeEnd = nodeStart + new Vector(-100, -100);
            window.MouseMove(nodeEnd, RawInputModifiers.LeftMouseButton);
            window.MouseUp(nodeEnd, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.True(node.X < 0 && node.Y < 0);
            Assert.Equal(initialTranslation, view.Translation);

            // The card must remain hit-testable after it crosses the world origin.
            window.MouseDown(nodeEnd, MouseButton.Left, RawInputModifiers.None);
            var nextEnd = nodeEnd + new Vector(40, 20);
            var previousX = node.X;
            window.MouseMove(nextEnd, RawInputModifiers.LeftMouseButton);
            window.MouseUp(nextEnd, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(previousX + 40 / view.Scale, node.X, 5);
            Assert.Equal(initialTranslation, view.Translation);
        }
        finally
        {
            window.Close();
        }
    }
}

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
}

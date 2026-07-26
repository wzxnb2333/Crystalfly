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
}

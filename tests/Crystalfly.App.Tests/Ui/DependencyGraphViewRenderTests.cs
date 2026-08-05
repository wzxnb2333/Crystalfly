using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Crystalfly.App.ViewModels.DependencyGraph;
using Crystalfly.App.Views.Controls;
using SkiaSharp;
using System.IO;

namespace Crystalfly.App.Tests.Ui;

public sealed class DependencyGraphViewRenderTests
{
    [AvaloniaFact]
    public void DependencyGraphEdges_renders_edges_with_the_line_brush_color()
    {
        var previousTheme = Application.Current!.RequestedThemeVariant;
        Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
        Window? window = null;
        try
        {
            var source = new DependencyGraphNodeViewModel(
                new DependencyGraphNodeDefinition("a", "A", string.Empty, "Enabled", DependencyGraphNodeState.Normal));
            var target = new DependencyGraphNodeViewModel(
                new DependencyGraphNodeDefinition("b", "B", string.Empty, "Enabled", DependencyGraphNodeState.Normal));
            source.SetPosition(28, 28);
            target.SetPosition(352, 28);
            var edge = new DependencyGraphEdgeViewModel(source, target);
            var control = new DependencyGraphEdges
            {
                Width = 608,
                Height = 138,
                Edges = [edge],
                LineBrush = new SolidColorBrush(Color.Parse("#565C66")),
                HighlightBrush = new SolidColorBrush(Colors.Red),
                ErrorBrush = new SolidColorBrush(Colors.Red)
            };
            window = new Window
            {
                Width = 700,
                Height = 220,
                Background = new SolidColorBrush(Colors.Black),
                Content = control
            };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            Dispatcher.UIThread.RunJobs();

            // Edge midpoint in graph coordinates: ((28 + NodeWidth + 352) / 2, 28 + NodeHeight / 2) = (304, 69).
            var frame = Assert.IsType<WriteableBitmap>(window.GetLastRenderedFrame());
            using var bitmap = Decode(frame);
            var screen = control.TranslatePoint(new Point(304, 69), window) ?? throw new InvalidOperationException();
            var pixel = bitmap.GetPixel((int)Math.Round(screen.X), (int)Math.Round(screen.Y));

            // CfEdgeBrush dark-theme value is #565C66; the midpoint must be painted with it rather than
            // left transparent (black window background) or painted with the highlight/error brushes.
            Assert.True(
                Math.Abs(pixel.Red - 0x56) <= 25
                    && Math.Abs(pixel.Green - 0x5C) <= 25
                    && Math.Abs(pixel.Blue - 0x66) <= 25,
                $"expected line brush color #565C66 at ({screen.X:F0},{screen.Y:F0}), got pixel RGB({pixel.Red},{pixel.Green},{pixel.Blue})");
        }
        finally
        {
            window?.Close();
            Application.Current.RequestedThemeVariant = previousTheme;
        }
    }

    [AvaloniaFact]
    public void DependencyGraphView_renders_a_highlighted_edge_with_the_accent_color()
    {
        var previousTheme = Application.Current!.RequestedThemeVariant;
        Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
        Window? window = null;
        try
        {
            var graph = DependencyGraphModel.Create(
                [
                    new DependencyGraphNodeDefinition("a", "Node A", "A", "Enabled", DependencyGraphNodeState.Normal),
                    new DependencyGraphNodeDefinition("b", "Node B", "B", "Enabled", DependencyGraphNodeState.Normal)
                ],
                [new DependencyGraphEdgeDefinition("a", "b")]);
            var view = new DependencyGraphView { DataContext = graph };
            window = new Window { Width = 900, Height = 600, Content = view };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            Dispatcher.UIThread.RunJobs();

            // The model selects the first node by default, which highlights the connected edge.
            Assert.True(graph.Edges.Single().IsHighlighted);
            var nodeA = graph.Nodes.Single(node => node.Id == "a");
            var nodeB = graph.Nodes.Single(node => node.Id == "b");
            var edgeMidpoint = new Point(
                (nodeA.X + DependencyGraphModel.NodeWidth + nodeB.X) / 2,
                nodeA.Y + DependencyGraphModel.NodeHeight / 2);
            var screen = new Point(
                edgeMidpoint.X * view.Scale + view.Translation.X,
                edgeMidpoint.Y * view.Scale + view.Translation.Y);

            var frame = Assert.IsType<WriteableBitmap>(window.GetLastRenderedFrame());
            using var bitmap = Decode(frame);
            var pixel = bitmap.GetPixel((int)Math.Round(screen.X), (int)Math.Round(screen.Y));

            // Highlighted edges use the accent color (#5AAEFA in the dark theme).
            Assert.True(
                Math.Abs(pixel.Red - 0x5A) <= 25
                    && Math.Abs(pixel.Green - 0xAE) <= 25
                    && Math.Abs(pixel.Blue - 0xFA) <= 25,
                $"expected accent color #5AAEFA at ({screen.X:F1},{screen.Y:F1}) scale={view.Scale:F3} translation={view.Translation}, got pixel RGB({pixel.Red},{pixel.Green},{pixel.Blue})");
        }
        finally
        {
            window?.Close();
            Application.Current.RequestedThemeVariant = previousTheme;
        }
    }

    [AvaloniaFact]
    public void Node_title_text_starts_near_the_left_edge_of_the_card()
    {
        var graph = DependencyGraphModel.Create(
            [
                new DependencyGraphNodeDefinition("a", "Node A", "A", "Enabled", DependencyGraphNodeState.Normal),
                new DependencyGraphNodeDefinition("b", "Node B", "B", "Enabled", DependencyGraphNodeState.Normal)
            ],
            [new DependencyGraphEdgeDefinition("a", "b")]);
        var view = new DependencyGraphView { DataContext = graph };
        var window = new Window { Width = 900, Height = 600, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            var card = window.GetVisualDescendants()
                .OfType<Button>()
                .Single(button => button.DataContext is DependencyGraphNodeViewModel { Id: "a" });
            var title = window.GetVisualDescendants()
                .OfType<TextBlock>()
                .Single(text => text.Text == "Node A");
            var titleInCard = title.TranslatePoint(new Point(0, 0), card);
            Assert.NotNull(titleInCard);
            // Title sits after the 8px accent bar plus the 11px content margin, so its left edge
            // must stay inside the first quarter of the card rather than floating toward the middle.
            Assert.InRange(titleInCard.Value.X, 0d, DependencyGraphModel.NodeWidth / 4);
        }
        finally
        {
            window.Close();
        }
    }

    private static SKBitmap Decode(WriteableBitmap frame)
    {
        using var stream = new MemoryStream();
        frame.Save(stream, new PngBitmapEncoderOptions());
        stream.Position = 0;
        return SKBitmap.Decode(stream);
    }
}

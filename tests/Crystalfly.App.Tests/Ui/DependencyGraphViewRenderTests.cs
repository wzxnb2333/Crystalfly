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
    private const string EdgeColor = "#565C66";
    private const string AccentColor = "#5AAEFA";

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Multiple_selection_is_visible_on_normal_missing_and_warning_cards(bool dark)
    {
        var previous = Application.Current!.RequestedThemeVariant;
        Application.Current.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var graph = DependencyGraphModel.Create([
            new("normal", "Normal", "", "Enabled", DependencyGraphNodeState.Normal),
            new("missing", "Missing", "", "Missing", DependencyGraphNodeState.Missing),
            new("disabled", "Disabled", "", "Disabled", DependencyGraphNodeState.Disabled)], []);
        graph.SetSelection(graph.Nodes.Select(node => node.Id));
        var window = new Window { Width = 900, Height = 650, Content = new DependencyGraphView { DataContext = graph } };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var cards = window.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("cfp-dependency-node")).ToArray();
            Assert.Equal(3, cards.Length);
            Assert.True(Application.Current.TryGetResource("CfAccentBrush", dark ? ThemeVariant.Dark : ThemeVariant.Light, out var resource));
            var accent = Assert.IsAssignableFrom<ISolidColorBrush>(resource);
            Assert.All(cards, card =>
            {
                Assert.Equal(new Thickness(2), card.BorderThickness);
                Assert.Equal(accent.Color, Assert.IsAssignableFrom<ISolidColorBrush>(card.BorderBrush).Color);
            });
        }
        finally { window.Close(); Application.Current.RequestedThemeVariant = previous; }
    }

    [AvaloniaFact]
    public void DependencyGraphEdges_renders_regular_edges_with_the_line_brush_color()
    {
        // A regular edge (not highlighted, not dimmed, not a cycle) must be painted with LineBrush.
        AssertEdgeColor(highlighted: false, EdgeColor);
    }

    [AvaloniaFact]
    public void DependencyGraphEdges_renders_highlighted_edges_with_the_highlight_brush_color()
    {
        // A highlighted edge (the model selects a node by default) must use the accent highlight brush.
        AssertEdgeColor(highlighted: true, AccentColor);
    }

    [AvaloniaFact]
    public void DependencyGraphEdges_keeps_edges_visible_after_nodes_cross_the_world_origin()
    {
        AssertEdgeColor(highlighted: true, AccentColor, negativeWorldPositions: true);
    }

    [AvaloniaFact]
    public void DependencyGraphView_selecting_a_node_highlights_its_connected_edges()
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
            // The model auto-selects the first node, which marks the connected edge as highlighted.
            var edge = Assert.Single(graph.Edges);
            Assert.True(edge.IsHighlighted);
            Assert.False(edge.IsDimmed);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Initial_framing_keeps_a_tall_graph_against_the_viewport_instead_of_leaving_empty_space()
    {
        var definitions = Enumerable.Range(0, 10).Select(index => new DependencyGraphNodeDefinition(
            $"node{index:D2}", "Node " + index, "", "Enabled", DependencyGraphNodeState.Normal)).ToArray();
        var graph = DependencyGraphModel.Create(definitions, [], "node09");
        var view = new DependencyGraphView { DataContext = graph };
        var window = new Window { Width = 650, Height = 400, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            Assert.True(view.Scale >= 0.7);
            Assert.True(view.Translation.Y + (graph.Top + graph.Height) * view.Scale >= view.Bounds.Height - 1);
            Assert.True(view.Translation.Y + graph.Top * view.Scale <= 1);
        }
        finally { window.Close(); }
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
            // Keep the title inside the first quarter and avoid decorative sidebars.
            Assert.DoesNotContain(card.GetVisualDescendants().OfType<Border>(), border => border.Classes.Contains("cfp-dependency-node-accent"));
            Assert.InRange(titleInCard.Value.X, 0d, DependencyGraphModel.NodeWidth / 4);
        }
        finally
        {
            window.Close();
        }
    }

    private static void AssertEdgeColor(bool highlighted, string expectedHex, bool negativeWorldPositions = false)
    {
        var previousTheme = Application.Current!.RequestedThemeVariant;
        Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
        Window? window = null;
        try
        {
            var graph = DependencyGraphModel.Create(
                [new DependencyGraphNodeDefinition("a", "A", string.Empty, "Enabled", DependencyGraphNodeState.Normal),
                 new DependencyGraphNodeDefinition("b", "B", string.Empty, "Enabled", DependencyGraphNodeState.Normal)],
                [new DependencyGraphEdgeDefinition("a", "b")]);
            if (negativeWorldPositions)
            {
                graph.MoveNode("a", -320, -180);
                graph.MoveNode("b", 4, -180);
            }
            var edge = Assert.Single(graph.Edges);
            edge.IsHighlighted = highlighted;
            var control = new DependencyGraphEdges
            {
                Width = 608,
                Height = 138,
                Edges = [edge],
                LineBrush = new SolidColorBrush(Color.Parse(EdgeColor)),
                HighlightBrush = new SolidColorBrush(Color.Parse(AccentColor)),
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
            var expected = Color.Parse(expectedHex);

            Assert.True(
                Math.Abs(pixel.Red - expected.R) <= 25
                    && Math.Abs(pixel.Green - expected.G) <= 25
                    && Math.Abs(pixel.Blue - expected.B) <= 25,
                $"expected edge color {expectedHex} at ({screen.X:F0},{screen.Y:F0}), got pixel RGB({pixel.Red},{pixel.Green},{pixel.Blue})");
        }
        finally
        {
            window?.Close();
            Application.Current.RequestedThemeVariant = previousTheme;
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

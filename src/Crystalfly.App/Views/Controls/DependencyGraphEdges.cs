using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Crystalfly.App.ViewModels.DependencyGraph;

namespace Crystalfly.App.Views.Controls;

public sealed class DependencyGraphEdges : Control
{
    private IReadOnlyList<DependencyGraphEdgeViewModel>? subscribedEdges;
    private IReadOnlyList<DependencyGraphNodeViewModel>? subscribedNodes;
    public static readonly StyledProperty<IReadOnlyList<DependencyGraphEdgeViewModel>?> EdgesProperty =
        AvaloniaProperty.Register<DependencyGraphEdges, IReadOnlyList<DependencyGraphEdgeViewModel>?>(nameof(Edges));

    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<DependencyGraphEdges, IBrush?>(nameof(LineBrush));

    public static readonly StyledProperty<IBrush?> HighlightBrushProperty =
        AvaloniaProperty.Register<DependencyGraphEdges, IBrush?>(nameof(HighlightBrush));

    public static readonly StyledProperty<IBrush?> ErrorBrushProperty =
        AvaloniaProperty.Register<DependencyGraphEdges, IBrush?>(nameof(ErrorBrush));

    public static readonly StyledProperty<double> RevealProgressProperty =
        AvaloniaProperty.Register<DependencyGraphEdges, double>(nameof(RevealProgress), 1);

    public double RevealProgress
    {
        get => GetValue(RevealProgressProperty);
        set => SetValue(RevealProgressProperty, value);
    }

    static DependencyGraphEdges()
    {
        AffectsRender<DependencyGraphEdges>(EdgesProperty, LineBrushProperty, HighlightBrushProperty, ErrorBrushProperty, RevealProgressProperty);
        EdgesProperty.Changed.AddClassHandler<DependencyGraphEdges>((control, args) =>
            control.Subscribe(control.subscribedEdges, control.Edges));
    }

    public IReadOnlyList<DependencyGraphEdgeViewModel>? Edges
    {
        get => GetValue(EdgesProperty);
        set => SetValue(EdgesProperty, value);
    }

    public IBrush? LineBrush
    {
        get => GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    public IBrush? HighlightBrush
    {
        get => GetValue(HighlightBrushProperty);
        set => SetValue(HighlightBrushProperty, value);
    }

    public IBrush? ErrorBrush
    {
        get => GetValue(ErrorBrushProperty);
        set => SetValue(ErrorBrushProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Edges is not { Count: > 0 } edges || LineBrush is null)
        {
            return;
        }

        foreach (var edge in edges.Where(edge => edge.IsDimmed))
        {
            DrawEdge(context, edge, LineBrush, 2, 0.8);
        }
        foreach (var edge in edges.Where(edge => !edge.IsDimmed && !edge.IsCycle))
        {
            DrawEdge(context, edge, edge.IsHighlighted ? HighlightBrush ?? LineBrush : LineBrush, edge.IsHighlighted ? 3 : 2.5, 1);
        }
        foreach (var edge in edges.Where(edge => edge.IsCycle))
        {
            DrawEdge(context, edge, ErrorBrush ?? HighlightBrush ?? LineBrush, 3, 1);
        }
    }

    private void DrawEdge(
        DrawingContext context,
        DependencyGraphEdgeViewModel edge,
        IBrush brush,
        double thickness,
        double opacity)
    {
        var start = new Point(
            edge.Source.CanvasX + DependencyGraphModel.NodeWidth,
            edge.Source.CanvasY + DependencyGraphModel.NodeHeight / 2);
        var end = new Point(
            edge.Target.CanvasX,
            edge.Target.CanvasY + DependencyGraphModel.NodeHeight / 2);
        var curve = Math.Max(34, Math.Abs(end.X - start.X) * 0.45);
        var control1 = new Point(start.X + curve, start.Y);
        var control2 = new Point(end.X - curve, end.Y);
        var progress = Math.Clamp(RevealProgress, 0, 1);
        // Subdivide the actual curve, so the drawn endpoint stays on its final path.
        var a = Mix(start, control1, progress);
        var b = Mix(control1, control2, progress);
        var c = Mix(control2, end, progress);
        var d = Mix(a, b, progress);
        var e = Mix(b, c, progress);
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            path.BeginFigure(start, false);
            path.CubicBezierTo(a, d, Mix(d, e, progress));
        }
        using (context.PushOpacity(opacity))
        {
            context.DrawGeometry(null, new Pen(brush, thickness, lineCap: PenLineCap.Round), geometry);
            if (progress >= 0.99)
            {
                context.DrawLine(new Pen(brush, thickness), end + new Vector(-8, -4), end);
                context.DrawLine(new Pen(brush, thickness), end + new Vector(-8, 4), end);
            }
        }
    }

    private static Point Mix(Point from, Point to, double progress) => from + (to - from) * progress;

    private void Subscribe(
        IReadOnlyList<DependencyGraphEdgeViewModel>? oldEdges,
        IReadOnlyList<DependencyGraphEdgeViewModel>? newEdges)
    {
        if (oldEdges is not null)
        {
            foreach (var edge in oldEdges)
            {
                edge.PropertyChanged -= OnEdgeChanged;
            }
        }
        if (subscribedNodes is not null)
        {
            foreach (var node in subscribedNodes)
            {
                node.PropertyChanged -= OnNodeChanged;
            }
        }
        if (newEdges is not null)
        {
            foreach (var edge in newEdges)
            {
                edge.PropertyChanged += OnEdgeChanged;
            }
        }
        var newNodes = newEdges?
            .SelectMany(edge => new[] { edge.Source, edge.Target })
            .Distinct()
            .ToArray();
        if (newNodes is not null)
        {
            foreach (var node in newNodes)
            {
                node.PropertyChanged += OnNodeChanged;
            }
        }
        subscribedEdges = newEdges;
        subscribedNodes = newNodes;
        InvalidateVisual();
    }

    private void OnEdgeChanged(object? sender, PropertyChangedEventArgs eventArgs) => InvalidateVisual();

    private void OnNodeChanged(object? sender, PropertyChangedEventArgs eventArgs) => InvalidateVisual();
}

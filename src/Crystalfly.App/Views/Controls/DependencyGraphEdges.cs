using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Crystalfly.App.ViewModels.DependencyGraph;

namespace Crystalfly.App.Views.Controls;

public sealed class DependencyGraphEdges : Control
{
    private IReadOnlyList<DependencyGraphEdgeViewModel>? subscribedEdges;
    public static readonly StyledProperty<IReadOnlyList<DependencyGraphEdgeViewModel>?> EdgesProperty =
        AvaloniaProperty.Register<DependencyGraphEdges, IReadOnlyList<DependencyGraphEdgeViewModel>?>(nameof(Edges));

    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<DependencyGraphEdges, IBrush?>(nameof(LineBrush));

    public static readonly StyledProperty<IBrush?> HighlightBrushProperty =
        AvaloniaProperty.Register<DependencyGraphEdges, IBrush?>(nameof(HighlightBrush));

    public static readonly StyledProperty<IBrush?> ErrorBrushProperty =
        AvaloniaProperty.Register<DependencyGraphEdges, IBrush?>(nameof(ErrorBrush));

    static DependencyGraphEdges()
    {
        AffectsRender<DependencyGraphEdges>(EdgesProperty, LineBrushProperty, HighlightBrushProperty, ErrorBrushProperty);
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
            DrawEdge(context, edge, LineBrush, 1, 0.28);
        }
        foreach (var edge in edges.Where(edge => !edge.IsDimmed && !edge.IsCycle))
        {
            DrawEdge(context, edge, edge.IsHighlighted ? HighlightBrush ?? LineBrush : LineBrush, edge.IsHighlighted ? 2.5 : 1.5, 1);
        }
        foreach (var edge in edges.Where(edge => edge.IsCycle))
        {
            DrawEdge(context, edge, ErrorBrush ?? HighlightBrush ?? LineBrush, 2.5, 1);
        }
    }

    private static void DrawEdge(
        DrawingContext context,
        DependencyGraphEdgeViewModel edge,
        IBrush brush,
        double thickness,
        double opacity)
    {
        var start = new Point(
            edge.Source.X + DependencyGraphModel.NodeWidth,
            edge.Source.Y + DependencyGraphModel.NodeHeight / 2);
        var end = new Point(
            edge.Target.X,
            edge.Target.Y + DependencyGraphModel.NodeHeight / 2);
        var curve = Math.Max(34, Math.Abs(end.X - start.X) * 0.45);
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            path.BeginFigure(start, false);
            path.CubicBezierTo(
                new Point(start.X + curve, start.Y),
                new Point(end.X - curve, end.Y),
                end);
        }
        using (context.PushOpacity(opacity))
        {
            context.DrawGeometry(null, new Pen(brush, thickness, lineCap: PenLineCap.Round), geometry);
        }
    }

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
        if (newEdges is not null)
        {
            foreach (var edge in newEdges)
            {
                edge.PropertyChanged += OnEdgeChanged;
            }
        }
        subscribedEdges = newEdges;
        InvalidateVisual();
    }

    private void OnEdgeChanged(object? sender, PropertyChangedEventArgs eventArgs) => InvalidateVisual();
}

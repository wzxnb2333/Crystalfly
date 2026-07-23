using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Crystalfly.App.ViewModels.Dialogs;

namespace Crystalfly.App.Views.Controls;

public sealed class DependencyTreeGuide : Control
{
    public const double ColumnWidth = 20;
    private const double LineOffset = 9;

    public static readonly StyledProperty<IReadOnlyList<TreeConnectorKind>?> ConnectorsProperty =
        AvaloniaProperty.Register<DependencyTreeGuide, IReadOnlyList<TreeConnectorKind>?>(
            nameof(Connectors));

    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<DependencyTreeGuide, IBrush?>(nameof(LineBrush));

    static DependencyTreeGuide()
    {
        AffectsMeasure<DependencyTreeGuide>(ConnectorsProperty);
        AffectsRender<DependencyTreeGuide>(ConnectorsProperty, LineBrushProperty);
    }

    public IReadOnlyList<TreeConnectorKind>? Connectors
    {
        get => GetValue(ConnectorsProperty);
        set => SetValue(ConnectorsProperty, value);
    }

    public IBrush? LineBrush
    {
        get => GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new((Connectors?.Count ?? 0) * ColumnWidth, 0);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Connectors is not { Count: > 0 } connectors || LineBrush is not { } brush)
        {
            return;
        }

        var pen = new Pen(brush, 1.5);
        var height = Bounds.Height;
        var midpoint = height / 2;
        for (var index = 0; index < connectors.Count; index++)
        {
            var x = index * ColumnWidth + LineOffset;
            var right = (index + 1) * ColumnWidth;
            switch (connectors[index])
            {
                case TreeConnectorKind.Continue:
                    context.DrawLine(pen, new Point(x, 0), new Point(x, height));
                    break;
                case TreeConnectorKind.Branch:
                    context.DrawLine(pen, new Point(x, 0), new Point(x, height));
                    context.DrawLine(pen, new Point(x, midpoint), new Point(right, midpoint));
                    break;
                case TreeConnectorKind.LastBranch:
                    context.DrawLine(pen, new Point(x, 0), new Point(x, midpoint));
                    context.DrawLine(pen, new Point(x, midpoint), new Point(right, midpoint));
                    break;
            }
        }
    }
}

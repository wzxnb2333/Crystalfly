using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Crystalfly.App.ViewModels.DependencyGraph;

namespace Crystalfly.App.Views.Controls;

public partial class DependencyGraphView : UserControl
{
    private const double MinimumScale = 0.35;
    private const double MaximumScale = 2;
    private const double ZoomStep = 0.12;
    private double scale = 1;
    private Vector translation;
    private Point lastPointerPosition;
    private IPointer? panningPointer;

    public DependencyGraphView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Dispatcher.UIThread.Post(FitToView, DispatcherPriority.Loaded);
        SizeChanged += (_, _) => Dispatcher.UIThread.Post(FitToView, DispatcherPriority.Render);
    }

    internal double Scale => scale;

    internal Vector Translation => translation;

    protected override void OnPointerPressed(PointerPressedEventArgs eventArgs)
    {
        base.OnPointerPressed(eventArgs);
        var point = eventArgs.GetCurrentPoint(Viewport);
        if (!point.Properties.IsMiddleButtonPressed)
        {
            return;
        }

        panningPointer = eventArgs.Pointer;
        lastPointerPosition = point.Position;
        eventArgs.Pointer.Capture(Viewport);
        eventArgs.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs eventArgs)
    {
        base.OnPointerMoved(eventArgs);
        if (panningPointer != eventArgs.Pointer)
        {
            return;
        }

        var position = eventArgs.GetPosition(Viewport);
        translation += position - lastPointerPosition;
        lastPointerPosition = position;
        ApplyTransform();
        eventArgs.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs eventArgs)
    {
        base.OnPointerReleased(eventArgs);
        if (panningPointer != eventArgs.Pointer)
        {
            return;
        }

        panningPointer = null;
        eventArgs.Pointer.Capture(null);
        eventArgs.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs eventArgs)
    {
        base.OnPointerWheelChanged(eventArgs);
        if (!eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            return;
        }

        ZoomAt(eventArgs.GetPosition(Viewport), eventArgs.Delta.Y > 0 ? ZoomStep : -ZoomStep);
        eventArgs.Handled = true;
    }

    internal void FitToView()
    {
        if (DataContext is not DependencyGraphModel { HasNodes: true } graph
            || Viewport.Bounds.Width <= 0
            || Viewport.Bounds.Height <= 0)
        {
            return;
        }

        var horizontal = Math.Max(1, Viewport.Bounds.Width - 40) / graph.Width;
        var vertical = Math.Max(1, Viewport.Bounds.Height - 40) / graph.Height;
        scale = Math.Clamp(Math.Min(horizontal, vertical), MinimumScale, 1.25);
        translation = new Vector(
            (Viewport.Bounds.Width - graph.Width * scale) / 2,
            (Viewport.Bounds.Height - graph.Height * scale) / 2);
        ApplyTransform();
    }

    internal void ResetViewport()
    {
        scale = 1;
        translation = new Vector(20, 20);
        ApplyTransform();
    }

    private void OnZoomOut(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs) =>
        ZoomAt(new Point(Viewport.Bounds.Width / 2, Viewport.Bounds.Height / 2), -ZoomStep);

    private void OnZoomIn(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs) =>
        ZoomAt(new Point(Viewport.Bounds.Width / 2, Viewport.Bounds.Height / 2), ZoomStep);

    private void OnFit(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs) => FitToView();

    private void OnReset(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs) => ResetViewport();

    private void ZoomAt(Point anchor, double delta)
    {
        var nextScale = Math.Clamp(scale + delta, MinimumScale, MaximumScale);
        if (Math.Abs(nextScale - scale) < double.Epsilon)
        {
            return;
        }

        var graphPoint = (anchor - translation) / scale;
        scale = nextScale;
        translation = anchor - graphPoint * scale;
        ApplyTransform();
    }

    private void ApplyTransform() => Surface.RenderTransform = new MatrixTransform(
        new Matrix(scale, 0, 0, scale, translation.X, translation.Y));
}

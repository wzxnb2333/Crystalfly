using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Controls.ApplicationLifetimes;
using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.DependencyGraph;
using System.Diagnostics.CodeAnalysis;

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

    private const double NodeDragThreshold = 4;
    private DependencyGraphModel? nodeDragGraph;
    private DependencyGraphNodeViewModel? nodeDragNode;
    private IPointer? nodeDragPointer;
    private Point nodeDragPointerStart;
    private Point nodeDragGraphStart;
    private Point nodeDragNodeStart;
    private bool isNodeDragActive;
    public DependencyGraphView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) =>
        {
            if (Localization is null
                && (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
                    ?.MainWindow?.DataContext is MainViewModel mainViewModel)
            {
                Localization = mainViewModel.Loc;
            }
        };
        DataContextChanged += (_, _) => Dispatcher.UIThread.Post(FitToView, DispatcherPriority.Loaded);
        SizeChanged += (_, _) => Dispatcher.UIThread.Post(FitToView, DispatcherPriority.Render);
        AddHandler(PointerPressedEvent, OnGraphPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerMovedEvent, OnGraphPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnGraphPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(ContextRequestedEvent, OnGraphContextRequested, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    public static readonly StyledProperty<LocalizationViewModel?> LocalizationProperty =
        AvaloniaProperty.Register<DependencyGraphView, LocalizationViewModel?>(nameof(Localization));

    public LocalizationViewModel? Localization
    {
        get => GetValue(LocalizationProperty);
        set => SetValue(LocalizationProperty, value);
    }

    internal double Scale => scale;

    internal Vector Translation => translation;

    private void OnGraphPointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (!eventArgs.GetCurrentPoint(Viewport).Properties.IsLeftButtonPressed
            || DataContext is not DependencyGraphModel graph
            || !TryGetGraphNode(eventArgs.Source, out _, out var node))
        {
            return;
        }

        nodeDragGraph = graph;
        nodeDragNode = node;
        nodeDragPointer = eventArgs.Pointer;
        nodeDragPointerStart = eventArgs.GetPosition(Viewport);
        nodeDragGraphStart = ToGraphPoint(nodeDragPointerStart);
        nodeDragNodeStart = new Point(node.X, node.Y);
        isNodeDragActive = false;
    }

    private void OnGraphPointerMoved(object? sender, PointerEventArgs eventArgs)
    {
        if (nodeDragPointer != eventArgs.Pointer
            || nodeDragGraph is null
            || nodeDragNode is null)
        {
            return;
        }

        var viewportPosition = eventArgs.GetPosition(Viewport);
        if (!isNodeDragActive)
        {
            var delta = viewportPosition - nodeDragPointerStart;
            if (Math.Abs(delta.X) < NodeDragThreshold && Math.Abs(delta.Y) < NodeDragThreshold)
            {
                return;
            }

            isNodeDragActive = true;
            nodeDragGraph.Select(nodeDragNode.Id);
            nodeDragGraph.NodeSelected?.Invoke(nodeDragNode.Id);
            eventArgs.Pointer.Capture(this);
        }

        var graphPosition = ToGraphPoint(viewportPosition);
        var targetX = nodeDragNodeStart.X + graphPosition.X - nodeDragGraphStart.X;
        var targetY = nodeDragNodeStart.Y + graphPosition.Y - nodeDragGraphStart.Y;
        var clampedX = Math.Max(DependencyGraphModel.CanvasPadding, targetX);
        var clampedY = Math.Max(DependencyGraphModel.CanvasPadding, targetY);
        nodeDragGraph.MoveNode(nodeDragNode.Id, clampedX, clampedY);
        if (clampedX != targetX || clampedY != targetY)
        {
            translation += new Vector(
                (targetX - clampedX) * scale,
                (targetY - clampedY) * scale);
            ApplyTransform();
        }
        eventArgs.Handled = true;
    }

    private void OnGraphPointerReleased(object? sender, PointerReleasedEventArgs eventArgs)
    {
        if (nodeDragPointer != eventArgs.Pointer)
        {
            return;
        }

        if (isNodeDragActive)
        {
            nodeDragGraph?.CommitNodePosition();
            eventArgs.Pointer.Capture(null);
            eventArgs.Handled = true;
        }
        ClearNodeDrag();
    }

    private void OnGraphContextRequested(object? sender, ContextRequestedEventArgs eventArgs)
    {
        if (DataContext is not DependencyGraphModel graph
            || !TryGetGraphNode(eventArgs.Source, out var button, out var node))
        {
            return;
        }

        graph.Select(node.Id);
        graph.NodeSelected?.Invoke(node.Id);
        if (!node.CanToggle && !node.CanDelete)
        {
            button.ContextMenu = null;
            return;
        }

        var menu = new ContextMenu();
        if (node.CanToggle)
        {
            var toggle = new MenuItem { Header = node.ToggleActionLabel };
            toggle.Click += (_, _) => graph.RequestNodeToggle(node);
            menu.Items.Add(toggle);
        }
        if (node.CanDelete)
        {
            var delete = new MenuItem { Header = Localization?["DependencyDelete"] ?? "Delete" };
            delete.Click += (_, _) => graph.RequestNodeDelete(node);
            menu.Items.Add(delete);
        }
        button.ContextMenu = menu;
    }

    private Point ToGraphPoint(Point point) => (point - translation) / scale;

    private static bool TryGetGraphNode(
        object? source,
        [NotNullWhen(true)] out Button? button,
        [NotNullWhen(true)] out DependencyGraphNodeViewModel? node)
    {
        button = source as Button
            ?? (source as Avalonia.Visual)?.FindAncestorOfType<Button>();
        node = button?.DataContext as DependencyGraphNodeViewModel;
        return button is not null && node is not null;
    }

    private void ClearNodeDrag()
    {
        nodeDragGraph = null;
        nodeDragNode = null;
        nodeDragPointer = null;
        isNodeDragActive = false;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs eventArgs)
    {
        base.OnPointerPressed(eventArgs);
        var point = eventArgs.GetCurrentPoint(Viewport);
        if (!point.Properties.IsLeftButtonPressed
            || eventArgs.Source is Button
            || eventArgs.Source is Avalonia.Visual visual
            && visual.FindAncestorOfType<Button>() is not null)
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

    private void OnRestoreAutomaticLayout(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs) =>
        (DataContext as DependencyGraphModel)?.RestoreAutomaticLayout();

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

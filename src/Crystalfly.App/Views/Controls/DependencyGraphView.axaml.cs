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
    private IReadOnlyDictionary<string, DependencyGraphNodePosition> nodeDragPositions = new Dictionary<string, DependencyGraphNodePosition>();
    private IPointer? marqueePointer;
    private Point marqueeStart;
    private IReadOnlyList<string> marqueeInitialSelection = [];
    private bool panned;
    private bool isNodeDragActive;
    private DependencyGraphModel? observedGraph;
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
        DataContextChanged += (_, _) =>
        {
            ClearNodeDrag();
            marqueePointer = null;
            panningPointer = null;
            SelectionRectangle.IsVisible = false;
            if (observedGraph is not null) observedGraph.PropertyChanged -= OnGraphBoundsChanged;
            observedGraph = DataContext as DependencyGraphModel;
            if (observedGraph is not null) observedGraph.PropertyChanged += OnGraphBoundsChanged;
            Dispatcher.UIThread.Post(ShowInitialView, DispatcherPriority.Loaded);
        };
        SizeChanged += (_, _) => Dispatcher.UIThread.Post(ShowInitialView, DispatcherPriority.Render);
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

        if ((eventArgs.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Shift)) != 0)
        {
            Focus(NavigationMethod.Pointer);
            graph.ToggleNodeSelection(node.Id);
            eventArgs.Handled = true;
            if (!node.IsSelected) return;
        }
        nodeDragGraph = graph;
        nodeDragNode = node;
        nodeDragPointer = eventArgs.Pointer;
        nodeDragPointerStart = eventArgs.GetPosition(Viewport);
        nodeDragGraphStart = ToGraphPoint(nodeDragPointerStart);
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
            if (!nodeDragNode.IsSelected) nodeDragGraph.SelectNodeCommand.Execute(nodeDragNode);
            else
            {
                nodeDragGraph.FocusNode(nodeDragNode.Id);
                nodeDragGraph.NodeSelected?.Invoke(nodeDragNode.Id);
            }
            nodeDragPositions = nodeDragGraph.SelectedNodes.ToDictionary(node => node.Id,
                node => new DependencyGraphNodePosition(node.X, node.Y), StringComparer.OrdinalIgnoreCase);
            eventArgs.Pointer.Capture(this);
        }

        var graphPosition = ToGraphPoint(viewportPosition);
        var deltaPosition = graphPosition - nodeDragGraphStart;
        nodeDragGraph.ApplySavedPositions(nodeDragPositions.ToDictionary(pair => pair.Key,
            pair => new DependencyGraphNodePosition(pair.Value.X + deltaPosition.X, pair.Value.Y + deltaPosition.Y),
            StringComparer.OrdinalIgnoreCase));
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

        if (!node.IsSelected) graph.SelectNodeCommand.Execute(node);
        else { graph.FocusNode(node.Id); graph.NodeSelected?.Invoke(node.Id); }
        if (graph.SelectedNodes.Count > 1 || !node.CanToggle && !node.CanDelete)
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

        Focus(NavigationMethod.Pointer);
        if (eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift) && DataContext is DependencyGraphModel graph)
        {
            marqueePointer = eventArgs.Pointer;
            marqueeStart = point.Position;
            marqueeInitialSelection = eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control)
                ? graph.SelectedNodes.Select(node => node.Id).ToArray() : [];
            UpdateMarquee(point.Position, false);
            eventArgs.Pointer.Capture(Viewport);
            eventArgs.Handled = true;
            return;
        }
        panningPointer = eventArgs.Pointer;
        panned = false;
        lastPointerPosition = point.Position;
        eventArgs.Pointer.Capture(Viewport);
        eventArgs.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs eventArgs)
    {
        base.OnPointerMoved(eventArgs);
        if (marqueePointer == eventArgs.Pointer)
        {
            UpdateMarquee(eventArgs.GetPosition(Viewport), false);
            eventArgs.Handled = true;
            return;
        }
        if (panningPointer != eventArgs.Pointer)
        {
            return;
        }

        var position = eventArgs.GetPosition(Viewport);
        if (position != lastPointerPosition) panned = true;
        translation += position - lastPointerPosition;
        lastPointerPosition = position;
        ApplyTransform();
        eventArgs.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs eventArgs)
    {
        base.OnPointerReleased(eventArgs);
        if (marqueePointer == eventArgs.Pointer)
        {
            UpdateMarquee(eventArgs.GetPosition(Viewport), true);
            marqueePointer = null;
            SelectionRectangle.IsVisible = false;
            eventArgs.Pointer.Capture(null);
            eventArgs.Handled = true;
            return;
        }
        if (panningPointer != eventArgs.Pointer)
        {
            return;
        }

        panningPointer = null;
        if (!panned && DataContext is DependencyGraphModel graph) graph.SetSelection([], notify: true);
        eventArgs.Pointer.Capture(null);
        eventArgs.Handled = true;
    }

    private void UpdateMarquee(Point end, bool notify)
    {
        var rectangle = new Rect(Math.Min(marqueeStart.X, end.X), Math.Min(marqueeStart.Y, end.Y),
            Math.Abs(end.X - marqueeStart.X), Math.Abs(end.Y - marqueeStart.Y));
        Canvas.SetLeft(SelectionRectangle, rectangle.X);
        Canvas.SetTop(SelectionRectangle, rectangle.Y);
        SelectionRectangle.Width = rectangle.Width;
        SelectionRectangle.Height = rectangle.Height;
        SelectionRectangle.IsVisible = true;
        if (DataContext is not DependencyGraphModel graph) return;
        var start = ToGraphPoint(rectangle.TopLeft);
        var selection = new Rect(start, new Size(rectangle.Width / scale, rectangle.Height / scale));
        graph.SetSelection(marqueeInitialSelection.Concat(graph.Nodes.Where(node => selection.Intersects(
            new Rect(node.X, node.Y, DependencyGraphModel.NodeWidth, DependencyGraphModel.NodeHeight))).Select(node => node.Id)), notify: notify);
    }

    protected override void OnKeyDown(KeyEventArgs eventArgs)
    {
        base.OnKeyDown(eventArgs);
        if (DataContext is not DependencyGraphModel graph) return;
        if (eventArgs.Key == Key.A && eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            graph.SetSelection(graph.Nodes.Select(node => node.Id), notify: true);
            eventArgs.Handled = true;
        }
        else if (eventArgs.Key == Key.Escape)
        {
            graph.SetSelection([], notify: true);
            eventArgs.Handled = true;
        }
    }

    private void OnSelectAll(object? sender, RoutedEventArgs args)
    {
        if (DataContext is DependencyGraphModel graph) graph.SetSelection(graph.Nodes.Select(node => node.Id), notify: true);
    }

    private void OnClearSelection(object? sender, RoutedEventArgs args) =>
        (DataContext as DependencyGraphModel)?.SetSelection([], notify: true);

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
            (Viewport.Bounds.Width - graph.Width * scale) / 2 - graph.Left * scale,
            (Viewport.Bounds.Height - graph.Height * scale) / 2 - graph.Top * scale);
        ApplyTransform();
    }

    internal void ResetViewport()
    {
        scale = 1;
        var graph = DataContext as DependencyGraphModel;
        translation = new Vector(20 - (graph?.Left ?? 0), 20 - (graph?.Top ?? 0));
        ApplyTransform();
    }

    private void ShowInitialView()
    {
        FitToView();
        if (scale >= 0.7 || DataContext is not DependencyGraphModel { SelectedNode: { } node }) return;
        scale = 0.75;
        var graph = (DependencyGraphModel)DataContext;
        var centerX = graph.Width * scale <= Viewport.Bounds.Width
            ? graph.Left + graph.Width / 2 : node.X + DependencyGraphModel.NodeWidth / 2;
        var centerY = graph.Height * scale <= Viewport.Bounds.Height
            ? graph.Top + graph.Height / 2 : node.Y + DependencyGraphModel.NodeHeight / 2;
        translation = new Vector(
            Viewport.Bounds.Width / 2 - centerX * scale,
            Viewport.Bounds.Height / 2 - centerY * scale);
        // Keep initial framing on the graph; free dragging remains unrestricted.
        if (graph.Width * scale > Viewport.Bounds.Width)
            translation = translation.WithX(Math.Clamp(translation.X,
                Viewport.Bounds.Width - (graph.Left + graph.Width) * scale, -graph.Left * scale));
        if (graph.Height * scale > Viewport.Bounds.Height)
            translation = translation.WithY(Math.Clamp(translation.Y,
                Viewport.Bounds.Height - (graph.Top + graph.Height) * scale, -graph.Top * scale));
        ApplyTransform();
    }

    private void OnZoomOut(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs) =>
        ZoomAt(new Point(Viewport.Bounds.Width / 2, Viewport.Bounds.Height / 2), -ZoomStep);

    private void OnZoomIn(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs) =>
        ZoomAt(new Point(Viewport.Bounds.Width / 2, Viewport.Bounds.Height / 2), ZoomStep);

    private void OnFit(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs) => FitToView();

    private void OnReset(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs) => ResetViewport();

    private void OnRestoreAutomaticLayout(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        (DataContext as DependencyGraphModel)?.RestoreAutomaticLayout();
        FitToView();
    }

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

    private void OnGraphBoundsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(DependencyGraphModel.Left) or nameof(DependencyGraphModel.Top)) ApplyTransform();
    }

    private void ApplyTransform()
    {
        var graph = DataContext as DependencyGraphModel;
        Surface.RenderTransform = new MatrixTransform(new Matrix(scale, 0, 0, scale,
            translation.X + (graph?.Left ?? 0) * scale,
            translation.Y + (graph?.Top ?? 0) * scale));
    }
}

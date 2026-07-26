using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Crystalfly.App.ViewModels.DependencyGraph;

public enum DependencyGraphNodeState
{
    Normal,
    Missing,
    Disabled,
    Attention,
    External,
    Local,
    Unknown,
    Cycle
}

public sealed record DependencyGraphNodeDefinition(
    string Id,
    string PrimaryName,
    string SecondaryName,
    string Status,
    DependencyGraphNodeState State,
    string? Action = null,
    bool CanToggle = false,
    string? ToggleActionLabel = null,
    bool CanDelete = false);

public sealed record DependencyGraphEdgeDefinition(string SourceId, string TargetId);

public sealed partial class DependencyGraphNodeViewModel : ObservableObject
{
    internal DependencyGraphNodeViewModel(DependencyGraphNodeDefinition definition)
    {
        Id = definition.Id;
        PrimaryName = definition.PrimaryName;
        SecondaryName = definition.SecondaryName;
        Status = definition.Status;
        State = definition.State;
        Action = definition.Action;
        CanToggle = definition.CanToggle;
        ToggleActionLabel = definition.ToggleActionLabel;
        CanDelete = definition.CanDelete;
    }

    public string Id { get; }

    public string PrimaryName { get; }

    public string SecondaryName { get; }

    public string Status { get; }

    public DependencyGraphNodeState State { get; internal set; }

    public string? Action { get; }

    public bool CanToggle { get; }

    public string? ToggleActionLabel { get; }

    public bool CanDelete { get; }

    [ObservableProperty]
    public partial double X { get; private set; }

    [ObservableProperty]
    public partial double Y { get; private set; }

    public bool IsMissing => State == DependencyGraphNodeState.Missing;

    public bool IsWarning => State == DependencyGraphNodeState.Disabled;

    public bool IsProblem => State is DependencyGraphNodeState.Attention or DependencyGraphNodeState.Cycle;

    public bool IsUnknown => State is DependencyGraphNodeState.External or DependencyGraphNodeState.Unknown;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial bool IsRelated { get; set; }

    public bool IsDimmed => !IsRelated;

    internal void SetHighlight(bool isSelected, bool isRelated)
    {
        IsSelected = isSelected;
        IsRelated = isRelated;
        OnPropertyChanged(nameof(IsDimmed));
    }

    internal void SetState(DependencyGraphNodeState state)
    {
        if (State == state)
        {
            return;
        }

        State = state;
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(IsProblem));
        OnPropertyChanged(nameof(IsMissing));
        OnPropertyChanged(nameof(IsWarning));
        OnPropertyChanged(nameof(IsUnknown));
    }

    internal void SetPosition(double x, double y)
    {
        X = x;
        Y = y;
    }
}

public sealed partial class DependencyGraphEdgeViewModel(
    DependencyGraphNodeViewModel source,
    DependencyGraphNodeViewModel target) : ObservableObject
{
    public DependencyGraphNodeViewModel Source { get; } = source;

    public DependencyGraphNodeViewModel Target { get; } = target;

    public bool IsCycle { get; internal set; }

    [ObservableProperty]
    public partial bool IsHighlighted { get; set; }

    [ObservableProperty]
    public partial bool IsDimmed { get; set; }
}

public sealed partial class DependencyGraphModel : ObservableObject
{
    public const double NodeWidth = 228;
    public const double NodeHeight = 82;
    private const double HorizontalGap = 96;
    private const double VerticalGap = 18;
    public const double CanvasPadding = 28;
    private readonly Dictionary<string, DependencyGraphNodeViewModel> nodesById;
    private readonly IReadOnlyList<DependencyGraphEdgeViewModel> edges;
    private readonly Dictionary<string, DependencyGraphNodePosition> automaticPositions = new(StringComparer.OrdinalIgnoreCase);

    private DependencyGraphModel(
        IReadOnlyList<DependencyGraphNodeViewModel> nodes,
        IReadOnlyList<DependencyGraphEdgeViewModel> edges)
    {
        Nodes = nodes;
        this.edges = edges;
        Edges = edges;
        nodesById = nodes.ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<DependencyGraphNodeViewModel> Nodes { get; }

    public IReadOnlyList<DependencyGraphEdgeViewModel> Edges { get; }

    public double Width { get; private set; }

    public double Height { get; private set; }

    public bool HasNodes => Nodes.Count != 0;

    public Action<string>? NodeSelected { get; set; }

    public Action? NodePositionCommitted { get; set; }

    public Action? AutomaticLayoutRestored { get; set; }

    public Action<string>? NodeToggleRequested { get; set; }

    public Action<string>? NodeDeleteRequested { get; set; }

    public static DependencyGraphModel Create(
        IEnumerable<DependencyGraphNodeDefinition> nodeDefinitions,
        IEnumerable<DependencyGraphEdgeDefinition> edgeDefinitions,
        string? selectedId = null)
    {
        ArgumentNullException.ThrowIfNull(nodeDefinitions);
        ArgumentNullException.ThrowIfNull(edgeDefinitions);

        var definitions = nodeDefinitions
            .Where(definition => !string.IsNullOrWhiteSpace(definition.Id))
            .GroupBy(definition => definition.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(definition => definition.PrimaryName, StringComparer.CurrentCulture)
            .ThenBy(definition => definition.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var nodes = definitions.Select(definition => new DependencyGraphNodeViewModel(definition)).ToArray();
        var nodesById = nodes.ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase);
        var edges = edgeDefinitions
            .Where(edge => nodesById.ContainsKey(edge.SourceId)
                && nodesById.ContainsKey(edge.TargetId)
                && !string.Equals(edge.SourceId, edge.TargetId, StringComparison.OrdinalIgnoreCase))
            .GroupBy(
                edge => $"{edge.SourceId}\0{edge.TargetId}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(edge => new DependencyGraphEdgeViewModel(nodesById[edge.SourceId], nodesById[edge.TargetId]))
            .ToArray();
        var graph = new DependencyGraphModel(nodes, edges);
        graph.Layout();
        graph.Select(selectedId);
        return graph;
    }

    public void Select(string? id)
    {
        var selected = !string.IsNullOrWhiteSpace(id) && nodesById.TryGetValue(id, out var found)
            ? found
            : Nodes.FirstOrDefault();
        var relatedIds = selected is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : FindConnectedIds(selected.Id);
        foreach (var node in Nodes)
        {
            node.SetHighlight(
                selected is not null && string.Equals(node.Id, selected.Id, StringComparison.OrdinalIgnoreCase),
                selected is null || relatedIds.Contains(node.Id));
        }
        foreach (var edge in edges)
        {
            edge.IsHighlighted = selected is not null
                && relatedIds.Contains(edge.Source.Id)
                && relatedIds.Contains(edge.Target.Id);
            edge.IsDimmed = selected is not null && !edge.IsHighlighted;
        }
    }

    [RelayCommand]
    private void SelectNode(DependencyGraphNodeViewModel? node)
    {
        if (node is null)
        {
            return;
        }

        Select(node.Id);
        NodeSelected?.Invoke(node.Id);
    }

    public DependencyGraphLayoutApplicationResult ApplySavedPositions(
        IReadOnlyDictionary<string, DependencyGraphNodePosition> positions)
    {
        ArgumentNullException.ThrowIfNull(positions);
        var appliedCount = 0;
        var hadExpiredNodes = false;
        foreach (var (id, position) in positions)
        {
            if (!nodesById.TryGetValue(id, out var node))
            {
                hadExpiredNodes = true;
                continue;
            }
            if (!double.IsFinite(position.X) || !double.IsFinite(position.Y))
            {
                hadExpiredNodes = true;
                continue;
            }

            node.SetPosition(Math.Max(CanvasPadding, position.X), Math.Max(CanvasPadding, position.Y));
            appliedCount++;
        }
        RecalculateBounds();
        return new DependencyGraphLayoutApplicationResult(appliedCount, hadExpiredNodes);
    }

    public bool MoveNode(string id, double x, double y)
    {
        if (!nodesById.TryGetValue(id, out var node)
            || !double.IsFinite(x)
            || !double.IsFinite(y))
        {
            return false;
        }

        node.SetPosition(Math.Max(CanvasPadding, x), Math.Max(CanvasPadding, y));
        RecalculateBounds();
        return true;
    }

    public IReadOnlyDictionary<string, DependencyGraphNodePosition> GetPositions() =>
        Nodes.ToDictionary(
            node => node.Id,
            node => new DependencyGraphNodePosition(node.X, node.Y),
            StringComparer.OrdinalIgnoreCase);

    public void RestoreAutomaticLayout()
    {
        foreach (var node in Nodes)
        {
            if (automaticPositions.TryGetValue(node.Id, out var position))
            {
                node.SetPosition(position.X, position.Y);
            }
        }
        RecalculateBounds();
        AutomaticLayoutRestored?.Invoke();
    }

    public void RequestNodeToggle(DependencyGraphNodeViewModel? node)
    {
        if (node?.CanToggle == true)
        {
            NodeToggleRequested?.Invoke(node.Id);
        }
    }

    public void RequestNodeDelete(DependencyGraphNodeViewModel? node)
    {
        if (node?.CanDelete == true)
        {
            NodeDeleteRequested?.Invoke(node.Id);
        }
    }

    public void CommitNodePosition() => NodePositionCommitted?.Invoke();

    private void Layout()
    {
        var cycleIds = FindCycleIds(Nodes, edges);
        foreach (var node in Nodes.Where(node => cycleIds.Contains(node.Id)))
        {
            node.SetState(DependencyGraphNodeState.Cycle);
        }
        foreach (var edge in edges)
        {
            edge.IsCycle = cycleIds.Contains(edge.Source.Id) && cycleIds.Contains(edge.Target.Id);
        }

        var layerById = Nodes.ToDictionary(node => node.Id, _ => 0, StringComparer.OrdinalIgnoreCase);
        var incoming = Nodes.ToDictionary(node => node.Id, _ => 0, StringComparer.OrdinalIgnoreCase);
        var outgoing = Nodes.ToDictionary(node => node.Id, _ => new List<DependencyGraphEdgeViewModel>(), StringComparer.OrdinalIgnoreCase);
        foreach (var edge in edges.Where(edge => !edge.IsCycle))
        {
            incoming[edge.Target.Id]++;
            outgoing[edge.Source.Id].Add(edge);
        }
        var pending = new Queue<DependencyGraphNodeViewModel>(Nodes
            .Where(node => incoming[node.Id] == 0)
            .OrderBy(node => node.PrimaryName, StringComparer.CurrentCulture)
            .ThenBy(node => node.Id, StringComparer.OrdinalIgnoreCase));
        while (pending.TryDequeue(out var node))
        {
            foreach (var edge in outgoing[node.Id])
            {
                layerById[edge.Target.Id] = Math.Max(layerById[edge.Target.Id], layerById[node.Id] + 1);
                if (--incoming[edge.Target.Id] == 0)
                {
                    pending.Enqueue(edge.Target);
                }
            }
        }

        foreach (var edge in edges.Where(edge => edge.IsCycle))
        {
            layerById[edge.Source.Id] = 0;
            layerById[edge.Target.Id] = 0;
        }
        foreach (var edge in edges.Where(edge => !edge.IsCycle && cycleIds.Contains(edge.Source.Id)))
        {
            layerById[edge.Target.Id] = Math.Max(layerById[edge.Target.Id], 1);
        }

        automaticPositions.Clear();
        foreach (var layer in Nodes.GroupBy(node => layerById[node.Id]).OrderBy(group => group.Key))
        {
            var row = 0;
            foreach (var node in layer
                .OrderBy(node => node.PrimaryName, StringComparer.CurrentCulture)
                .ThenBy(node => node.Id, StringComparer.OrdinalIgnoreCase))
            {
                node.SetPosition(
                    CanvasPadding + layer.Key * (NodeWidth + HorizontalGap),
                    CanvasPadding + row++ * (NodeHeight + VerticalGap));
                automaticPositions[node.Id] = new DependencyGraphNodePosition(node.X, node.Y);
            }
        }
        RecalculateBounds();
        OnPropertyChanged(nameof(HasNodes));
    }

    private void RecalculateBounds()
    {
        var maxX = CanvasPadding;
        var maxY = CanvasPadding;
        foreach (var node in Nodes)
        {
            maxX = Math.Max(maxX, node.X + NodeWidth);
            maxY = Math.Max(maxY, node.Y + NodeHeight);
        }
        Width = maxX + CanvasPadding;
        Height = maxY + CanvasPadding;
        OnPropertyChanged(nameof(Width));
        OnPropertyChanged(nameof(Height));
    }

    private HashSet<string> FindConnectedIds(string selectedId)
    {
        var connected = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in Nodes)
        {
            connected[node.Id] = [];
        }
        foreach (var edge in edges)
        {
            connected[edge.Source.Id].Add(edge.Target.Id);
            connected[edge.Target.Id].Add(edge.Source.Id);
        }
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { selectedId };
        var pending = new Queue<string>([selectedId]);
        while (pending.TryDequeue(out var current))
        {
            foreach (var next in connected[current])
            {
                if (result.Add(next))
                {
                    pending.Enqueue(next);
                }
            }
        }
        return result;
    }

    private static HashSet<string> FindCycleIds(
        IReadOnlyList<DependencyGraphNodeViewModel> nodes,
        IReadOnlyList<DependencyGraphEdgeViewModel> edges)
    {
        var outgoing = edges.GroupBy(edge => edge.Source.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.Target.Id).ToArray(), StringComparer.OrdinalIgnoreCase);
        var states = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var stack = new List<string>();
        var cycles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes)
        {
            Visit(node.Id);
        }
        return cycles;

        void Visit(string id)
        {
            if (states.TryGetValue(id, out var state))
            {
                if (state == 1)
                {
                    var cycleStart = stack.FindLastIndex(candidate => string.Equals(candidate, id, StringComparison.OrdinalIgnoreCase));
                    foreach (var cycleId in stack.Skip(Math.Max(0, cycleStart)))
                    {
                        cycles.Add(cycleId);
                    }
                }
                return;
            }

            states[id] = 1;
            stack.Add(id);
            if (outgoing.TryGetValue(id, out var targets))
            {
                foreach (var target in targets)
                {
                    Visit(target);
                }
            }
            stack.RemoveAt(stack.Count - 1);
            states[id] = 2;
        }
    }

}

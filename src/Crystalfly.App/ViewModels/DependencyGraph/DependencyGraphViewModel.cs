using CommunityToolkit.Mvvm.Input;

namespace Crystalfly.App.ViewModels.DependencyGraph;

public sealed record DependencyGraphDependencies(
    Func<LocalizationViewModel> GetLoc,
    Func<string, MarketModItemViewModel?> FindMarketMod,
    Func<string, string> GetInstalledModGraphLayoutPath,
    Func<string?> GetSelectedInstanceId,
    Action<string?> SetErrorMessage);

public sealed partial class DependencyGraphViewModel : ViewModelBase
{
    private readonly DependencyGraphDependencies dependencies;

    public DependencyGraphViewModel(DependencyGraphDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        this.dependencies = dependencies;
    }

    private LocalizationViewModel Loc => dependencies.GetLoc();

    public event Action<string>? NodeSelectedRequested;

    public event Action<string>? NodeToggleRequested;

    public event Action<string>? NodeDeleteRequested;

    public DependencyGraphModel Graph { get; private set; } = DependencyGraphModel.Create([], []);

    public string? SelectedNodeId { get; private set; }

    public HashSet<string> ExpandedNodeIds { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsSelectedNodeExpanded => SelectedNodeId is not null && IsExpanded(SelectedNodeId);

    public bool IsExpanded(string id) => ExpandedNodeIds.Contains(id);

    public IReadOnlyList<string> MissingNodeIds => Graph.Nodes
        .Where(node => node.IsMissing)
        .Select(node => node.Id)
        .ToArray();

    [RelayCommand]
    private void ExpandNode(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || !IsNodePresent(id))
        {
            return;
        }
        ExpandedNodeIds.Add(id);
        NotifyExpansionStateChanged();
    }

    [RelayCommand]
    private void CollapseNode(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || !IsNodePresent(id))
        {
            return;
        }
        ExpandedNodeIds.Remove(id);
        NotifyExpansionStateChanged();
    }

    private void NotifyExpansionStateChanged()
    {
        OnPropertyChanged(nameof(IsSelectedNodeExpanded));
        OnPropertyChanged(nameof(IsExpanded));
    }

    private bool IsNodePresent(string id) => Graph.Nodes.Any(node =>
        string.Equals(node.Id, id, StringComparison.OrdinalIgnoreCase));

    public void SelectNode(string? id)
    {
        SelectedNodeId = id;
        OnPropertyChanged(nameof(IsSelectedNodeExpanded));
        Graph.Select(id);
    }

    public void Rebuild(
        IReadOnlyList<InstalledModItemViewModel> mods,
        string? selectedId,
        string? instanceId)
    {
        ArgumentNullException.ThrowIfNull(mods);
        var targetSelectedId = selectedId
            ?? mods.FirstOrDefault(mod => mod.HasHealthIssue)?.Id
            ?? mods.FirstOrDefault()?.Id;
        var definitions = new Dictionary<string, DependencyGraphNodeDefinition>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<DependencyGraphEdgeDefinition>();
        foreach (var mod in mods)
        {
            var state = mod.IsExternal
                ? DependencyGraphNodeState.External
                : !mod.IsEnabled
                    ? DependencyGraphNodeState.Disabled
                    : mod.HasHealthIssue
                        ? DependencyGraphNodeState.Attention
                        : mod.IsLocal
                            ? DependencyGraphNodeState.Local
                            : DependencyGraphNodeState.Normal;
            var status = mod.IsExternal
                ? Loc["DependencyRelationshipUnknown"]
                : !mod.IsEnabled
                    ? Loc["Disabled"]
                    : mod.HasHealthIssue
                        ? mod.HealthDisplayName
                        : mod.OwnershipDisplayName;
            definitions[mod.Id] = new(
                mod.Id,
                mod.PrimaryName,
                mod.SecondaryName,
                status,
                state,
                CanToggle: mod.CanToggle,
                ToggleActionLabel: mod.IsEnabled ? Loc["Disable"] : Loc["Enable"],
                CanDelete: mod.CanUninstall);
        }

        foreach (var mod in mods.Where(mod => mod.Receipt is not null))
        {
            foreach (var dependencyId in mod.Receipt!.Dependencies.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!definitions.ContainsKey(dependencyId))
                {
                    var display = dependencies.FindMarketMod(dependencyId);
                    definitions[dependencyId] = new(
                        dependencyId,
                        display?.PrimaryName ?? dependencyId,
                        display?.SecondaryName ?? string.Empty,
                        Loc["Missing"],
                        DependencyGraphNodeState.Missing);
                }
                edges.Add(new DependencyGraphEdgeDefinition(dependencyId, mod.Id));
            }
        }

        var graph = DependencyGraphModel.Create(definitions.Values, edges, targetSelectedId);
        graph.NodeSelected = id =>
        {
            SelectedNodeId = id;
            OnPropertyChanged(nameof(IsSelectedNodeExpanded));
            NodeSelectedRequested?.Invoke(id);
        };
        Graph = graph;
        SelectedNodeId = targetSelectedId;
        OnPropertyChanged(nameof(IsSelectedNodeExpanded));
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        graph.NodePositionCommitted = () => _ = SaveInstalledModGraphLayoutAsync(graph, instanceId);
        graph.AutomaticLayoutRestored = () => _ = ClearInstalledModGraphLayoutAsync(graph, instanceId);
        graph.NodeToggleRequested = id => NodeToggleRequested?.Invoke(id);
        graph.NodeDeleteRequested = id => NodeDeleteRequested?.Invoke(id);
        _ = ApplyInstalledModGraphLayoutAsync(graph, instanceId);
    }

    private async Task ApplyInstalledModGraphLayoutAsync(DependencyGraphModel graph, string instanceId)
    {
        var layout = await DependencyGraphLayoutStore.TryReadAsync(
            dependencies.GetInstalledModGraphLayoutPath(instanceId));
        if (layout is null
            || !ReferenceEquals(Graph, graph)
            || !string.Equals(dependencies.GetSelectedInstanceId(), instanceId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var applied = graph.ApplySavedPositions(layout.Positions);
        if (applied.HadExpiredNodes)
        {
            await SaveInstalledModGraphLayoutAsync(graph, instanceId);
        }
    }

    private async Task SaveInstalledModGraphLayoutAsync(DependencyGraphModel graph, string instanceId)
    {
        if (!ReferenceEquals(Graph, graph)
            || !string.Equals(dependencies.GetSelectedInstanceId(), instanceId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            await DependencyGraphLayoutStore.WriteAsync(
                dependencies.GetInstalledModGraphLayoutPath(instanceId),
                new DependencyGraphLayout { Positions = new(graph.GetPositions(), StringComparer.OrdinalIgnoreCase) });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            dependencies.SetErrorMessage($"{Loc["OperationFailed"]}: {exception.Message}");
        }
    }

    private async Task ClearInstalledModGraphLayoutAsync(DependencyGraphModel graph, string instanceId)
    {
        if (!ReferenceEquals(Graph, graph)
            || !string.Equals(dependencies.GetSelectedInstanceId(), instanceId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            await DependencyGraphLayoutStore.WriteAsync(
                dependencies.GetInstalledModGraphLayoutPath(instanceId),
                new DependencyGraphLayout());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            dependencies.SetErrorMessage($"{Loc["OperationFailed"]}: {exception.Message}");
        }
    }
}

using System.Text;

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
    private readonly object layoutCacheLock = new();
    private readonly Dictionary<string, DependencyGraphLayout?> cachedLayouts = new(StringComparer.OrdinalIgnoreCase);
    private string? rebuildSignature;

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

    public IReadOnlyList<string> MissingNodeIds => Graph.Nodes
        .Where(node => node.IsMissing)
        .Select(node => node.Id)
        .ToArray();

    public void SelectNode(string? id)
    {
        SelectedNodeId = id;
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
        var signature = new StringBuilder();
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
            var toggleActionLabel = mod.IsEnabled ? Loc["Disable"] : Loc["Enable"];
            AppendSignature(signature, mod.Id);
            AppendSignature(signature, mod.PrimaryName);
            AppendSignature(signature, mod.SecondaryName);
            AppendSignature(signature, status);
            AppendSignature(signature, state.ToString());
            AppendSignature(signature, mod.CanToggle.ToString());
            AppendSignature(signature, toggleActionLabel);
            AppendSignature(signature, mod.CanUninstall.ToString());
            definitions[mod.Id] = new(
                mod.Id,
                mod.PrimaryName,
                mod.SecondaryName,
                status,
                state,
                CanToggle: mod.CanToggle,
                ToggleActionLabel: toggleActionLabel,
                CanDelete: mod.CanUninstall);
        }

        foreach (var mod in mods.Where(mod => mod.Receipt is not null))
        {
            var dependencyIds = mod.Receipt!.Dependencies.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            foreach (var dependencyId in dependencyIds)
            {
                if (!definitions.ContainsKey(dependencyId))
                {
                    var display = dependencies.FindMarketMod(dependencyId);
                    var primaryName = display?.PrimaryName ?? dependencyId;
                    var secondaryName = display?.SecondaryName ?? string.Empty;
                    var missingStatus = Loc["Missing"];
                    definitions[dependencyId] = new(
                        dependencyId,
                        primaryName,
                        secondaryName,
                        missingStatus,
                        DependencyGraphNodeState.Missing);
                    AppendSignature(signature, dependencyId);
                    AppendSignature(signature, primaryName);
                    AppendSignature(signature, secondaryName);
                    AppendSignature(signature, missingStatus);
                    AppendSignature(signature, DependencyGraphNodeState.Missing.ToString());
                }
                edges.Add(new DependencyGraphEdgeDefinition(dependencyId, mod.Id));
                AppendSignature(signature, dependencyId);
                AppendSignature(signature, mod.Id);
            }
        }
        AppendSignature(signature, targetSelectedId);
        AppendSignature(signature, instanceId);

        if (string.Equals(signature.ToString(), rebuildSignature, StringComparison.Ordinal))
        {
            return;
        }
        rebuildSignature = signature.ToString();

        var graph = DependencyGraphModel.Create(definitions.Values, edges, targetSelectedId);
        graph.NodeSelected = id =>
        {
            SelectedNodeId = id;
            NodeSelectedRequested?.Invoke(id);
        };
        Graph = graph;
        OnPropertyChanged(nameof(Graph));
        SelectedNodeId = targetSelectedId;
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
        var layout = await GetCachedLayoutAsync(instanceId);
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

    private async Task<DependencyGraphLayout?> GetCachedLayoutAsync(string instanceId)
    {
        lock (layoutCacheLock)
        {
            if (cachedLayouts.TryGetValue(instanceId, out var cached))
            {
                return cached;
            }
        }

        var layout = await DependencyGraphLayoutStore.TryReadAsync(
            dependencies.GetInstalledModGraphLayoutPath(instanceId));
        lock (layoutCacheLock)
        {
            // A write that completed while the read was in flight wins over the stale read.
            cachedLayouts.TryAdd(instanceId, layout);
        }
        return layout;
    }

    private async Task SaveInstalledModGraphLayoutAsync(DependencyGraphModel graph, string instanceId)
    {
        if (!ReferenceEquals(Graph, graph)
            || !string.Equals(dependencies.GetSelectedInstanceId(), instanceId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await WriteLayoutIfChangedAsync(
            instanceId,
            new DependencyGraphLayout { Positions = new(graph.GetPositions(), StringComparer.OrdinalIgnoreCase) });
    }

    private async Task ClearInstalledModGraphLayoutAsync(DependencyGraphModel graph, string instanceId)
    {
        if (!ReferenceEquals(Graph, graph)
            || !string.Equals(dependencies.GetSelectedInstanceId(), instanceId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await WriteLayoutIfChangedAsync(instanceId, new DependencyGraphLayout());
    }

    private async Task WriteLayoutIfChangedAsync(string instanceId, DependencyGraphLayout layout)
    {
        lock (layoutCacheLock)
        {
            if (cachedLayouts.TryGetValue(instanceId, out var cached)
                && LayoutsEqual(cached, layout))
            {
                return;
            }
        }

        try
        {
            await DependencyGraphLayoutStore.WriteAsync(
                dependencies.GetInstalledModGraphLayoutPath(instanceId),
                layout);
            lock (layoutCacheLock)
            {
                cachedLayouts[instanceId] = layout;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            dependencies.SetErrorMessage($"{Loc["OperationFailed"]}: {exception.Message}");
        }
    }

    private static bool LayoutsEqual(DependencyGraphLayout? cached, DependencyGraphLayout layout)
    {
        if (cached is null)
        {
            return false;
        }

        var cachedPositions = cached.Positions;
        var positions = layout.Positions;
        if (cachedPositions.Count != positions.Count)
        {
            return false;
        }

        foreach (var (id, position) in cachedPositions)
        {
            if (!positions.TryGetValue(id, out var other)
                || other.X != position.X
                || other.Y != position.Y)
            {
                return false;
            }
        }
        return true;
    }

    private static void AppendSignature(StringBuilder builder, string? value)
    {
        builder.Append(value?.Length ?? 0).Append(':').Append(value).Append('');
    }
}

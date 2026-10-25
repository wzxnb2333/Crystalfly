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
    private bool applyingSelection;
    private IReadOnlyDictionary<string, InstalledModItemViewModel> installedById =
        new Dictionary<string, InstalledModItemViewModel>(StringComparer.OrdinalIgnoreCase);

    public DependencyGraphViewModel(DependencyGraphDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        this.dependencies = dependencies;
    }

    private LocalizationViewModel Loc => dependencies.GetLoc();

    public event Action<string>? NodeSelectedRequested;

    public event Action<IReadOnlyList<string>>? SelectionChangedRequested;

    public event Action<string>? NodeToggleRequested;

    public event Action<string>? NodeDeleteRequested;

    public DependencyGraphModel Graph { get; private set; } = DependencyGraphModel.Create([], []);

    public string? SelectedNodeId { get; private set; }

    public InstalledModItemViewModel? SelectedMod => Graph.SelectedNode is { } node
        && installedById.TryGetValue(node.Id, out var mod) ? mod : null;

    public IReadOnlyList<string> MissingNodeIds => Graph.Nodes
        .Where(node => node.IsMissing)
        .Select(node => node.Id)
        .ToArray();

    public void SelectNode(string? id)
    {
        SelectedNodeId = id;
        Graph.FocusNode(id);
        OnPropertyChanged(nameof(SelectedMod));
    }

    public void SetSelectedNodes(IEnumerable<string> ids)
    {
        if (!applyingSelection) Graph.SetSelection(ids, Graph.SelectedNode?.Id);
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

        installedById = mods.ToDictionary(mod => mod.Id, StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<(string Id, IEnumerable<string> Dependencies)>();
        foreach (var mod in mods)
        {
            var manifest = mod.CatalogManifest ?? dependencies.FindMarketMod(mod.Id)?.Manifest;
            // Catalog updates must not replace the requirements of an installed older version.
            var catalogDependencies = manifest is not null && (mod.Receipt is null
                || string.Equals(mod.Version, manifest.Version, StringComparison.OrdinalIgnoreCase))
                ? manifest.Dependencies : [];
            pending.Enqueue((mod.Id, (mod.Receipt?.Dependencies ?? []).Concat(catalogDependencies)));
        }
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (pending.TryDequeue(out var current))
        {
            if (!visited.Add(current.Id)) continue;
            var dependencyIds = current.Dependencies.Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var dependencyId in dependencyIds)
            {
                if (!definitions.ContainsKey(dependencyId))
                {
                    var display = dependencies.FindMarketMod(dependencyId);
                    var primaryName = display?.PrimaryName ?? (dependencyId.StartsWith("hkmod:", StringComparison.OrdinalIgnoreCase)
                        ? dependencyId["hkmod:".Length..] : dependencyId);
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
                    pending.Enqueue((dependencyId, display?.Dependencies ?? []));
                }
                edges.Add(new DependencyGraphEdgeDefinition(dependencyId, current.Id));
                AppendSignature(signature, dependencyId);
                AppendSignature(signature, current.Id);
            }
        }
        AppendSignature(signature, targetSelectedId);
        AppendSignature(signature, instanceId);

        if (string.Equals(signature.ToString(), rebuildSignature, StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(SelectedMod));
            return;
        }
        rebuildSignature = signature.ToString();

        var graph = DependencyGraphModel.Create(definitions.Values, edges, targetSelectedId);
        if (mods.Any(mod => mod.IsSelected)) graph.SetSelection(mods.Where(mod => mod.IsSelected).Select(mod => mod.Id), targetSelectedId);
        graph.SelectionChanged = ids =>
        {
            applyingSelection = true;
            try { SelectionChangedRequested?.Invoke(ids); }
            finally { applyingSelection = false; }
        };
        graph.NodeSelected = id =>
        {
            SelectNode(id);
            NodeSelectedRequested?.Invoke(id);
        };
        Graph = graph;
        OnPropertyChanged(nameof(Graph));
        SelectedNodeId = targetSelectedId;
        OnPropertyChanged(nameof(SelectedMod));
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
            dependencies.SetErrorMessage(Loc.ErrorMessageFor(exception));
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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.App.ViewModels.DependencyGraph;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;
using Crystalfly.Core.Thunderstore;

namespace Crystalfly.App.ViewModels;

public sealed partial class ThunderstoreViewModel
{
    private readonly HashSet<string> selectedInstalled = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, ModHealthReport> installedHealth = new Dictionary<string, ModHealthReport>();
    private bool applyingInstalledGraphSelection;
    private string? installedSelectionAnchor;
    private string? uninstallContext;
    private IReadOnlyList<string> uninstallNames = [];
    private readonly SemaphoreSlim installedLayoutGate = new(1, 1);
    [ObservableProperty] public partial bool IsInstalledGraphVisible { get; set; }
    public bool IsInstalledListVisible => !IsInstalledGraphVisible;
    [ObservableProperty] public partial SettingOption<string>? InstalledStatus { get; set; }
    [ObservableProperty] public partial DependencyGraphModel InstalledGraph { get; private set; } = DependencyGraphModel.Create([], []);
    public IReadOnlyList<SettingOption<string>> InstalledStatusOptions =>
    [
        new("all", loc["FilterAll"]), new("enabled", loc["TsEnabled"]), new("disabled", loc["TsDisabled"]),
        new("updates", loc["UpdateAvailable"]), new("pinned", loc["TsPinned"]),
        new("local", loc["Local"]), new("attention", loc["NeedsAttention"])
    ];
    public int InstalledCount => Installed.Count(item => !item.IsLoader);
    public int SelectedInstalledCount => selectedInstalled.Count;
    public bool HasInstalledBatchSelection => SelectedInstalledCount > 0;
    public string InstalledSelectionText => string.Format(loc["SelectedModCount"], SelectedInstalledCount);
    public string InstalledVersionText => SelectedInstalled is null ? "" : SelectedInstalled.Version
        + (HasInstalledUpdate(SelectedInstalled) ? " → " + InstalledCatalogPackage(SelectedInstalled)?.Latest?.VersionNumber : "");
    public string InstalledDescription => SelectedInstalled is null ? "" : InstalledCatalogPackage(SelectedInstalled) is { } package
        ? Display(package, package.Versions.FirstOrDefault(item => item.VersionNumber == SelectedInstalled.Version) ?? package.Latest).Description : "";
    public string InstalledAuthor => SelectedInstalled is null ? "" : InstalledCatalogPackage(SelectedInstalled)?.Owner ?? "";
    public string InstalledDependenciesText => SelectedInstalled is null ? "" : string.Join("\n", SelectedInstalled.Dependencies
        .Select(raw => { var (name, version) = ThunderstoreIdentity.Dependency(raw); return DisplayName(name) + " ≥ " + version; }));
    public string InstalledDependentsText => SelectedInstalled is null ? "" : string.Join("\n", Installed
        .Where(item => item.Dependencies.Any(raw => ThunderstoreIdentity.Dependency(raw).Name == SelectedInstalled.FullName))
        .Select(item => DisplayName(item.FullName)));
    public string InstalledHealthText => SelectedInstalled is null ? "" : InstalledProblemText(SelectedInstalled);
    public string InstalledHealthDetails => SelectedInstalled is not null && installedHealth.TryGetValue(SelectedInstalled.FullName, out var health)
        ? string.Join("\n", health.MissingFiles.Concat(health.ModifiedFiles)) : "";
    private bool CanManageSelectedInstalled() => CanManage && HasInstalledBatchSelection;

    partial void OnInstalledStatusChanged(SettingOption<string>? value) => FilterInstalled();
    partial void OnIsInstalledGraphVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(IsInstalledListVisible));
        if (value && selectedInstalled.Count > 0) InstalledGraph.SetSelection(selectedInstalled, SelectedInstalled?.FullName);
    }
    [RelayCommand] private void ShowInstalledList() => IsInstalledGraphVisible = false;
    [RelayCommand] private void ShowInstalledGraph()
    {
        if (SelectedInstalled is null) SelectedInstalled = Installed.FirstOrDefault(item => item.FullName == InstalledGraph.SelectedNode?.Id);
        IsInstalledGraphVisible = true;
    }

    public void SetInstalledSelected(string fullName, bool selected)
    {
        if (!Installed.Any(item => !item.IsLoader && item.FullName == fullName)) return;
        if (selected ? selectedInstalled.Add(fullName) : selectedInstalled.Remove(fullName)) NotifyInstalledSelection();
    }
    public void SelectInstalledItem(ThunderstoreInstalledPackage item, bool control, bool shift)
    {
        SelectedInstalled = item;
        var anchor = InstalledMods.ToList().FindIndex(candidate => candidate.FullName == installedSelectionAnchor);
        var index = InstalledMods.IndexOf(item);
        if (shift && anchor >= 0 && index >= 0)
        {
            if (!control) selectedInstalled.Clear();
            foreach (var candidate in InstalledMods.Skip(Math.Min(anchor, index)).Take(Math.Abs(index - anchor) + 1))
                selectedInstalled.Add(candidate.FullName);
        }
        else
        {
            if (!control && !shift) selectedInstalled.Clear();
            if (!selectedInstalled.Add(item.FullName)) selectedInstalled.Remove(item.FullName);
            installedSelectionAnchor = item.FullName;
        }
        NotifyInstalledSelection();
    }
    [RelayCommand] private void SelectAllInstalled()
    {
        foreach (var item in InstalledMods) selectedInstalled.Add(item.FullName);
        NotifyInstalledSelection();
    }
    [RelayCommand] private void ClearInstalledSelection() { selectedInstalled.Clear(); installedSelectionAnchor = null; SelectedInstalled = null; NotifyInstalledSelection(); }
    private void NotifyInstalledSelection()
    {
        OnPropertyChanged(nameof(SelectedInstalledCount));
        OnPropertyChanged(nameof(HasInstalledBatchSelection));
        OnPropertyChanged(nameof(InstalledSelectionText));
        foreach (var item in installedDisplayItems ?? []) item.IsSelected = selectedInstalled.Contains(item.FullName);
        if (!applyingInstalledGraphSelection && IsInstalledGraphVisible) InstalledGraph.SetSelection(selectedInstalled, SelectedInstalled?.FullName);
        NotifyInstalledCommands();
    }
    private void NotifyInstalledCommands()
    {
        EnableInstalledSelectedCommand.NotifyCanExecuteChanged();
        DisableInstalledSelectedCommand.NotifyCanExecuteChanged();
        UpdateInstalledSelectedCommand.NotifyCanExecuteChanged();
        RequestUninstallSelectedCommand.NotifyCanExecuteChanged();
        CheckInstalledHealthCommand.NotifyCanExecuteChanged();
    }
    private void NotifyInstalledDetails()
    {
        foreach (var name in new[] { nameof(InstalledVersionText), nameof(InstalledDescription), nameof(InstalledAuthor),
            nameof(InstalledDependenciesText), nameof(InstalledDependentsText), nameof(InstalledHealthText), nameof(InstalledHealthDetails), nameof(InstalledCount) })
            OnPropertyChanged(name);
    }
    private ThunderstorePackage? InstalledCatalogPackage(ThunderstoreInstalledPackage item) =>
        item.LocalPackage ?? catalog?.Packages.FirstOrDefault(package => package.FullName == item.FullName);
    private bool HasInstalledUpdate(ThunderstoreInstalledPackage item) => !item.IsLocal && !item.Pinned
        && InstalledCatalogPackage(item)?.Latest is { } latest
        && ThunderstoreIdentity.Version(latest.VersionNumber) > ThunderstoreIdentity.Version(item.Version);
    private string InstalledProblemText(ThunderstoreInstalledPackage item)
    {
        var dependencies = item.Dependencies.Select(ThunderstoreIdentity.Dependency).Where(dependency =>
            !Installed.Any(other => other.FullName == dependency.Name && (!item.Enabled || other.Enabled)
                && ThunderstoreIdentity.Version(other.Version) >= ThunderstoreIdentity.Version(dependency.Version))).ToArray();
        if (dependencies.Length > 0) return loc["DependencyProblem"] + ": " + string.Join(", ", dependencies.Select(dependency => DisplayName(dependency.Name)));
        return installedHealth.TryGetValue(item.FullName, out var health) ? loc["ModHealth" + health.Status] : loc["Installed"];
    }
    private bool InstalledNeedsAttention(ThunderstoreInstalledPackage item) =>
        installedHealth.TryGetValue(item.FullName, out var health) && health.Status != ModHealthStatus.Healthy
        || item.Dependencies.Select(ThunderstoreIdentity.Dependency).Any(dependency => !Installed.Any(other => other.FullName == dependency.Name
            && (!item.Enabled || other.Enabled) && ThunderstoreIdentity.Version(other.Version) >= ThunderstoreIdentity.Version(dependency.Version)));
    private bool MatchesInstalledStatus(ThunderstoreInstalledPackage item) => InstalledStatus?.Value switch
    {
        "enabled" => item.Enabled, "disabled" => !item.Enabled, "pinned" => item.Pinned, "local" => item.IsLocal,
        "updates" => HasInstalledUpdate(item), "attention" => InstalledNeedsAttention(item), _ => true
    };

    [RelayCommand(CanExecute = nameof(CanManage))]
    private Task CheckInstalledHealthAsync() => RunAsync(async () =>
    {
        var target = RequireTarget();
        var context = generation;
        var reports = await new ThunderstoreInstallService(target, store).AssessHealthAsync();
        if (context != generation || getTarget() != target) return;
        installedHealth = reports.ToDictionary(item => item.ModId, StringComparer.OrdinalIgnoreCase);
        FilterInstalled();
        RebuildInstalledGraph();
        NotifyInstalledDetails();
    });
    [RelayCommand(CanExecute = nameof(CanManageSelectedInstalled))]
    private Task EnableInstalledSelectedAsync() => ChangeInstalledSelectedAsync(true);
    [RelayCommand(CanExecute = nameof(CanManageSelectedInstalled))]
    private Task DisableInstalledSelectedAsync() => ChangeInstalledSelectedAsync(false);
    private Task ChangeInstalledSelectedAsync(bool enabled) => RunAsync(async () =>
    {
        await new ThunderstoreInstallService(RequireTarget(), store).SetEnabledAsync(selectedInstalled.ToArray(), enabled);
        await RefreshInstalledAsync();
        await refreshInstance();
    });
    [RelayCommand(CanExecute = nameof(CanManageSelectedInstalled))]
    private void RequestUninstallSelected() => PrepareUninstall(selectedInstalled.ToArray());
    private void PrepareUninstall(IReadOnlyList<string> names)
    {
        ResetPlan();
        uninstallNames = names;
        PendingUninstall = string.Join("\n", names);
    }
    [RelayCommand(CanExecute = nameof(CanManageSelectedInstalled))]
    private Task UpdateInstalledSelectedAsync() => RunAsync(async () =>
    {
        var target = RequireTarget();
        var context = generation;
        var names = selectedInstalled.ToHashSet(StringComparer.OrdinalIgnoreCase);
        await RequireCatalogAsync();
        if (context != generation || getTarget() != target) return;
        var state = await new ThunderstoreInstallService(target, store).ReadStateAsync();
        if (context != generation || getTarget() != target) return;
        var requests = state.Packages.Where(item => names.Contains(item.FullName) && HasInstalledUpdate(item))
            .Select(item => (item.FullName, (string?)InstalledCatalogPackage(item)!.Latest!.VersionNumber)).ToArray();
        if (requests.Length == 0) { Status = loc["NoUpdateAvailable"]; return; }
        var plan = new ThunderstoreDependencyResolver(catalog!).Resolve(target.GameId, target.BuildId, requests, state.Packages);
        PresentPlan(target, plan, false);
        installRequests = requests;
    });

    private void RebuildInstalledGraph()
    {
        var positions = InstalledGraph.GetPositions();
        var definitions = new Dictionary<string, DependencyGraphNodeDefinition>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<DependencyGraphEdgeDefinition>();
        foreach (var item in Installed)
            definitions[item.FullName] = new(item.FullName, DisplayName(item.FullName), item.Version, item.IsLoader ? loc["Loader"]
                : InstalledNeedsAttention(item) ? loc["NeedsAttention"] : loc[item.Enabled ? "TsEnabled" : "TsDisabled"],
                InstalledNeedsAttention(item) ? DependencyGraphNodeState.Attention : item.Enabled ? DependencyGraphNodeState.Normal : DependencyGraphNodeState.Disabled,
                CanToggle: !item.IsLoader, ToggleActionLabel: loc[item.Enabled ? "TsDisable" : "TsEnable"], CanDelete: !item.IsLoader);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void AddDependencies(string name, IReadOnlyList<string> dependencies)
        {
            if (!visited.Add(name)) return;
            foreach (var raw in dependencies)
            {
                var (dependency, version) = ThunderstoreIdentity.Dependency(raw);
                edges.Add(new(dependency, name));
                if (definitions.ContainsKey(dependency)) continue;
                definitions[dependency] = new(dependency, DisplayName(dependency), "≥ " + version, loc["Missing"], DependencyGraphNodeState.Missing);
                var package = catalog?.Packages.FirstOrDefault(item => item.FullName == dependency);
                AddDependencies(dependency, package?.Versions.FirstOrDefault(item => item.VersionNumber == version)?.Dependencies ?? []);
            }
        }
        foreach (var item in Installed) AddDependencies(item.FullName, item.Dependencies);
        var graph = DependencyGraphModel.Create(definitions.Values.ToArray(), edges, SelectedInstalled?.FullName);
        graph.ApplySavedPositions(positions);
        if (selectedInstalled.Count > 0) graph.SetSelection(selectedInstalled, SelectedInstalled?.FullName);
        graph.SelectionChanged = ids =>
        {
            applyingInstalledGraphSelection = true;
            try
            {
                selectedInstalled.Clear();
                selectedInstalled.UnionWith(Installed.Where(item => !item.IsLoader && ids.Contains(item.FullName, StringComparer.OrdinalIgnoreCase)).Select(item => item.FullName));
                if (ids.Count == 0) SelectedInstalled = null;
                NotifyInstalledSelection();
            }
            finally { applyingInstalledGraphSelection = false; }
        };
        graph.NodeSelected = id => SelectedInstalled = Installed.FirstOrDefault(item => item.FullName == id);
        graph.NodeToggleRequested = id => { SelectedInstalled = Installed.FirstOrDefault(item => item.FullName == id); ToggleEnabledCommand.Execute(null); };
        graph.NodeDeleteRequested = id => { SelectedInstalled = Installed.FirstOrDefault(item => item.FullName == id); RequestUninstallCommand.Execute(null); };
        InstalledGraph = graph;
        if (getTarget() is not { } target || getInstalledGraphLayoutPath?.Invoke(target.Id) is not { } path) return;
        var moved = false;
        graph.NodePositionCommitted = () =>
        {
            moved = true;
            _ = SaveInstalledLayoutAsync(graph, path, new() { Positions = new(graph.GetPositions(), StringComparer.OrdinalIgnoreCase) });
        };
        graph.AutomaticLayoutRestored = () => { moved = true; _ = SaveInstalledLayoutAsync(graph, path, new()); };
        _ = LoadInstalledLayoutAsync();

        async Task LoadInstalledLayoutAsync()
        {
            var saved = await DependencyGraphLayoutStore.TryReadAsync(path);
            if (!moved && ReferenceEquals(InstalledGraph, graph) && getTarget() == target && saved is not null)
                graph.ApplySavedPositions(saved.Positions);
        }
    }

    private async Task SaveInstalledLayoutAsync(DependencyGraphModel graph, string path, DependencyGraphLayout layout)
    {
        await installedLayoutGate.WaitAsync();
        try
        {
            if (ReferenceEquals(InstalledGraph, graph)) await DependencyGraphLayoutStore.WriteAsync(path, layout);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { Error = exception.Message; }
        finally { installedLayoutGate.Release(); }
    }

    public string GetInstalledDirectory(ThunderstoreInstalledPackage? package = null)
    {
        var target = RequireTarget();
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(target.RootPath));
        var file = package?.Files.FirstOrDefault(item => !item.IsConfiguration)?.Path;
        var relative = file is null ? "BepInEx/plugins" : package!.Enabled ? file
            : $"{ThunderstoreInstallService.MetadataDirectory}/disabled/{package.FullName}/{file}";
        var path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Path leaves the instance.");
        if (file is not null) path = Path.GetDirectoryName(path)!;
        InstanceDirectory.RejectReparseAncestors(path);
        InstanceDirectory.RejectReparseAncestors(root);
        return Directory.Exists(path) ? path : root;
    }
}

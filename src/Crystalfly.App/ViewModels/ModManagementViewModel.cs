using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.Core.Catalog;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;

namespace Crystalfly.App.ViewModels;

public partial class ModManagementViewModel : ViewModelBase
{
    private readonly ModManagementDependencies dependencies;
    private string? installedModSelectionAnchorId;

    public ModManagementViewModel(ModManagementDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        this.dependencies = dependencies;
    }

    private GameCatalog Catalog => dependencies.GetCatalog();

    private LocalizationViewModel Loc => dependencies.GetLoc();

    public event Action? InstalledModsRefreshed;

    public event Action? GraphVisibilityChanged;

    public ObservableCollection<ModManifest> AvailableMods { get; } = [];

    public ObservableCollection<ModManifest> VisibleAvailableMods { get; } = [];

    public ObservableCollection<InstalledModItemViewModel> InstalledMods { get; } = [];

    public ObservableCollection<InstalledModItemViewModel> VisibleInstalledMods { get; } = [];

    public ObservableCollection<SettingOption<ModStatusFilter>> ModStatusOptions { get; } = [];

    public ObservableCollection<string> UnusedDependencySuggestions { get; } = [];

    public bool HasSelectedMods => InstalledMods.Any(mod => mod.IsSelected);

    public int SelectedModCount => InstalledMods.Count(mod => mod.IsSelected);

    public bool IsInstalledModListVisible => !IsInstalledModGraphVisible;

    public bool IsInstalledModBulkBarVisible => IsInstalledModListVisible && HasSelectedMods;

    public bool HasUnusedDependencySuggestions => UnusedDependencySuggestions.Count > 0;

    public string UnusedDependencySummary => string.Format(
        CultureInfo.CurrentUICulture,
        Loc["UnusedDependencyCountFormat"],
        UnusedDependencySuggestions.Count);

    [ObservableProperty]
    public partial string ModSearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LocalModPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ModStatusFilter SelectedModStatus { get; set; } = ModStatusFilter.All;

    [ObservableProperty]
    public partial SettingOption<ModStatusFilter>? SelectedModStatusOption { get; set; }

    [ObservableProperty]
    public partial ModManifest? SelectedAvailableMod { get; set; }

    [ObservableProperty]
    public partial InstalledModItemViewModel? SelectedInstalledMod { get; set; }

    [ObservableProperty]
    public partial bool IsInstalledModGraphVisible { get; set; }

    partial void OnModSearchTextChanged(string value) => ApplyModFilters();

    partial void OnSelectedModStatusChanged(ModStatusFilter value) => ApplyModFilters();

    partial void OnSelectedModStatusOptionChanged(SettingOption<ModStatusFilter>? value)
    {
        if (value is not null)
        {
            SelectedModStatus = value.Value;
        }
    }

    partial void OnIsInstalledModGraphVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(IsInstalledModListVisible));
        OnPropertyChanged(nameof(IsInstalledModBulkBarVisible));
        if (value)
        {
            GraphVisibilityChanged?.Invoke();
        }
    }

    public void RebuildStatusOptions()
    {
        ModStatusOptions.Clear();
        ModStatusOptions.Add(new(ModStatusFilter.All, Loc["FilterAll"]));
        ModStatusOptions.Add(new(ModStatusFilter.Enabled, Loc["Enabled"]));
        ModStatusOptions.Add(new(ModStatusFilter.Disabled, Loc["Disabled"]));
        ModStatusOptions.Add(new(ModStatusFilter.Local, Loc["Local"]));
        ModStatusOptions.Add(new(ModStatusFilter.Updates, Loc["Updates"]));
        ModStatusOptions.Add(new(ModStatusFilter.External, Loc["External"]));
        ModStatusOptions.Add(new(ModStatusFilter.Pinned, Loc["Pinned"]));
        ModStatusOptions.Add(new(ModStatusFilter.NeedsAttention, Loc["NeedsAttentionFilter"]));
        SelectedModStatusOption = ModStatusOptions.First(option => option.Value == SelectedModStatus);
    }

    public void ReplaceAvailableMods(InstanceRecord record)
    {
        AvailableMods.Clear();
        foreach (var mod in ModCatalogCompatibility.ProjectForBuild(Catalog.Mods, record.BuildId)
                     .Where(mod => mod.SupportedBuildIds.Contains(record.BuildId, StringComparer.OrdinalIgnoreCase)))
        {
            AvailableMods.Add(mod);
        }
        ApplyModFilters();
    }

    public void ClearAvailableMods() => AvailableMods.Clear();

    public void ClearInstalledMods()
    {
        InstalledMods.Clear();
        VisibleInstalledMods.Clear();
        NotifySelectionChanged();
    }

    public void LoadInstalledMods(
        IReadOnlyList<ModDiscoveryEntry> mods,
        IReadOnlyList<InstalledModReceipt> installed,
        IReadOnlyList<ModHealthReport> healthReports,
        InstanceRecord record)
    {
        InstalledMods.Clear();
        foreach (var mod in mods)
        {
            var receipt = installed.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, mod.Id, StringComparison.OrdinalIgnoreCase));
            var healthReport = healthReports.First(report =>
                string.Equals(report.ModId, mod.Id, StringComparison.OrdinalIgnoreCase));
            var catalogManifest = ModCatalogCompatibility.ProjectForBuild(Catalog.Mods, record.BuildId)
                .FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, mod.Id, StringComparison.OrdinalIgnoreCase));
            InstalledMods.Add(new InstalledModItemViewModel(
                mod,
                receipt,
                healthReport,
                catalogManifest,
                NotifySelectionChanged,
                catalogManifest is null ? null : dependencies.ProjectMarketMod(catalogManifest, null),
                dependencies.OwnershipDisplay(mod.Ownership),
                dependencies.HealthDisplay(healthReport.Status)));
        }
        NotifySelectionChanged();
        ApplyModFilters();
        InstalledModsRefreshed?.Invoke();
    }

    public void RebuildCatalogProjection()
    {
        if (InstalledMods.Count == 0)
        {
            return;
        }

        var selectedIds = InstalledMods
            .Where(mod => mod.IsSelected)
            .Select(mod => mod.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var focusedId = SelectedInstalledMod?.Id;
        var items = InstalledMods.ToArray();
        var selectedInstance = dependencies.GetSelectedInstance();
        var compatibleMods = selectedInstance is null
            ? Catalog.Mods
            : ModCatalogCompatibility.ProjectForBuild(Catalog.Mods, selectedInstance.BuildId);
        InstalledMods.Clear();
        foreach (var item in items)
        {
            var receipt = item.Receipt;
            var manifest = compatibleMods.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, item.Id, StringComparison.OrdinalIgnoreCase));
            InstalledMods.Add(new InstalledModItemViewModel(
                item.Discovery,
                receipt,
                item.HealthReport,
                manifest,
                NotifySelectionChanged,
                manifest is null ? null : dependencies.ProjectMarketMod(manifest, null),
                dependencies.OwnershipDisplay(item.Ownership),
                dependencies.HealthDisplay(item.HealthStatus))
            {
                IsSelected = selectedIds.Contains(item.Id)
            });
        }
        SelectedInstalledMod = focusedId is null
            ? null
            : InstalledMods.FirstOrDefault(mod =>
                string.Equals(mod.Id, focusedId, StringComparison.OrdinalIgnoreCase));
        ApplyModFilters();
        NotifySelectionChanged();
        InstalledModsRefreshed?.Invoke();
    }

    [RelayCommand]
    private void ShowInstalledModList() => IsInstalledModGraphVisible = false;

    [RelayCommand]
    private void ShowInstalledModGraph()
    {
        IsInstalledModGraphVisible = true;
    }

    [RelayCommand]
    private async Task InstallSelectedModAsync()
    {
        if (dependencies.GetSelectedInstance() is null || SelectedAvailableMod is null)
        {
            dependencies.SetErrorMessage(Loc["SelectMod"]);
            return;
        }

        await dependencies.RunInstanceMutation(async record =>
        {
            var loader = await dependencies.CreateLoaderManager(record).GetReceiptAsync()
                ?? throw new InvalidOperationException(Loc["LoaderRequired"]);
            var compatibleMods = ModCatalogCompatibility.ProjectForBuild(Catalog.Mods, record.BuildId);
            var order = ModDependencyResolver.ResolveInstallOrder(compatibleMods, [SelectedAvailableMod.Id]);
            foreach (var mod in order)
            {
                if (!string.Equals(mod.LoaderId, loader.PackageId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"{Loc["WrongLoader"]}: {mod.Name}");
                }
            }
            await dependencies.CreateModManager(record).InstallWithDependenciesFromUrisAsync(
                compatibleMods,
                [SelectedAvailableMod.Id]);
        });
    }

    [RelayCommand]
    private async Task ToggleSelectedModAsync()
    {
        if (dependencies.GetSelectedInstance() is null || SelectedInstalledMod is null)
        {
            dependencies.SetErrorMessage(Loc["SelectMod"]);
            return;
        }
        var modId = SelectedInstalledMod.Id;
        var enabled = !SelectedInstalledMod.Enabled;
        await SetInstalledModsEnabledAsync([modId], enabled);
    }

    [RelayCommand]
    private async Task UpdateSelectedModAsync()
    {
        if (SelectedInstalledMod?.CatalogManifest is not ModManifest manifest
            || !SelectedInstalledMod.HasUpdate)
        {
            dependencies.SetErrorMessage(Loc["NoUpdateAvailable"]);
            return;
        }
        await dependencies.RunInstanceMutation(record =>
            dependencies.CreateModInstallService(record).UpdateAsync(manifest.Id));
    }

    [RelayCommand]
    private async Task UninstallSelectedModAsync()
    {
        if (dependencies.GetSelectedInstance() is null || SelectedInstalledMod is null)
        {
            dependencies.SetErrorMessage(Loc["SelectMod"]);
            return;
        }
        if (!SelectedInstalledMod.CanUninstall)
        {
            dependencies.SetErrorMessage(SelectedInstalledMod.IsPinned
                ? Loc["UnpinBeforeUninstall"]
                : Loc["ExternalModReadOnly"]);
            return;
        }
        var modId = SelectedInstalledMod.Id;
        IReadOnlyList<InstalledModReceipt> unused = [];
        await dependencies.RunInstanceMutation(async record =>
        {
            var manager = dependencies.CreateModManager(record);
            var before = await manager.GetInstalledAsync();
            var removed = before.Single(mod => string.Equals(mod.Id, modId, StringComparison.OrdinalIgnoreCase));
            await manager.UninstallIgnoringDependentsAsync(modId);
            var remaining = await manager.GetInstalledAsync();
            unused = InstalledModDependencyGraph.FindUnusedDependencies([removed], remaining);
        });
        SetUnusedDependencySuggestions(unused);
    }

    [RelayCommand]
    private async Task TakeOverSelectedModAsync()
    {
        if (SelectedInstalledMod is not { CanTakeOver: true } selected)
        {
            return;
        }
        await dependencies.RunInstanceMutation(record =>
            dependencies.CreateModManager(record).TakeOverAsync(selected.Id, selected.Discovery));
    }

    [RelayCommand]
    private async Task ToggleSelectedModPinnedAsync()
    {
        if (SelectedInstalledMod is not { CanPin: true, Receipt: { } receipt })
        {
            return;
        }
        await dependencies.RunInstanceMutation(record =>
            dependencies.CreateModManager(record).SetPinnedAsync(receipt.Id, !receipt.Pinned));
    }

    [RelayCommand]
    private async Task RepairSelectedModAsync()
    {
        if (SelectedInstalledMod is not { CanRepair: true, CatalogManifest: { } manifest } selected)
        {
            return;
        }
        await dependencies.RunInstanceMutation(async record =>
        {
            var manager = dependencies.CreateModManager(record);
            var plan = await manager.GetRepairPlanAsync(
                selected.Id,
                ModCatalogCompatibility.ProjectForBuild(Catalog.Mods, record.BuildId));
            switch (plan.Action)
            {
                case ModRepairAction.Repair when plan.Manifest is not null:
                    await manager.RepairFromUriAsync(plan.Manifest);
                    break;
                case ModRepairAction.Update when plan.Manifest is not null:
                    await dependencies.CreateModInstallService(record).UpdateAsync(plan.Manifest.Id);
                    break;
                default:
                    throw new InvalidOperationException(plan.Reason);
            }
        });
    }

    [RelayCommand]
    private async Task AcceptSelectedLocalModFilesAsync()
    {
        if (SelectedInstalledMod is not { CanAcceptCurrent: true } selected)
        {
            return;
        }
        await dependencies.RunInstanceMutation(record =>
            dependencies.CreateModManager(record).AcceptCurrentLocalFilesAsync(selected.Id));
    }

    [RelayCommand]
    private async Task ReimportSelectedLocalModAsync()
    {
        if (SelectedInstalledMod is not { CanReimport: true } selected || !File.Exists(LocalModPath))
        {
            dependencies.SetErrorMessage(Loc["LocalModPathRequired"]);
            return;
        }
        await dependencies.RunInstanceMutation(record =>
        {
            var manager = dependencies.CreateModManager(record);
            return string.Equals(Path.GetExtension(LocalModPath), ".dll", StringComparison.OrdinalIgnoreCase)
                ? manager.ReimportLocalDllAsync(selected.Id, LocalModPath)
                : manager.ReimportLocalZipAsync(selected.Id, LocalModPath);
        });
    }

    [RelayCommand]
    private async Task EnableSelectedModsAsync()
    {
        var selected = GetSelectedModReceipts();
        if (selected.Count == 0)
        {
            return;
        }
        await SetInstalledModsEnabledAsync(selected.Select(mod => mod.Id), enabled: true);
    }

    [RelayCommand]
    private async Task DisableSelectedModsAsync()
    {
        var selected = GetSelectedModReceipts();
        if (selected.Count == 0)
        {
            return;
        }
        await SetInstalledModsEnabledAsync(selected.Select(mod => mod.Id), enabled: false);
    }

    private Task SetInstalledModsEnabledAsync(IEnumerable<string> requestedIds, bool enabled)
    {
        var selectedIds = requestedIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var installed = GetManagedModReceipts();
        var selected = installed
            .Where(mod => selectedIds.Contains(mod.Id))
            .ToArray();
        if (selected.Length == 0)
        {
            return Task.CompletedTask;
        }

        if (enabled)
        {
            var disabledDependencies = selected
                .SelectMany(mod => mod.Dependencies)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(id => installed.FirstOrDefault(mod => string.Equals(mod.Id, id, StringComparison.OrdinalIgnoreCase)))
                .Where(mod => mod is not null && !mod.Enabled && !selectedIds.Contains(mod.Id))
                .Select(mod => mod!.Name)
                .ToArray();
            if (disabledDependencies.Length > 0)
            {
                dependencies.SetErrorMessage(
                    $"{Loc["DependencyProblem"]}: {string.Join(", ", disabledDependencies)}");
                return Task.CompletedTask;
            }
        }

        var order = InstalledModDependencyGraph.OrderDependentsFirst(installed, selectedIds);
        if (enabled)
        {
            order = order.Reverse().ToArray();
        }
        return dependencies.RunInstanceMutation(async record =>
        {
            var manager = dependencies.CreateModManager(record);
            foreach (var mod in order.Where(mod => mod.Enabled != enabled))
            {
                if (enabled)
                {
                    await manager.SetEnabledAsync(mod.Id, enabled: true);
                }
                else
                {
                    await manager.DisableIgnoringDependentsAsync(mod.Id);
                }
            }
        });
    }

    [RelayCommand]
    private async Task UpdateSelectedModsAsync()
    {
        var updates = InstalledMods.Where(mod => mod.IsSelected && mod.HasUpdate).ToArray();
        if (updates.Length == 0)
        {
            dependencies.SetErrorMessage(Loc["NoUpdateAvailable"]);
            return;
        }
        await dependencies.RunInstanceMutation(async record =>
        {
            foreach (var mod in updates)
            {
                await dependencies.CreateModInstallService(record).UpdateAsync(mod.CatalogManifest!.Id);
            }
        });
    }

    [RelayCommand]
    private async Task UninstallSelectedModsAsync()
    {
        var selected = GetSelectedModReceipts();
        if (selected.Count == 0)
        {
            return;
        }
        var selectedIds = selected.Select(mod => mod.Id).ToArray();
        ModBatchUninstallResult? result = null;
        await dependencies.RunInstanceMutation(async record =>
        {
            result = await dependencies.CreateModManager(record).UninstallBatchAsync(selectedIds);
        });
        SetUnusedDependencySuggestions(result?.UnusedDependencies ?? []);
    }

    private void SetUnusedDependencySuggestions(IReadOnlyList<InstalledModReceipt> suggestions)
    {
        UnusedDependencySuggestions.Clear();
        foreach (var suggestion in suggestions)
        {
            UnusedDependencySuggestions.Add(suggestion.Name);
        }
        OnPropertyChanged(nameof(HasUnusedDependencySuggestions));
        OnPropertyChanged(nameof(UnusedDependencySummary));
    }

    public ModRemovalImpactPlan CreateModRemovalPlan(bool bulk)
    {
        var selectedIds = bulk
            ? InstalledMods.Where(mod => mod.IsSelected).Select(mod => mod.Id).ToArray()
            : SelectedInstalledMod is null ? [] : [SelectedInstalledMod.Id];
        if (selectedIds.Length == 0)
        {
            throw new InvalidOperationException(Loc["SelectMod"]);
        }
        return InstalledModDependencyGraph.CreateRemovalPlan(
            GetManagedModReceipts(),
            selectedIds);
    }

    public ModDependencyRepairPlan CreateModDependencyRepairPlan()
    {
        var selectedInstance = dependencies.GetSelectedInstance()
            ?? throw new InvalidOperationException(Loc["NoInstance"]);
        var installed = GetManagedModReceipts();
        var loaderIds = installed
            .Select(mod => mod.LoaderId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (loaderIds.Length != 1)
        {
            throw new InvalidOperationException(Loc["WrongLoader"]);
        }
        return ModDependencyRepairPlanner.CreatePlan(
            installed,
            ModCatalogCompatibility.ProjectForBuild(Catalog.Mods, selectedInstance.BuildId),
            selectedInstance.BuildId,
            loaderIds[0]);
    }

    [RelayCommand]
    private async Task RepairModDependenciesAsync()
    {
        var plan = CreateModDependencyRepairPlan();
        if (plan.Items.All(item => item.Action == ModDependencyRepairAction.Unresolved))
        {
            dependencies.SetErrorMessage(Loc["CannotRepair"]);
            return;
        }
        await dependencies.EnqueueModDependencyRepair(plan);
    }

    public IReadOnlyList<string> GetSelectedModExternalDependentNames(bool bulk)
    {
        var selectedIds = bulk
            ? InstalledMods.Where(mod => mod.IsSelected).Select(mod => mod.Id).ToArray()
            : SelectedInstalledMod is null ? [] : [SelectedInstalledMod.Id];
        return InstalledModDependencyGraph.FindExternalDependents(
                GetManagedModReceipts(),
                selectedIds)
            .Select(mod => mod.Name)
            .ToArray();
    }

    public string GetSelectedModNames(bool bulk) => string.Join(
        Environment.NewLine,
        bulk
            ? InstalledMods.Where(mod => mod.IsSelected).Select(mod => mod.Name)
            : SelectedInstalledMod is null ? [] : [SelectedInstalledMod.Name]);

    private IReadOnlyList<InstalledModReceipt> GetSelectedModReceipts() => InstalledMods
        .Where(mod => mod.IsSelected && mod.Receipt is not null)
        .Select(mod => mod.Receipt)
        .OfType<InstalledModReceipt>()
        .ToArray();

    private IReadOnlyList<InstalledModReceipt> GetManagedModReceipts() => InstalledMods
        .Select(mod => mod.Receipt)
        .OfType<InstalledModReceipt>()
        .ToArray();

    [RelayCommand]
    private async Task ImportLocalModAsync()
    {
        if (dependencies.GetSelectedInstance() is null || !File.Exists(LocalModPath))
        {
            dependencies.SetErrorMessage(Loc["LocalModPathRequired"]);
            return;
        }
        await dependencies.RunInstanceMutation(async record =>
        {
            var loader = await dependencies.CreateLoaderManager(record).GetReceiptAsync()
                ?? throw new InvalidOperationException(Loc["LoaderRequired"]);
            var fileName = Path.GetFileNameWithoutExtension(LocalModPath);
            var id = $"local-{fileName}";
            var manager = dependencies.CreateModManager(record);
            if (string.Equals(Path.GetExtension(LocalModPath), ".dll", StringComparison.OrdinalIgnoreCase))
            {
                await manager.ImportLocalDllAsync(id, fileName, loader.PackageId, LocalModPath);
            }
            else if (string.Equals(Path.GetExtension(LocalModPath), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                await manager.ImportLocalZipAsync(id, fileName, loader.PackageId, LocalModPath);
            }
            else
            {
                throw new InvalidDataException(Loc["LocalModType"]);
            }
            LocalModPath = string.Empty;
        });
    }

    private void ApplyModFilters()
    {
        VisibleAvailableMods.Clear();
        foreach (var mod in AvailableMods.Where(mod =>
            string.IsNullOrWhiteSpace(ModSearchText)
            || mod.Name.Contains(ModSearchText, StringComparison.OrdinalIgnoreCase)
            || mod.Id.Contains(ModSearchText, StringComparison.OrdinalIgnoreCase)
            || mod.Version.Contains(ModSearchText, StringComparison.OrdinalIgnoreCase)))
        {
            VisibleAvailableMods.Add(mod);
        }

        VisibleInstalledMods.Clear();
        foreach (var mod in InstalledMods.Where(mod => mod.Matches(ModSearchText, SelectedModStatus)))
        {
            VisibleInstalledMods.Add(mod);
        }
    }

    internal void SelectModFromGraph(string id)
    {
        var selected = InstalledMods.FirstOrDefault(mod =>
            string.Equals(mod.Id, id, StringComparison.OrdinalIgnoreCase));
        if (selected is not null && !ReferenceEquals(SelectedInstalledMod, selected))
        {
            SelectedInstalledMod = selected;
        }
    }

    internal async Task ToggleInstalledModFromGraphAsync(string modId)
    {
        var mod = InstalledMods.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, modId, StringComparison.OrdinalIgnoreCase));
        if (mod is null || !mod.CanToggle)
        {
            return;
        }

        SelectedInstalledMod = mod;
        await SetInstalledModsEnabledAsync([mod.Id], enabled: !mod.IsEnabled);
    }

    public void SelectInstalledMod(
        InstalledModItemViewModel item,
        bool control,
        bool shift)
    {
        ArgumentNullException.ThrowIfNull(item);
        SelectedInstalledMod = item;

        if (shift && installedModSelectionAnchorId is not null)
        {
            var anchorIndex = VisibleInstalledMods
                .Select((candidate, index) => (candidate, index))
                .FirstOrDefault(pair => string.Equals(
                    pair.candidate.Id,
                    installedModSelectionAnchorId,
                    StringComparison.OrdinalIgnoreCase)).index;
            var itemIndex = VisibleInstalledMods.IndexOf(item);
            var hasVisibleAnchor = anchorIndex >= 0
                && anchorIndex < VisibleInstalledMods.Count
                && string.Equals(
                    VisibleInstalledMods[anchorIndex].Id,
                    installedModSelectionAnchorId,
                    StringComparison.OrdinalIgnoreCase);
            if (hasVisibleAnchor && itemIndex >= 0)
            {
                if (!control)
                {
                    SetInstalledModSelection(InstalledMods, selected: false);
                }
                var first = Math.Min(anchorIndex, itemIndex);
                var last = Math.Max(anchorIndex, itemIndex);
                SetInstalledModSelection(
                    VisibleInstalledMods.Skip(first).Take(last - first + 1),
                    selected: true);
                return;
            }
        }

        if (control)
        {
            item.IsSelected = !item.IsSelected;
        }
        else
        {
            SetInstalledModSelection(InstalledMods, selected: false);
            item.IsSelected = true;
        }
        installedModSelectionAnchorId = item.Id;
    }

    [RelayCommand]
    private void SelectAllInstalledMods()
    {
        SetInstalledModSelection(InstalledMods, selected: true);
        installedModSelectionAnchorId = InstalledMods.FirstOrDefault()?.Id;
    }

    [RelayCommand]
    private void ClearInstalledModSelection()
    {
        SetInstalledModSelection(InstalledMods, selected: false);
        SelectedInstalledMod = null;
        installedModSelectionAnchorId = null;
    }

    private static void SetInstalledModSelection(
        IEnumerable<InstalledModItemViewModel> mods,
        bool selected)
    {
        foreach (var mod in mods)
        {
            mod.IsSelected = selected;
        }
    }

    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(HasSelectedMods));
        OnPropertyChanged(nameof(SelectedModCount));
        OnPropertyChanged(nameof(IsInstalledModBulkBarVisible));
    }
}

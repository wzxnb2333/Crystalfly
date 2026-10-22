using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.App.Downloads;
using Crystalfly.App.Services;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;
using Crystalfly.Core.Thunderstore;

namespace Crystalfly.App.ViewModels;

public sealed partial class ThunderstoreViewModel(
    LocalizationViewModel loc, ThunderstoreCatalogSource source, ThunderstorePackageStore store,
    Func<InstanceRecord?> getTarget, Func<bool> isOffline,
    Func<DownloadQueueGroup, Task> enqueue, Func<Task> refreshInstance,
    Func<InstanceRecord, ThunderstoreInstallPlan, bool, Task>? installOffline = null,
    Action? chooseInstance = null, Action? browseMarket = null,
    SilksongModTranslationSource? translationSource = null,
    ThunderstorePackageIconLoader? packageIconLoader = null) : ViewModelBase
{
    private ThunderstoreCatalog? catalog;
    private ThunderstoreInstallPlan? pendingPlan;
    private string? pendingTarget;
    private bool repairPlan;
    private int generation;
    private string? requestedCategory;
    private string? contextTargetKey;
    public int SortIndex => Math.Max(0, SortOptions.ToList().IndexOf(Sort));
    public void RestoreFilters(string? category, int sort, bool showDeprecated)
    {
        requestedCategory = category;
        Sort = SortOptions[Math.Clamp(sort, 0, SortOptions.Count - 1)];
        ShowDeprecated = showDeprecated;
        RebuildCategories();
    }
    public LocalizationViewModel Loc => loc;
    public ThunderstorePackageIconLoader? PackageIconLoader => packageIconLoader;
    public ObservableCollection<ThunderstorePackage> Packages { get; } = [];
    public ObservableCollection<ThunderstoreInstalledPackage> Installed { get; } = [];
    public ObservableCollection<SettingOption<string>> Categories { get; } = [];
    public IReadOnlyList<string> SortOptions { get; private set; } = [loc["TsSortUpdated"], loc["TsSortDownloads"], loc["TsSortName"]];
    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial SettingOption<string>? Category { get; set; }
    [ObservableProperty] public partial string Sort { get; set; } = loc["TsSortUpdated"];
    [ObservableProperty] public partial bool ShowDeprecated { get; set; }
    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial string Status { get; set; } = "";
    [ObservableProperty] public partial string? Error { get; set; }
    [ObservableProperty] public partial ThunderstorePackage? SelectedPackage { get; set; }
    [ObservableProperty] public partial ThunderstoreVersion? SelectedVersion { get; set; }
    [ObservableProperty] public partial ThunderstoreInstalledPackage? SelectedInstalled { get; set; }
    [ObservableProperty] public partial IReadOnlyList<ThunderstoreVersion> Versions { get; set; } = [];
    [ObservableProperty] public partial string PreviewText { get; set; } = "";
    [ObservableProperty] public partial bool HasPlan { get; set; }
    [ObservableProperty] public partial bool AcceptUnverified { get; set; }
    [ObservableProperty] public partial string Document { get; set; } = "";
    [ObservableProperty] public partial string? PendingUninstall { get; set; }
    public void RefreshLocalization()
    {
        var index = SortOptions.ToList().IndexOf(Sort);
        SortOptions = [loc["TsSortUpdated"], loc["TsSortDownloads"], loc["TsSortName"]];
        OnPropertyChanged(nameof(SortOptions));
        Sort = SortOptions[Math.Max(0, index)];
        OnPropertyChanged(nameof(TargetName));
        RebuildCategories();
        NotifyTranslationProjection();
        ShowOriginalDocument = !Chinese;
        PresentDocument();
        NotifyActions();
    }
    private void RebuildCategories()
    {
        var selected = requestedCategory ?? Category?.Value ?? "";
        Categories.Clear(); Categories.Add(new("", loc["TsAllCategories"]));
        foreach (var category in (catalog?.Packages.SelectMany(item => item.Categories) ?? []).Distinct().Order())
        {
            var key = "TsCategory" + category.Replace(" ", "", StringComparison.Ordinal);
            var label = loc[key];
            Categories.Add(new(category, Chinese && translations.Catalog.CategoryNames.TryGetValue(category, out var translated)
                ? translated : label == key ? category : label));
        }
        Category = Categories.FirstOrDefault(item => item.Value == selected) ?? Categories[0];
        if (catalog is not null) requestedCategory = null;
    }
    public string TargetName => getTarget()?.Name ?? loc["NoInstance"];

    partial void OnSearchTextChanged(string value)
    {
        if (Error == loc["TsVersionMissing"]) Error = null;
        ApplyPackageLocatorSearch(value);
    }
    partial void OnCategoryChanged(SettingOption<string>? value) => Filter();
    partial void OnSortChanged(string value) => Filter();
    partial void OnShowDeprecatedChanged(bool value) => Filter();
    partial void OnSelectedPackageChanged(ThunderstorePackage? value)
    {
        Versions = value?.Versions.Where(item => item.IsActive).OrderByDescending(item => ThunderstoreIdentity.Version(item.VersionNumber)).ToArray() ?? [];
        SelectedVersion = Versions.FirstOrDefault();
        ResetDocument();
        NotifyTranslationProjection();
        ResetPlan();
    }
    partial void OnSelectedVersionChanged(ThunderstoreVersion? value) { ResetDocument(); ResetPlan(); NotifyTranslationProjection(); NotifyActions(); }
    partial void OnSelectedInstalledChanged(ThunderstoreInstalledPackage? value)
    {
        PendingUninstall = null;
        NotifyActions();
        NotifyTranslationProjection();
        if (value is null) return;
        SelectedPackage = catalog?.Packages.FirstOrDefault(item => item.FullName == value.FullName);
        SelectedVersion = Versions.FirstOrDefault(item => item.VersionNumber == value.Version) ?? SelectedVersion;
    }
    public async Task SetContextAsync()
    {
        var target = getTarget();
        var key = target is null ? "" : $"{target.GameId}:{target.Id}:{target.RootPath}";
        if (contextTargetKey == key)
        {
            await RefreshInstalledAsync();
            return;
        }
        contextTargetKey = key;
        generation++;
        ResetPlan();
        PendingUninstall = null;
        SelectedInstalled = null;
        ResetDocument();
        Error = null;
        Status = "";
        NotifyActions();
        OnPropertyChanged(nameof(TargetName));
        await RefreshInstalledAsync();
    }
    private void ResetPlan() { HasPlan = false; pendingPlan = null; pendingTarget = null; AcceptUnverified = false; NeedsCompatibilityConfirmation = false; PreviewText = ""; NotifyActions(); }
    private void Filter(string? locatedPackage = null)
    {
        if (catalog is null) return;
        locatedPackage ??= ResolvePackageLocator(SearchText)?.Package.FullName;
        var query = catalog.Packages.Where(item => (ShowDeprecated || !item.IsDeprecated)
            && (locatedPackage is not null ? item.FullName.Equals(locatedPackage, StringComparison.OrdinalIgnoreCase)
                : (string.IsNullOrEmpty(Category?.Value) || item.Categories.Contains(Category.Value, StringComparer.OrdinalIgnoreCase))
                    && (string.IsNullOrWhiteSpace(SearchText) || MatchesSearch(item, SearchText.Trim()))));
        query = Sort == loc["TsSortName"] ? query.OrderBy(item => item.FullName, StringComparer.OrdinalIgnoreCase)
            : Sort == loc["TsSortDownloads"] ? query.OrderByDescending(item => item.Versions.Sum(version => version.Downloads))
            : query.OrderByDescending(item => item.UpdatedAt);
        var previous = SelectedPackage?.FullName;
        var version = SelectedVersion?.VersionNumber;
        var filtered = query.ToArray();
        Packages.Clear();
        foreach (var package in filtered) Packages.Add(package);
        if (previous is not null)
        {
            var selected = Packages.FirstOrDefault(item => item.FullName == previous);
            SelectedPackage = selected;
            if (version is not null && selected is not null)
                SelectedVersion = Versions.FirstOrDefault(item => item.VersionNumber == version) ?? SelectedVersion;
        }
        NotifyActions();
        NotifyTranslationProjection();
    }
    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private Task RefreshAsync() => RunAsync(RefreshCatalogAsync);
    private async Task RefreshCatalogAsync()
    {
        var version = generation;
        var result = await source.LoadAsync(isOffline());
        if (version != generation) return;
        catalog = result;
        RebuildCategories();
        ApplyPackageLocatorSearch(SearchText);
        await RefreshTranslationsAsync(version);
        Status = $"Thunderstore · {result.Packages.Count} · {loc[source.UsingCache ? "TsCached" : "TsOnline"]}";
        if (source.RefreshError is not null) Status += " · " + loc["TsRefreshFailedCache"];
        await RefreshInstalledAsync();
        NotifyActions();
    }
    private async Task RefreshInstalledAsync()
    {
        var target = getTarget();
        contextTargetKey ??= target is null ? "" : $"{target.GameId}:{target.Id}:{target.RootPath}";
        var version = generation;
        var selectedName = SelectedInstalled?.FullName;
        Installed.Clear();
        InstalledMods.Clear();
        if (target?.GameId != GameId.Silksong) { SelectedInstalled = null; NotifyActions(); return; }
        try
        {
            var state = await new ThunderstoreInstallService(target, store).ReadStateAsync();
            if (version != generation || getTarget()?.Id != target.Id) return;
            foreach (var package in state.Packages) Installed.Add(package);
            if (catalog is not null)
            {
                var local = state.Packages.Where(item => item.IsLocal && item.LocalPackage is not null).Select(item => item.LocalPackage!).ToArray();
                var names = local.Select(item => item.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
                catalog = catalog with { Packages = catalog.Packages.Where(item => item.Owner != "Local" && !names.Contains(item.FullName)).Concat(local).ToArray() };
                Filter();
            }
            FilterInstalled();
            SelectedInstalled = Installed.FirstOrDefault(item => item.FullName == selectedName);
            NotifyActions();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or JsonException)
        { if (version == generation) Error = exception.Message; }
    }
    [RelayCommand(CanExecute = nameof(CanPreview))] private Task PreviewInstallAsync() => PreviewAsync(false, false);
    [RelayCommand(CanExecute = nameof(CanPreview))] private Task PreviewUpdateAsync() => PreviewAsync(true, false);
    [RelayCommand(CanExecute = nameof(CanChangeInstalled))] private Task PreviewRepairAsync() => PreviewInstalledAsync(false, true);
    private Task PreviewAsync(bool latest, bool repair) => RunAsync(async () =>
    {
        var target = RequireTarget();
        if (catalog is null || SelectedPackage is null) throw new InvalidOperationException(loc["SelectMod"]);
        var selected = SelectedPackage;
        if (latest) SelectedVersion = selected.Latest;
        var version = SelectedVersion ?? throw new InvalidOperationException(loc["SelectMod"]);
        await CreatePlanAsync(target, selected, version, repair);
    });
    private void PresentPlan(InstanceRecord target, ThunderstoreInstallPlan plan, bool repair)
    {
        if (getTarget()?.Id != target.Id) return;
        PendingUninstall = null;
        pendingPlan = plan;
        pendingTarget = target.Id;
        repairPlan = repair;
        PreviewText = PlanText(plan);
        HasPlan = true;
        NeedsCompatibilityConfirmation = plan.Warnings.Count > 0;
        AcceptUnverified = plan.Warnings.Count == 0;
    }
    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private Task ConfirmInstallAsync() => RunAsync(async () =>
    {
        var target = RequireTarget();
        var plan = pendingPlan ?? throw new InvalidOperationException(loc["TsPreviewFirst"]);
        if (target.Id != pendingTarget) throw new InvalidOperationException(loc["TsTargetChanged"]);
        if (!AcceptUnverified && plan.Warnings.Count > 0) throw new InvalidOperationException(loc["TsCompatibilityNotice"]);
        if (isOffline() && installOffline is not null)
        {
            await installOffline(target, plan, repairPlan);
            ResetPlan();
            await RefreshInstalledAsync();
            await refreshInstance();
            Status = loc["Complete"];
        }
        else
        {
            await enqueue(ThunderstoreQueueExecutor.Create(target, plan, SelectedPackage?.FullName ?? loc["TsPreset"], repairPlan));
            ResetPlan();
            Status = loc["AddedToDownloadQueue"];
        }
    });
    [RelayCommand] private void CancelPreview() => ResetPlan();
    [RelayCommand(CanExecute = nameof(CanChangeInstalled))]
    private Task ToggleEnabledAsync() => ChangeInstalledAsync(async (service, package) =>
        await service.SetEnabledAsync(package.FullName, !package.Enabled));
    [RelayCommand(CanExecute = nameof(CanChangeInstalled))]
    private Task TogglePinnedAsync() => ChangeInstalledAsync(async (service, package) =>
        await service.SetPinnedAsync(package.FullName, !package.Pinned));
    [RelayCommand(CanExecute = nameof(CanChangeInstalled))] private void RequestUninstall() { ResetPlan(); PendingUninstall = SelectedInstalled?.FullName; }
    [RelayCommand] private void CancelUninstall() => PendingUninstall = null;
    [RelayCommand(CanExecute = nameof(CanUninstall))]
    private Task ConfirmUninstallAsync() => RunAsync(async () =>
    {
        if (PendingUninstall is not { } name) return;
        await new ThunderstoreInstallService(RequireTarget(), store).UninstallAsync(name);
        PendingUninstall = null;
        await RefreshInstalledAsync();
        await refreshInstance();
    });
    private Task ChangeInstalledAsync(Func<ThunderstoreInstallService, ThunderstoreInstalledPackage, Task> action) => RunAsync(async () =>
    {
        var selected = SelectedInstalled ?? throw new InvalidOperationException(loc["SelectMod"]);
        await action(new ThunderstoreInstallService(RequireTarget(), store), selected);
        await RefreshInstalledAsync();
        await refreshInstance();
    });
    private (ThunderstorePackage Package, string? Version)? ResolvePackageLocator(string value)
    {
        value = value.Trim();
        if (catalog is null || value.Length == 0) return null;
        var exact = catalog.Packages.FirstOrDefault(item => item.FullName.Equals(value, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return (exact, null);
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Host != "thunderstore.io") return null;
        try
        {
            var (name, version) = ThunderstoreIdentity.ParseLink(value);
            var package = catalog.Packages.FirstOrDefault(item => item.FullName.Equals(name, StringComparison.OrdinalIgnoreCase));
            return package is null ? null : (package, version);
        }
        catch (InvalidDataException) { return null; }
    }
    private void ApplyPackageLocatorSearch(string value)
    {
        var located = ResolvePackageLocator(value);
        if (located?.Package.IsDeprecated == true && !ShowDeprecated) ShowDeprecated = true;
        Filter(located?.Package.FullName);
        if (located is not { } target) return;
        Error = null;
        SelectedPackage = target.Package;
        SelectedVersion = target.Version is null ? Versions.FirstOrDefault()
            : Versions.FirstOrDefault(item => item.VersionNumber == target.Version);
        if (target.Version is not null && SelectedVersion is null) Error = loc["TsVersionMissing"];
    }
    [RelayCommand(CanExecute = nameof(CanReadDocument))]
    private Task ReadDocumentAsync(string name) => RunAsync(() => LoadTranslatedDocumentAsync(name));
    public Task ImportZipAsync(string path) => RunAsync(async () =>
    {
        var target = RequireTarget();
        await RequireCatalogAsync();
        var manifest = await ThunderstorePackageStore.ValidateAsync(path, null);
        var package = new ThunderstorePackage
        {
            Name = manifest.Name, Owner = "Local", FullName = "Local-" + manifest.Name,
            Versions = [new ThunderstoreVersion { Name = manifest.Name, FullName = "Local-" + manifest.Name + "-" + manifest.Version,
                VersionNumber = manifest.Version, FileSize = new FileInfo(path).Length, Dependencies = manifest.Dependencies }]
        };
        var cached = await store.ImportLocalAsync(path, new(package, package.Versions[0]));
        _ = cached;
        var augmented = catalog! with { Packages = catalog.Packages.Where(item => item.FullName != package.FullName).Append(package).ToArray() };
        catalog = augmented;
        Filter();
        SelectedPackage = package;
        var state = await new ThunderstoreInstallService(target, store).ReadStateAsync();
        var plan = new ThunderstoreDependencyResolver(augmented).Resolve(target.GameId, target.BuildId,
            [(package.FullName, manifest.Version)], state.Packages);
        plan = plan with { Packages = plan.Packages.Select(item => item.Package.FullName == package.FullName ? item with { IsLocal = true } : item).ToArray() };
        PresentPlan(target, plan, false);
    });
    public Task ExportPresetAsync(string path) => RunAsync(async () =>
    {
        var preset = await new ThunderstoreInstallService(RequireTarget(), store).CreatePresetAsync(Path.GetFileNameWithoutExtension(path));
        await AtomicJsonStore.WriteAsync(path, preset);
        Status = loc["Complete"];
    });
    public Task ImportPresetAsync(string path) => RunAsync(async () =>
    {
        var target = RequireTarget();
        await RequireCatalogAsync();
        if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException(loc["TsInvalidPreset"]);
        var preset = await AtomicJsonStore.ReadAsync<ThunderstorePreset>(path);
        if (preset.GameId != target.GameId || preset.Packages is null || preset.Packages.Count > 1000
            || preset.Packages.Any(item => item is null)
            || preset.Packages.Select(item => item.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != preset.Packages.Count)
            throw new InvalidDataException(loc["TsInvalidPreset"]);
        var state = await new ThunderstoreInstallService(target, store).ReadStateAsync();
        var plan = new ThunderstoreDependencyResolver(catalog!).Resolve(target.GameId, target.BuildId,
            preset.Packages.Select(item => (item.FullName, (string?)item.Version)).ToArray(), state.Packages);
        plan = plan with { Packages = plan.Packages.Select(item =>
        {
            var saved = preset.Packages.FirstOrDefault(value => value.FullName == item.Package.FullName);
            return saved is null ? item : item with { DesiredEnabled = saved.Enabled, DesiredPinned = saved.Pinned };
        }).ToArray() };
        PresentPlan(target, plan, false);
    });
    private InstanceRecord RequireTarget() => getTarget() is { GameId: GameId.Silksong } target
        ? target : throw new InvalidOperationException(loc["NoInstance"]);
    private async Task RunAsync(Func<Task> action)
    {
        if (IsLoading) return;
        var generationAtStart = generation;
        Error = null;
        IsLoading = true;
        try { await action(); }
        catch (OperationCanceledException) { if (generationAtStart == generation) Status = loc["Canceled"]; }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException
            or UnauthorizedAccessException or HttpRequestException or JsonException or ArgumentException or BadImageFormatException)
        { if (generationAtStart == generation) Error = exception.Message; }
        finally { IsLoading = false; }
    }
}

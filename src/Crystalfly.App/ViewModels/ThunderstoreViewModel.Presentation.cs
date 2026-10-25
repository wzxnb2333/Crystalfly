using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.Core.Models;
using Crystalfly.Core.Thunderstore;

namespace Crystalfly.App.ViewModels;

public sealed partial class ThunderstoreViewModel
{
    public ObservableCollection<ThunderstoreInstalledPackage> InstalledMods { get; } = [];
    [ObservableProperty] public partial string InstalledSearchText { get; set; } = "";
    public bool HasUninstallConfirmation => PendingUninstall is not null;
    public bool HasConfirmation => HasPlan || HasUninstallConfirmation;
    public IReadOnlyList<ThunderstorePlanItemViewModel> PlanItems => pendingPlan?.Packages.Select(item => new ThunderstorePlanItemViewModel(
        loc[item.Package.FullName == SilksongLoaderAdapter.PackageName ? "Loader" : "Mods"],
        DisplayName(item.Package.FullName, item.Package.Name), item.Package.FullName,
        $"{item.PreviousVersion ?? "—"} → {item.Version.VersionNumber}{(item.Pinned ? " · " + loc["TsPinned"] : "")}")).ToArray() ?? [];
    public bool HasTarget => getTarget()?.GameId == GameId.Silksong;
    public bool HasCatalog => catalog is not null;
    public bool HasPackageSelection => SelectedPackage is not null;
    public bool HasInstalledSelection => SelectedInstalled is not null;
    public bool HasDocument => !string.IsNullOrEmpty(Document);
    public bool CanManage => HasTarget && !IsLoading;
    public ThunderstoreInstalledPackage? InstalledLoader => Installed.FirstOrDefault(item => item.IsLoader);
    public bool HasLoader => InstalledLoader is not null;
    public string EnableActionLabel => loc[SelectedInstalled?.Enabled == true ? "TsDisable" : "TsEnable"];
    public string PinActionLabel => loc[SelectedInstalled?.Pinned == true ? "TsUnpin" : "TsPin"];
    public string CatalogEmptyTitle => loc[HasCatalog ? "TsNoSearchResults" : "TsCatalogEmpty"];
    public string CatalogEmptyHint => loc[HasCatalog ? "TsAdjustFilters" : "TsCatalogEmptyHint"];
    private bool CanPreview() => !IsLoading && GetAvailableInstallTargets().Count > 0 && SelectedPackage is not null && SelectedVersion is not null;
    private bool CanChangeInstalled() => CanManage && SelectedInstalled is not null;
    private bool CanReadDocument() => !IsLoading && SelectedPackage is not null && SelectedVersion is not null;
    private bool CanConfirm() => !IsLoading && HasPlan && installTarget is not null;
    private bool CanUninstall() => CanManage && PendingUninstall is not null;
    private bool CanRefresh() => !IsLoading;

    private void NotifyActions()
    {
        foreach (var name in new[] { nameof(HasTarget), nameof(CanManage), nameof(HasPackageSelection),
            nameof(HasInstalledSelection), nameof(EnableActionLabel), nameof(PinActionLabel), nameof(HasLoader),
            nameof(InstalledLoader), nameof(HasCatalog), nameof(CatalogEmptyTitle), nameof(CatalogEmptyHint) })
            OnPropertyChanged(name);
        RefreshCommand.NotifyCanExecuteChanged();
        PreviewInstallCommand.NotifyCanExecuteChanged();
        PreviewSelectedCommand.NotifyCanExecuteChanged();
        PreviewUpdateCommand.NotifyCanExecuteChanged();
        PreviewRepairCommand.NotifyCanExecuteChanged();
        PreviewInstalledUpdateCommand.NotifyCanExecuteChanged();
        PreviewLoaderCommand.NotifyCanExecuteChanged();
        ConfirmInstallCommand.NotifyCanExecuteChanged();
        ToggleEnabledCommand.NotifyCanExecuteChanged();
        TogglePinnedCommand.NotifyCanExecuteChanged();
        RequestUninstallCommand.NotifyCanExecuteChanged();
        RequestLoaderUninstallCommand.NotifyCanExecuteChanged();
        ConfirmUninstallCommand.NotifyCanExecuteChanged();
        ReadDocumentCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsLoadingChanged(bool value) => NotifyActions();
    partial void OnHasPlanChanged(bool value) { OnPropertyChanged(nameof(PlanItems)); OnPropertyChanged(nameof(HasConfirmation)); NotifyActions(); }
    partial void OnPendingUninstallChanged(string? value)
    {
        OnPropertyChanged(nameof(HasUninstallConfirmation));
        OnPropertyChanged(nameof(HasConfirmation));
        ConfirmUninstallCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(PendingUninstallDisplay));
    }
    partial void OnDocumentChanged(string value) { OnPropertyChanged(nameof(HasDocument)); OnPropertyChanged(nameof(DocumentMarkdown)); }
    partial void OnInstalledSearchTextChanged(string value) => FilterInstalled();
    [RelayCommand] private void ChooseInstance() => chooseInstance?.Invoke();
    [RelayCommand] private void BrowseMarket() => browseMarket?.Invoke();
    [RelayCommand] private void ClearFilters() { SearchText = ""; Category = Categories.FirstOrDefault(); }
    private bool CanRemoveLoader() => CanManage && HasLoader;
    [RelayCommand(CanExecute = nameof(CanRemoveLoader))]
    private void RequestLoaderUninstall()
    {
        ResetPlan();
        PendingUninstall = InstalledLoader?.FullName;
    }

    private void FilterInstalled()
    {
        var previous = SelectedInstalled?.FullName;
        InstalledMods.Clear();
        foreach (var item in Installed.Where(item => !item.IsLoader && (string.IsNullOrWhiteSpace(InstalledSearchText)
            || (item.FullName + "\n" + DisplayName(item.FullName)).Contains(InstalledSearchText.Trim(), StringComparison.OrdinalIgnoreCase))))
            InstalledMods.Add(item);
        SelectedInstalled = InstalledMods.FirstOrDefault(item => item.FullName == previous);
        NotifyTranslationProjection();
    }

    [RelayCommand(CanExecute = nameof(CanChangeInstalled))]
    private Task PreviewInstalledUpdateAsync() => PreviewInstalledAsync(true, false);

    private Task PreviewInstalledAsync(bool latest, bool repair) => RunAsync(async () =>
    {
        var target = RequireTarget();
        var selected = SelectedInstalled ?? throw new InvalidOperationException(loc["SelectMod"]);
        if (catalog is null) await RefreshCatalogAsync();
        var package = catalog?.Packages.FirstOrDefault(item => item.FullName == selected.FullName)
            ?? selected.LocalPackage ?? throw new InvalidDataException(loc["TsPackageMissing"]);
        var version = latest ? package.Latest : package.Versions.FirstOrDefault(item => item.VersionNumber == selected.Version);
        if (version is null) throw new InvalidDataException(loc["TsVersionMissing"]);
        await CreatePlanAsync(target, package, version, repair);
    });

    [RelayCommand(CanExecute = nameof(CanManage))]
    private Task PreviewLoaderAsync() => RunAsync(async () =>
    {
        var target = RequireTarget();
        if (catalog is null) await RefreshCatalogAsync();
        var package = catalog?.Packages.FirstOrDefault(item => item.FullName == SilksongLoaderAdapter.PackageName)
            ?? throw new InvalidDataException(loc["TsPackageMissing"]);
        var version = package.Versions.FirstOrDefault(item => item.VersionNumber == SilksongLoaderAdapter.PackageVersion)
            ?? throw new InvalidDataException(loc["TsVersionMissing"]);
        await CreatePlanAsync(target, package, version, InstalledLoader is not null);
    });

    private async Task CreatePlanAsync(InstanceRecord target, ThunderstorePackage package, ThunderstoreVersion version, bool repair)
    {
        var expectedGeneration = generation;
        var selectedCatalog = catalog ?? throw new InvalidOperationException(loc["TsCatalogRequired"]);
        var state = await new ThunderstoreInstallService(target, store).ReadStateAsync();
        if (expectedGeneration != generation || !GetAvailableInstallTargets().Contains(target)) return;
        var plan = new ThunderstoreDependencyResolver(selectedCatalog).Resolve(target.GameId, target.BuildId,
            [(package.FullName, version.VersionNumber)], state.Packages);
        PresentPlan(target, plan, repair);
        installRequests = [(package.FullName, version.VersionNumber)];
    }

    public int ContextVersion => generation;
    private async Task RequireCatalogAsync()
    {
        if (catalog is not null) return;
        try { await RefreshCatalogAsync(); }
        catch (Exception exception) when (exception is IOException or HttpRequestException or InvalidOperationException)
        { throw new InvalidOperationException(loc["TsCatalogRequired"], exception); }
        if (catalog is null) throw new InvalidOperationException(loc["TsCatalogRequired"]);
    }

    public async Task EnsureLoadedAsync()
    {
        if (IsLoading) return;
        if (catalog is null) await RefreshAsync();
        else await RefreshInstalledAsync();
    }
}

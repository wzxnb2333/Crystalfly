using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.Core.Models;
using Crystalfly.Core.Thunderstore;

namespace Crystalfly.App.ViewModels;

public sealed partial class ThunderstoreViewModel
{
    private readonly HashSet<string> selectedPackages = new(StringComparer.OrdinalIgnoreCase);
    private InstanceRecord? installTarget;
    private string? installContext;
    private IReadOnlyList<(string Name, string? Version)> installRequests = [];
    public ObservableCollection<ThunderstoreInstallTargetOptionViewModel> InstallTargets { get; } = [];
    public string InstallTargetName => installTarget?.Name ?? TargetName;
    public int SelectedPackageCount => selectedPackages.Count;
    public string SelectedPackagesText => string.Format(loc.Culture, loc["SelectedModCount"], SelectedPackageCount);
    public bool HasSelectedPackages => SelectedPackageCount > 0;

    public void SetPackageSelected(string name, bool selected)
    {
        if (selected ? !selectedPackages.Add(name) : !selectedPackages.Remove(name)) return;
        foreach (var item in PackageDisplayItems.Where(item => item.FullName.Equals(name, StringComparison.OrdinalIgnoreCase)))
            item.IsSelected = selected;
        OnPropertyChanged(nameof(SelectedPackageCount));
        OnPropertyChanged(nameof(SelectedPackagesText));
        OnPropertyChanged(nameof(HasSelectedPackages));
        PreviewSelectedCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ClearPackageSelection()
    {
        foreach (var name in selectedPackages.ToArray()) SetPackageSelected(name, false);
    }

    private bool CanPreviewSelected() => !IsLoading && HasSelectedPackages && GetAvailableInstallTargets().Count > 0;
    [RelayCommand(CanExecute = nameof(CanPreviewSelected))]
    private Task PreviewSelectedAsync() => RunAsync(async () =>
    {
        var target = GetAvailableInstallTargets().FirstOrDefault(item => item.Id == getTarget()?.Id)
            ?? GetAvailableInstallTargets().FirstOrDefault() ?? throw new InvalidOperationException(loc["NoInstance"]);
        var selectedCatalog = catalog ?? throw new InvalidOperationException(loc["TsCatalogRequired"]);
        var requests = selectedPackages.Select(name => (name, (string?)null)).ToArray();
        var expected = generation;
        var state = await new ThunderstoreInstallService(target, store).ReadStateAsync();
        if (expected != generation) return;
        var plan = new ThunderstoreDependencyResolver(selectedCatalog).Resolve(target.GameId, target.BuildId, requests, state.Packages);
        PresentPlan(target, plan, false);
        installRequests = requests;
    });

    internal IReadOnlyList<InstanceRecord> GetAvailableInstallTargets() =>
        (getInstallTargets?.Invoke() ?? (getTarget() is { } target ? [target] : []))
            .Where(item => item.GameId == GameId.Silksong && item.Purpose != InstancePurpose.OfficialSpeedrun).ToArray();

    private string ContextKey() => getTarget() is { } target ? $"{target.GameId}:{target.Id}:{target.RootPath}:{target.BuildId}" : "";

    public async Task SelectInstallTargetAsync(InstanceRecord target)
    {
        await RunAsync(async () =>
        {
            if (pendingPlan is null || !GetAvailableInstallTargets().Contains(target))
                throw new InvalidOperationException(loc["TsTargetChanged"]);
            var expected = generation;
            var requests = installRequests;
            var state = await new ThunderstoreInstallService(target, store).ReadStateAsync();
            if (expected != generation || pendingPlan is null) return;
            var plan = new ThunderstoreDependencyResolver(catalog ?? throw new InvalidOperationException(loc["TsCatalogRequired"]))
                .Resolve(target.GameId, target.BuildId, requests, state.Packages);
            var previous = pendingPlan;
            pendingPlan = plan with { Packages = plan.Packages.Select(item =>
            {
                var old = previous.Packages.FirstOrDefault(candidate => candidate.Package.FullName == item.Package.FullName);
                return old is null ? item : item with { DesiredEnabled = old.DesiredEnabled, DesiredPinned = old.DesiredPinned, IsLocal = old.IsLocal };
            }).ToArray() };
            installTarget = target;
            pendingTarget = target.Id;
            PreviewText = PlanText(pendingPlan);
            OnPropertyChanged(nameof(PlanItems));
        });
        NotifyInstallTarget();
    }

    private void NotifyInstallTarget()
    {
        foreach (var option in InstallTargets) option.SetSelected(option.Target.Id == installTarget?.Id);
        OnPropertyChanged(nameof(InstallTargetName));
        NotifyActions();
    }
}

public sealed class ThunderstoreInstallTargetOptionViewModel(ThunderstoreViewModel owner, InstanceRecord target) : ViewModelBase
{
    private bool isSelected;
    public InstanceRecord Target { get; } = target;
    public string DisplayName => Target.Name;
    public string BuildDisplayName => Target.BuildId;
    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (SetProperty(ref isSelected, value) && value && !owner.IsLoading)
                _ = owner.SelectInstallTargetAsync(Target);
        }
    }
    internal void SetSelected(bool selected) => SetProperty(ref isSelected, selected, nameof(IsSelected));
}

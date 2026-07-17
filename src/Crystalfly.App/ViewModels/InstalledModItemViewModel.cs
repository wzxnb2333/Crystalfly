using CommunityToolkit.Mvvm.ComponentModel;
using Crystalfly.Core.Models;

namespace Crystalfly.App.ViewModels;

public enum ModStatusFilter
{
    All,
    Enabled,
    Disabled,
    Local,
    Updates
}

public partial class InstalledModItemViewModel : ViewModelBase
{
    private readonly Action selectionChanged;

    public InstalledModItemViewModel(
        InstalledModReceipt receipt,
        ModManifest? catalogManifest,
        Action selectionChanged)
    {
        Receipt = receipt;
        CatalogManifest = catalogManifest;
        this.selectionChanged = selectionChanged;
    }

    public InstalledModReceipt Receipt { get; }

    public ModManifest? CatalogManifest { get; }

    public string Id => Receipt.Id;

    public string Name => Receipt.Name;

    public string Version => Receipt.Version;

    public string LoaderId => Receipt.LoaderId;

    public bool IsEnabled => Receipt.Enabled;

    public bool Enabled => IsEnabled;

    public bool IsLocal => Receipt.IsLocal;

    public bool HasUpdate => !IsLocal
        && CatalogManifest is not null
        && !string.Equals(Version, CatalogManifest.Version, StringComparison.OrdinalIgnoreCase);

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged(bool value) => selectionChanged();

    public bool Matches(string search, ModStatusFilter status)
    {
        bool matchesText = string.IsNullOrWhiteSpace(search)
            || Name.Contains(search, StringComparison.OrdinalIgnoreCase)
            || Id.Contains(search, StringComparison.OrdinalIgnoreCase)
            || Version.Contains(search, StringComparison.OrdinalIgnoreCase);
        return matchesText && status switch
        {
            ModStatusFilter.Enabled => IsEnabled,
            ModStatusFilter.Disabled => !IsEnabled,
            ModStatusFilter.Local => IsLocal,
            ModStatusFilter.Updates => HasUpdate,
            _ => true
        };
    }
}

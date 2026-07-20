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
    private readonly string searchText;

    public InstalledModItemViewModel(
        InstalledModReceipt receipt,
        ModManifest? catalogManifest,
        Action selectionChanged,
        MarketModItemViewModel? marketDisplay = null)
    {
        Receipt = receipt;
        CatalogManifest = catalogManifest;
        MarketDisplay = marketDisplay;
        this.selectionChanged = selectionChanged;
        searchText = string.Join('\n', new[]
        {
            receipt.Id,
            receipt.Name,
            receipt.Version,
            receipt.LoaderId,
            marketDisplay?.SearchText
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    public InstalledModReceipt Receipt { get; }

    public ModManifest? CatalogManifest { get; }

    public MarketModItemViewModel? MarketDisplay { get; }

    public string Id => Receipt.Id;

    public string PrimaryName => MarketDisplay?.PrimaryName ?? Receipt.Name;

    public string SecondaryName => MarketDisplay?.SecondaryName ?? string.Empty;

    public bool HasSecondaryName => !string.IsNullOrWhiteSpace(SecondaryName);

    public string Name => PrimaryName;

    public string ReceiptName => Receipt.Name;

    public string? Description => MarketDisplay?.PrimaryDescription;

    public IReadOnlyList<MarketTagViewModel> Tags => MarketDisplay?.Tags ?? [];

    public bool HasCatalogManifest => CatalogManifest is not null;

    public string InstallRoot => Receipt.InstallRoot;

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
            || searchText.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase);
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

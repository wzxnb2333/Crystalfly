using CommunityToolkit.Mvvm.ComponentModel;
using Crystalfly.Core.Models;

namespace Crystalfly.App.ViewModels;

public enum ModStatusFilter
{
    All,
    Enabled,
    Disabled,
    Local,
    Updates,
    External,
    Pinned,
    NeedsAttention
}

public partial class InstalledModItemViewModel : ViewModelBase
{
    private readonly Action selectionChanged;
    private readonly string searchText;

    public InstalledModItemViewModel(
        InstalledModReceipt receipt,
        ModManifest? catalogManifest,
        Action selectionChanged,
        MarketModItemViewModel? marketDisplay = null,
        string? ownershipDisplayName = null,
        string? healthDisplayName = null)
        : this(
            FromReceipt(receipt),
            receipt,
            new ModHealthReport { ModId = receipt.Id, Status = ModHealthStatus.Healthy },
            catalogManifest,
            selectionChanged,
            marketDisplay,
            ownershipDisplayName,
            healthDisplayName)
    {
    }

    public InstalledModItemViewModel(
        ModDiscoveryEntry discovery,
        InstalledModReceipt? receipt,
        ModHealthReport healthReport,
        ModManifest? catalogManifest,
        Action selectionChanged,
        MarketModItemViewModel? marketDisplay = null,
        string? ownershipDisplayName = null,
        string? healthDisplayName = null)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(healthReport);
        Discovery = discovery;
        Receipt = receipt;
        HealthReport = healthReport;
        CatalogManifest = catalogManifest;
        MarketDisplay = marketDisplay;
        OwnershipDisplayName = ownershipDisplayName ?? discovery.Ownership.ToString();
        HealthDisplayName = healthDisplayName ?? healthReport.Status.ToString();
        this.selectionChanged = selectionChanged;
        searchText = string.Join('\n', new[]
        {
            discovery.Id,
            discovery.Name,
            receipt?.Version,
            discovery.LoaderId,
            discovery.Ownership.ToString(),
            healthReport.Status.ToString(),
            marketDisplay?.SearchText
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    public ModDiscoveryEntry Discovery { get; }

    public InstalledModReceipt? Receipt { get; }

    public ModHealthReport HealthReport { get; }

    public ModManifest? CatalogManifest { get; }

    public MarketModItemViewModel? MarketDisplay { get; }

    public string OwnershipDisplayName { get; }

    public string HealthDisplayName { get; }

    public string Id => Discovery.Id;

    public string PrimaryName => MarketDisplay?.PrimaryName ?? Discovery.Name;

    public string SecondaryName => MarketDisplay?.SecondaryName ?? string.Empty;

    public bool HasSecondaryName => !string.IsNullOrWhiteSpace(SecondaryName);

    public string Name => PrimaryName;

    public string ReceiptName => Receipt?.Name ?? Discovery.Name;

    public string? Description => MarketDisplay?.PrimaryDescription;

    public IReadOnlyList<MarketTagViewModel> Tags => MarketDisplay?.Tags ?? [];

    public bool HasCatalogManifest => CatalogManifest is not null;

    public string InstallRoot => Discovery.InstallRoot;

    public string Version => Receipt?.Version ?? "external";

    public string LoaderId => Discovery.LoaderId;

    public bool IsEnabled => Discovery.Enabled;

    public bool Enabled => IsEnabled;

    public bool IsLocal => Receipt?.IsLocal == true || Discovery.Ownership == ModOwnership.LocalTakenOver;

    public ModOwnership Ownership => Discovery.Ownership;

    public bool IsExternal => Ownership == ModOwnership.External;

    public bool IsReadOnly => IsExternal;

    public bool IsPinned => Receipt?.Pinned == true;

    public ModHealthStatus HealthStatus => HealthReport.Status;

    public bool HasHealthIssue => HealthStatus != ModHealthStatus.Healthy;

    public bool CanTakeOver => IsExternal;

    public bool CanPin => Receipt is not null && !IsReadOnly;

    public bool CanToggle => Receipt is not null && !IsReadOnly;

    public bool CanUpdate => Receipt is not null && !IsReadOnly && !IsLocal && !IsPinned;

    public bool CanUninstall => Receipt is not null && !IsReadOnly && !IsPinned;

    public bool CanRepair => Receipt is not null
        && !IsReadOnly
        && !IsLocal
        && !IsPinned
        && HealthStatus is ModHealthStatus.CriticalFileMissing or ModHealthStatus.ModifiedFile
        && CatalogManifest is not null;

    public bool CanReinstall => Receipt is not null
        && Ownership == ModOwnership.Managed
        && !IsLocal
        && !IsPinned
        && CatalogManifest is not null
        && string.Equals(Version, CatalogManifest.Version, StringComparison.OrdinalIgnoreCase)
        && string.Equals(LoaderId, CatalogManifest.LoaderId, StringComparison.OrdinalIgnoreCase);

    public bool CanAcceptCurrent => IsLocal && HasHealthIssue;

    public bool CanReimport => IsLocal;

    public bool HasUpdate => CanUpdate
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
            ModStatusFilter.External => IsExternal,
            ModStatusFilter.Pinned => IsPinned,
            ModStatusFilter.NeedsAttention => HasHealthIssue,
            _ => true
        };
    }

    private static ModDiscoveryEntry FromReceipt(InstalledModReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return new ModDiscoveryEntry
        {
            Id = receipt.Id,
            Name = receipt.Name,
            LoaderId = receipt.LoaderId,
            InstallRoot = receipt.InstallRoot,
            Enabled = receipt.Enabled,
            Ownership = receipt.Ownership,
            Files = receipt.Files.Select(file => file.RelativePath).ToArray(),
            EntryFiles = receipt.EntryFiles
        };
    }
}

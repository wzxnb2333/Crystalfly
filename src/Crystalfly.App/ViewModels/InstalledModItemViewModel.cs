using System.Globalization;
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
        string? healthDisplayName = null,
        string? instanceRoot = null)
        : this(
            FromReceipt(receipt),
            receipt,
            new ModHealthReport { ModId = receipt.Id, Status = ModHealthStatus.Healthy },
            catalogManifest,
            selectionChanged,
            marketDisplay,
            ownershipDisplayName,
            healthDisplayName,
            instanceRoot)
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
        string? healthDisplayName = null,
        string? instanceRoot = null)
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
        InstallDateText = FormatInstallDate(instanceRoot, receipt);
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

    public string DependenciesText => CatalogManifest is null
        ? string.Empty
        : string.Join(", ", CatalogManifest.Dependencies);

    public string AuthorsText => CatalogManifest is null
        ? string.Empty
        : string.Join(", ", CatalogManifest.Authors);

    public string RepositoryUrl => CatalogManifest?.RepositoryUrl ?? string.Empty;

    public bool HasRepositoryUrl => !string.IsNullOrWhiteSpace(RepositoryUrl);

    public string InstallDateText { get; }

    public bool HasInstallDate => !string.IsNullOrWhiteSpace(InstallDateText);

    public string LatestVersionText => HasUpdate
        ? CatalogManifest!.Version
        : string.Empty;

    public bool HasLatestVersion => HasUpdate;

    public string ModifiedFilesText => string.Join(Environment.NewLine, HealthReport.ModifiedFiles);

    public bool HasModifiedFilesText => !string.IsNullOrWhiteSpace(ModifiedFilesText);

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged(bool value) => selectionChanged();

    [ObservableProperty]
    public partial bool HasConflicts { get; set; }

    [ObservableProperty]
    public partial string ConflictWithText { get; set; } = string.Empty;

    internal void SetConflict(string? conflictWithText)
    {
        HasConflicts = !string.IsNullOrWhiteSpace(conflictWithText);
        ConflictWithText = conflictWithText ?? string.Empty;
    }

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

    private static string FormatInstallDate(string? instanceRoot, InstalledModReceipt? receipt)
    {
        if (string.IsNullOrWhiteSpace(instanceRoot) || receipt is null || receipt.Files.Count == 0)
        {
            return string.Empty;
        }

        DateTimeOffset? newest = null;
        foreach (var file in receipt.Files)
        {
            try
            {
                var path = Path.Combine(
                    instanceRoot,
                    file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(path))
                {
                    var time = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
                    if (newest is null || time > newest)
                    {
                        newest = time;
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
        return newest is null
            ? string.Empty
            : newest.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
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

using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.Core.Thunderstore;

namespace Crystalfly.App.ViewModels;

public sealed partial class ThunderstoreViewModel
{
    private readonly SilksongModTranslationSource translations = translationSource ?? new();
    private bool projecting;
    private int documentRequest;
    private SilksongTranslatedDocument? loadedDocument;
    private bool Chinese => loc.Culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
    private IReadOnlyList<ThunderstorePackageDisplayItem>? packageDisplayItems;
    public IReadOnlyList<ThunderstorePackageDisplayItem> PackageDisplayItems => packageDisplayItems ??=
        Packages.Select(item => Display(item, item.Latest)).ToArray();
    private IReadOnlyList<ThunderstoreInstalledDisplayItem>? installedDisplayItems;
    public IReadOnlyList<ThunderstoreInstalledDisplayItem> InstalledDisplayItems => installedDisplayItems ??= InstalledMods
        .Select(item => new ThunderstoreInstalledDisplayItem(item, DisplayName(item.FullName))
        { IsSelected = selectedInstalled.Contains(item.FullName), SelectionChanged = selected => SetInstalledSelected(item.FullName, selected),
            IconUrl = (InstalledCatalogPackage(item)?.Versions.FirstOrDefault(version => version.VersionNumber == item.Version) ?? InstalledCatalogPackage(item)?.Latest)?.IconUrl ?? "",
            HasUpdate = HasInstalledUpdate(item), Status = InstalledNeedsAttention(item) ? loc["NeedsAttention"] : loc[item.Enabled ? "TsEnabled" : "TsDisabled"] }).ToArray();
    public ThunderstorePackageDisplayItem? SelectedDisplayPackage
    {
        get => PackageDisplayItems.FirstOrDefault(item => item.FullName == SelectedPackage?.FullName);
        set { if (!projecting && value is not null) SelectedPackage = value.Package; }
    }
    public ThunderstoreInstalledDisplayItem? SelectedInstalledDisplay
    {
        get => InstalledDisplayItems.FirstOrDefault(item => item.FullName == SelectedInstalled?.FullName);
        set { if (!projecting && value is not null) SelectedInstalled = value.Package; }
    }
    public string SelectedPackageName => SelectedPackage is null ? "" : DisplayName(SelectedPackage.FullName, SelectedPackage.Name);
    public string SelectedPackageOriginalName => SelectedPackage?.Name ?? "";
    public bool HasSelectedOriginalName => Chinese && SelectedPackageName != SelectedPackageOriginalName;
    public string SelectedDescription => SelectedPackage is null ? "" : Display(SelectedPackage, SelectedVersion).Description;
    public string SelectedOriginalDescription => SelectedVersion?.Description ?? "";
    public string SelectedCategories => string.Join(" · ", SelectedPackage?.Categories ?? []);
    public string SelectedDependencies => string.Join("\n", SelectedVersion?.Dependencies ?? []);
    public bool HasSelectedOriginalDescription => SelectedDescription != SelectedOriginalDescription;
    public string PendingUninstallDisplay => PendingUninstall is null ? "" : string.Join("\n", uninstallNames.Select(name => DisplayName(name) + " (" + name + ")"));
    public string SelectedInstalledName => SelectedInstalled is null ? "" : DisplayName(SelectedInstalled.FullName);
    public string TranslationNotice => Chinese && SelectedPackage is not null
        && !Display(SelectedPackage, SelectedVersion).DescriptionTranslated && !string.IsNullOrEmpty(SelectedVersion?.Description)
        ? loc["TsTranslationMissing"] : "";
    [ObservableProperty] public partial bool ShowOriginalDocument { get; set; }
    [ObservableProperty] public partial string DocumentNotice { get; set; } = "";
    public bool HasDocumentTranslation => loadedDocument?.Chinese is not null;
    public bool ChineseDocumentSelected => !ShowOriginalDocument && HasDocumentTranslation;
    public bool OriginalDocumentSelected => !ChineseDocumentSelected;
    public bool HasDocumentNotice => !string.IsNullOrEmpty(DocumentNotice);

    private ThunderstorePackageDisplayItem Display(ThunderstorePackage package, ThunderstoreVersion? version)
    {
        var entry = translations.Find(package.FullName);
        var name = Chinese && entry is not null ? entry.DisplayName : package.Name;
        var translatedVersion = entry?.Versions.FirstOrDefault(item => item.Version == version?.VersionNumber);
        var translated = Chinese && translatedVersion?.Description is not null && version is not null
            && translatedVersion.DescriptionSha256?.Equals(SilksongModTranslationSource.Hash(version.Description), StringComparison.OrdinalIgnoreCase) == true;
        return new(package, name, Chinese && name != package.Name ? package.Name : "",
            translated ? translatedVersion!.Description! : version?.Description ?? "", translated)
        {
            IsSelected = selectedPackages.Contains(package.FullName),
            SelectionChanged = selected => SetPackageSelected(package.FullName, selected)
        };
    }

    private string DisplayName(string fullName, string? original = null) => Chinese && translations.Find(fullName) is { } entry
        ? entry.DisplayName : original ?? catalog?.Packages.FirstOrDefault(item => item.FullName == fullName)?.Name ?? fullName;

    private bool MatchesSearch(ThunderstorePackage package, string query)
    {
        var entry = translations.Find(package.FullName);
        var version = entry?.Versions.FirstOrDefault(item => item.Version == package.Latest?.VersionNumber
            && item.DescriptionSha256?.Equals(SilksongModTranslationSource.Hash(package.Latest.Description), StringComparison.OrdinalIgnoreCase) == true);
        return string.Join('\n', new[] { package.FullName, package.Name, package.Owner, package.Latest?.Description,
            entry?.DisplayName, version?.Description }.Concat(package.Categories)
            .Concat(package.Categories.Select(item => translations.Catalog.CategoryNames.GetValueOrDefault(item, item))))
            .Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void NotifyTranslationProjection()
    {
        packageDisplayItems = null;
        installedDisplayItems = null;
        projecting = true;
        try
        {
            foreach (var name in new[] { nameof(PackageDisplayItems), nameof(InstalledDisplayItems), nameof(SelectedDisplayPackage),
                nameof(SelectedInstalledDisplay), nameof(SelectedPackageName), nameof(SelectedPackageOriginalName), nameof(HasSelectedOriginalName),
                nameof(SelectedDescription), nameof(SelectedOriginalDescription), nameof(HasSelectedOriginalDescription), nameof(TranslationNotice),
                nameof(PendingUninstallDisplay), nameof(SelectedInstalledName), nameof(SelectedCategories), nameof(SelectedDependencies) })
                OnPropertyChanged(name);
            if (pendingPlan is not null) PreviewText = PlanText(pendingPlan);
            OnPropertyChanged(nameof(PlanItems));
        }
        finally { projecting = false; }
    }

    private string PlanText(ThunderstoreInstallPlan plan) => string.Join(Environment.NewLine, plan.Packages.Select(item =>
        $"{DisplayName(item.Package.FullName, item.Package.Name)} ({item.Package.FullName})  {item.PreviousVersion ?? "—"} → {item.Version.VersionNumber}{(item.Pinned ? " [" + loc["TsPinned"] + "]" : "")}"));

    private async Task RefreshTranslationsAsync(int expectedGeneration)
    {
        await translations.RefreshAsync(isOffline());
        if (expectedGeneration != generation) return;
        RebuildCategories();
        Filter();
        NotifyTranslationProjection();
    }

    private void ResetDocument()
    {
        Interlocked.Increment(ref documentRequest);
        loadedDocument = null;
        Document = "";
        DocumentNotice = "";
        ShowOriginalDocument = !Chinese;
        OnPropertyChanged(nameof(HasDocumentTranslation));
        NotifyDocumentSelection();
    }

    [RelayCommand] private void SetDocumentLanguage(string mode) => ShowOriginalDocument = mode == "original";
    partial void OnShowOriginalDocumentChanged(bool value) { PresentDocument(); NotifyDocumentSelection(); }
    private void NotifyDocumentSelection()
    {
        OnPropertyChanged(nameof(ChineseDocumentSelected));
        OnPropertyChanged(nameof(OriginalDocumentSelected));
    }
    private void PresentDocument()
    {
        if (loadedDocument is null) return;
        Document = loadedDocument.Missing ? loc["TsNoDocument"]
            : (!ShowOriginalDocument ? loadedDocument.Chinese : null) ?? loadedDocument.Original;
        DocumentNotice = loadedDocument.Missing ? "" : !ShowOriginalDocument && loadedDocument.Chinese is null
            ? loc["TsTranslationMissing"] : !ShowOriginalDocument ? loc["TsTranslationAttribution"] : "";
        OnPropertyChanged(nameof(HasDocumentTranslation));
        NotifyDocumentSelection();
    }
    partial void OnDocumentNoticeChanged(string value) => OnPropertyChanged(nameof(HasDocumentNotice));

    // Images remain links in the viewer so reading an offline document cannot fetch third-party images.
    public string DocumentMarkdown => Regex.Replace(Document, @"(?s)(```.*?```|~~~.*?~~~)|(!\[([^\]]*)\]\(([^)]+)\))|(<img\b[^>]*>)",
        match => match.Groups[1].Success ? match.Value : match.Groups[2].Success
            ? $"[{match.Groups[3].Value}]({match.Groups[4].Value})" : "", RegexOptions.IgnoreCase);

    private async Task LoadTranslatedDocumentAsync(string name)
    {
        if (name is not ("README.md" or "CHANGELOG.md")) throw new InvalidDataException("Unsupported document.");
        var package = SelectedPackage ?? throw new InvalidOperationException(loc["SelectMod"]);
        var version = SelectedVersion ?? throw new InvalidOperationException(loc["SelectMod"]);
        var context = generation;
        var request = Interlocked.Increment(ref documentRequest);
        var result = await translations.ReadDocumentAsync(package.FullName, version.VersionNumber, name, isOffline());
        // If an archive is already cached, it is authoritative and its document bytes must still match.
        var archivePath = Path.Combine(store.Root, package.FullName + "-" + version.VersionNumber + ".zip");
        if (result is null || File.Exists(archivePath))
        {
            var cached = await store.GetAsync(new(package, version), isOffline());
            var original = await ThunderstorePackageStore.ReadDocumentAsync(cached.Path, name);
            result = new(original, result is { Missing: false } && original == result.Original ? result.Chinese : null, string.IsNullOrWhiteSpace(original));
        }
        if (context != generation || request != documentRequest || SelectedPackage?.FullName != package.FullName
            || SelectedVersion?.VersionNumber != version.VersionNumber || getTarget()?.GameId is { } game && game != Crystalfly.Core.Models.GameId.Silksong) return;
        loadedDocument = result;
        PresentDocument();
    }
}

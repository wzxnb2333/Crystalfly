using Crystalfly.Core.Thunderstore;

namespace Crystalfly.App.ViewModels;

public sealed record ThunderstorePlanItemViewModel(string Kind, string Name, string FullName, string VersionChange);

public sealed class ThunderstorePackageDisplayItem(ThunderstorePackage package, string primaryName,
    string secondaryName, string description, bool descriptionTranslated) : ViewModelBase
{
    public ThunderstorePackage Package { get; } = package;
    public string PrimaryName { get; } = primaryName;
    public string SecondaryName { get; } = secondaryName;
    public string Description { get; } = description;
    public bool DescriptionTranslated { get; } = descriptionTranslated;
    private bool isSelected;
    public Action<bool>? SelectionChanged { get; init; }
    public bool IsSelected
    {
        get => isSelected;
        set { if (SetProperty(ref isSelected, value)) SelectionChanged?.Invoke(value); }
    }
    public string FullName => Package.FullName;
    public string Owner => Package.Owner;
    public string Version => Package.Latest?.VersionNumber ?? "";
    public string IconUrl => Package.Latest?.IconUrl ?? "";
    public bool HasSecondaryName => !string.IsNullOrEmpty(SecondaryName);
}

public sealed record ThunderstoreInstalledDisplayItem(ThunderstoreInstalledPackage Package, string PrimaryName)
{
    public string FullName => Package.FullName;
    public string Version => Package.Version;
    public bool Enabled => Package.Enabled;
    public bool Pinned => Package.Pinned;
}

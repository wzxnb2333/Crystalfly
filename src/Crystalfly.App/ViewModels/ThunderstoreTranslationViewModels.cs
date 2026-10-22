using Crystalfly.Core.Thunderstore;

namespace Crystalfly.App.ViewModels;

public sealed record ThunderstorePackageDisplayItem(ThunderstorePackage Package, string PrimaryName,
    string SecondaryName, string Description, bool DescriptionTranslated)
{
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

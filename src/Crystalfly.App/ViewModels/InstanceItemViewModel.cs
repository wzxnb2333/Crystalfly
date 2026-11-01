using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;

namespace Crystalfly.App.ViewModels;

public sealed record InstanceItemViewModel(
    InstanceRecord Record,
    string DisplayVersion,
    string LoaderDisplay,
    int ModCount,
    bool IsFavorite = false)
{
    public string Id => Record.Id;

    public string Name => Record.Name;

    public string RootPath => Record.RootPath;

    public bool IsKnownBuild => BuildIdentity.IsKnown(Record.BuildId);

    public string GroupHeading { get; set; } = string.Empty;

    public bool HasGroupHeading => GroupHeading.Length > 0;

    public Avalonia.Thickness HierarchyMargin { get; set; }

    public string StorageRelationship { get; set; } = string.Empty;

    public bool IsChildInstance { get; set; }

    public string GameName => GameDefinition.Get(Record.GameId).Name;
    public bool IsSilksong => Record.GameId == GameId.Silksong;
    public int GroupCount { get; set; }
    public int HierarchyDepth { get; set; }
    public bool HasChildren { get; set; }
    public string StorageSummary { get; set; } = string.Empty;
    public Avalonia.Thickness HierarchyContentMargin => new(40, 0, 152, 0);
    public string? TreeGeometry { get; set; }
}

public sealed record SettingOption<T>(T Value, string Name);

public sealed record MarketInstallTargetViewModel(
    InstanceItemViewModel Instance,
    string BuildDisplayName,
    string LoaderDisplayName,
    string StatusText,
    bool IsAvailable,
    bool RequiresLoader)
{
    public string DisplayName => Instance.Name;
}

public sealed record DownloadBuildOption(
    string BuildId,
    string DisplayName,
    ulong? ManifestId)
{
    public string ReleaseDateText { get; init; } = string.Empty;

    public string FirstSeenDateText { get; init; } = string.Empty;

    public string VerificationText { get; init; } = string.Empty;

    public bool IsDownloaded { get; init; }
}

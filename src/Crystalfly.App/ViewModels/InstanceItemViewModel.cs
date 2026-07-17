using Crystalfly.Core.Models;

namespace Crystalfly.App.ViewModels;

public sealed record InstanceItemViewModel(
    InstanceRecord Record,
    string DisplayVersion,
    string LoaderDisplay,
    int ModCount)
{
    public string Id => Record.Id;

    public string Name => Record.Name;

    public string RootPath => Record.RootPath;

    public bool IsKnownBuild => Record.BuildId != "unknown";
}

public sealed record SettingOption<T>(T Value, string Name);

public sealed record DownloadBuildOption(
    string BuildId,
    string DisplayName,
    ulong? ManifestId);

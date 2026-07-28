using CommunityToolkit.Mvvm.ComponentModel;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Instances;

namespace Crystalfly.App.ViewModels;

public sealed partial class GameDirectoryItemViewModel(GameDirectoryRegistration registration) : ViewModelBase
{
    public GameDirectoryRegistration Registration { get; private set; } = registration;

    public string Path => Registration.Path;

    public string DisplayName => Registration.DisplayName;

    public bool IsSteam => Registration.Source == GameDirectorySourceKind.Steam;

    public bool SteamRiskAccepted => Registration.SteamRiskAccepted;

    [ObservableProperty]
    public partial int InstanceCount { get; set; }

    [ObservableProperty]
    public partial string ScanStatus { get; set; } = string.Empty;

    internal void UpdateRegistration(GameDirectoryRegistration value)
    {
        Registration = value;
        OnPropertyChanged(nameof(Path));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(IsSteam));
        OnPropertyChanged(nameof(SteamRiskAccepted));
    }
}

public sealed partial class GameDirectoryCandidateItemViewModel(GameDirectoryCandidate candidate) : ViewModelBase
{
    public GameDirectoryCandidate Candidate { get; } = candidate;

    public string Path => Candidate.Path;

    public string DisplayName => Candidate.DisplayName;

    public bool IsSteam => Candidate.Source == GameDirectorySourceKind.Steam;

    [ObservableProperty]
    public partial bool IsConfirmed { get; set; }
}

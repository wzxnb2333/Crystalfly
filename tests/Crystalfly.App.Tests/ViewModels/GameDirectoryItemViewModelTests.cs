using Crystalfly.App.ViewModels;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Instances;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class GameDirectoryItemViewModelTests
{
    [Fact]
    public void Registration_is_projected_for_the_directory_rail()
    {
        var registration = new GameDirectoryRegistration
        {
            Path = @"D:\HK_ver",
            DisplayName = "HK versions",
            Source = GameDirectorySourceKind.Steam,
            SteamRiskAccepted = true
        };

        var item = new GameDirectoryItemViewModel(registration)
        {
            InstanceCount = 4,
            ScanStatus = "Ready"
        };

        Assert.Equal(registration, item.Registration);
        Assert.Equal(@"D:\HK_ver", item.Path);
        Assert.Equal("HK versions", item.DisplayName);
        Assert.True(item.IsSteam);
        Assert.Equal(4, item.InstanceCount);
        Assert.Equal("Ready", item.ScanStatus);
    }

    [Fact]
    public void Candidate_selection_is_explicit_before_registration()
    {
        var candidate = new GameDirectoryCandidate
        {
            Path = @"C:\Steam\steamapps\common\Hollow Knight",
            DisplayName = "Hollow Knight",
            Source = GameDirectorySourceKind.Steam
        };

        var item = new GameDirectoryCandidateItemViewModel(candidate);

        Assert.False(item.IsConfirmed);
        Assert.True(item.IsSteam);
        item.IsConfirmed = true;
        Assert.True(item.IsConfirmed);
    }
}

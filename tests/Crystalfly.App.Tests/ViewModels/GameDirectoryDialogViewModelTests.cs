using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.Dialogs;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Instances;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class GameDirectoryDialogViewModelTests
{
    [Fact]
    public void Discovery_dialog_returns_requested_action()
    {
        var candidates = new[]
        {
            new GameDirectoryCandidateItemViewModel(new GameDirectoryCandidate
            {
                Path = @"D:\HK",
                DisplayName = "HK",
                Source = GameDirectorySourceKind.Custom
            })
        };
        var dialog = new GameDirectoryDiscoveryDialogViewModel(
            "title", "message", candidates, "scan", "add", "confirm", "skip");
        object? result = null;
        dialog.RequestClose += (_, value) => result = value;

        dialog.ConfirmCommand.Execute(null);

        Assert.Equal(GameDirectoryDiscoveryDialogResult.Confirm, result);
    }

    [Fact]
    public void Three_choice_dialog_returns_primary_and_secondary_actions()
    {
        var dialog = new ThreeChoiceDialogViewModel("title", "message", "target", "primary", "secondary", "cancel", true);
        object? result = null;
        dialog.RequestClose += (_, value) => result = value;

        dialog.PrimaryCommand.Execute(null);
        Assert.Equal(ThreeChoiceDialogResult.Primary, result);

        dialog.SecondaryCommand.Execute(null);
        Assert.Equal(ThreeChoiceDialogResult.Secondary, result);
    }
}

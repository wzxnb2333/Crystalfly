using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.Dialogs;
using Crystalfly.App.ViewModels.DependencyGraph;
using Avalonia.Media;
using Crystalfly.Core.Models;
using Crystalfly.Core.Runtime;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class DialogViewModelTests
{
    [Fact]
    public void Accent_color_dialog_synchronizes_hex_preview_and_result()
    {
        var previews = new List<string>();
        var dialog = new AccentColorDialogViewModel(
            "Theme color",
            "Original",
            "New",
            "HEX",
            "Invalid color",
            "Confirm",
            "Cancel",
            "#0F6CBD",
            previews.Add);
        object? result = "unchanged";
        dialog.RequestClose += (_, value) => result = value;

        dialog.HexText = "be185d";

        Assert.True(dialog.CanConfirm);
        Assert.Equal("#BE185D", previews[^1]);
        Assert.Equal(Color.Parse("#BE185D"), dialog.SelectedColor);

        dialog.HexText = "invalid";
        Assert.False(dialog.CanConfirm);
        Assert.Equal("#BE185D", previews[^1]);

        dialog.SelectedColor = Color.Parse("#15803D");
        Assert.Equal("#15803D", dialog.HexText);
        Assert.Equal("#15803D", previews[^1]);
        dialog.ConfirmCommand.Execute(null);
        Assert.Equal("#15803D", result);
    }

    [Fact]
    public void Text_input_trims_confirmed_value_and_rejects_blank_text()
    {
        var dialog = new TextInputDialogViewModel(
            "Clone instance",
            "Enter a name",
            "  Practice Copy  ",
            "Instance name",
            "Confirm",
            "Cancel");
        object? result = null;
        dialog.RequestClose += (_, value) => result = value;

        Assert.True(dialog.CanConfirm);
        Assert.True(dialog.ConfirmCommand.CanExecute(null));
        dialog.ConfirmCommand.Execute(null);

        Assert.Equal("Practice Copy", result);

        result = "unchanged";
        dialog.Text = "   ";
        Assert.False(dialog.CanConfirm);
        Assert.False(dialog.ConfirmCommand.CanExecute(null));
        dialog.ConfirmCommand.Execute(null);
        Assert.Equal("unchanged", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("18446744073709551616")]
    [InlineData("42.0")]
    public void Historical_manifest_dialog_rejects_invalid_manifest_text(string manifestId)
    {
        var dialog = new HistoricalManifestDialogViewModel(
            [], "Title", "Message", "Manifest", "Instance", "Known: {0}",
            "Unverified", "Invalid", "Confirm", "Cancel")
        {
            ManifestId = manifestId,
            InstanceName = "Historical"
        };

        Assert.False(dialog.CanConfirm);
    }

    [Fact]
    public void Silksong_history_dialog_uses_its_depot_and_keeps_candidates_unverified()
    {
        var dialog = new HistoricalManifestDialogViewModel(
            [new("silksong-1.0.30000", "1.0.30000", 4421626056705534276UL),
                new("steam-manifest-3229726349000518284", "Manifest 3229726349000518284", 3229726349000518284UL)],
            "Title", "Message", "Manifest", "Instance", "Known: {0}",
            "Unverified", "Invalid", "Confirm", "Cancel", GameId.Silksong)
        {
            ManifestId = "3229726349000518284", InstanceName = "History"
        };
        Assert.Equal("AppID 1030300 · Windows Depot 1030301", dialog.DepotIdentity);
        Assert.True(dialog.CanConfirm);
        Assert.False(dialog.IsKnownManifest);
        Assert.Equal("Unverified", dialog.ValidationMessage);
        dialog.ManifestId = "4421626056705534276";
        Assert.True(dialog.IsKnownManifest);
        Assert.Equal("Known: 1.0.30000", dialog.ValidationMessage);
    }

    [Fact]
    public void Launch_issue_dialog_returns_force_choice_and_warning_acknowledgement()
    {
        var dialog = new LaunchIssuesDialogViewModel(
            "Launch warnings",
            "Review these issues before launching.",
            [new LaunchIssueItemViewModel("Modified file", "DebugMod.dll", LaunchIssueSeverity.Warning)],
            "Force launch",
            "Cancel",
            "Do not remind for unchanged warnings",
            canForceLaunch: true)
        {
            DoNotRemind = true
        };
        object? result = null;
        dialog.RequestClose += (_, value) => result = value;

        dialog.ForceLaunchCommand.Execute(null);

        var launchResult = Assert.IsType<LaunchIssuesDialogResult>(result);
        Assert.True(launchResult.ForceLaunch);
        Assert.True(launchResult.DoNotRemind);
    }

    [Fact]
    public void Launch_issue_dialog_never_forces_absolute_blockers()
    {
        var dialog = new LaunchIssuesDialogViewModel(
            "Launch blocked",
            "Resolve blocking issues first.",
            [new LaunchIssueItemViewModel("Loader conflict", string.Empty, LaunchIssueSeverity.Blocking)],
            "Force launch",
            "Close",
            "Do not remind",
            canForceLaunch: false);
        object? result = "unchanged";
        dialog.RequestClose += (_, value) => result = value;

        Assert.False(dialog.ForceLaunchCommand.CanExecute(null));
        dialog.ForceLaunchCommand.Execute(null);
        Assert.Equal("unchanged", result);

        dialog.Close();
        var closeResult = Assert.IsType<LaunchIssuesDialogResult>(result);
        Assert.False(closeResult.ForceLaunch);
        Assert.False(closeResult.DoNotRemind);
    }
}

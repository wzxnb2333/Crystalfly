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
    public void Accent_color_dialog_cancel_and_close_return_no_color()
    {
        var dialog = new AccentColorDialogViewModel(
            "Theme color",
            "Original",
            "New",
            "HEX",
            "Invalid color",
            "Confirm",
            "Cancel",
            "#0F6CBD",
            _ => { });
        var results = new List<object?>();
        dialog.RequestClose += (_, value) => results.Add(value);

        dialog.CancelCommand.Execute(null);
        dialog.Close();

        Assert.Equal([null, null], results);
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

    [Fact]
    public void Text_input_cancel_and_close_return_null()
    {
        var dialog = new TextInputDialogViewModel("Title", "Message", "Value", "Placeholder", "OK", "Cancel");
        var results = new List<object?>();
        dialog.RequestClose += (_, value) => results.Add(value);

        dialog.CancelCommand.Execute(null);
        dialog.Close();

        Assert.Equal([null, null], results);
    }

    [Fact]
    public void Mod_pack_editor_trims_name_and_returns_selected_apply_mode()
    {
        var dialog = new ModPackEditorDialogViewModel(
            "New Mod Pack",
            "Capture current Mods",
            "  Practice  ",
            ModPresetApplyMode.Append,
            "Name",
            "Mode",
            "Append",
            "Exact",
            "Create",
            "Cancel");
        object? result = null;
        dialog.RequestClose += (_, value) => result = value;
        dialog.SelectedMode = dialog.ModeOptions.Single(option => option.Value == ModPresetApplyMode.Exact);

        dialog.ConfirmCommand.Execute(null);

        Assert.Equal(new ModPackEditorDialogResult("Practice", ModPresetApplyMode.Exact), result);
        dialog.Name = "   ";
        Assert.False(dialog.ConfirmCommand.CanExecute(null));
    }
    [Fact]
    public void Historical_manifest_dialog_accepts_only_nonzero_unsigned_ids_and_returns_the_request()
    {
        var dialog = new HistoricalManifestDialogViewModel(
            [new DownloadBuildOption("known-build", "Known build", 42)],
            "Download historical version",
            "Use a Windows Depot manifest ID.",
            "Manifest ID",
            "Instance name",
            "Known: {0}",
            "Unverified historical version · vanilla only",
            "Invalid manifest ID",
            "Continue",
            "Cancel");
        object? result = null;
        dialog.RequestClose += (_, value) => result = value;

        dialog.ManifestId = "0";
        dialog.InstanceName = "Historical";
        Assert.False(dialog.CanConfirm);
        Assert.Equal("Invalid manifest ID", dialog.ValidationMessage);

        dialog.ManifestId = "42";
        Assert.True(dialog.CanConfirm);
        Assert.True(dialog.IsKnownManifest);
        Assert.Equal("Known: Known build", dialog.ValidationMessage);
        dialog.ConfirmCommand.Execute(null);

        Assert.Equal(new HistoricalManifestDownloadRequest(42, "Historical"), result);
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
    public void Dependency_dialog_projects_graph_and_respects_confirmation_state()
    {
        var nodes = new[]
        {
            new DependencyPlanNodeViewModel(
                "feature",
                "Feature",
                "功能",
                "Enabled",
                DependencyGraphNodeState.Normal,
                "Will delete",
                ["library"]),
            new DependencyPlanNodeViewModel(
                "library",
                "Library",
                "前置库",
                "Missing",
                DependencyGraphNodeState.Missing)
        };
        var dialog = new DependencyPlanDialogViewModel(
            "Dependency impact",
            "Review affected mods",
            nodes,
            "Delete",
            "Cancel",
            canConfirm: false,
            isDangerous: true);
        object? result = null;
        dialog.RequestClose += (_, value) => result = value;

        Assert.Same(nodes, dialog.Nodes);
        Assert.True(dialog.IsDangerous);
        Assert.False(dialog.ConfirmCommand.CanExecute(null));
        Assert.Contains(dialog.Graph.Edges, edge => edge.Source.Id == "library" && edge.Target.Id == "feature");
        Assert.Equal(DependencyGraphNodeState.Missing, Assert.Single(dialog.Graph.Nodes, node => node.Id == "library").State);
        dialog.ConfirmCommand.Execute(null);
        Assert.Null(result);

        dialog.CancelCommand.Execute(null);
        Assert.Equal(false, result);
    }

    [Fact]
    public void Dependency_dialog_projects_real_relationships_into_a_graph()
    {
        var nodes = new[]
        {
            new DependencyPlanNodeViewModel(
                "feature",
                "Feature",
                string.Empty,
                "Enabled",
                DependencyGraphNodeState.Normal,
                PrerequisiteIds: ["library"]),

            new DependencyPlanNodeViewModel(
                "library",
                "Library",
                string.Empty,
                "Missing",
                DependencyGraphNodeState.Missing)



        };
        var dialog = new DependencyPlanDialogViewModel(
            "Dependency impact",
            "Review affected mods",
            nodes,
            "Delete",
            "Cancel",
            canConfirm: true,
            isDangerous: true);

        Assert.Contains(dialog.Graph.Edges, edge => edge.Source.Id == "library" && edge.Target.Id == "feature");
        var missing = Assert.Single(dialog.Graph.Nodes, node => node.Id == "library");
        Assert.True(missing.IsMissing);
        Assert.False(missing.IsProblem);

    }

    [Fact]
    public void Dependency_dialog_confirm_and_close_return_expected_results()
    {
        var dialog = new DependencyPlanDialogViewModel(
            "Repair dependencies",
            "Review actions",
            [],
            "Repair",
            "Cancel",
            canConfirm: true,
            isDangerous: false);
        var results = new List<object?>();
        dialog.RequestClose += (_, value) => results.Add(value);

        dialog.ConfirmCommand.Execute(null);
        dialog.Close();

        Assert.Equal([true, false], results);
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

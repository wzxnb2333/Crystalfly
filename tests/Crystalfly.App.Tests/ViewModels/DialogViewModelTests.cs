using Crystalfly.App.ViewModels.Dialogs;
using Crystalfly.Core.Runtime;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class DialogViewModelTests
{
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
    public void Dependency_nodes_expose_indent_and_dialog_respects_confirmation_state()
    {
        var nodes = new[]
        {
            new DependencyPlanNodeViewModel("Feature", "功能", "hkmod:feature", "C:/Mods/Feature", "Enabled", 0, true, false),
            new DependencyPlanNodeViewModel("Library", "前置库", "hkmod:library", "C:/Mods/Library", "Missing", 2, false, true)
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

        Assert.Equal(0, nodes[0].Indent);
        Assert.Equal(32, nodes[1].Indent);
        var targetLabel = typeof(DependencyPlanNodeViewModel).GetProperty("TargetLabel");
        var unresolvedLabel = typeof(DependencyPlanNodeViewModel).GetProperty("UnresolvedLabel");
        Assert.NotNull(targetLabel);
        Assert.NotNull(unresolvedLabel);
        Assert.Equal("Target", targetLabel.GetValue(nodes[0]));
        Assert.Equal("Unresolved", unresolvedLabel.GetValue(nodes[1]));
        Assert.Same(nodes, dialog.Nodes);
        Assert.True(dialog.IsDangerous);
        Assert.False(dialog.ConfirmCommand.CanExecute(null));
        dialog.ConfirmCommand.Execute(null);
        Assert.Null(result);

        dialog.CancelCommand.Execute(null);
        Assert.Equal(false, result);
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

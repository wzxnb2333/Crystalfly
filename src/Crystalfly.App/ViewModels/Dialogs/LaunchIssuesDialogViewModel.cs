using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.Core.Runtime;
using Irihi.Avalonia.Shared.Contracts;

namespace Crystalfly.App.ViewModels.Dialogs;

public sealed record LaunchIssueItemViewModel(
    string Title,
    string Detail,
    LaunchIssueSeverity Severity)
{
    public bool IsBlocking => Severity == LaunchIssueSeverity.Blocking;

    public bool IsForceable => Severity == LaunchIssueSeverity.Forceable;

    public bool IsWarning => Severity == LaunchIssueSeverity.Warning;
}

public sealed record LaunchIssuesDialogResult(bool ForceLaunch, bool DoNotRemind);

public sealed partial class LaunchIssuesDialogViewModel : ViewModelBase, IDialogContext
{
    public LaunchIssuesDialogViewModel(
        string title,
        string message,
        IReadOnlyList<LaunchIssueItemViewModel> issues,
        string forceLaunchText,
        string cancelText,
        string doNotRemindText,
        bool canForceLaunch)
    {
        Title = title;
        Message = message;
        Issues = issues;
        ForceLaunchText = forceLaunchText;
        CancelText = cancelText;
        DoNotRemindText = doNotRemindText;
        CanForceLaunch = canForceLaunch;
    }

    public string Title { get; }

    public string Message { get; }

    public IReadOnlyList<LaunchIssueItemViewModel> Issues { get; }

    public string ForceLaunchText { get; }

    public string CancelText { get; }

    public string DoNotRemindText { get; }

    public bool CanForceLaunch { get; }

    public bool ShowDoNotRemind => CanForceLaunch
        && Issues.Any(issue => issue.Severity == LaunchIssueSeverity.Warning);

    [ObservableProperty]
    public partial bool DoNotRemind { get; set; }

    public event EventHandler<object?>? RequestClose;

    public void Close() => Cancel();

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, new LaunchIssuesDialogResult(false, false));

    [RelayCommand(CanExecute = nameof(CanForceLaunch))]
    private void ForceLaunch()
    {
        if (CanForceLaunch)
        {
            RequestClose?.Invoke(this, new LaunchIssuesDialogResult(true, DoNotRemind));
        }
    }
}

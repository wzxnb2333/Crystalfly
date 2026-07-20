using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;

namespace Crystalfly.App.ViewModels.Dialogs;

public sealed class DependencyPlanNodeViewModel(
    string primaryName,
    string secondaryName,
    string modId,
    string installRoot,
    string status,
    int depth,
    bool isTarget,
    bool isUnresolved,
    string targetLabel = "Target",
    string unresolvedLabel = "Unresolved")
{
    public string PrimaryName { get; } = primaryName;

    public string SecondaryName { get; } = secondaryName;

    public string ModId { get; } = modId;

    public string InstallRoot { get; } = installRoot;

    public string Status { get; } = status;

    public int Depth { get; } = Math.Max(0, depth);

    public int Indent => Depth * 16;

    public bool IsTarget { get; } = isTarget;

    public bool IsUnresolved { get; } = isUnresolved;

    public string TargetLabel { get; } = targetLabel;

    public string UnresolvedLabel { get; } = unresolvedLabel;
}

public sealed partial class DependencyPlanDialogViewModel : ViewModelBase, IDialogContext
{
    public DependencyPlanDialogViewModel(
        string title,
        string message,
        IReadOnlyList<DependencyPlanNodeViewModel> nodes,
        string confirmText,
        string cancelText,
        bool canConfirm,
        bool isDangerous)
    {
        Title = title;
        Message = message;
        Nodes = nodes;
        ConfirmText = confirmText;
        CancelText = cancelText;
        CanConfirm = canConfirm;
        IsDangerous = isDangerous;
    }

    public string Title { get; }

    public string Message { get; }

    public IReadOnlyList<DependencyPlanNodeViewModel> Nodes { get; }

    public string ConfirmText { get; }

    public string CancelText { get; }

    public bool CanConfirm { get; }

    public bool IsDangerous { get; }

    public event EventHandler<object?>? RequestClose;

    public void Close() => Cancel();

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, false);

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        if (CanConfirm)
        {
            RequestClose?.Invoke(this, true);
        }
    }
}

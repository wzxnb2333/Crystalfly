using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;

namespace Crystalfly.App.ViewModels.Dialogs;

public sealed partial class ConfirmationDialogViewModel : ViewModelBase, IDialogContext
{
    public ConfirmationDialogViewModel(
        string title,
        string message,
        string target,
        string confirmText,
        string cancelText,
        bool canConfirm,
        bool isDangerous)
    {
        Title = title;
        Message = message;
        Target = target;
        ConfirmText = confirmText;
        CancelText = cancelText;
        CanConfirm = canConfirm;
        IsDangerous = isDangerous;
    }

    public string Title { get; }

    public string Message { get; }

    public string Target { get; }

    public string ConfirmText { get; }

    public string CancelText { get; }

    public bool CanConfirm { get; }

    public bool IsDangerous { get; }

    public event EventHandler<object?>? RequestClose;

    public void Close() => Cancel();

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, false);

    [RelayCommand]
    private void Confirm()
    {
        if (CanConfirm)
        {
            RequestClose?.Invoke(this, true);
        }
    }
}

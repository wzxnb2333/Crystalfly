using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;

namespace Crystalfly.App.ViewModels.Dialogs;

public sealed partial class SteamGuardDialogViewModel : ViewModelBase, IDialogContext
{
    public SteamGuardDialogViewModel(
        string title,
        string message,
        string placeholder,
        string confirmText,
        string cancelText,
        string incorrectHintText,
        bool showIncorrectHint)
    {
        Title = title;
        Message = message;
        Placeholder = placeholder;
        ConfirmText = confirmText;
        CancelText = cancelText;
        IncorrectHintText = incorrectHintText;
        ShowIncorrectHint = showIncorrectHint;
    }

    public string Title { get; }

    public string Message { get; }

    public string Placeholder { get; }

    public string ConfirmText { get; }

    public string CancelText { get; }

    public string IncorrectHintText { get; }

    public bool ShowIncorrectHint { get; }

    [ObservableProperty]
    private string code = string.Empty;

    public bool CanConfirm => !string.IsNullOrWhiteSpace(Code);

    public event EventHandler<object?>? RequestClose;

    public void Close() => Cancel();

    partial void OnCodeChanged(string value)
    {
        OnPropertyChanged(nameof(CanConfirm));
        ConfirmCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, null);

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        if (CanConfirm)
        {
            RequestClose?.Invoke(this, Code.Trim());
        }
    }
}

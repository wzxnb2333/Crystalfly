using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;

namespace Crystalfly.App.ViewModels.Dialogs;

public sealed partial class TextInputDialogViewModel : ViewModelBase, IDialogContext
{
    public TextInputDialogViewModel(
        string title,
        string message,
        string initialValue,
        string placeholder,
        string confirmText,
        string cancelText)
    {
        Title = title;
        Message = message;
        text = initialValue;
        Placeholder = placeholder;
        ConfirmText = confirmText;
        CancelText = cancelText;
    }

    public string Title { get; }

    public string Message { get; }

    [ObservableProperty]
    private string text;

    public string Placeholder { get; }

    public string ConfirmText { get; }

    public string CancelText { get; }

    public bool CanConfirm => !string.IsNullOrWhiteSpace(Text);

    public event EventHandler<object?>? RequestClose;

    public void Close() => Cancel();

    partial void OnTextChanged(string value)
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
            RequestClose?.Invoke(this, Text.Trim());
        }
    }
}

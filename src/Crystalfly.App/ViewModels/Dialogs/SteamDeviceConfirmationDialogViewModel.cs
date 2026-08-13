using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;

namespace Crystalfly.App.ViewModels.Dialogs;

public sealed partial class SteamDeviceConfirmationDialogViewModel : ViewModelBase, IDialogContext
{
    public SteamDeviceConfirmationDialogViewModel(
        string title,
        string message,
        string confirmText,
        string switchToCodeText,
        string cancelText)
    {
        Title = title;
        Message = message;
        ConfirmText = confirmText;
        SwitchToCodeText = switchToCodeText;
        CancelText = cancelText;
    }

    public string Title { get; }

    public string Message { get; }

    public string ConfirmText { get; }

    public string SwitchToCodeText { get; }

    public string CancelText { get; }

    public event EventHandler<object?>? RequestClose;

    public void Close() => Cancel();

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, null);

    [RelayCommand]
    private void Confirm() => RequestClose?.Invoke(this, true);

    [RelayCommand]
    private void SwitchToCode() => RequestClose?.Invoke(this, false);
}

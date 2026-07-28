using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;

namespace Crystalfly.App.ViewModels.Dialogs;

public enum ThreeChoiceDialogResult
{
    Primary,
    Secondary,
    Cancel
}

public sealed partial class ThreeChoiceDialogViewModel(
    string title,
    string message,
    string target,
    string primaryText,
    string secondaryText,
    string cancelText,
    bool primaryDangerous) : ViewModelBase, IDialogContext
{
    public string Title { get; } = title;
    public string Message { get; } = message;
    public string Target { get; } = target;
    public string PrimaryText { get; } = primaryText;
    public string SecondaryText { get; } = secondaryText;
    public string CancelText { get; } = cancelText;
    public bool PrimaryDangerous { get; } = primaryDangerous;
    public event EventHandler<object?>? RequestClose;
    public void Close() => Cancel();
    [RelayCommand] private void Primary() => RequestClose?.Invoke(this, ThreeChoiceDialogResult.Primary);
    [RelayCommand] private void Secondary() => RequestClose?.Invoke(this, ThreeChoiceDialogResult.Secondary);
    [RelayCommand] private void Cancel() => RequestClose?.Invoke(this, ThreeChoiceDialogResult.Cancel);
}

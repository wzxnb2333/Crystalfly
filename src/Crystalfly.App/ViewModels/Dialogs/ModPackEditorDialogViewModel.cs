using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.App.ViewModels;
using Crystalfly.Core.Models;
using Irihi.Avalonia.Shared.Contracts;

namespace Crystalfly.App.ViewModels.Dialogs;

public sealed record ModPackEditorDialogResult(string Name, ModPresetApplyMode ApplyMode);

public sealed partial class ModPackEditorDialogViewModel : ViewModelBase, IDialogContext
{
    public ModPackEditorDialogViewModel(
        string title,
        string message,
        string initialName,
        ModPresetApplyMode initialMode,
        string namePlaceholder,
        string modeLabel,
        string appendText,
        string exactText,
        string confirmText,
        string cancelText)
    {
        Title = title;
        Message = message;
        NamePlaceholder = namePlaceholder;
        ModeLabel = modeLabel;
        ConfirmText = confirmText;
        CancelText = cancelText;
        name = initialName;
        ModeOptions =
        [
            new SettingOption<ModPresetApplyMode>(ModPresetApplyMode.Append, appendText),
            new SettingOption<ModPresetApplyMode>(ModPresetApplyMode.Exact, exactText)
        ];
        selectedMode = ModeOptions.Single(option => option.Value == initialMode);
    }

    public string Title { get; }

    public string Message { get; }

    public string NamePlaceholder { get; }

    public string ModeLabel { get; }

    public string ConfirmText { get; }

    public string CancelText { get; }

    public IReadOnlyList<SettingOption<ModPresetApplyMode>> ModeOptions { get; }

    [ObservableProperty]
    private string name;

    [ObservableProperty]
    private SettingOption<ModPresetApplyMode> selectedMode;

    public bool CanConfirm => !string.IsNullOrWhiteSpace(Name);

    public event EventHandler<object?>? RequestClose;

    public void Close() => Cancel();

    partial void OnNameChanged(string value)
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
            RequestClose?.Invoke(this, new ModPackEditorDialogResult(Name.Trim(), SelectedMode.Value));
        }
    }
}

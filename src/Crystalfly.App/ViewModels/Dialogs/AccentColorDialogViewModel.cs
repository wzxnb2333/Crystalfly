using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.Core.Configuration;
using Irihi.Avalonia.Shared.Contracts;

namespace Crystalfly.App.ViewModels.Dialogs;

public sealed partial class AccentColorDialogViewModel : ViewModelBase, IDialogContext
{
    private readonly Action<string> preview;
    private bool synchronizing;

    public AccentColorDialogViewModel(
        string title,
        string originalLabel,
        string newLabel,
        string hexLabel,
        string invalidColorText,
        string confirmText,
        string cancelText,
        string initialColor,
        Action<string> preview)
    {
        Title = title;
        OriginalLabel = originalLabel;
        NewLabel = newLabel;
        HexLabel = hexLabel;
        InvalidColorText = invalidColorText;
        ConfirmText = confirmText;
        CancelText = cancelText;
        OriginalColor = AccentColorPalette.Normalize(initialColor);
        this.preview = preview;
        synchronizing = true;
        SelectedColor = Color.Parse(OriginalColor);
        HexText = OriginalColor;
        synchronizing = false;
    }

    public string Title { get; }

    public string OriginalLabel { get; }

    public string NewLabel { get; }

    public string HexLabel { get; }

    public string InvalidColorText { get; }

    public string ConfirmText { get; }

    public string CancelText { get; }

    public string OriginalColor { get; }

    public IBrush OriginalBrush => new SolidColorBrush(Color.Parse(OriginalColor));

    public IBrush NewBrush => new SolidColorBrush(SelectedColor);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NewBrush))]
    public partial Color SelectedColor { get; set; }

    [ObservableProperty]
    public partial string HexText { get; set; }

    public bool CanConfirm => AccentColorPalette.TryNormalize(HexText, out _);

    public bool HasValidationError => !CanConfirm;

    public event EventHandler<object?>? RequestClose;

    public void Close() => Cancel();

    partial void OnSelectedColorChanged(Color value)
    {
        if (synchronizing)
        {
            return;
        }

        synchronizing = true;
        try
        {
            HexText = $"#{value.R:X2}{value.G:X2}{value.B:X2}";
        }
        finally
        {
            synchronizing = false;
        }
        preview(HexText);
    }

    partial void OnHexTextChanged(string value)
    {
        OnPropertyChanged(nameof(CanConfirm));
        OnPropertyChanged(nameof(HasValidationError));
        ConfirmCommand.NotifyCanExecuteChanged();
        if (synchronizing || !AccentColorPalette.TryNormalize(value, out var normalized))
        {
            return;
        }

        synchronizing = true;
        try
        {
            SelectedColor = Color.Parse(normalized);
            HexText = normalized;
        }
        finally
        {
            synchronizing = false;
        }
        OnPropertyChanged(nameof(NewBrush));
        preview(normalized);
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, null);

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        if (AccentColorPalette.TryNormalize(HexText, out var normalized))
        {
            RequestClose?.Invoke(this, normalized);
        }
    }
}

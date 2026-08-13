using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.Core.Models;
using Irihi.Avalonia.Shared.Contracts;

namespace Crystalfly.App.ViewModels.Dialogs;

public sealed partial class ModCatalogMatchDialogViewModel : ViewModelBase, IDialogContext
{
    public ModCatalogMatchDialogViewModel(
        string title,
        string message,
        IReadOnlyList<ModManifest> candidates,
        string confirmText,
        string cancelText)
    {
        Title = title;
        Message = message;
        Candidates = candidates;
        ConfirmText = confirmText;
        CancelText = cancelText;
        if (candidates.Count != 0)
        {
            selectedCandidate = candidates[0];
        }
    }

    public string Title { get; }

    public string Message { get; }

    public IReadOnlyList<ModManifest> Candidates { get; }

    public string ConfirmText { get; }

    public string CancelText { get; }

    [ObservableProperty]
    private ModManifest? selectedCandidate;

    public bool CanConfirm => SelectedCandidate is not null;

    public event EventHandler<object?>? RequestClose;

    public void Close() => Cancel();

    partial void OnSelectedCandidateChanged(ModManifest? value)
    {
        OnPropertyChanged(nameof(CanConfirm));
        ConfirmCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, null);

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        if (SelectedCandidate is not null)
        {
            RequestClose?.Invoke(this, SelectedCandidate);
        }
    }
}

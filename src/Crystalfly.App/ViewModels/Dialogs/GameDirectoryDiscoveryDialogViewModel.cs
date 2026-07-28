using Irihi.Avalonia.Shared.Contracts;

namespace Crystalfly.App.ViewModels.Dialogs;

public enum GameDirectoryDiscoveryDialogResult
{
    StartScan,
    AddCustom,
    Confirm,
    Skip
}

public sealed class GameDirectoryDiscoveryDialogViewModel(
    string title,
    string message,
    IReadOnlyList<GameDirectoryCandidateItemViewModel> candidates,
    string scanText,
    string addText,
    string confirmText,
    string skipText) : ViewModelBase, IDialogContext
{
    public string Title { get; } = title;
    public string Message { get; } = message;
    public IReadOnlyList<GameDirectoryCandidateItemViewModel> Candidates { get; } = candidates;
    public string ScanText { get; } = scanText;
    public string AddText { get; } = addText;
    public string ConfirmText { get; } = confirmText;
    public string SkipText { get; } = skipText;
    public bool HasCandidates => Candidates.Count > 0;
    public event EventHandler<object?>? RequestClose;
    public void Close() => RequestClose?.Invoke(this, GameDirectoryDiscoveryDialogResult.Skip);
    public CommunityToolkit.Mvvm.Input.IRelayCommand ScanCommand => new CommunityToolkit.Mvvm.Input.RelayCommand(() => RequestClose?.Invoke(this, GameDirectoryDiscoveryDialogResult.StartScan));
    public CommunityToolkit.Mvvm.Input.IRelayCommand AddCustomCommand => new CommunityToolkit.Mvvm.Input.RelayCommand(() => RequestClose?.Invoke(this, GameDirectoryDiscoveryDialogResult.AddCustom));
    public CommunityToolkit.Mvvm.Input.IRelayCommand ConfirmCommand => new CommunityToolkit.Mvvm.Input.RelayCommand(() => RequestClose?.Invoke(this, GameDirectoryDiscoveryDialogResult.Confirm));
    public CommunityToolkit.Mvvm.Input.IRelayCommand SkipCommand => new CommunityToolkit.Mvvm.Input.RelayCommand(Close);
}

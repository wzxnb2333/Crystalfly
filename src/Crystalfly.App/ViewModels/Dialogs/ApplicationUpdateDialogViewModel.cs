using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;

namespace Crystalfly.App.ViewModels.Dialogs;

public enum ApplicationUpdateDialogResult
{
    Update,
    Later,
    SkipVersion
}

public sealed partial class ApplicationUpdateDialogViewModel : ViewModelBase, IDialogContext
{
    public ApplicationUpdateDialogViewModel(
        string title,
        string version,
        string notesMarkdown,
        string updateText,
        string laterText,
        string skipText)
    {
        Title = title;
        Version = version;
        NotesMarkdown = notesMarkdown;
        UpdateText = updateText;
        LaterText = laterText;
        SkipText = skipText;
    }

    public string Title { get; }

    public string Version { get; }

    public string NotesMarkdown { get; }

    public string UpdateText { get; }

    public string LaterText { get; }

    public string SkipText { get; }

    public event EventHandler<object?>? RequestClose;

    public void Close() => Later();

    [RelayCommand]
    private void Update() => RequestClose?.Invoke(this, ApplicationUpdateDialogResult.Update);

    [RelayCommand]
    private void Later() => RequestClose?.Invoke(this, ApplicationUpdateDialogResult.Later);

    [RelayCommand]
    private void Skip() => RequestClose?.Invoke(this, ApplicationUpdateDialogResult.SkipVersion);
}

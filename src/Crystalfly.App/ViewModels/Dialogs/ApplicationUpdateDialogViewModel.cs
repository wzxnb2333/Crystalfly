using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.App.Updates;
using Irihi.Avalonia.Shared.Contracts;

namespace Crystalfly.App.ViewModels.Dialogs;

public enum ApplicationUpdateDialogResult
{
    Later,
    SkipVersion
}

public enum ApplicationUpdateDialogState
{
    Available,
    Downloading,
    Verifying,
    StartingUpdater,
    Failed
}

public sealed partial class ApplicationUpdateDialogViewModel : ViewModelBase, IDialogContext
{
    private readonly Func<IProgress<ApplicationUpdateProgress>, CancellationToken, Task<bool>> startUpdate;
    private CancellationTokenSource? updateCancellation;

    public ApplicationUpdateDialogViewModel(
        LocalizationViewModel localization,
        string version,
        string notesMarkdown,
        Func<IProgress<ApplicationUpdateProgress>, CancellationToken, Task<bool>> startUpdate)
    {
        Loc = localization ?? throw new ArgumentNullException(nameof(localization));
        Version = version;
        NotesMarkdown = notesMarkdown;
        this.startUpdate = startUpdate ?? throw new ArgumentNullException(nameof(startUpdate));
    }

    public LocalizationViewModel Loc { get; }

    public string Version { get; }

    public string NotesMarkdown { get; }

    public string UpdateText => State == ApplicationUpdateDialogState.Failed
        ? Loc["Retry"]
        : Loc["UpdateNow"];

    public string StatusText => State switch
    {
        ApplicationUpdateDialogState.Downloading => Loc["DownloadingApplicationUpdate"],
        ApplicationUpdateDialogState.Verifying => Loc["VerifyingApplicationUpdate"],
        ApplicationUpdateDialogState.StartingUpdater => Loc["ApplicationUpdateStarting"],
        ApplicationUpdateDialogState.Failed => Loc["ApplicationUpdateFailed"],
        _ => string.Empty
    };

    public bool CanStartUpdate => State is ApplicationUpdateDialogState.Available
        or ApplicationUpdateDialogState.Failed;

    public bool CanCancel => State is ApplicationUpdateDialogState.Downloading
        or ApplicationUpdateDialogState.Verifying;

    public bool IsProgressVisible => State is ApplicationUpdateDialogState.Downloading
        or ApplicationUpdateDialogState.Verifying
        or ApplicationUpdateDialogState.StartingUpdater;

    public bool HasError => State == ApplicationUpdateDialogState.Failed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateText))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(CanStartUpdate))]
    [NotifyPropertyChangedFor(nameof(CanCancel))]
    [NotifyPropertyChangedFor(nameof(IsProgressVisible))]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial ApplicationUpdateDialogState State { get; private set; } = ApplicationUpdateDialogState.Available;

    [ObservableProperty]
    public partial double ProgressValue { get; private set; }

    [ObservableProperty]
    public partial string ProgressText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string ErrorText { get; private set; } = string.Empty;

    public event EventHandler<object?>? RequestClose;

    public void Close()
    {
        if (CanCancel)
        {
            Cancel();
        }
        else if (State != ApplicationUpdateDialogState.StartingUpdater)
        {
            Later();
        }
    }

    [RelayCommand]
    private void CloseDialog() => Close();

    [RelayCommand(CanExecute = nameof(CanStartUpdate))]
    private async Task UpdateAsync()
    {
        updateCancellation?.Dispose();
        updateCancellation = new CancellationTokenSource();
        ErrorText = string.Empty;
        ProgressText = string.Empty;
        ProgressValue = 0;
        State = ApplicationUpdateDialogState.Downloading;

        var progress = new Progress<ApplicationUpdateProgress>(ApplyProgress);
        try
        {
            bool started = await startUpdate(progress, updateCancellation.Token);
            State = started
                ? ApplicationUpdateDialogState.StartingUpdater
                : ApplicationUpdateDialogState.Available;
        }
        catch (OperationCanceledException) when (updateCancellation.IsCancellationRequested)
        {
            State = ApplicationUpdateDialogState.Available;
        }
        catch (Exception exception) when (exception is HttpRequestException
            or IOException
            or InvalidDataException
            or UnauthorizedAccessException
            or InvalidOperationException
            or System.ComponentModel.Win32Exception)
        {
            ErrorText = Loc.ErrorMessageFor(exception);
            State = ApplicationUpdateDialogState.Failed;
        }
        finally
        {
            updateCancellation.Dispose();
            updateCancellation = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => updateCancellation?.Cancel();

    [RelayCommand(CanExecute = nameof(CanStartUpdate))]
    private void Later() => RequestClose?.Invoke(this, ApplicationUpdateDialogResult.Later);

    [RelayCommand(CanExecute = nameof(CanStartUpdate))]
    private void Skip() => RequestClose?.Invoke(this, ApplicationUpdateDialogResult.SkipVersion);

    private void ApplyProgress(ApplicationUpdateProgress progress)
    {
        if (State is ApplicationUpdateDialogState.Available or ApplicationUpdateDialogState.Failed)
        {
            return;
        }
        State = progress.Stage switch
        {
            ApplicationUpdateProgressStage.Downloading => ApplicationUpdateDialogState.Downloading,
            ApplicationUpdateProgressStage.Verifying => ApplicationUpdateDialogState.Verifying,
            ApplicationUpdateProgressStage.StartingUpdater => ApplicationUpdateDialogState.StartingUpdater,
            _ => State
        };
        ProgressValue = progress.TotalBytes <= 0
            ? 0
            : Math.Clamp((double)progress.BytesReceived / progress.TotalBytes * 100, 0, 100);
        ProgressText = progress.TotalBytes <= 0
            ? string.Empty
            : string.Format(
                System.Globalization.CultureInfo.CurrentUICulture,
                Loc["ApplicationUpdateProgressFormat"],
                QueueDisplayText.Size(progress.BytesReceived),
                QueueDisplayText.Size(progress.TotalBytes));
    }

    partial void OnStateChanged(ApplicationUpdateDialogState value)
    {
        UpdateCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        LaterCommand.NotifyCanExecuteChanged();
        SkipCommand.NotifyCanExecuteChanged();
    }
}

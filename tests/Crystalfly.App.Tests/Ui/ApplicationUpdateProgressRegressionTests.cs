using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Crystalfly.App.Updates;
using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.Dialogs;

namespace Crystalfly.App.Tests.Ui;

public sealed class ApplicationUpdateProgressRegressionTests
{
    [AvaloniaFact]
    public async Task Active_update_progress_remains_visible_and_cancellable()
    {
        var dialog = new ApplicationUpdateDialogViewModel(new LocalizationViewModel(), "1.1.6", "notes",
            async (progress, cancellationToken) =>
            {
                progress.Report(new(ApplicationUpdateProgressStage.Verifying, 5, 10));
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return false;
            });

        var update = dialog.UpdateCommand.ExecuteAsync(null);
        try
        {
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(ApplicationUpdateDialogState.Verifying, dialog.State);
            Assert.Equal(50, dialog.ProgressValue);
            Assert.True(dialog.CanCancel);
        }
        finally
        {
            dialog.CancelCommand.Execute(null);
            await update;
        }
        Assert.Equal(ApplicationUpdateDialogState.Available, dialog.State);
        Assert.True(dialog.CanStartUpdate);
    }

    [AvaloniaFact]
    public async Task Queued_progress_from_a_failed_attempt_does_not_override_retry_state()
    {
        var attempts = 0;
        var retryCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialog = new ApplicationUpdateDialogViewModel(new LocalizationViewModel(), "1.1.6", "notes",
            (progress, _) =>
            {
                if (++attempts == 1)
                {
                    progress.Report(new(ApplicationUpdateProgressStage.StartingUpdater, 10, 10));
                    throw new IOException("Unable to launch the updater.");
                }
                return retryCompletion.Task;
            });

        await dialog.UpdateCommand.ExecuteAsync(null);
        Assert.Equal(ApplicationUpdateDialogState.Failed, dialog.State);
        var retry = dialog.UpdateCommand.ExecuteAsync(null);
        try
        {
            Assert.Equal(ApplicationUpdateDialogState.Downloading, dialog.State);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(ApplicationUpdateDialogState.Downloading, dialog.State);
            Assert.Equal(0, dialog.ProgressValue);
            Assert.True(dialog.CanCancel);
        }
        finally
        {
            retryCompletion.TrySetResult(false);
            await retry;
        }
    }

    [AvaloniaFact]
    public async Task Queued_download_progress_does_not_reopen_cancellation_after_updater_started()
    {
        var dialog = new ApplicationUpdateDialogViewModel(new LocalizationViewModel(), "1.1.6", "notes",
            (progress, _) =>
            {
                progress.Report(new(ApplicationUpdateProgressStage.Downloading, 5, 10));
                return Task.FromResult(true);
            });

        await dialog.UpdateCommand.ExecuteAsync(null);
        Assert.Equal(ApplicationUpdateDialogState.StartingUpdater, dialog.State);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(ApplicationUpdateDialogState.StartingUpdater, dialog.State);
        Assert.False(dialog.CanCancel);
    }
}

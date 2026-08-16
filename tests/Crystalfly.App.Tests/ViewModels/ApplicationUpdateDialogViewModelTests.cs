using Crystalfly.App.ViewModels.Dialogs;
using Crystalfly.App.Updates;
using Crystalfly.App.ViewModels;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class ApplicationUpdateDialogViewModelTests
{
    [Theory]
    [InlineData("LaterCommand", ApplicationUpdateDialogResult.Later)]
    [InlineData("SkipCommand", ApplicationUpdateDialogResult.SkipVersion)]
    public void Passive_commands_close_with_the_selected_result(
        string commandProperty,
        ApplicationUpdateDialogResult expected)
    {
        var viewModel = CreateViewModel((_, _) => Task.FromResult(false));
        object? result = null;
        viewModel.RequestClose += (_, value) => result = value;

        var command = Assert.IsAssignableFrom<System.Windows.Input.ICommand>(
            typeof(ApplicationUpdateDialogViewModel).GetProperty(commandProperty)!.GetValue(viewModel));
        command.Execute(null);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task Failed_update_stays_open_and_can_retry()
    {
        int attempts = 0;
        var viewModel = CreateViewModel((progress, _) =>
        {
            attempts++;
            progress.Report(new(
                ApplicationUpdateProgressStage.Downloading,
                BytesReceived: 5,
                TotalBytes: 10));
            if (attempts == 1)
            {
                throw new IOException("network failed");
            }
            progress.Report(new(ApplicationUpdateProgressStage.StartingUpdater, 10, 10));
            return Task.FromResult(true);
        });

        await viewModel.UpdateCommand.ExecuteAsync(null);

        Assert.Equal(ApplicationUpdateDialogState.Failed, viewModel.State);
        Assert.Contains("network failed", viewModel.ErrorText, StringComparison.OrdinalIgnoreCase);

        await viewModel.UpdateCommand.ExecuteAsync(null);

        Assert.Equal(ApplicationUpdateDialogState.StartingUpdater, viewModel.State);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task Cancel_stops_the_download_and_returns_to_available()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var viewModel = CreateViewModel(async (progress, cancellationToken) =>
        {
            progress.Report(new(ApplicationUpdateProgressStage.Downloading, 1, 10));
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return false;
        });

        Task update = viewModel.UpdateCommand.ExecuteAsync(null);
        await started.Task;
        viewModel.CancelCommand.Execute(null);
        await update;

        Assert.Equal(ApplicationUpdateDialogState.Available, viewModel.State);
        Assert.True(viewModel.CanStartUpdate);
    }

    private static ApplicationUpdateDialogViewModel CreateViewModel(
        Func<IProgress<ApplicationUpdateProgress>, CancellationToken, Task<bool>> startUpdate) =>
        new(new LocalizationViewModel(), "1.1.4", "notes", startUpdate);
}

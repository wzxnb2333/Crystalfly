using Crystalfly.App.Downloads;
using Crystalfly.App.ViewModels;
using Crystalfly.Core.Configuration;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class DownloadQueueGroupItemViewModelTests
{
    [Theory]
    [InlineData(DownloadQueueGroupState.Failed, true)]
    [InlineData(DownloadQueueGroupState.Completed, false)]
    [InlineData(DownloadQueueGroupState.Canceled, false)]
    public void Individual_cancel_is_available_for_every_unfinished_group(
        DownloadQueueGroupState state, bool canCancel)
    {
        var viewModel = new DownloadQueueGroupItemViewModel(
            Group() with { State = state }, new LocalizationViewModel());

        Assert.Equal(canCancel, viewModel.CanCancel);
    }

    [Fact]
    public void Update_projects_progress_actions_and_localized_state_without_losing_expansion()
    {
        var localization = new LocalizationViewModel();
        localization.Apply(UiLanguage.English);
        var viewModel = new DownloadQueueGroupItemViewModel(Group(), localization)
        {
            IsExpanded = true
        };

        Assert.Equal("Running", viewModel.StateText);
        Assert.Equal("50%", viewModel.ProgressText);
        Assert.Equal("2.0 KiB/s", viewModel.SpeedText);
        Assert.True(viewModel.CanCancel);
        Assert.False(viewModel.CanRetry);
        Assert.Single(viewModel.Items);
        Assert.Equal("Retries: 2", viewModel.Items[0].RetryText);

        viewModel.Update(Group() with
        {
            State = DownloadQueueGroupState.Failed,
            Stage = "Failed",
            Error = "network failed"
        }, localization);

        Assert.True(viewModel.IsExpanded);
        Assert.Equal("Failed", viewModel.StateText);
        Assert.True(viewModel.CanCancel);
        Assert.True(viewModel.CanRetry);
        Assert.Equal("network failed", viewModel.Error);
    }

    [Theory]
    [InlineData("SHA-256 hash failed at C:\\Users\\owner\\Downloads\\package.zip", "QueueFailureVerificationSummary", "DownloadErrorVerification")]
    [InlineData("Access denied at C:\\Users\\owner\\Games\\Practice", "QueueFailurePermissionSummary", "DownloadErrorPermission")]
    [InlineData("Unmanaged external Mod found at C:\\Users\\owner\\Games\\Practice", "QueueFailureUnmanagedSummary", "DownloadErrorUnmanaged")]
    [InlineData("Unmanaged memory allocation failed at C:\\Users\\owner\\Games\\Practice", "QueueFailureOtherSummary", "DownloadErrorOther")]
    public void Failure_summary_is_safe_localized_and_keeps_the_raw_error(
        string error, string summaryKey, string categoryKey)
    {
        var localization = new LocalizationViewModel();
        localization.Apply(UiLanguage.SimplifiedChinese);
        var viewModel = new DownloadQueueGroupItemViewModel(
            Group() with { State = DownloadQueueGroupState.Failed, Error = error }, localization);

        Assert.Equal(localization[summaryKey], viewModel.ErrorSummaryText);
        Assert.Equal(localization[categoryKey], viewModel.ErrorCategoryText);
        Assert.Equal(error, viewModel.Error);
        Assert.DoesNotContain("C:\\Users\\owner", viewModel.ErrorSummaryText, StringComparison.OrdinalIgnoreCase);
        Assert.True(viewModel.CanRetry);
        Assert.True(viewModel.CanCancel);

        localization.Apply(UiLanguage.English);
        Assert.Equal(localization[summaryKey], viewModel.ErrorSummaryText);
        Assert.Equal(error, viewModel.Error);
    }

    private static DownloadQueueGroup Group() => new()
    {
        Id = "group",
        DeduplicationKey = "instance:mod",
        Name = "Feature",
        TargetInstanceName = "Practice",
        State = DownloadQueueGroupState.Running,
        Stage = "Downloading",
        CompletedBytes = 50,
        TotalBytes = 100,
        BytesPerSecond = 2048,
        Items =
        [
            new DownloadQueueItem
            {
                Id = "item",
                PackageId = "feature",
                Name = "Feature",
                Version = "1.0",
                LoaderId = "modding-api-77",
                Kind = DownloadQueueItemKind.Mod,
                State = DownloadQueueItemState.Transferring,
                CompletedBytes = 50,
                TotalBytes = 100,
                BytesPerSecond = 2048,
                RetryCount = 2,
                Stage = "Downloading"
            }
        ]
    };
}

using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.App.Downloads;

namespace Crystalfly.App.ViewModels;

public enum DownloadErrorCategory
{
    Offline,
    Network,
    Verification,
    Permission,
    Other
}

public sealed partial class DownloadQueueGroupItemViewModel : ViewModelBase
{
    private DownloadQueueGroup group;
    private LocalizationViewModel localization;
    private bool isExpanded;

    public DownloadQueueGroupItemViewModel(
        DownloadQueueGroup group,
        LocalizationViewModel localization)
    {
        this.group = group ?? throw new ArgumentNullException(nameof(group));
        this.localization = localization ?? throw new ArgumentNullException(nameof(localization));
        UpdateItems(group.Items, localization);
    }

    public ObservableCollection<DownloadQueueItemViewModel> Items { get; } = [];

    public string Id => group.Id;

    public string Name => group.Name;

    public string TargetInstanceName => group.TargetInstanceName;

    public DownloadQueueGroupState State => group.State;

    public string StateText => QueueDisplayText.GroupState(localization, group.State);

    public string StageText => QueueDisplayText.Stage(localization, group.Stage, StateText);

    public string? Error => group.Error;

    public double Progress => group.Progress;

    public string ProgressText => group.Progress.ToString("P0", CultureInfo.CurrentCulture);

    public string SpeedText => QueueDisplayText.Speed(group.BytesPerSecond);

    public bool CanCancel => group.State is
        DownloadQueueGroupState.Pending or DownloadQueueGroupState.Running or DownloadQueueGroupState.Failed;

    public bool CanRetry => group.State == DownloadQueueGroupState.Failed;

    public bool HasError => !string.IsNullOrWhiteSpace(group.Error);

    public DownloadErrorCategory ErrorCategory =>
        DownloadCenterViewModel.ClassifyError(group.Error, group.State);

    public string ErrorCategoryText => localization[$"DownloadError{ErrorCategory}"];

    public string DurationText => group.StartedAt is { } started && group.CompletedAt is { } completed
        ? (completed - started).ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
        : string.Empty;

    public bool HasDuration => group.StartedAt is not null && group.CompletedAt is not null;

    public string EtaText => QueueDisplayText.Eta(
        group.BytesPerSecond,
        group.CompletedBytes,
        group.TotalBytes);

    [RelayCommand]
    private async Task CopyErrorAsync()
    {
        if (string.IsNullOrWhiteSpace(group.Error))
        {
            return;
        }
        if (ResolveClipboard() is { } clipboard)
        {
            await clipboard.SetTextAsync(group.Error);
        }
    }

    internal static Func<IClipboard?>? ClipboardResolverOverride { get; set; }

    private static IClipboard? ResolveClipboard()
    {
        if (ClipboardResolverOverride is { } resolverOverride)
        {
            return resolverOverride();
        }
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
            { MainWindow: { } window }
            ? window.Clipboard
            : null;
    }

    public bool IsExpanded
    {
        get => isExpanded;
        set => SetProperty(ref isExpanded, value);
    }

    public void Update(DownloadQueueGroup value, LocalizationViewModel valueLocalization)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(valueLocalization);
        group = value;
        localization = valueLocalization;
        UpdateItems(value.Items, valueLocalization);
        foreach (var property in new[]
                 {
                     nameof(Name), nameof(TargetInstanceName), nameof(State), nameof(StateText),
                     nameof(StageText), nameof(Error), nameof(Progress), nameof(ProgressText),
                     nameof(SpeedText), nameof(CanCancel), nameof(CanRetry), nameof(HasError),
                     nameof(EtaText), nameof(ErrorCategory), nameof(ErrorCategoryText),
                     nameof(DurationText), nameof(HasDuration)
                 })
        {
            OnPropertyChanged(property);
        }
    }

    private void UpdateItems(
        IReadOnlyList<DownloadQueueItem> values,
        LocalizationViewModel valueLocalization)
    {
        if (Items.Count == values.Count
            && Items.Select(item => item.Id).SequenceEqual(values.Select(item => item.Id), StringComparer.Ordinal))
        {
            for (var index = 0; index < values.Count; index++)
            {
                Items[index].Update(values[index], valueLocalization);
            }
            return;
        }

        Items.Clear();
        foreach (var item in values)
        {
            Items.Add(new DownloadQueueItemViewModel(item, valueLocalization));
        }
    }
}

public sealed class DownloadQueueItemViewModel : ViewModelBase
{
    private DownloadQueueItem item;
    private LocalizationViewModel localization;

    public DownloadQueueItemViewModel(
        DownloadQueueItem item,
        LocalizationViewModel localization)
    {
        this.item = item ?? throw new ArgumentNullException(nameof(item));
        this.localization = localization ?? throw new ArgumentNullException(nameof(localization));
    }

    public string Id => item.Id;

    public string Name => item.Name;

    public string Version => item.Version;

    public string StateText => QueueDisplayText.ItemState(localization, item.State);

    public string StageText => QueueDisplayText.Stage(localization, item.Stage, StateText);

    public string? Error => item.Error;

    public double Progress => item.Progress;

    public string ProgressText => item.Progress.ToString("P0", CultureInfo.CurrentCulture);

    public string SpeedText => QueueDisplayText.Speed(item.BytesPerSecond);

    public string EtaText => QueueDisplayText.Eta(
        item.BytesPerSecond,
        item.CompletedBytes,
        item.TotalBytes);

    public int RetryCount => item.RetryCount;

    public string RetryText => item.RetryCount > 0
        ? string.Format(CultureInfo.CurrentCulture, localization["QueueRetryCount"], item.RetryCount)
        : string.Empty;

    public bool HasRetries => item.RetryCount > 0;

    public bool HasError => !string.IsNullOrWhiteSpace(item.Error);

    public void Update(DownloadQueueItem value, LocalizationViewModel valueLocalization)
    {
        item = value ?? throw new ArgumentNullException(nameof(value));
        localization = valueLocalization ?? throw new ArgumentNullException(nameof(valueLocalization));
        foreach (var property in new[]
                 {
                     nameof(Name), nameof(Version), nameof(StateText), nameof(StageText),
                     nameof(Error), nameof(Progress), nameof(ProgressText), nameof(SpeedText),
                     nameof(EtaText), nameof(RetryCount), nameof(RetryText), nameof(HasRetries),
                     nameof(HasError)
                 })
        {
            OnPropertyChanged(property);
        }
    }
}

internal static class QueueDisplayText
{
    public static string GroupState(LocalizationViewModel localization, DownloadQueueGroupState state) =>
        localization[$"QueueState{state}"];

    public static string ItemState(LocalizationViewModel localization, DownloadQueueItemState state) =>
        localization[$"QueueState{state}"];

    public static string Stage(
        LocalizationViewModel localization,
        string? stage,
        string fallback)
    {
        if (string.IsNullOrWhiteSpace(stage))
        {
            return fallback;
        }
        return stage switch
        {
            "Pending" => localization["QueueStatePending"],
            "Starting" => localization["QueueStageStarting"],
            "Downloading" => localization["QueueStateTransferring"],
            "Retrying" => localization["QueueStageRetrying"],
            "Waiting for game exit" => localization["QueueStateWaitingForGameExit"],
            "Waiting for Steam login" => localization["QueueStateWaitingForSteamLogin"],
            "Installing" => localization["QueueStateInstalling"],
            "Completed" => localization["QueueStateCompleted"],
            "Failed" => localization["QueueStateFailed"],
            "Blocked" => localization["QueueStateBlocked"],
            "Canceled" => localization["QueueStateCanceled"],
            "Cached" => localization["QueueStageCached"],
            "Satisfied" => localization["QueueStageSatisfied"],
            _ => stage
        };
    }

    public static string Speed(double bytesPerSecond) => bytesPerSecond > 0
        ? $"{FormatBytes(bytesPerSecond)}/s"
        : string.Empty;

    public static string Eta(double bytesPerSecond, long completed, long total)
        => bytesPerSecond > 0 && total > completed
            ? TimeSpan.FromSeconds((total - completed) / bytesPerSecond)
                .ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
            : string.Empty;

    private static string FormatBytes(double bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB"];
        var value = Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return string.Format(CultureInfo.CurrentCulture, "{0:0.0} {1}", value, units[unit]);
    }
}

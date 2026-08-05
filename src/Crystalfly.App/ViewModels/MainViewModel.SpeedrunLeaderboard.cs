using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.Core.Serialization;
using Crystalfly.Core.Speedrun;

namespace Crystalfly.App.ViewModels;

public partial class MainViewModel
{
    private readonly SpeedrunComClient speedrunComClient;
    private CancellationTokenSource? speedrunLeaderboardLoadCancellation;
    private Task speedrunLeaderboardLoadTask = Task.CompletedTask;
    private long speedrunLeaderboardLoadGeneration;
    private DateTimeOffset? speedrunActivityLastLoadedAt;
    private static readonly TimeSpan SpeedrunActivityCacheLifetime = TimeSpan.FromMinutes(15);
    private static TimeSpan SpeedrunActivityRefreshInterval = TimeSpan.FromMinutes(15);

    public ObservableCollection<SpeedrunActivityItemViewModel> SpeedrunActivities { get; } = [];
    public ObservableCollection<SpeedrunActivityItemViewModel> VisibleSpeedrunActivities { get; } = [];

    public bool IsSpeedrunEnvironmentTab => CurrentSpeedrunTab == "Environment";
    public bool IsSpeedrunActivityTab => CurrentSpeedrunTab == "Activity";
    public bool IsSpeedrunActivityFilterAll => SpeedrunActivityFilter == "All";
    public bool IsSpeedrunActivityFilterHollowKnight => SpeedrunActivityFilter == "HollowKnight";
    public bool IsSpeedrunActivityFilterSilksong => SpeedrunActivityFilter == "Silksong";
    public bool HasSpeedrunActivities => VisibleSpeedrunActivities.Count > 0;
    public bool ShowSpeedrunActivityEmptyState => !IsSpeedrunActivityLoading && !HasSpeedrunActivities;
    public bool HasSpeedrunActivityError => !string.IsNullOrWhiteSpace(SpeedrunActivityError);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSpeedrunEnvironmentTab))]
    [NotifyPropertyChangedFor(nameof(IsSpeedrunActivityTab))]
    public partial string CurrentSpeedrunTab { get; set; } = "Environment";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSpeedrunActivityFilterAll))]
    [NotifyPropertyChangedFor(nameof(IsSpeedrunActivityFilterHollowKnight))]
    [NotifyPropertyChangedFor(nameof(IsSpeedrunActivityFilterSilksong))]
    public partial string SpeedrunActivityFilter { get; set; } = "All";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSpeedrunActivityEmptyState))]
    public partial bool IsSpeedrunActivityLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSpeedrunActivityError))]
    public partial string? SpeedrunActivityError { get; set; }

    [ObservableProperty]
    public partial string SpeedrunActivityStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SpeedrunActivityUpdatedAt { get; set; } = string.Empty;

    partial void OnCurrentPageChanged(string value)
    {
        if (value == "Speedrun" && IsSpeedrunActivityTab)
        {
            EnsureSpeedrunActivityLoaded();
        }
    }

    partial void OnCurrentSpeedrunTabChanged(string value)
    {
        if (value == "Activity")
        {
            EnsureSpeedrunActivityLoaded();
        }
    }

    partial void OnSpeedrunActivityFilterChanged(string value) => ApplySpeedrunActivityFilter();

    [RelayCommand]
    private Task RefreshSpeedrunActivityAsync() => LoadSpeedrunActivityAsync(forceRefresh: true, showLoading: true);

    [RelayCommand]
    private void SelectSpeedrunActivityFilter(string? filter)
    {
        if (filter is "All" or "HollowKnight" or "Silksong")
        {
            SpeedrunActivityFilter = filter;
        }
    }

    [RelayCommand]
    private void SelectSpeedrunTab(string? tab)
    {
        if (tab is "Environment" or "Activity")
        {
            CurrentSpeedrunTab = tab;
        }
    }

    [RelayCommand]
    private void DismissSpeedrunReminder() => SpeedrunReminderText = string.Empty;

    private void BeginSpeedrunActivityLoad(bool forceRefresh, bool showLoading = true) =>
        speedrunLeaderboardLoadTask = LoadSpeedrunActivityAsync(forceRefresh, showLoading);

    private void EnsureSpeedrunActivityLoaded()
    {
        if (speedrunActivityLastLoadedAt is { } lastLoaded
            && speedrunComClient.UtcNow - lastLoaded < SpeedrunActivityCacheLifetime)
        {
            return;
        }

        if (speedrunActivityLastLoadedAt is null)
        {
            BeginSpeedrunActivityLoad(forceRefresh: false, showLoading: true);
            return;
        }

        BeginSpeedrunActivityLoad(forceRefresh: true, showLoading: false);
    }

    internal void StartSpeedrunActivityRefreshLoop() => _ = SpeedrunActivityRefreshLoopAsync();

    private async Task SpeedrunActivityRefreshLoopAsync()
    {
        try
        {
            while (!lifetimeCancellation.IsCancellationRequested)
            {
                await Task.Delay(SpeedrunActivityRefreshInterval, lifetimeCancellation.Token);
                if (lifetimeCancellation.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    BeginSpeedrunActivityLoad(forceRefresh: true, showLoading: false);
                    await speedrunLeaderboardLoadTask;
                }
                catch (OperationCanceledException) when (!lifetimeCancellation.IsCancellationRequested)
                {
                    SpeedrunActivityError = Loc["SpeedrunActivityUnavailable"];
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    SpeedrunActivityError = exception.Message;
                }
            }
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
        }
    }

    private async Task LoadSpeedrunActivityAsync(bool forceRefresh, bool showLoading)
    {
        long generation = Interlocked.Increment(ref speedrunLeaderboardLoadGeneration);
        var replacement = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref speedrunLeaderboardLoadCancellation, replacement);
        previous?.Cancel();
        previous?.Dispose();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            lifetimeCancellation.Token,
            replacement.Token);
        CancellationToken cancellationToken = linked.Token;
        IsSpeedrunActivityLoading = showLoading;
        SpeedrunActivityError = null;
        SpeedrunActivityStatus = showLoading ? Loc["SpeedrunActivityLoading"] : string.Empty;

        try
        {
            SpeedrunActivityDocument document = await ReadSpeedrunActivityDocumentAsync(cancellationToken);
            var successful = new List<SpeedrunBoardSnapshot>();
            var failures = new List<string>();
            var statuses = new List<SpeedrunDataStatus>();
            DateTimeOffset? fetchedAt = null;
            using var gate = new SemaphoreSlim(3, 3);
            foreach (SpeedrunGame game in Enum.GetValues<SpeedrunGame>())
            {
                SpeedrunDataResult<IReadOnlyList<SpeedrunBoardDescriptor>> boards = await speedrunComClient
                    .GetBoardsAsync(game, forceRefresh, cancellationToken);
                statuses.Add(boards.Status);
                fetchedAt = Latest(fetchedAt, boards.FetchedAt);
                if (boards.Data is null)
                {
                    failures.Add(boards.Reason ?? Loc["SpeedrunActivityUnavailable"]);
                    continue;
                }
                Task[] tasks = boards.Data.Select(async board =>
                {
                    await gate.WaitAsync(cancellationToken);
                    try
                    {
                        SpeedrunDataResult<SpeedrunBoardSnapshot> result = await speedrunComClient
                            .GetPodiumAsync(board, forceRefresh, cancellationToken);
                        lock (successful)
                        {
                            statuses.Add(result.Status);
                            fetchedAt = Latest(fetchedAt, result.FetchedAt);
                            if (result.Data is not null)
                            {
                                successful.Add(result.Data);
                            }
                            else
                            {
                                failures.Add(result.Reason ?? board.DisplayName);
                            }
                        }
                    }
                    finally
                    {
                        gate.Release();
                    }
                }).ToArray();
                await Task.WhenAll(tasks);
            }
            if (!IsCurrentSpeedrunActivityLoad(generation))
            {
                return;
            }

            SpeedrunActivityDetectionResult detection = SpeedrunActivityDetector.Apply(
                document,
                successful,
                speedrunComClient.UtcNow);
            await AtomicJsonStore.WriteAsync(SpeedrunActivityPath, detection.Document, cancellationToken);
            Replace(SpeedrunActivities, detection.Document.Activities.Select(ProjectActivity));
            ApplySpeedrunActivityFilter();
            SpeedrunActivityError = failures.Count == 0
                ? null
                : string.Format(CultureInfo.CurrentCulture, Loc["SpeedrunActivityPartialFailure"], failures.Count);
            SpeedrunActivityStatus = statuses.Count > 0 && statuses.All(status => status == SpeedrunDataStatus.Offline)
                ? Loc["OfflineMode"]
                : statuses.Any(status => status == SpeedrunDataStatus.Cached)
                    ? Loc["SpeedrunActivityCached"]
                    : Loc["SpeedrunActivityReady"];
            SpeedrunActivityUpdatedAt = fetchedAt is { } value
                ? string.Format(CultureInfo.CurrentCulture, Loc["SpeedrunActivityUpdatedAt"], value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture))
                : string.Empty;
            speedrunActivityLastLoadedAt = speedrunComClient.UtcNow;

            if (CurrentPage == "Speedrun")
            {
                foreach (SpeedrunActivityEntry activity in detection.NewActivities)
                {
                    NotifyToast(ActivityToastText(activity));
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            SpeedrunActivityError = exception.Message;
            SpeedrunActivityStatus = Loc["SpeedrunActivityUnavailable"];
        }
        finally
        {
            if (IsCurrentSpeedrunActivityLoad(generation))
            {
                IsSpeedrunActivityLoading = false;
                OnPropertyChanged(nameof(ShowSpeedrunActivityEmptyState));
            }
        }
    }

    private string ActivityToastText(SpeedrunActivityEntry activity) => string.Format(
        CultureInfo.CurrentCulture,
        Loc[activity.Kind switch
        {
            SpeedrunActivityKind.WorldRecord => "SpeedrunActivityToastWorldRecord",
            SpeedrunActivityKind.TiedWorldRecord => "SpeedrunActivityToastTiedWorldRecord",
            SpeedrunActivityKind.SecondPlace => "SpeedrunActivityToastSecond",
            _ => "SpeedrunActivityToastThird"
        }],
        activity.Run.PlayerName,
        ActivityBoardName(activity),
        activity.Run.DisplayTime);

    private SpeedrunActivityItemViewModel ProjectActivity(SpeedrunActivityEntry activity) => new(
        activity,
        Loc[activity.Kind switch
        {
            SpeedrunActivityKind.WorldRecord => "SpeedrunActivityWorldRecord",
            SpeedrunActivityKind.TiedWorldRecord => "SpeedrunActivityTiedWorldRecord",
            SpeedrunActivityKind.SecondPlace => "SpeedrunActivitySecond",
            _ => "SpeedrunActivityThird"
        }],
        ActivityBoardName(activity));

    private string ActivityBoardName(SpeedrunActivityEntry activity) =>
        $"{Loc[activity.Board.Game == SpeedrunGame.HollowKnight ? "SpeedrunGameHollowKnight" : "SpeedrunGameSilksong"]} · {activity.Board.DisplayName}";

    private void ApplySpeedrunActivityFilter()
    {
        SpeedrunGame? game = SpeedrunActivityFilter switch
        {
            "HollowKnight" => SpeedrunGame.HollowKnight,
            "Silksong" => SpeedrunGame.Silksong,
            _ => null
        };
        Replace(VisibleSpeedrunActivities, SpeedrunActivities.Where(item => game is null || item.Entry.Board.Game == game));
        OnPropertyChanged(nameof(HasSpeedrunActivities));
        OnPropertyChanged(nameof(ShowSpeedrunActivityEmptyState));
    }

    private async Task<SpeedrunActivityDocument> ReadSpeedrunActivityDocumentAsync(CancellationToken cancellationToken)
    {
        try
        {
            SpeedrunActivityDocument document = await AtomicJsonStore.ReadAsync<SpeedrunActivityDocument>(
                SpeedrunActivityPath,
                cancellationToken);
            return document.SchemaVersion == 1 ? document : new();
        }
        catch (Exception exception) when (exception is FileNotFoundException
            or DirectoryNotFoundException
            or IOException
            or UnauthorizedAccessException
            or System.Text.Json.JsonException)
        {
            return new();
        }
    }

    private string SpeedrunActivityPath => Path.Combine(paths.ApplicationDataRoot, "speedrun-activity.json");

    private bool IsCurrentSpeedrunActivityLoad(long generation) =>
        generation == Volatile.Read(ref speedrunLeaderboardLoadGeneration);

    private static DateTimeOffset? Latest(DateTimeOffset? left, DateTimeOffset? right) =>
        left is null ? right : right is null || left >= right ? left : right;

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (T value in values)
        {
            target.Add(value);
        }
    }
}

public sealed record SpeedrunActivityItemViewModel(
    SpeedrunActivityEntry Entry,
    string KindText,
    string BoardName)
{
    public string RunId => Entry.RunId;
    public SpeedrunPodiumEntry Run => Entry.Run;
    public bool IsWorldRecord => Entry.IsWorldRecord;
    public string DisplayVerifiedAt => Entry.DisplayVerifiedAt;
}

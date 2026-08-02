using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Speedrun;

namespace Crystalfly.App.ViewModels;

public partial class MainViewModel
{
    private readonly SpeedrunComClient speedrunComClient;
    private CancellationTokenSource? speedrunLeaderboardLoadCancellation;
    private Task speedrunLeaderboardLoadTask = Task.CompletedTask;
    private long speedrunLeaderboardLoadGeneration;
    private bool updatingSpeedrunLeaderboardSelection;

    public ObservableCollection<SettingOption<SpeedrunGame>> SpeedrunGameOptions { get; } = [];

    public ObservableCollection<SpeedrunCategory> SpeedrunCategories { get; } = [];

    public ObservableCollection<SpeedrunRun> SpeedrunLeaderboardRuns { get; } = [];

    public ObservableCollection<SpeedrunRun> RecentSpeedrunRuns { get; } = [];

    public bool IsSpeedrunEnvironmentTab => CurrentSpeedrunTab == "Environment";

    public bool IsSpeedrunLeaderboardTab => CurrentSpeedrunTab == "Leaderboard";

    public bool HasSpeedrunLeaderboardData => SpeedrunLeaderboardRuns.Count > 0 || RecentSpeedrunRuns.Count > 0;

    public bool HasSpeedrunLeaderboardError =>
        !HasSpeedrunLeaderboardData && !string.IsNullOrWhiteSpace(SpeedrunLeaderboardError);

    public bool ShowSpeedrunLeaderboardEmptyState =>
        !IsSpeedrunLeaderboardLoading && !HasSpeedrunLeaderboardData && !HasSpeedrunLeaderboardError;

    public bool ShowSpeedrunLeaderboardRunsEmptyState =>
        !IsSpeedrunLeaderboardLoading && SpeedrunLeaderboardRuns.Count == 0;

    public bool ShowRecentSpeedrunRunsEmptyState =>
        !IsSpeedrunLeaderboardLoading && RecentSpeedrunRuns.Count == 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSpeedrunEnvironmentTab))]
    [NotifyPropertyChangedFor(nameof(IsSpeedrunLeaderboardTab))]
    public partial string CurrentSpeedrunTab { get; set; } = "Environment";

    [ObservableProperty]
    public partial SettingOption<SpeedrunGame>? SelectedSpeedrunGameOption { get; set; }

    [ObservableProperty]
    public partial SpeedrunGame SelectedSpeedrunGame { get; set; } = SpeedrunGame.HollowKnight;

    [ObservableProperty]
    public partial SpeedrunCategory? SelectedSpeedrunCategory { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSpeedrunLeaderboardData))]
    [NotifyPropertyChangedFor(nameof(ShowSpeedrunLeaderboardRunsEmptyState))]
    [NotifyPropertyChangedFor(nameof(ShowRecentSpeedrunRunsEmptyState))]
    [NotifyPropertyChangedFor(nameof(ShowSpeedrunLeaderboardEmptyState))]
    public partial bool IsSpeedrunLeaderboardLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSpeedrunLeaderboardError))]
    [NotifyPropertyChangedFor(nameof(ShowSpeedrunLeaderboardEmptyState))]
    public partial string? SpeedrunLeaderboardError { get; set; }

    [ObservableProperty]
    public partial string SpeedrunLeaderboardStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SpeedrunLeaderboardUpdatedAt { get; set; } = string.Empty;

    partial void OnCurrentPageChanged(string value)
    {
        if (value == "Speedrun" && IsSpeedrunLeaderboardTab)
        {
            BeginSpeedrunLeaderboardLoad(forceRefresh: false);
        }
    }

    partial void OnCurrentSpeedrunTabChanged(string value)
    {
        if (value == "Leaderboard")
        {
            BeginSpeedrunLeaderboardLoad(forceRefresh: false);
        }
    }

    partial void OnSelectedSpeedrunGameOptionChanged(SettingOption<SpeedrunGame>? value)
    {
        if (value is not null && value.Value != SelectedSpeedrunGame)
        {
            SelectedSpeedrunGame = value.Value;
        }
    }

    partial void OnSelectedSpeedrunGameChanged(SpeedrunGame value)
    {
        if (SelectedSpeedrunGameOption?.Value != value)
        {
            SelectedSpeedrunGameOption = SpeedrunGameOptions.FirstOrDefault(option => option.Value == value);
        }
        if (IsSpeedrunLeaderboardTab)
        {
            BeginSpeedrunLeaderboardLoad(forceRefresh: false);
        }
    }

    partial void OnSelectedSpeedrunCategoryChanged(SpeedrunCategory? value)
    {
        if (!updatingSpeedrunLeaderboardSelection && value is not null && IsSpeedrunLeaderboardTab)
        {
            BeginSpeedrunLeaderboardLoad(forceRefresh: false);
        }
    }

    [RelayCommand]
    private Task RefreshSpeedrunLeaderboardDataAsync() => LoadSpeedrunLeaderboardDataAsync(forceRefresh: true);

    [RelayCommand]
    private void SelectSpeedrunTab(string? tab)
    {
        if (tab is "Environment" or "Leaderboard")
        {
            CurrentSpeedrunTab = tab;
        }
    }

    private void RebuildSpeedrunGameOptions()
    {
        var selected = SelectedSpeedrunGame;
        SpeedrunGameOptions.Clear();
        SpeedrunGameOptions.Add(new(SpeedrunGame.HollowKnight, Loc["SpeedrunGameHollowKnight"]));
        SpeedrunGameOptions.Add(new(SpeedrunGame.Silksong, Loc["SpeedrunGameSilksong"]));
        SelectedSpeedrunGameOption = SpeedrunGameOptions.First(option => option.Value == selected);
    }

    private void BeginSpeedrunLeaderboardLoad(bool forceRefresh)
    {
        speedrunLeaderboardLoadTask = LoadSpeedrunLeaderboardDataAsync(forceRefresh);
    }

    private async Task LoadSpeedrunLeaderboardDataAsync(bool forceRefresh)
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
        IsSpeedrunLeaderboardLoading = true;
        SpeedrunLeaderboardError = null;
        SpeedrunLeaderboardStatus = Loc["SpeedrunLeaderboardLoading"];

        try
        {
            SpeedrunDataResult<IReadOnlyList<SpeedrunCategory>> categories = await speedrunComClient
                .GetCategoriesAsync(SelectedSpeedrunGame, forceRefresh, cancellationToken);
            if (!IsCurrentSpeedrunLeaderboardLoad(generation))
            {
                return;
            }
            if (categories.Data is null || categories.Data.Count == 0)
            {
                ClearSpeedrunLeaderboardData();
                SpeedrunLeaderboardError = categories.Reason ?? Loc["SpeedrunLeaderboardUnavailable"];
                SpeedrunLeaderboardStatus = Loc["SpeedrunLeaderboardUnavailable"];
                return;
            }

            updatingSpeedrunLeaderboardSelection = true;
            try
            {
                string? previousCategoryId = SelectedSpeedrunCategory?.Id;
                SpeedrunCategories.Clear();
                foreach (SpeedrunCategory category in categories.Data)
                {
                    SpeedrunCategories.Add(category);
                }
                SelectedSpeedrunCategory = SpeedrunCategories.FirstOrDefault(category =>
                    string.Equals(category.Id, previousCategoryId, StringComparison.Ordinal))
                    ?? SpeedrunCategories[0];
            }
            finally
            {
                updatingSpeedrunLeaderboardSelection = false;
            }

            SpeedrunCategory selectedCategory = SelectedSpeedrunCategory!;
            Task<SpeedrunDataResult<SpeedrunLeaderboard>> leaderboardTask = speedrunComClient.GetLeaderboardAsync(
                SelectedSpeedrunGame,
                selectedCategory,
                forceRefresh,
                cancellationToken);
            Task<SpeedrunDataResult<SpeedrunRecentRuns>> recentTask = speedrunComClient.GetRecentRunsAsync(
                SelectedSpeedrunGame,
                forceRefresh,
                cancellationToken);
            await Task.WhenAll(leaderboardTask, recentTask);
            if (!IsCurrentSpeedrunLeaderboardLoad(generation))
            {
                return;
            }

            SpeedrunDataResult<SpeedrunLeaderboard> leaderboard = await leaderboardTask;
            SpeedrunDataResult<SpeedrunRecentRuns> recent = await recentTask;
            Replace(SpeedrunLeaderboardRuns, leaderboard.Data?.Runs ?? []);
            Replace(RecentSpeedrunRuns, recent.Data?.Runs ?? []);
            OnPropertyChanged(nameof(HasSpeedrunLeaderboardData));
            OnPropertyChanged(nameof(HasSpeedrunLeaderboardError));
            OnPropertyChanged(nameof(ShowSpeedrunLeaderboardRunsEmptyState));
            OnPropertyChanged(nameof(ShowRecentSpeedrunRunsEmptyState));
            OnPropertyChanged(nameof(ShowSpeedrunLeaderboardEmptyState));
            SpeedrunLeaderboardError = FirstReason(categories, leaderboard, recent);
            SpeedrunLeaderboardStatus = DataStatus(categories, leaderboard, recent);
            SpeedrunLeaderboardUpdatedAt = LatestFetchedAt(categories, leaderboard, recent) is { } fetchedAt
                ? string.Format(CultureInfo.CurrentCulture, Loc["SpeedrunLeaderboardUpdatedAt"], fetchedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture))
                : string.Empty;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (IsCurrentSpeedrunLeaderboardLoad(generation))
            {
                IsSpeedrunLeaderboardLoading = false;
            }
        }
    }

    private bool IsCurrentSpeedrunLeaderboardLoad(long generation) =>
        generation == Volatile.Read(ref speedrunLeaderboardLoadGeneration);

    private void ClearSpeedrunLeaderboardData()
    {
        SpeedrunCategories.Clear();
        SpeedrunLeaderboardRuns.Clear();
        RecentSpeedrunRuns.Clear();
        SelectedSpeedrunCategory = null;
        OnPropertyChanged(nameof(HasSpeedrunLeaderboardData));
        OnPropertyChanged(nameof(HasSpeedrunLeaderboardError));
        OnPropertyChanged(nameof(ShowSpeedrunLeaderboardRunsEmptyState));
        OnPropertyChanged(nameof(ShowRecentSpeedrunRunsEmptyState));
        OnPropertyChanged(nameof(ShowSpeedrunLeaderboardEmptyState));
        SpeedrunLeaderboardUpdatedAt = string.Empty;
    }

    private string? FirstReason(
        SpeedrunDataResult<IReadOnlyList<SpeedrunCategory>> categories,
        SpeedrunDataResult<SpeedrunLeaderboard> leaderboard,
        SpeedrunDataResult<SpeedrunRecentRuns> recent) =>
        new[] { categories.Reason, leaderboard.Reason, recent.Reason }
            .FirstOrDefault(reason => !string.IsNullOrWhiteSpace(reason));

    private string DataStatus(
        SpeedrunDataResult<IReadOnlyList<SpeedrunCategory>> categories,
        SpeedrunDataResult<SpeedrunLeaderboard> leaderboard,
        SpeedrunDataResult<SpeedrunRecentRuns> recent)
    {
        var results = new[] { categories.Status, leaderboard.Status, recent.Status };
        if (results.All(status => status == SpeedrunDataStatus.Remote))
        {
            return Loc["SpeedrunLeaderboardLive"];
        }
        if (categories.IsStale || leaderboard.IsStale || recent.IsStale)
        {
            return Loc["SpeedrunLeaderboardCachedStale"];
        }
        return results.All(status => status == SpeedrunDataStatus.Offline)
            ? Loc["OfflineMode"]
            : Loc["SpeedrunLeaderboardCached"];
    }

    private static DateTimeOffset? LatestFetchedAt(
        SpeedrunDataResult<IReadOnlyList<SpeedrunCategory>> categories,
        SpeedrunDataResult<SpeedrunLeaderboard> leaderboard,
        SpeedrunDataResult<SpeedrunRecentRuns> recent)
    {
        DateTimeOffset? latest = null;
        foreach (DateTimeOffset? fetchedAt in new[] { categories.FetchedAt, leaderboard.FetchedAt, recent.FetchedAt })
        {
            if (fetchedAt is { } value && (latest is null || value > latest))
            {
                latest = value;
            }
        }
        return latest;
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (T value in values)
        {
            target.Add(value);
        }
    }
}

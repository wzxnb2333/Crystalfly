using System.Globalization;
using System.Xml;

namespace Crystalfly.Core.Speedrun;

public enum SpeedrunGame
{
    HollowKnight,
    Silksong
}

public static class SpeedrunGameCatalog
{
    public static string GetId(SpeedrunGame game) => game switch
    {
        SpeedrunGame.HollowKnight => "76rqmld8",
        SpeedrunGame.Silksong => "y65r7g81",
        _ => throw new ArgumentOutOfRangeException(nameof(game), game, null)
    };

    public static string GetSlug(SpeedrunGame game) => game switch
    {
        SpeedrunGame.HollowKnight => "hollowknight",
        SpeedrunGame.Silksong => "silksong",
        _ => throw new ArgumentOutOfRangeException(nameof(game), game, null)
    };
}

public sealed record SpeedrunBoardVariable(
    string VariableId,
    string VariableName,
    string ValueId,
    string ValueName);

public sealed record SpeedrunBoardDescriptor(
    SpeedrunGame Game,
    string CategoryId,
    string CategoryName,
    string? LevelId,
    string? LevelName,
    IReadOnlyList<SpeedrunBoardVariable> Subcategories)
{
    public string Key => string.Join('|',
        SpeedrunGameCatalog.GetId(Game),
        CategoryId,
        LevelId ?? string.Empty,
        string.Join(',', Subcategories
            .OrderBy(item => item.VariableId, StringComparer.Ordinal)
            .Select(item => $"{item.VariableId}={item.ValueId}")));

    public string DisplayName => string.Join(" · ", new[]
    {
        CategoryName,
        LevelName,
        Subcategories.Count == 0
            ? null
            : string.Join(" / ", Subcategories.Select(item => item.ValueName))
    }.Where(value => !string.IsNullOrWhiteSpace(value)));
}

public sealed record SpeedrunPodiumEntry(
    string RunId,
    int Place,
    string PlayerName,
    string PrimaryTime,
    double PrimaryTimeSeconds,
    DateTimeOffset? VerifiedAt,
    string? RunUrl)
{
    public string DisplayTime
    {
        get
        {
            try
            {
                TimeSpan duration = XmlConvert.ToTimeSpan(PrimaryTime);
                string hours = duration.TotalHours >= 1 ? $"{(int)duration.TotalHours:00}:" : string.Empty;
                string milliseconds = duration.Milliseconds > 0 ? $".{duration.Milliseconds:000}" : string.Empty;
                return $"{hours}{duration.Minutes:00}:{duration.Seconds:00}{milliseconds}";
            }
            catch (FormatException)
            {
                return PrimaryTime;
            }
        }
    }
}

public sealed record SpeedrunBoardSnapshot(
    SpeedrunBoardDescriptor Board,
    IReadOnlyList<SpeedrunPodiumEntry> Entries);

public enum SpeedrunActivityKind
{
    WorldRecord,
    TiedWorldRecord,
    SecondPlace,
    ThirdPlace
}

public sealed record SpeedrunActivityEntry(
    string RunId,
    SpeedrunActivityKind Kind,
    SpeedrunBoardDescriptor Board,
    SpeedrunPodiumEntry Run,
    DateTimeOffset DetectedAt)
{
    public bool IsWorldRecord => Kind is SpeedrunActivityKind.WorldRecord or SpeedrunActivityKind.TiedWorldRecord;
    public string DisplayVerifiedAt => Run.VerifiedAt?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) ?? "-";
}

public sealed record SpeedrunBoardBaseline(
    SpeedrunBoardDescriptor Board,
    DateTimeOffset LastSuccessfulCheck,
    IReadOnlyList<SpeedrunPodiumEntry> Entries);

public sealed record SpeedrunActivityDocument
{
    public int SchemaVersion { get; init; } = 1;
    public IReadOnlyDictionary<string, SpeedrunBoardBaseline> Boards { get; init; }
        = new Dictionary<string, SpeedrunBoardBaseline>(StringComparer.Ordinal);
    public IReadOnlyList<SpeedrunActivityEntry> Activities { get; init; } = [];
}

public sealed record SpeedrunActivityDetectionResult(
    SpeedrunActivityDocument Document,
    IReadOnlyList<SpeedrunActivityEntry> NewActivities);

public static class SpeedrunActivityDetector
{
    public static SpeedrunActivityDetectionResult Apply(
        SpeedrunActivityDocument document,
        IReadOnlyList<SpeedrunBoardSnapshot> successfulSnapshots,
        DateTimeOffset checkedAt)
    {
        ArgumentNullException.ThrowIfNull(document);
        var boards = document.Boards.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var additions = new List<SpeedrunActivityEntry>();
        foreach (SpeedrunBoardSnapshot snapshot in successfulSnapshots)
        {
            if (!boards.TryGetValue(snapshot.Board.Key, out SpeedrunBoardBaseline? previous))
            {
                boards[snapshot.Board.Key] = new(snapshot.Board, checkedAt, snapshot.Entries);
                continue;
            }

            var knownRuns = previous.Entries
                .Select(run => run.RunId)
                .Concat(document.Activities
                    .Where(activity =>
                        string.Equals(activity.Board.Key, snapshot.Board.Key, StringComparison.Ordinal))
                    .Select(activity => activity.RunId))
                .ToHashSet(StringComparer.Ordinal);
            double? oldRecord = previous.Entries
                .Where(run => run.Place == 1)
                .Select(run => (double?)run.PrimaryTimeSeconds)
                .Min();
            foreach (SpeedrunPodiumEntry run in snapshot.Entries)
            {
                if (knownRuns.Contains(run.RunId)
                    || run.VerifiedAt is not { } verifiedAt
                    || verifiedAt <= previous.LastSuccessfulCheck)
                {
                    continue;
                }

                SpeedrunActivityKind? kind = run.Place switch
                {
                    1 when oldRecord is { } record && run.PrimaryTimeSeconds < record => SpeedrunActivityKind.WorldRecord,
                    1 when oldRecord is { } record && run.PrimaryTimeSeconds == record => SpeedrunActivityKind.TiedWorldRecord,
                    2 => SpeedrunActivityKind.SecondPlace,
                    3 => SpeedrunActivityKind.ThirdPlace,
                    _ => null
                };
                if (kind is { } value)
                {
                    additions.Add(new(run.RunId, value, snapshot.Board, run, checkedAt));
                }
            }
            boards[snapshot.Board.Key] = new(snapshot.Board, checkedAt, snapshot.Entries);
        }

        additions.Sort((left, right) => Nullable.Compare(right.Run.VerifiedAt, left.Run.VerifiedAt));
        IReadOnlyList<SpeedrunActivityEntry> activities = additions
            .Concat(document.Activities)
            .DistinctBy(item => item.RunId)
            .Take(100)
            .ToArray();
        return new(
            document with { Boards = boards, Activities = activities },
            additions);
    }
}

public enum SpeedrunDataStatus
{
    Remote,
    Cached,
    Offline,
    Unavailable
}

public sealed record SpeedrunDataResult<T>(
    SpeedrunDataStatus Status,
    T? Data,
    bool IsStale,
    DateTimeOffset? FetchedAt,
    string? Reason)
    where T : class;

using Crystalfly.Core.Speedrun;

namespace Crystalfly.Core.Tests.Speedrun;

public sealed class SpeedrunActivityDetectorTests
{
    private static readonly SpeedrunBoardDescriptor Board = new(
        SpeedrunGame.HollowKnight,
        "category",
        "Any%",
        null,
        null,
        [new("glitch", "Rules", "nmg", "NMG")]);

    [Fact]
    public void First_scan_establishes_baseline_without_activity()
    {
        var now = DateTimeOffset.Parse("2026-08-03T00:00:00Z");
        var result = SpeedrunActivityDetector.Apply(
            new SpeedrunActivityDocument(),
            [new(Board, [Run("first", 1, 100, now)])],
            now);

        Assert.Empty(result.NewActivities);
        Assert.Equal("first", Assert.Single(result.Document.Boards[Board.Key].Entries).RunId);
    }

    [Theory]
    [InlineData(90, 1, SpeedrunActivityKind.WorldRecord)]
    [InlineData(100, 1, SpeedrunActivityKind.TiedWorldRecord)]
    [InlineData(110, 2, SpeedrunActivityKind.SecondPlace)]
    [InlineData(120, 3, SpeedrunActivityKind.ThirdPlace)]
    public void Detects_new_verified_podium_results(
        double seconds,
        int place,
        SpeedrunActivityKind expected)
    {
        var checkedAt = DateTimeOffset.Parse("2026-08-02T00:00:00Z");
        var old = Document(checkedAt, Run("old", 1, 100, checkedAt));
        var verifiedAt = checkedAt.AddHours(1);

        var result = SpeedrunActivityDetector.Apply(
            old,
            [new(Board, [Run("old", 1, 100, checkedAt), Run("new", place, seconds, verifiedAt)])],
            verifiedAt);

        Assert.Equal(expected, Assert.Single(result.NewActivities).Kind);
    }

    [Fact]
    public void Keeps_all_ties_and_ignores_passive_promotions_and_missing_verification_time()
    {
        var checkedAt = DateTimeOffset.Parse("2026-08-02T00:00:00Z");
        var old = Document(checkedAt,
            Run("old-first", 1, 100, checkedAt),
            Run("promoted", 3, 130, checkedAt));
        var next = new SpeedrunBoardSnapshot(Board,
        [
            Run("tie-a", 1, 100, checkedAt.AddHours(1)),
            Run("tie-b", 1, 100, checkedAt.AddHours(2)),
            Run("promoted", 2, 130, checkedAt),
            Run("unknown-date", 3, 140, null)
        ]);

        var result = SpeedrunActivityDetector.Apply(old, [next], checkedAt.AddHours(3));

        Assert.Equal(2, result.NewActivities.Count);
        Assert.All(result.NewActivities, item => Assert.Equal(SpeedrunActivityKind.TiedWorldRecord, item.Kind));
    }

    [Fact]
    public void Retains_failed_boards_and_only_keeps_latest_one_hundred_activities()
    {
        var checkedAt = DateTimeOffset.Parse("2026-08-02T00:00:00Z");
        var old = Document(checkedAt, Run("old", 1, 100, checkedAt)) with
        {
            Activities = Enumerable.Range(0, 100)
                .Select(index => Activity($"history-{index}", checkedAt.AddMinutes(index)))
                .ToArray()
        };

        var result = SpeedrunActivityDetector.Apply(
            old,
            [new(Board, [Run("new", 1, 90, checkedAt.AddHours(3))])],
            checkedAt.AddHours(4));

        Assert.Equal(100, result.Document.Activities.Count);
        Assert.Equal("new", result.Document.Activities[0].RunId);
    }

    private static SpeedrunActivityDocument Document(
        DateTimeOffset checkedAt,
        params SpeedrunPodiumEntry[] entries) => new()
        {
            Boards = new Dictionary<string, SpeedrunBoardBaseline>(StringComparer.Ordinal)
            {
                [Board.Key] = new(Board, checkedAt, entries)
            }
        };

    private static SpeedrunPodiumEntry Run(
        string id,
        int place,
        double seconds,
        DateTimeOffset? verifiedAt) => new(
            id,
            place,
            id,
            $"PT{seconds}S",
            seconds,
            verifiedAt,
            $"https://www.speedrun.com/hollowknight/runs/{id}");

    private static SpeedrunActivityEntry Activity(string id, DateTimeOffset at) => new(
        id,
        SpeedrunActivityKind.ThirdPlace,
        Board,
        Run(id, 3, 120, at),
        at);
}

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

public sealed record SpeedrunCategory(string Id, string Name, string? RunUrl);

public sealed record SpeedrunRun(
    string Id,
    int? Place,
    string PlayerName,
    string PrimaryTime,
    DateOnly? PlayedOn,
    DateTimeOffset? VerifiedAt,
    string? CategoryName,
    string? RunUrl)
{
    public string DisplayTime
    {
        get
        {
            try
            {
                TimeSpan duration = XmlConvert.ToTimeSpan(PrimaryTime);
                string hours = duration.TotalHours >= 1
                    ? $"{(int)duration.TotalHours:00}:"
                    : string.Empty;
                string milliseconds = duration.Milliseconds > 0
                    ? $".{duration.Milliseconds:000}"
                    : string.Empty;
                return $"{hours}{duration.Minutes:00}:{duration.Seconds:00}{milliseconds}";
            }
            catch (FormatException)
            {
                return PrimaryTime;
            }
        }
    }

    public string DisplayDate => PlayedOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "-";
}

public sealed record SpeedrunLeaderboard(
    SpeedrunCategory Category,
    IReadOnlyList<SpeedrunRun> Runs);

public sealed record SpeedrunRecentRuns(IReadOnlyList<SpeedrunRun> Runs);

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

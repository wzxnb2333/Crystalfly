namespace Crystalfly.Core.Models;

public enum GameId
{
    HollowKnight,
    Silksong
}

/// <summary>Stable game identity. Never infer compatibility from a version number alone.</summary>
public sealed record GameDefinition(
    GameId Id, string Name, string ChineseName, string ExecutableName, string DataDirectoryName,
    uint SteamAppId, uint WindowsDepotId, string LocalLowDirectoryName)
{
    public string ProcessName => Path.GetFileNameWithoutExtension(ExecutableName);
    public string MutexName => $"Crystalfly.Game.{Id}";
    public string GlobalGameManagersPath => $"{DataDirectoryName}/globalgamemanagers";
    public string GetLocalLowPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "AppData", "LocalLow", "Team Cherry", LocalLowDirectoryName);

    public static GameDefinition HollowKnight { get; } = new(
        GameId.HollowKnight, "Hollow Knight", "空洞骑士", "hollow_knight.exe",
        "hollow_knight_Data", 367520, 367521, "Hollow Knight");
    public static GameDefinition Silksong { get; } = new(
        GameId.Silksong, "Hollow Knight: Silksong", "丝之歌", "Hollow Knight Silksong.exe",
        "Hollow Knight Silksong_Data", 1030300, 1030301, "Hollow Knight Silksong");
    public static IReadOnlyList<GameDefinition> All { get; } = [HollowKnight, Silksong];
    public static GameDefinition Get(GameId id) => id switch
    {
        GameId.HollowKnight => HollowKnight,
        GameId.Silksong => Silksong,
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };
    public static GameDefinition? Detect(string root) => All.SingleOrDefault(game =>
        File.Exists(Path.Combine(root, game.ExecutableName))
        && File.Exists(Path.Combine(root, game.DataDirectoryName, "globalgamemanagers")));
}

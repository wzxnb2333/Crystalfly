using Crystalfly.Core.Models;

namespace Crystalfly.Core.Configuration;

public static class GameWorkspaceMigration
{
    public static CrystalflySettings Capture(CrystalflySettings settings)
    {
        _ = GameDefinition.Get(settings.SelectedGameId);
        var previous = settings.GameWorkspaces.FirstOrDefault(item => item.GameId == settings.SelectedGameId)
            ?? new GameWorkspaceSettings { GameId = settings.SelectedGameId };
        return settings with
        {
            GameWorkspaces = settings.GameWorkspaces.Where(item => item.GameId != settings.SelectedGameId)
                .Append(previous with { VersionRoot = settings.VersionRoot, CurrentInstanceId = settings.CurrentInstanceId }).ToArray()
        };
    }

    public static CrystalflySettings Select(CrystalflySettings settings, GameId gameId)
    {
        _ = GameDefinition.Get(gameId);
        settings = Capture(settings);
        var target = settings.GameWorkspaces.FirstOrDefault(item => item.GameId == gameId);
        return settings with
        {
            SelectedGameId = gameId, VersionRoot = target?.VersionRoot, CurrentInstanceId = target?.CurrentInstanceId
        };
    }
}

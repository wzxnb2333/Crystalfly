using Crystalfly.Core.Models;

namespace Crystalfly.Core.Saves;

public static class SaveSlotPath
{
    public static bool HasSteamProfileSaves(string root) => Directory.Exists(root)
        && LocalLow.LocalLowDirectory.EnumerateRelativeFiles(root).Any(path =>
            IsEditable(GameId.Silksong, path) && path.Contains('/')
            && !path.StartsWith("default/", StringComparison.OrdinalIgnoreCase));

    public static bool IsProfileName(string name) => name == "default"
        || name.Length is > 0 and <= 20 && name.All(character => character is >= '0' and <= '9');

    public static bool IsEditable(GameId gameId, string path) =>
        TryGetFileName(gameId, path, out var name) && IsSlotFileName(name);

    public static bool IsSaveFile(GameId gameId, string path)
    {
        if (!TryGetFileName(gameId, path, out var name) || name.Length < 9 || !IsSlotFileName(name[..9]))
            return false;
        if (name.Length == 9) return true;
        var suffix = name.AsSpan(9);
        return suffix.StartsWith(".bak", StringComparison.OrdinalIgnoreCase)
            && (suffix.Length > 4 || gameId == GameId.Silksong)
            && suffix[4..].IndexOfAnyExceptInRange('0', '9') < 0;
    }

    private static bool TryGetFileName(GameId gameId, string path, out string fileName)
    {
        fileName = string.Empty;
        if (string.IsNullOrWhiteSpace(path)) return false;
        var parts = path.Replace('\\', '/').Split('/');
        if (parts.Length == 1) { fileName = parts[0]; return true; }
        if (parts.Length == 2 && gameId == GameId.Silksong && IsProfileName(parts[0]))
        { fileName = parts[1]; return true; }
        return false;
    }

    private static bool IsSlotFileName(string name) => name.Length == 9
        && name.StartsWith("user", StringComparison.OrdinalIgnoreCase)
        && name[4] is >= '1' and <= '4'
        && name.EndsWith(".dat", StringComparison.OrdinalIgnoreCase);
}

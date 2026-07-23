namespace Crystalfly.Updater;

internal static class PathSafety
{
    public static bool IsStrictDescendant(string rootPath, string candidatePath)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        string candidate = Path.GetFullPath(candidatePath);
        string prefix = root + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}

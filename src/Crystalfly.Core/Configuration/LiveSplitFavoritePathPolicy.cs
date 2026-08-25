namespace Crystalfly.Core.Configuration;

public static class LiveSplitFavoritePathPolicy
{
    public static IReadOnlyList<string> NormalizeStoredPaths(IEnumerable<string>? paths)
    {
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths ?? [])
        {
            try
            {
                var fullPath = Path.GetFullPath(path.Trim());
                if (seen.Add(fullPath))
                {
                    normalized.Add(fullPath);
                }
            }
            catch (Exception exception) when (exception is ArgumentException
                or IOException
                or NotSupportedException)
            {
            }
        }

        return normalized.ToArray();
    }

    public static bool TryNormalizeExistingFile(string? path, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(path.Trim());
            if (!string.Equals(Path.GetExtension(fullPath), ".lss", StringComparison.OrdinalIgnoreCase)
                || !File.Exists(fullPath))
            {
                return false;
            }

            var attributes = File.GetAttributes(fullPath);
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            {
                return false;
            }

            normalized = fullPath;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            return false;
        }
    }
}

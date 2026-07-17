namespace Crystalfly.Steam.Downloads;

public static class DownloadPath
{
    public static string ResolveUnderRoot(string root, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathFullyQualified(normalized) || normalized.Contains(':', StringComparison.Ordinal))
            throw new InvalidDataException($"Depot path is not relative: {relativePath}");

        string fullRoot = Path.GetFullPath(root);
        string candidate = Path.GetFullPath(normalized, fullRoot);
        string rootPrefix = Path.TrimEndingDirectorySeparator(fullRoot) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Depot path escapes the staging directory: {relativePath}");

        return candidate;
    }
}

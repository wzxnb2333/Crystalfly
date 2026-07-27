namespace Crystalfly.Core.Instances;

public static class GameDirectoryIntegrityChecker
{
    private static readonly string[] CoreFiles =
    [
        "hollow_knight.exe",
        "hollow_knight_Data/globalgamemanagers"
    ];

    public static GameDirectoryIntegrityReport Inspect(
        string rootPath,
        IEnumerable<string>? requiredRelativePaths = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        var requiredFiles = CoreFiles
            .Concat(requiredRelativePaths ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        try
        {
            if (!Directory.Exists(root))
            {
                return new GameDirectoryIntegrityReport
                {
                    RootPath = root,
                    DirectoryExists = false,
                    MissingRequiredFiles = requiredFiles
                };
            }

            var isReparsePoint = (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0;
            var missingFiles = requiredFiles
                .Where(relativePath => !File.Exists(ResolveRelativeFile(root, relativePath)))
                .ToArray();
            return new GameDirectoryIntegrityReport
            {
                RootPath = root,
                DirectoryExists = true,
                IsReparsePoint = isReparsePoint,
                HasPendingDownload = File.Exists(Path.Combine(root, InstanceDirectory.PendingDownloadMarkerFileName)),
                UnityPlayerExists = File.Exists(Path.Combine(root, "UnityPlayer.dll")),
                MissingRequiredFiles = missingFiles
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new GameDirectoryIntegrityReport
            {
                RootPath = root,
                DirectoryExists = Directory.Exists(root),
                IsAccessible = false,
                MissingRequiredFiles = requiredFiles
            };
        }
    }

    private static string ResolveRelativeFile(string root, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (Path.IsPathFullyQualified(relativePath))
        {
            throw new ArgumentException("Required file path must be relative.", nameof(relativePath));
        }

        var normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(root, normalizedRelativePath));
        var prefix = root + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Required file path must stay inside the game directory.", nameof(relativePath));
        }
        return path;
    }
}

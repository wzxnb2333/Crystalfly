namespace Crystalfly.Core.Instances;

public static class VersionDirectoryScanner
{
    public static IReadOnlyList<string> Scan(string rootPath) =>
        Directory.EnumerateDirectories(rootPath, "*", SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(
                Path.GetFileName(path),
                ".crystalfly",
                StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}

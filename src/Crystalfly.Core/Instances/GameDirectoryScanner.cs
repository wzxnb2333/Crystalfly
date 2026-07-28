using Crystalfly.Core.Configuration;

namespace Crystalfly.Core.Instances;

public sealed class GameDirectoryScanner
{
    private readonly Func<string, IEnumerable<string>> enumerateDirectories;
    private readonly Func<string, GameDirectoryIntegrityReport> inspect;

    public GameDirectoryScanner()
        : this(
            path => Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly),
            path => GameDirectoryIntegrityChecker.Inspect(path))
    {
    }

    internal GameDirectoryScanner(
        Func<string, IEnumerable<string>> enumerateDirectories,
        Func<string, GameDirectoryIntegrityReport> inspect)
    {
        ArgumentNullException.ThrowIfNull(enumerateDirectories);
        ArgumentNullException.ThrowIfNull(inspect);
        this.enumerateDirectories = enumerateDirectories;
        this.inspect = inspect;
    }

    public Task<GameDirectoryScanResult> ScanAsync(
        string rootPath,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(rootPath, cancellationToken), cancellationToken);

    private GameDirectoryScanResult Scan(
        string rootPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        cancellationToken.ThrowIfCancellationRequested();
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        var paths = new List<string> { root };
        var skipped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            paths.AddRange(enumerateDirectories(root));
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or DirectoryNotFoundException)
        {
            skipped.Add(root);
        }

        var candidates = new List<GameDirectoryCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string fullPath;
            try
            {
                fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
            {
                skipped.Add(path);
                continue;
            }

            if (!seen.Add(fullPath)
                || !string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(Path.GetFileName(fullPath), ".crystalfly", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            GameDirectoryIntegrityReport report;
            try
            {
                report = inspect(fullPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                skipped.Add(fullPath);
                continue;
            }

            if (!report.IsValid)
            {
                skipped.Add(fullPath);
                continue;
            }

            var displayName = Path.GetFileName(fullPath);
            candidates.Add(new GameDirectoryCandidate
            {
                Path = fullPath,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? fullPath : displayName,
                Source = GameDirectorySourceKind.Custom
            });
        }

        return new GameDirectoryScanResult
        {
            RootPath = root,
            Candidates = candidates
                .OrderBy(candidate => string.Equals(candidate.Path, root, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            SkippedPaths = skipped.Order(StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }
}

using Crystalfly.Core.Configuration;

namespace Crystalfly.Core.Instances;

public sealed class GameDirectoryTreeScanner
{
    private const int DefaultMaxDepth = 8;

    private readonly Func<IEnumerable<string>> getRoots;
    private readonly Func<string, GameDirectoryIntegrityReport> inspect;
    private readonly int maxDepth;

    public GameDirectoryTreeScanner(int? maxDepth = null)
        : this(GetDriveRoots, path => GameDirectoryIntegrityChecker.Inspect(path), maxDepth)
    {
    }

    internal GameDirectoryTreeScanner(
        Func<IEnumerable<string>> getRoots,
        Func<string, GameDirectoryIntegrityReport> inspect,
        int? maxDepth = null)
    {
        ArgumentNullException.ThrowIfNull(getRoots);
        ArgumentNullException.ThrowIfNull(inspect);
        if (maxDepth is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "Max depth must be at least 1.");
        }
        this.getRoots = getRoots;
        this.inspect = inspect;
        this.maxDepth = maxDepth ?? DefaultMaxDepth;
    }

    public Task<GameDirectoryScanResult> ScanAllDrivesAsync(
        CancellationToken cancellationToken = default,
        IProgress<int>? progress = null) =>
        Task.Run(() => ScanAllDrives(cancellationToken, progress), cancellationToken);

    private GameDirectoryScanResult ScanAllDrives(
        CancellationToken cancellationToken,
        IProgress<int>? progress)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var candidates = new List<GameDirectoryCandidate>();
        var skipped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = 0;

        foreach (string root in getRoots())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }
            ScanRoot(root, candidates, skipped, seen, cancellationToken, progress, ref visited);
        }

        return new GameDirectoryScanResult
        {
            RootPath = string.Empty,
            Candidates = candidates
                .OrderBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            SkippedPaths = skipped.Order(StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    private void ScanRoot(
        string root,
        ICollection<GameDirectoryCandidate> candidates,
        ISet<string> skipped,
        ISet<string> seen,
        CancellationToken cancellationToken,
        IProgress<int>? progress,
        ref int visited)
    {
        string fullRoot;
        try
        {
            fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            skipped.Add(root);
            return;
        }

        var pending = new Stack<(string Path, int Depth)>();
        pending.Push((fullRoot, 1));
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (path, depth) = pending.Pop();
            if (++visited % 250 == 0)
            {
                progress?.Report(visited);
            }
            if (!seen.Add(path))
            {
                continue;
            }
            if (IsMetadataDirectory(path))
            {
                continue;
            }

            // Fast pre-check: only directories that contain a hollow_knight_Data
            // sibling can be game roots, so skip the full integrity inspect
            // otherwise. This keeps a full-drive walk cheap.
            bool preCheck = false;
            try
            {
                preCheck = Directory.Exists(Path.Combine(path, "hollow_knight_Data"));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                skipped.Add(path);
                continue;
            }

            if (preCheck)
            {
                GameDirectoryIntegrityReport report;
                try
                {
                    report = inspect(path);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    skipped.Add(path);
                    continue;
                }
                if (report.IsValid)
                {
                    var displayName = Path.GetFileName(path);
                    candidates.Add(new GameDirectoryCandidate
                    {
                        Path = path,
                        DisplayName = string.IsNullOrWhiteSpace(displayName) ? path : displayName,
                        Source = GameDirectorySourceKind.Custom
                    });
                    // A game root is a leaf; do not descend into a found game.
                    continue;
                }
                if (!report.DirectoryExists || report.IsReparsePoint)
                {
                    skipped.Add(path);
                    continue;
                }
            }

            if (depth >= maxDepth)
            {
                continue;
            }
            try
            {
                foreach (string child in Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly))
                {
                    pending.Push((child, depth + 1));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
            {
                skipped.Add(path);
            }
        }
    }

    private static bool IsMetadataDirectory(string path) =>
        string.Equals(Path.GetFileName(path), ".crystalfly", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> GetDriveRoots()
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType is DriveType.CDRom or DriveType.Network || !drive.IsReady)
            {
                continue;
            }
            yield return drive.RootDirectory.FullName;
        }
    }
}

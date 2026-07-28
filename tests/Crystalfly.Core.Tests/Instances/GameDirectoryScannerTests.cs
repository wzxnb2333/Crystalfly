using Crystalfly.Core.Configuration;
using Crystalfly.Core.Instances;

namespace Crystalfly.Core.Tests.Instances;

public sealed class GameDirectoryScannerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"crystalfly-game-scan-{Guid.NewGuid():N}");

    [Fact]
    public async Task Scan_checks_selected_directory_and_direct_children_only()
    {
        var selectedGame = CreateGame(Path.Combine(root, "selected"));
        var childGame = CreateGame(Path.Combine(selectedGame, "child"));
        CreateGame(Path.Combine(childGame, "nested"));

        var result = await new GameDirectoryScanner().ScanAsync(selectedGame);

        Assert.Equal(Path.GetFullPath(selectedGame), result.RootPath);
        Assert.Equal([selectedGame, childGame], result.Candidates.Select(candidate => candidate.Path));
        Assert.All(result.Candidates, candidate => Assert.Equal(GameDirectorySourceKind.Custom, candidate.Source));
    }

    [Fact]
    public async Task Scan_skips_metadata_pending_incomplete_and_reparse_directories()
    {
        Directory.CreateDirectory(root);
        CreateGame(Path.Combine(root, ".crystalfly"));
        var pending = CreateGame(Path.Combine(root, "pending"));
        File.WriteAllText(Path.Combine(pending, InstanceDirectory.PendingDownloadMarkerFileName), "{}");
        Directory.CreateDirectory(Path.Combine(root, "incomplete"));
        var target = CreateGame(Path.Combine(root, "target"));
        Directory.CreateSymbolicLink(Path.Combine(root, "linked"), target);

        var result = await new GameDirectoryScanner().ScanAsync(root);

        Assert.Equal([target], result.Candidates.Select(candidate => candidate.Path));
        Assert.Contains(Path.Combine(root, "pending"), result.SkippedPaths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Path.Combine(root, "incomplete"), result.SkippedPaths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Path.Combine(root, "linked"), result.SkippedPaths, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scan_deduplicates_normalized_paths()
    {
        var game = CreateGame(Path.Combine(root, "game"));
        var scanner = new GameDirectoryScanner(
            _ => [game, Path.Combine(game, ".")],
            path => GameDirectoryIntegrityChecker.Inspect(path));

        var result = await scanner.ScanAsync(root);

        Assert.Single(result.Candidates);
    }

    [Fact]
    public async Task Scan_records_inaccessible_root_without_throwing()
    {
        Directory.CreateDirectory(root);
        var scanner = new GameDirectoryScanner(
            _ => throw new UnauthorizedAccessException("denied"),
            path => GameDirectoryIntegrityChecker.Inspect(path));

        var result = await scanner.ScanAsync(root);

        Assert.Empty(result.Candidates);
        Assert.Equal([Path.GetFullPath(root)], result.SkippedPaths);
    }

    [Fact]
    public async Task Scan_propagates_cancellation()
    {
        Directory.CreateDirectory(root);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new GameDirectoryScanner().ScanAsync(root, cancellation.Token));
    }

    private static string CreateGame(string path)
    {
        Directory.CreateDirectory(Path.Combine(path, "hollow_knight_Data"));
        File.WriteAllText(Path.Combine(path, "hollow_knight.exe"), "exe");
        File.WriteAllText(Path.Combine(path, "hollow_knight_Data", "globalgamemanagers"), "data");
        return Path.GetFullPath(path);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

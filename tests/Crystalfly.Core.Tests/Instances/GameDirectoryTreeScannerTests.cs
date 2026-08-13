using Crystalfly.Core.Configuration;
using Crystalfly.Core.Instances;

namespace Crystalfly.Core.Tests.Instances;

public sealed class GameDirectoryTreeScannerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"crystalfly-tree-scan-{Guid.NewGuid():N}");

    [Fact]
    public async Task ScanAllDrives_recursively_finds_nested_game_directories()
    {
        var deepGame = CreateGame(Path.Combine(root, "a", "b", "c", "Hollow Knight"));
        Directory.CreateDirectory(Path.Combine(root, "empty"));

        var result = await new GameDirectoryTreeScanner(
            getRoots: () => [root],
            inspect: path => GameDirectoryIntegrityChecker.Inspect(path))
            .ScanAllDrivesAsync();

        Assert.Equal([deepGame], result.Candidates.Select(candidate => candidate.Path));
        Assert.All(result.Candidates, candidate => Assert.Equal(GameDirectorySourceKind.Custom, candidate.Source));
    }

    [Fact]
    public async Task ScanAllDrives_does_not_descend_into_a_found_game_directory()
    {
        var outer = CreateGame(Path.Combine(root, "outer"));
        CreateGame(Path.Combine(outer, "inner"));

        var result = await new GameDirectoryTreeScanner(
            getRoots: () => [root],
            inspect: path => GameDirectoryIntegrityChecker.Inspect(path))
            .ScanAllDrivesAsync();

        Assert.Equal([outer], result.Candidates.Select(candidate => candidate.Path));
    }

    [Fact]
    public async Task ScanAllDrives_skips_incomplete_and_metadata_directories()
    {
        var game = CreateGame(Path.Combine(root, "game"));
        var incomplete = Path.Combine(root, "incomplete");
        Directory.CreateDirectory(incomplete);
        CreateGame(Path.Combine(root, "meta", ".crystalfly"));

        var result = await new GameDirectoryTreeScanner(
            getRoots: () => [root],
            inspect: path => GameDirectoryIntegrityChecker.Inspect(path))
            .ScanAllDrivesAsync();

        Assert.Equal([game], result.Candidates.Select(candidate => candidate.Path));
    }

    [Fact]
    public async Task ScanAllDrives_respects_the_maximum_depth()
    {
        // root is depth 1; "within" lands at depth 4 and "deep" at depth 6.
        var within = CreateGame(Path.Combine(root, "l1", "l2", "game"));
        var beyond = CreateGame(Path.Combine(root, "l1", "l2", "l3", "l4", "deep"));

        var result = await new GameDirectoryTreeScanner(
            getRoots: () => [root],
            inspect: path => GameDirectoryIntegrityChecker.Inspect(path),
            maxDepth: 4)
            .ScanAllDrivesAsync();

        Assert.Equal([within], result.Candidates.Select(candidate => candidate.Path));
        Assert.DoesNotContain(beyond, result.Candidates.Select(candidate => candidate.Path));
    }

    [Fact]
    public async Task ScanAllDrives_records_inaccessible_roots_without_throwing()
    {
        var scanner = new GameDirectoryTreeScanner(
            getRoots: () => [root],
            inspect: _ => new GameDirectoryIntegrityReport
            {
                RootPath = Path.GetFullPath(root),
                DirectoryExists = false,
                IsAccessible = false,
                MissingRequiredFiles = []
            });

        var result = await scanner.ScanAllDrivesAsync();

        Assert.Empty(result.Candidates);
    }

    [Fact]
    public async Task ScanAllDrives_propagates_cancellation()
    {
        Directory.CreateDirectory(root);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new GameDirectoryTreeScanner(
                getRoots: () => [root],
                inspect: path => GameDirectoryIntegrityChecker.Inspect(path))
                .ScanAllDrivesAsync(cancellation.Token));
    }

    [Fact]
    public async Task ScanAllDrives_returns_no_candidates_for_empty_roots()
    {
        Directory.CreateDirectory(Path.Combine(root, "nested"));

        var result = await new GameDirectoryTreeScanner(
            getRoots: () => [root],
            inspect: path => GameDirectoryIntegrityChecker.Inspect(path))
            .ScanAllDrivesAsync();

        Assert.Empty(result.Candidates);
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

using Crystalfly.Steam.Discovery;

namespace Crystalfly.Steam.Tests.Discovery;

public sealed class SteamLibraryDiscoveryServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"crystalfly-steam-libraries-{Guid.NewGuid():N}");

    [Fact]
    public async Task DiscoverAsyncFindsDefaultAndCustomLibrariesWithoutDuplicates()
    {
        string steamRoot = CreateLibrary("Steam", "Hollow Knight", "Hollow Knight");
        string customRoot = CreateLibrary("Library", "Hollow Knight Beta", "Hollow Knight Beta");
        WriteLibraryFolders(steamRoot, steamRoot, customRoot, customRoot + Path.DirectorySeparatorChar);
        SteamLibraryDiscoveryService service = new(new FixedPathProvider(steamRoot, steamRoot + Path.DirectorySeparatorChar));

        IReadOnlyList<SteamLibraryCandidate> candidates = await service.DiscoverAsync();

        Assert.Equal(2, candidates.Count);
        Assert.Contains(candidates, candidate =>
            candidate.GamePath == Path.GetFullPath(Path.Combine(steamRoot, "steamapps", "common", "Hollow Knight")) &&
            candidate.LibraryPath == Path.GetFullPath(steamRoot) &&
            candidate.DisplayName == "Hollow Knight");
        Assert.Contains(candidates, candidate =>
            candidate.GamePath == Path.GetFullPath(Path.Combine(customRoot, "steamapps", "common", "Hollow Knight Beta")) &&
            candidate.LibraryPath == Path.GetFullPath(customRoot));
    }

    [Fact]
    public async Task DiscoverAsyncStillScansDefaultLibraryWhenLibraryFoldersIsMissingOrMalformed()
    {
        string missingVdfRoot = CreateLibrary("MissingVdf", "Hollow Knight", "Hollow Knight");
        string malformedVdfRoot = CreateLibrary("MalformedVdf", "Hollow Knight", "Hollow Knight");
        File.WriteAllText(Path.Combine(malformedVdfRoot, "steamapps", "libraryfolders.vdf"), "not a vdf {");
        SteamLibraryDiscoveryService service = new(new FixedPathProvider(missingVdfRoot, malformedVdfRoot));

        IReadOnlyList<SteamLibraryCandidate> candidates = await service.DiscoverAsync();

        Assert.Equal(2, candidates.Count);
    }

    [Fact]
    public async Task DiscoverAsyncRejectsManifestForAnotherApp()
    {
        string steamRoot = CreateLibrary("WrongApp", "Hollow Knight", "Wrong");
        File.WriteAllText(AppManifestPath(steamRoot), "\"AppState\" { \"appid\" \"123\" \"installdir\" \"Hollow Knight\" }");
        SteamLibraryDiscoveryService service = new(new FixedPathProvider(steamRoot));

        Assert.Empty(await service.DiscoverAsync());
    }

    [Fact]
    public async Task DiscoverAsyncSkipsInvalidManifestsAndIncompleteGameDirectories()
    {
        string steamRoot = CreateEmptyLibrary("Steam");
        string malformedManifestRoot = CreateEmptyLibrary("MalformedManifest");
        string traversalManifestRoot = CreateEmptyLibrary("TraversalManifest");
        string incompleteGameRoot = CreateEmptyLibrary("IncompleteGame");
        string validRoot = CreateLibrary("Valid", "Hollow Knight", "Hollow Knight");
        File.WriteAllText(AppManifestPath(malformedManifestRoot), "broken {");
        WriteAppManifest(traversalManifestRoot, "../Hollow Knight", "Traversal");
        WriteAppManifest(incompleteGameRoot, "Hollow Knight", "Incomplete");
        Directory.CreateDirectory(Path.Combine(incompleteGameRoot, "steamapps", "common", "Hollow Knight"));
        File.WriteAllText(Path.Combine(incompleteGameRoot, "steamapps", "common", "Hollow Knight", "hollow_knight.exe"), "game");
        WriteLibraryFolders(steamRoot, malformedManifestRoot, traversalManifestRoot, incompleteGameRoot, validRoot);
        SteamLibraryDiscoveryService service = new(new FixedPathProvider(steamRoot));

        SteamLibraryCandidate candidate = Assert.Single(await service.DiscoverAsync());

        Assert.Equal(Path.GetFullPath(Path.Combine(validRoot, "steamapps", "common", "Hollow Knight")), candidate.GamePath);
    }

    [Fact]
    public async Task DiscoverAsyncContinuesAfterOneLibraryCannotBeRead()
    {
        string steamRoot = CreateEmptyLibrary("Steam");
        string deniedRoot = CreateLibrary("Denied", "Hollow Knight", "Denied");
        string validRoot = CreateLibrary("Valid", "Hollow Knight", "Valid");
        WriteLibraryFolders(steamRoot, deniedRoot, validRoot);
        string deniedManifest = Path.GetFullPath(AppManifestPath(deniedRoot));
        SteamLibraryDiscoveryService service = new(
            new FixedPathProvider(steamRoot),
            path => string.Equals(Path.GetFullPath(path), deniedManifest, StringComparison.OrdinalIgnoreCase)
                ? throw new UnauthorizedAccessException("denied")
                : File.OpenRead(path));

        SteamLibraryCandidate candidate = Assert.Single(await service.DiscoverAsync());

        Assert.Equal("Valid", candidate.DisplayName);
    }

    [Fact]
    public async Task DiscoverAsyncReadsLegacyLibraryFolderValues()
    {
        string steamRoot = CreateEmptyLibrary("Steam");
        string customRoot = CreateLibrary("LegacyLibrary", "Hollow Knight", "Legacy");
        File.WriteAllText(Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf"), $$"""
            "LibraryFolders"
            {
                "1" "{{customRoot.Replace("\\", "\\\\", StringComparison.Ordinal)}}"
            }
            """);
        SteamLibraryDiscoveryService service = new(new FixedPathProvider(steamRoot));

        SteamLibraryCandidate candidate = Assert.Single(await service.DiscoverAsync());

        Assert.Equal("Legacy", candidate.DisplayName);
    }

    [Fact]
    public async Task DiscoverAsyncPropagatesCancellation()
    {
        string steamRoot = CreateLibrary("Steam", "Hollow Knight", "Hollow Knight");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        SteamLibraryDiscoveryService service = new(new FixedPathProvider(steamRoot));

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.DiscoverAsync(cancellation.Token));
    }

    private string CreateLibrary(string name, string installDirectory, string displayName)
    {
        string library = CreateEmptyLibrary(name);
        WriteAppManifest(library, installDirectory, displayName);
        string gameDirectory = Path.Combine(library, "steamapps", "common", installDirectory);
        Directory.CreateDirectory(Path.Combine(gameDirectory, "hollow_knight_Data"));
        File.WriteAllText(Path.Combine(gameDirectory, "hollow_knight.exe"), "game");
        File.WriteAllText(Path.Combine(gameDirectory, "hollow_knight_Data", "globalgamemanagers"), "data");
        return library;
    }

    private string CreateEmptyLibrary(string name)
    {
        string library = Path.Combine(_root, name);
        Directory.CreateDirectory(Path.Combine(library, "steamapps"));
        return library;
    }

    private static string AppManifestPath(string library) => Path.Combine(library, "steamapps", "appmanifest_367520.acf");

    private static void WriteAppManifest(string library, string installDirectory, string displayName)
    {
        File.WriteAllText(AppManifestPath(library), $$"""
            "AppState"
            {
                "appid" "367520"
                "name" "{{displayName}}"
                "installdir" "{{installDirectory}}"
            }
            """);
    }

    private static void WriteLibraryFolders(string steamRoot, params string[] libraryPaths)
    {
        string entries = string.Join(Environment.NewLine, libraryPaths.Select((path, index) => $$"""
                "{{index}}"
                {
                    "path" "{{path.Replace("\\", "\\\\", StringComparison.Ordinal)}}"
                }
                """));
        File.WriteAllText(Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf"), $$"""
            "libraryfolders"
            {
            {{entries}}
            }
            """);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class FixedPathProvider(params string[] paths) : ISteamInstallPathProvider
    {
        public IReadOnlyList<string> GetInstallPaths() => paths;
    }
}

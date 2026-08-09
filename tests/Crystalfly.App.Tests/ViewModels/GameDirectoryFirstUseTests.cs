using Crystalfly.App.ViewModels;
using Crystalfly.Core.Instances;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class GameDirectoryFirstUseTests : IDisposable
{
    private readonly TestDirectory testDirectory = new();

    [Fact]
    public async Task Adding_a_new_game_directory_creates_the_crystalfly_metadata_root()
    {
        var applicationDataRoot = testDirectory.CreateDirectory("app-data");
        var gameRoot = testDirectory.CreateDirectory("games");
        var gamePath = CreateGameDirectory(Path.Combine(gameRoot, "Hollow Knight"));

        await using var viewModel = new MainViewModel(applicationDataRoot);
        await viewModel.InitializeAsync();

        await viewModel.Instances.AddCustomGameDirectoryAsync(gamePath);
        var candidate = Assert.Single(viewModel.Instances.GameDirectoryCandidates);
        candidate.IsConfirmed = true;
        await viewModel.Instances.ConfirmGameDirectoryCandidatesCommand.ExecuteAsync(null);

        Assert.Equal(Path.GetFullPath(gameRoot), viewModel.VersionRoot);
        Assert.Contains(viewModel.Instances.GameDirectories, directory =>
            string.Equals(directory.Path, Path.GetFullPath(gameRoot), StringComparison.OrdinalIgnoreCase));

        Assert.True(
            Directory.Exists(Path.Combine(gameRoot, ".crystalfly")),
            "Registering a game directory must create the .crystalfly metadata root under the version root.");
    }

    [Fact]
    public async Task Refreshing_after_registering_a_new_directory_imports_the_game_as_an_instance()
    {
        var applicationDataRoot = testDirectory.CreateDirectory("app-data");
        var gameRoot = testDirectory.CreateDirectory("games");
        var gamePath = CreateGameDirectory(Path.Combine(gameRoot, "Hollow Knight"));

        await using var viewModel = new MainViewModel(applicationDataRoot);
        await viewModel.InitializeAsync();

        await viewModel.Instances.AddCustomGameDirectoryAsync(gamePath);
        var candidate = Assert.Single(viewModel.Instances.GameDirectoryCandidates);
        candidate.IsConfirmed = true;
        await viewModel.Instances.ConfirmGameDirectoryCandidatesCommand.ExecuteAsync(null);
        await viewModel.RefreshCommand.ExecuteAsync(null);

        var instance = Assert.Single(viewModel.Instances.Instances);
        Assert.Equal(Path.GetFullPath(gamePath), instance.RootPath);
        Assert.True(Directory.Exists(Path.Combine(gameRoot, ".crystalfly", "instances")));
        Assert.True(File.Exists(InstanceSidecar.GetMarkerPath(gamePath)));
    }

    [Fact]
    public async Task Adding_a_directory_with_existing_crystalfly_metadata_preserves_it()
    {
        var applicationDataRoot = testDirectory.CreateDirectory("app-data");
        var gameRoot = testDirectory.CreateDirectory("games");
        var metadataRoot = Directory.CreateDirectory(Path.Combine(gameRoot, ".crystalfly", "downloads")).FullName;
        var keptFile = Path.Combine(metadataRoot, "keep.txt");
        await File.WriteAllTextAsync(keptFile, "keep");
        var gamePath = CreateGameDirectory(Path.Combine(gameRoot, "Hollow Knight"));

        await using var viewModel = new MainViewModel(applicationDataRoot);
        await viewModel.InitializeAsync();

        await viewModel.Instances.AddCustomGameDirectoryAsync(gamePath);
        var candidate = Assert.Single(viewModel.Instances.GameDirectoryCandidates);
        candidate.IsConfirmed = true;
        await viewModel.Instances.ConfirmGameDirectoryCandidatesCommand.ExecuteAsync(null);

        Assert.True(File.Exists(keptFile), "Registering a directory must not disturb existing .crystalfly state.");
    }

    private static string CreateGameDirectory(string path)
    {
        Directory.CreateDirectory(Path.Combine(path, "hollow_knight_Data"));
        File.WriteAllText(Path.Combine(path, "hollow_knight.exe"), "exe");
        File.WriteAllText(Path.Combine(path, "UnityPlayer.dll"), "unity");
        File.WriteAllText(Path.Combine(path, "hollow_knight_Data", "globalgamemanagers"), "data");
        return Path.GetFullPath(path);
    }

    public void Dispose() => testDirectory.Dispose();

    private sealed class TestDirectory : IDisposable
    {
        private readonly string root = Path.Combine(
            Path.GetTempPath(),
            "Crystalfly.Tests",
            Guid.NewGuid().ToString("N"));

        public string CreateDirectory(params string[] parts)
        {
            var path = parts.Aggregate(root, Path.Combine);
            Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}

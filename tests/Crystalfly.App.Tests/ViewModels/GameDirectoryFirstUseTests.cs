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

        var outcome = await viewModel.Instances.AddCustomGameDirectoryAsync(gamePath);
        Assert.Equal(CustomGameDirectoryScanOutcome.CandidatesFound, outcome);
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

    [Fact]
    public async Task Adding_an_empty_folder_registers_it_as_the_version_root()
    {
        var applicationDataRoot = testDirectory.CreateDirectory("app-data");
        var emptyRoot = testDirectory.CreateDirectory("empty-library");

        await using var viewModel = new MainViewModel(applicationDataRoot);
        await viewModel.InitializeAsync();

        var outcome = await viewModel.Instances.AddCustomGameDirectoryAsync(emptyRoot);
        Assert.Equal(CustomGameDirectoryScanOutcome.NoGameInstallation, outcome);
        Assert.True(await viewModel.Instances.AddGameDirectoryAsVersionRootAsync(emptyRoot));

        Assert.Equal(Path.GetFullPath(emptyRoot), viewModel.VersionRoot);
        Assert.Equal(Path.GetFullPath(emptyRoot), viewModel.Instances.SelectedGameDirectory?.Path);
        Assert.Contains(viewModel.Instances.GameDirectories, directory =>
            string.Equals(directory.Path, Path.GetFullPath(emptyRoot), StringComparison.OrdinalIgnoreCase));
        Assert.True(Directory.Exists(Path.Combine(emptyRoot, ".crystalfly")));
        Assert.Empty(viewModel.Instances.Instances);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal(viewModel.Loc["EmptyGameDirectoryAdded"], viewModel.StatusMessage);
    }

    [Fact]
    public async Task Registered_empty_version_root_survives_restart()
    {
        var applicationDataRoot = testDirectory.CreateDirectory("app-data");
        var emptyRoot = testDirectory.CreateDirectory("empty-library");

        await using (var first = new MainViewModel(applicationDataRoot))
        {
            await first.InitializeAsync();
            Assert.True(await first.Instances.AddGameDirectoryAsVersionRootAsync(emptyRoot));
        }

        await using var reopened = new MainViewModel(applicationDataRoot);
        await reopened.InitializeAsync();

        Assert.Equal(Path.GetFullPath(emptyRoot), reopened.VersionRoot);
        Assert.Contains(reopened.Instances.GameDirectories, directory =>
            string.Equals(directory.Path, Path.GetFullPath(emptyRoot), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Game_files_added_to_a_subfolder_of_an_empty_version_root_are_discovered()
    {
        var applicationDataRoot = testDirectory.CreateDirectory("app-data");
        var emptyRoot = testDirectory.CreateDirectory("empty-library");

        await using var viewModel = new MainViewModel(applicationDataRoot);
        await viewModel.InitializeAsync();
        Assert.True(await viewModel.Instances.AddGameDirectoryAsVersionRootAsync(emptyRoot));
        Assert.True(await viewModel.Instances.AddGameDirectoryAsVersionRootAsync(emptyRoot));
        Assert.Single(viewModel.Instances.GameDirectories);

        var gamePath = CreateGameDirectory(Path.Combine(emptyRoot, "Hollow Knight"));
        await viewModel.RefreshCommand.ExecuteAsync(null);

        var instance = Assert.Single(viewModel.Instances.Instances);
        Assert.Equal(gamePath, instance.RootPath);
    }

    [Fact]
    public async Task Folder_with_another_game_installation_is_not_offered_as_a_version_root()
    {
        var applicationDataRoot = testDirectory.CreateDirectory("app-data");
        var silksongRoot = testDirectory.CreateDirectory("silksong");
        Directory.CreateDirectory(Path.Combine(silksongRoot, "Hollow Knight Silksong_Data"));
        await File.WriteAllTextAsync(Path.Combine(silksongRoot, "Hollow Knight Silksong.exe"), "exe");
        await File.WriteAllTextAsync(
            Path.Combine(silksongRoot, "Hollow Knight Silksong_Data", "globalgamemanagers"), "data");

        await using var viewModel = new MainViewModel(applicationDataRoot);
        await viewModel.InitializeAsync();

        var outcome = await viewModel.Instances.AddCustomGameDirectoryAsync(silksongRoot);

        Assert.Equal(CustomGameDirectoryScanOutcome.OtherGameInstallation, outcome);
        Assert.Empty(viewModel.Instances.GameDirectoryCandidates);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
    }

    [Fact]
    public async Task Non_empty_folder_without_a_game_is_not_offered_as_a_version_root()
    {
        var applicationDataRoot = testDirectory.CreateDirectory("app-data");
        var folder = testDirectory.CreateDirectory("not-empty");
        await File.WriteAllTextAsync(Path.Combine(folder, "keep.txt"), "keep");

        await using var viewModel = new MainViewModel(applicationDataRoot);
        await viewModel.InitializeAsync();

        var outcome = await viewModel.Instances.AddCustomGameDirectoryAsync(folder);

        Assert.Equal(CustomGameDirectoryScanOutcome.NotAnEmptyFolder, outcome);
        Assert.Empty(viewModel.Instances.GameDirectories);
        Assert.False(Directory.Exists(Path.Combine(folder, ".crystalfly")));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
        Assert.False(await viewModel.Instances.AddGameDirectoryAsVersionRootAsync(folder));
        Assert.Empty(viewModel.Instances.GameDirectories);
    }

    [Fact]
    public async Task Empty_folder_nested_in_a_version_root_is_not_offered_as_a_new_version_root()
    {
        var applicationDataRoot = testDirectory.CreateDirectory("app-data");
        var library = testDirectory.CreateDirectory("library");
        Directory.CreateDirectory(Path.Combine(library, ".crystalfly"));
        var nested = testDirectory.CreateDirectory("library", "nested");

        await using var viewModel = new MainViewModel(applicationDataRoot);
        await viewModel.InitializeAsync();

        var outcome = await viewModel.Instances.AddCustomGameDirectoryAsync(nested);

        Assert.Equal(CustomGameDirectoryScanOutcome.NestedVersionRoot, outcome);
        Assert.Empty(viewModel.Instances.GameDirectories);
        Assert.False(Directory.Exists(Path.Combine(nested, ".crystalfly")));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
    }

    [Fact]
    public async Task Complete_game_installation_is_not_registered_as_a_version_root()
    {
        var applicationDataRoot = testDirectory.CreateDirectory("app-data");
        var gamePath = CreateGameDirectory(
            Path.Combine(testDirectory.CreateDirectory("games"), "Hollow Knight"));

        await using var viewModel = new MainViewModel(applicationDataRoot);
        await viewModel.InitializeAsync();

        Assert.False(await viewModel.Instances.AddGameDirectoryAsVersionRootAsync(gamePath));
        Assert.Empty(viewModel.Instances.GameDirectories);
        Assert.False(Directory.Exists(Path.Combine(gamePath, ".crystalfly")));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
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

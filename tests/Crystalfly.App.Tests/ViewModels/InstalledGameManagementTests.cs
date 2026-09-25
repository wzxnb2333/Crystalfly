using System.Reflection;
using Crystalfly.App.ViewModels;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class InstalledGameManagementTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"crystalfly-installed-games-{Guid.NewGuid():N}");

    [Fact]
    public async Task Failed_registration_does_not_persist_a_phantom_directory()
    {
        var appData = CreateDirectory("app-data");
        var versions = CreateDirectory("versions");
        var game = CreateGame(versions, "game");
        await File.WriteAllTextAsync(Path.Combine(versions, ".crystalfly"), "occupied");
        await using var viewModel = CreateViewModel(appData);
        await InitializeAsync(viewModel);
        await viewModel.Instances.AddCustomGameDirectoryAsync(game);
        var candidate = Assert.Single(viewModel.Instances.GameDirectoryCandidates);
        candidate.IsConfirmed = true;

        await viewModel.Instances.ConfirmGameDirectoryCandidatesCommand.ExecuteAsync(null);

        Assert.Empty(viewModel.Instances.GameDirectories);
        Assert.True(string.IsNullOrEmpty(viewModel.VersionRoot));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
        Assert.Contains(candidate, viewModel.Instances.GameDirectoryCandidates);
        await viewModel.DisposeAsync();
        Assert.Empty((await CrystalflySettingsStore.LoadAsync(Path.Combine(appData, "settings.json"))).GameDirectories);
    }

    [Fact]
    public async Task Refresh_shows_healthy_games_and_reports_an_unreadable_instance_marker()
    {
        var appData = CreateDirectory("app-data");
        var versions = CreateDirectory("versions");
        var healthy = CreateGame(versions, "healthy");
        var broken = CreateGame(versions, "broken");
        await File.WriteAllTextAsync(InstanceSidecar.GetMarkerPath(broken), "{broken");
        await SaveSettingsAsync(appData, versions);
        await using var viewModel = CreateViewModel(appData);

        await InitializeAsync(viewModel);

        Assert.Equal(healthy, Assert.Single(viewModel.Instances.Instances).RootPath);
        Assert.Contains("broken", viewModel.ErrorMessage ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Disconnected_disk_clears_stale_instances_and_recovers_when_it_returns()
    {
        var appData = CreateDirectory("app-data");
        var versions = CreateDirectory("versions");
        var game = CreateGame(versions, "game");
        await SaveSettingsAsync(appData, versions);
        await using var viewModel = CreateViewModel(appData);
        await InitializeAsync(viewModel);
        var identity = Assert.Single(viewModel.Instances.Instances).Id;
        Directory.Move(versions, versions + "-offline");

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Empty(viewModel.Instances.Instances);
        Assert.Null(viewModel.SelectedInstance);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
        Assert.Equal(0, viewModel.Instances.SelectedGameDirectory!.InstanceCount);
        Assert.Equal(viewModel.Loc["ScanFailed"], viewModel.Instances.SelectedGameDirectory.ScanStatus);
        Directory.Move(versions + "-offline", versions);
        await viewModel.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(identity, Assert.Single(viewModel.Instances.Instances).Id);
        Assert.Equal(game, viewModel.SelectedInstance!.RootPath);
    }

    [Fact]
    public async Task Installed_mode_reopens_registered_disk_games_with_the_same_identity()
    {
        var appData = CreateDirectory("app-data");
        var versions = CreateDirectory("versions");
        var game = CreateGame(versions, "game");
        string identity;
        await using (var first = CreateViewModel(appData))
        {
            await InitializeAsync(first);
            await first.Instances.AddCustomGameDirectoryAsync(game);
            Assert.Single(first.Instances.GameDirectoryCandidates).IsConfirmed = true;
            await first.Instances.ConfirmGameDirectoryCandidatesCommand.ExecuteAsync(null);
            await first.RefreshCommand.ExecuteAsync(null);
            identity = Assert.Single(first.Instances.Instances).Id;
        }
        await using var reopened = CreateViewModel(appData);

        await InitializeAsync(reopened);

        Assert.Equal(versions, reopened.VersionRoot);
        Assert.Equal(identity, Assert.Single(reopened.Instances.Instances).Id);
        Assert.Equal(game, reopened.SelectedInstance!.RootPath);
        Assert.True(File.Exists(Path.Combine(appData, "settings.json")));
        Assert.True(File.Exists(InstanceSidecar.GetMetadataPath(game, identity)));
    }

    [Fact]
    public async Task Restart_rejects_a_linked_metadata_root_before_running_recovery()
    {
        var appData = CreateDirectory("app-data");
        var versions = CreateDirectory("versions");
        var game = CreateGame(versions, "game");
        var external = CreateDirectory("external");
        var retained = Directory.CreateDirectory(Path.Combine(external, "delete-pending", "empty")).FullName;
        Directory.CreateSymbolicLink(Path.Combine(versions, ".crystalfly"), external);
        await SaveSettingsAsync(appData, versions);
        await using var viewModel = CreateViewModel(appData);

        await InitializeAsync(viewModel);

        Assert.Empty(viewModel.Instances.Instances);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
        Assert.True(Directory.Exists(retained));
        Assert.False(File.Exists(InstanceSidecar.GetMarkerPath(game)));
    }

    [Fact]
    public async Task Corrupt_loader_metadata_does_not_hide_other_games()
    {
        var appData = CreateDirectory("app-data");
        var versions = CreateDirectory("versions");
        var healthy = CreateGame(versions, "healthy");
        var broken = CreateGame(versions, "broken");
        var records = await InstanceImportService.DiscoverAsync(versions, new GameCatalog());
        var brokenRecord = records.Single(record => record.RootPath == broken);
        var state = Path.GetDirectoryName(InstanceSidecar.GetMetadataPath(broken, brokenRecord.Id))!;
        await File.WriteAllTextAsync(Path.Combine(state, "loader.json"), "{broken");
        await SaveSettingsAsync(appData, versions);
        await using var viewModel = CreateViewModel(appData);

        await InitializeAsync(viewModel);

        Assert.Equal(healthy, Assert.Single(viewModel.Instances.Instances).RootPath);
        Assert.Contains("broken", viewModel.ErrorMessage ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal(viewModel.Loc["ScanPartial"], viewModel.Instances.SelectedGameDirectory!.ScanStatus);
    }

    [Fact]
    public async Task Failed_refresh_clears_previously_available_game_actions()
    {
        var appData = CreateDirectory("app-data");
        var versions = CreateDirectory("versions");
        _ = CreateGame(versions, "game");
        await SaveSettingsAsync(appData, versions);
        await using var viewModel = CreateViewModel(appData);
        await InitializeAsync(viewModel);
        Assert.Single(viewModel.Instances.Instances);
        var metadata = Path.Combine(versions, ".crystalfly");
        Directory.Move(metadata, metadata + "-original");
        Directory.CreateSymbolicLink(metadata, CreateDirectory("external"));

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Empty(viewModel.Instances.Instances);
        Assert.Null(viewModel.SelectedInstance);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
    }

    [Fact]
    public async Task Refresh_cannot_publish_old_disk_results_after_the_active_root_changes()
    {
        var appData = CreateDirectory("app-data");
        var versions = CreateDirectory("versions");
        var otherVersions = CreateDirectory("other-versions");
        _ = CreateGame(versions, "game");
        await SaveSettingsAsync(appData, versions);
        await using var viewModel = CreateViewModel(appData);
        await InitializeAsync(viewModel);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SetField(viewModel, "instanceDiscovery", new Func<string, GameCatalog, CancellationToken, Task<IReadOnlyList<InstanceRecord>>>(
            async (scanRoot, catalog, cancellation) =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(cancellation);
                return await InstanceImportService.DiscoverAsync(scanRoot, catalog, cancellation);
            }));
        var refresh = viewModel.RefreshCommand.ExecuteAsync(null);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            viewModel.VersionRoot = otherVersions;
        }
        finally
        {
            release.TrySetResult();
        }

        await refresh;

        Assert.Empty(viewModel.Instances.Instances);
        Assert.Null(viewModel.SelectedInstance);
        Assert.False(Directory.Exists(Path.Combine(otherVersions, ".crystalfly")));
    }

    private MainViewModel CreateViewModel(string appData)
    {
        var viewModel = new MainViewModel(appData, sharedLocalLowPathOverride: CreateDirectory("shared-saves"));
        SetField(viewModel, "catalogLoader", new Func<CancellationToken, Task<GameCatalog>>(_ => Task.FromResult(new GameCatalog())));
        SetField(viewModel, "steamReconnect", new Func<Task>(() => Task.CompletedTask));
        return viewModel;
    }

    private static async Task InitializeAsync(MainViewModel viewModel)
    {
        await viewModel.InitializeAsync();
        var field = typeof(MainViewModel).GetField("catalogRefreshTask", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)field.GetValue(viewModel)!;
    }

    private static void SetField(MainViewModel viewModel, string name, object value) =>
        typeof(MainViewModel).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(viewModel, value);

    private static Task SaveSettingsAsync(string appData, string versions) =>
        CrystalflySettingsStore.SaveAsync(Path.Combine(appData, "settings.json"), new CrystalflySettings
        {
            VersionRoot = versions, OfflineMode = true, OnboardingCompleted = true
        });

    private string CreateDirectory(string name) => Directory.CreateDirectory(Path.Combine(root, name)).FullName;

    private static string CreateGame(string versions, string name)
    {
        var game = Path.Combine(versions, name);
        Directory.CreateDirectory(Path.Combine(game, "hollow_knight_Data"));
        File.WriteAllText(Path.Combine(game, "hollow_knight.exe"), "exe");
        File.WriteAllText(Path.Combine(game, "hollow_knight_Data", "globalgamemanagers"), "data");
        return game;
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

using Crystalfly.App.ViewModels;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Models;
using Crystalfly.Steam.Security;
using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class MainViewModelStateTests : IDisposable
{
    private readonly TestDirectory applicationData = new();

    [Fact]
    public async Task Injected_application_data_root_keeps_real_settings_unchanged()
    {
        using var test = new TestDirectory();
        var applicationDataRoot = test.CreateDirectory("app-data");
        var realSettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Crystalfly",
            "settings.json");
        var before = ReadFileHash(realSettingsPath);
        var viewModel = new MainViewModel(applicationDataRoot);

        viewModel.SelectedLanguage = new(
            Crystalfly.Core.Configuration.UiLanguage.English,
            "English");
        await viewModel.DisposeAsync();

        Assert.Equal(before, ReadFileHash(realSettingsPath));
    }

    [Fact]
    public async Task Dispose_persists_latest_language_theme_and_instance()
    {
        using var test = new TestDirectory();
        var applicationDataRoot = test.CreateDirectory("app-data");
        var versionRoot = test.CreateDirectory("versions");
        var instanceRoot = test.CreateDirectory("versions", "practice");
        var viewModel = new MainViewModel(applicationDataRoot)
        {
            VersionRoot = versionRoot
        };

        viewModel.SelectedLanguage = new(UiLanguage.English, "English");
        viewModel.SelectedTheme = new(UiTheme.Dark, "Dark");
        viewModel.SelectedInstance = new(
            Instance("practice", instanceRoot),
            "1.5.78.11833",
            "Vanilla",
            0);
        await viewModel.DisposeAsync();

        var saved = await CrystalflySettingsStore.LoadAsync(
            Path.Combine(applicationDataRoot, "settings.json"));
        Assert.Equal(UiLanguage.English, saved.Language);
        Assert.Equal(UiTheme.Dark, saved.Theme);
        Assert.Equal("practice", saved.CurrentInstanceId);
    }

    [Fact]
    public async Task Dispose_is_idempotent_and_waits_for_running_commands_before_releasing_steam()
    {
        var launchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLaunch = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var downloadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var downloadCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDownloadCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var events = new ConcurrentQueue<string>();
        var viewModel = new MainViewModel(
            applicationData.CreateDirectory("app-data"),
            async () =>
            {
                launchStarted.SetResult();
                await releaseLaunch.Task;
                events.Enqueue("launch-complete");
            },
            async cancellationToken =>
            {
                downloadStarted.SetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                finally
                {
                    downloadCancelled.SetResult();
                    await releaseDownloadCleanup.Task;
                    events.Enqueue("download-complete");
                }
            },
            () =>
            {
                events.Enqueue("steam-disposed");
                return Task.CompletedTask;
            });

        var launch = viewModel.LaunchGameCommand.ExecuteAsync(null);
        var download = viewModel.DownloadBuildCommand.ExecuteAsync(null);
        await Task.WhenAll(launchStarted.Task, downloadStarted.Task);

        var firstDispose = viewModel.DisposeAsync().AsTask();
        var secondDispose = viewModel.DisposeAsync().AsTask();

        Assert.Same(firstDispose, secondDispose);
        await downloadCancelled.Task;
        Assert.False(firstDispose.IsCompleted);

        releaseLaunch.SetResult();
        await launch;
        Assert.False(firstDispose.IsCompleted);

        releaseDownloadCleanup.SetResult();
        await Task.WhenAll(download, firstDispose);

        Assert.Equal(
            ["launch-complete", "download-complete", "steam-disposed"],
            events.ToArray());
    }

    [Fact]
    public async Task Steam_sign_in_failure_is_reported_without_faulting_the_command()
    {
        var viewModel = new MainViewModel(
            applicationData.CreateDirectory("app-data"),
            launchOverride: null,
            downloadOverride: null,
            disposeSteamOverride: null,
            qrSignInOverride: _ => Task.FromException<RefreshTokenCredential>(new Exception("poll failed")));

        await viewModel.SignInWithQrCommand.ExecuteAsync(null);

        Assert.Equal("Steam: poll failed", viewModel.ErrorMessage);
        Assert.Equal("Not signed in", viewModel.SteamStatus);
        Assert.False(viewModel.IsSteamLoggedIn);
    }

    [Fact]
    public async Task Dispose_cancels_and_waits_for_running_Steam_sign_in()
    {
        var signInStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var signInCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSignInCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var viewModel = new MainViewModel(
            applicationData.CreateDirectory("app-data"),
            launchOverride: null,
            downloadOverride: null,
            disposeSteamOverride: null,
            qrSignInOverride: async cancellationToken =>
            {
                signInStarted.SetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    return new RefreshTokenCredential("unused", "unused");
                }
                finally
                {
                    signInCancelled.SetResult();
                    await releaseSignInCleanup.Task;
                }
            });

        var signIn = viewModel.SignInWithQrCommand.ExecuteAsync(null);
        await signInStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var dispose = viewModel.DisposeAsync().AsTask();

        await signInCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(dispose.IsCompleted);
        releaseSignInCleanup.SetResult();
        await Task.WhenAll(signIn, dispose).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("Not signed in", viewModel.SteamStatus);
    }

    [Fact]
    public async Task Steam_sign_in_does_not_start_after_disposal()
    {
        var signInCalled = false;
        var viewModel = new MainViewModel(
            applicationData.CreateDirectory("app-data"),
            launchOverride: null,
            downloadOverride: null,
            disposeSteamOverride: null,
            qrSignInOverride: _ =>
            {
                signInCalled = true;
                return Task.FromResult(new RefreshTokenCredential("unused", "unused"));
            });

        await viewModel.DisposeAsync();
        await viewModel.SignInWithQrCommand.ExecuteAsync(null);

        Assert.False(signInCalled);
    }

    [Fact]
    public async Task Steam_download_failure_is_reported_without_faulting_the_command()
    {
        var viewModel = new MainViewModel(
            applicationData.CreateDirectory("app-data"),
            launchOverride: null,
            downloadOverride: _ => Task.FromException(new HttpRequestException("CDN unavailable")),
            disposeSteamOverride: null);

        await viewModel.DownloadBuildCommand.ExecuteAsync(null);

        Assert.Equal("Steam: CDN unavailable", viewModel.ErrorMessage);
        Assert.Equal("Failed", viewModel.DownloadStatus);
    }

    [Fact]
    public void Clone_name_rejects_whitespace()
    {
        var viewModel = CreateViewModel();
        viewModel.CloneInstanceName = "   ";

        Assert.False(viewModel.CanCloneInstance);

        viewModel.CloneInstanceName = "Practice";

        Assert.True(viewModel.CanCloneInstance);
    }

    [Theory]
    [InlineData(@"..\..\outside")]
    [InlineData(@"C:\outside")]
    [InlineData(@"\\server\share\outside")]
    [InlineData("nested/child")]
    [InlineData(@"nested\child")]
    public void Download_instance_name_rejects_invalid_catalog_display_version(string displayVersion)
    {
        using var test = new TestDirectory();
        var viewModel = CreateViewModel();
        viewModel.VersionRoot = test.CreateDirectory("versions");

        var exception = Assert.Throws<TargetInvocationException>(() =>
            InvokeUniqueInstanceName(viewModel, displayVersion));

        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    [Fact]
    public void Download_instance_name_reserves_space_for_conflict_suffix()
    {
        using var test = new TestDirectory();
        var versionRoot = test.CreateDirectory("versions");
        var viewModel = CreateViewModel();
        viewModel.VersionRoot = versionRoot;
        var displayVersion = new string('x', 255 - "Hollow Knight ".Length);
        File.WriteAllText(Path.Combine(versionRoot, $"Hollow Knight {displayVersion}"), "occupied");

        var name = InvokeUniqueInstanceName(viewModel, displayVersion);

        Assert.EndsWith(" (2)", name, StringComparison.Ordinal);
        Assert.True(name.Length <= 255);
    }

    [Fact]
    public void Download_instance_name_skips_an_existing_file()
    {
        using var test = new TestDirectory();
        var versionRoot = test.CreateDirectory("versions");
        File.WriteAllText(Path.Combine(versionRoot, "Hollow Knight 1.5"), "occupied");
        var viewModel = CreateViewModel();
        viewModel.VersionRoot = versionRoot;

        var name = InvokeUniqueInstanceName(viewModel, "1.5");

        Assert.Equal("Hollow Knight 1.5 (2)", name);
    }

    [Fact]
    public void Busy_or_running_state_locks_navigation()
    {
        var viewModel = CreateViewModel();
        Assert.True(viewModel.CanNavigate);

        viewModel.IsBusy = true;
        Assert.False(viewModel.CanNavigate);

        viewModel.IsBusy = false;
        viewModel.IsGameRunning = true;
        Assert.False(viewModel.CanNavigate);
    }

    [Fact]
    public void Mod_search_and_status_filter_both_lists()
    {
        var viewModel = CreateViewModel();
        var catalog = Manifest("debugmod", "2.0.0");
        viewModel.AvailableMods.Add(catalog);
        viewModel.AvailableMods.Add(Manifest("benchwarp", "1.0.0"));
        viewModel.InstalledMods.Add(new InstalledModItemViewModel(
            Receipt("debugmod", "1.0.0", enabled: true),
            catalog,
            static () => { }));
        viewModel.InstalledMods.Add(new InstalledModItemViewModel(
            Receipt("local-helper", "1.0.0", enabled: false, isLocal: true),
            null,
            static () => { }));

        viewModel.ModSearchText = "debug";

        Assert.Single(viewModel.VisibleAvailableMods);
        Assert.Single(viewModel.VisibleInstalledMods);

        viewModel.ModSearchText = string.Empty;
        viewModel.SelectedModStatus = ModStatusFilter.Local;

        Assert.Single(viewModel.VisibleInstalledMods);
        Assert.Equal("local-helper", viewModel.VisibleInstalledMods[0].Id);
    }

    [Fact]
    public async Task Language_change_refreshes_localized_bindings_and_selected_option()
    {
        var viewModel = CreateViewModel();
        InvokeRebuildSettingOptions(viewModel);
        var english = viewModel.LanguageOptions.Single(option =>
            option.Value == Crystalfly.Core.Configuration.UiLanguage.English);
        var previousLocalization = viewModel.Loc;
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.SelectedLanguage = english;

        Assert.Contains(nameof(MainViewModel.Loc), changedProperties);
        Assert.NotSame(previousLocalization, viewModel.Loc);
        Assert.Same(
            viewModel.LanguageOptions.Single(option => option.Value == english.Value),
            viewModel.SelectedLanguage);
        await viewModel.DisposeAsync();
    }

    [Fact]
    public async Task Instance_details_reports_access_failures_from_background_load()
    {
        using var test = new TestDirectory();
        var instanceRoot = test.CreateDirectory("instance");
        var versionRoot = test.CreateDirectory("versions");
        var record = Instance("practice", instanceRoot);
        _ = test.CreateDirectory(
            "versions",
            ".crystalfly",
            "instances",
            record.Id,
            "snapshots",
            "snapshot-1",
            "snapshot.json");
        var viewModel = CreateViewModel();
        viewModel.VersionRoot = versionRoot;

        await InvokeLoadInstanceDetailsAsync(viewModel, record);

        Assert.StartsWith(viewModel.Loc["OperationFailed"], viewModel.ErrorMessage);
    }

    [Theory]
    [InlineData(typeof(UnauthorizedAccessException), true)]
    [InlineData(typeof(InvalidOperationException), true)]
    [InlineData(typeof(NullReferenceException), false)]
    public void Instance_details_exception_boundary_handles_only_expected_failures(
        Type exceptionType,
        bool expected)
    {
        var method = typeof(MainViewModel).GetMethod(
            "IsExpectedInstanceDetailsException",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;
        Assert.Equal(expected, method.Invoke(null, [exception]));
    }

    [Theory]
    [InlineData(typeof(IOException), true)]
    [InlineData(typeof(UnauthorizedAccessException), true)]
    [InlineData(typeof(InvalidOperationException), false)]
    public void Settings_exception_boundary_handles_only_file_failures(
        Type exceptionType,
        bool expected)
    {
        var method = typeof(MainViewModel).GetMethod(
            "IsExpectedSettingsException",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;
        Assert.Equal(expected, method.Invoke(null, [exception]));
    }

    [Fact]
    public async Task Instance_details_does_not_mask_programming_errors()
    {
        using var test = new TestDirectory();
        var viewModel = CreateViewModel();
        viewModel.VersionRoot = test.CreateDirectory("versions");
        var record = Instance("..", test.CreateDirectory("instance"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            InvokeLoadInstanceDetailsAsync(viewModel, record));
    }

    private static InstalledModReceipt Receipt(
        string id,
        string version,
        bool enabled,
        bool isLocal = false) => new()
    {
        Id = id,
        Name = id,
        Version = version,
        LoaderId = "modding-api",
        InstallRoot = $"Mods/{id}",
        Enabled = enabled,
        IsLocal = isLocal
    };

    private static ModManifest Manifest(string id, string version) => new()
    {
        Id = id,
        Name = id,
        Version = version,
        DownloadUrl = $"https://example.invalid/{id}.zip",
        Sha256 = new string('A', 64),
        LoaderId = "modding-api"
    };

    private static InstanceRecord Instance(string id, string rootPath) => new()
    {
        Id = id,
        Name = id,
        RootPath = rootPath,
        BuildId = "build-1",
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static Task InvokeLoadInstanceDetailsAsync(
        MainViewModel viewModel,
        InstanceRecord record)
    {
        var method = typeof(MainViewModel).GetMethod(
            "LoadInstanceDetailsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsAssignableFrom<Task>(method.Invoke(viewModel, [record, 0L]));
    }

    private static void InvokeRebuildSettingOptions(MainViewModel viewModel)
    {
        var method = typeof(MainViewModel).GetMethod(
            "RebuildSettingOptions",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(viewModel, null);
    }

    private static string InvokeUniqueInstanceName(MainViewModel viewModel, string version)
    {
        var method = typeof(MainViewModel).GetMethod(
            "UniqueInstanceName",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<string>(method.Invoke(viewModel, [version]));
    }

    private MainViewModel CreateViewModel() => new(applicationData.CreateDirectory("app-data"));

    public void Dispose() => applicationData.Dispose();

    private static string? ReadFileHash(string path) => File.Exists(path)
        ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))
        : null;

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

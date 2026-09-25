using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Crystalfly.Core.LocalLow;
using Crystalfly.App.ViewModels;
using Crystalfly.Core.Models;
using Crystalfly.Core.Serialization;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class LaunchReliabilityTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"crystalfly-launch-{Guid.NewGuid():N}");

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Failed_launch_recheck_does_not_reuse_a_previous_success(bool force, bool corruptJson)
    {
        var record = CreateInstance();
        var launched = 0;
        await using var viewModel = CreateViewModel(record, () => { launched++; return Task.CompletedTask; });
        await viewModel.LaunchGameCommand.ExecuteAsync(null);
        Assert.Equal(1, launched);
        Assert.True(viewModel.LaunchPreflight.CanLaunchNormally);
        launched = 0;
        var receiptPath = Path.Combine(GetStateRoot(record), "loader.json");
        await File.WriteAllTextAsync(receiptPath, corruptJson ? "{broken" : "{}");
        using var lockedReceipt = corruptJson ? null : new FileStream(receiptPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        await (force ? viewModel.ForceLaunchGameCommand : viewModel.LaunchGameCommand).ExecuteAsync(null);

        Assert.Equal(0, launched);
        Assert.False(viewModel.LaunchPreflight.CanAttemptLaunch);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disconnected_disk_cannot_reuse_a_previous_launch_check(bool force)
    {
        var record = CreateInstance();
        var launched = 0;
        await using var viewModel = CreateViewModel(record, () => { launched++; return Task.CompletedTask; });
        await viewModel.LaunchGameCommand.ExecuteAsync(null);
        Assert.Equal(1, launched);
        launched = 0;
        Directory.Move(viewModel.VersionRoot, viewModel.VersionRoot + "-disconnected");

        await (force ? viewModel.ForceLaunchGameCommand : viewModel.LaunchGameCommand).ExecuteAsync(null);

        Assert.Equal(0, launched);
        Assert.False(viewModel.LaunchPreflight.CanAttemptLaunch);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(193)]
    [InlineData(740)]
    [InlineData(1223)]
    public async Task Windows_start_failure_restores_saves_and_allows_retry(int nativeError)
    {
        var record = CreateInstance();
        var shared = Path.Combine(root, "shared-saves");
        Directory.CreateDirectory(shared);
        await File.WriteAllTextAsync(Path.Combine(shared, "user1.dat"), "original-save");
        var isolatedSave = Path.Combine(GetStateRoot(record), "local-low", "user1.dat");
        await File.WriteAllTextAsync(isolatedSave, "instance-save");
        var attempts = 0;
        await using var viewModel = CreateViewModel(record, null, info =>
        {
            attempts++;
            Assert.Equal(record.RootPath, info.WorkingDirectory);
            Assert.Equal("instance-save", File.ReadAllText(Path.Combine(shared, "user1.dat")));
            throw new Win32Exception(nativeError, "Simulated native start failure");
        });

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            await viewModel.LaunchGameCommand.ExecuteAsync(null);

            Assert.Equal(attempt, attempts);
            Assert.False(viewModel.IsGameRunning);
            Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
            Assert.Equal("original-save", await File.ReadAllTextAsync(Path.Combine(shared, "user1.dat")));
            Assert.Equal("instance-save", await File.ReadAllTextAsync(isolatedSave));
            var isolation = new LocalLowIsolationService(shared, Path.Combine(viewModel.VersionRoot, ".crystalfly"));
            Assert.Empty(await isolation.RecoverPendingAsync());
        }
    }

    [Theory]
    [InlineData("build-b", "modding-api-77", true, false)]
    [InlineData("build-a", "modding-api-78", true, false)]
    [InlineData("build-a", "modding-api-78", false, true)]
    [InlineData("build-a", "modding-api-77", true, true)]
    public async Task Launch_checks_the_actual_loader_build_and_enabled_mod_combination(
        string buildId, string modLoaderId, bool modEnabled, bool allowed)
    {
        var record = CreateInstance() with { BuildId = buildId };
        await WriteLoaderAsync(record, verified: true);
        var installRoot = modEnabled
            ? "hollow_knight_Data/Managed/Mods/Example"
            : "hollow_knight_Data/Managed/Mods/Disabled/Example";
        var entry = installRoot + "/Example.dll";
        Directory.CreateDirectory(Path.Combine(record.RootPath, installRoot));
        await File.WriteAllTextAsync(Path.Combine(record.RootPath, entry), "mod");
        await AtomicJsonStore.WriteAsync(Path.Combine(GetStateRoot(record), "mods", "example.json"),
            new InstalledModReceipt
            {
                Id = "example", Name = "Example", Version = "1", LoaderId = modLoaderId,
                InstallRoot = installRoot, Enabled = modEnabled, EntryFiles = [entry],
                Files = [new InstalledFileReceipt { RelativePath = entry, Sha256 = Hash("mod") }]
            });
        var launched = 0;
        await using var viewModel = CreateViewModel(record, () => { launched++; return Task.CompletedTask; });
        SetCatalog(viewModel);

        await viewModel.LaunchGameCommand.ExecuteAsync(null);

        Assert.Equal(allowed ? 1 : 0, launched);
        Assert.Equal(allowed, viewModel.LaunchPreflight.CanLaunchNormally);
        if (!allowed)
        {
            Assert.NotEmpty(viewModel.LaunchPreflight.Issues);
            Assert.True(viewModel.LaunchPreflight.CanForceLaunch);
        }
    }

    [Fact]
    public async Task Taken_over_loader_requires_acknowledgement_of_unverified_compatibility()
    {
        var record = CreateInstance() with { BuildId = "build-a" };
        await WriteLoaderAsync(record, verified: false);
        var launched = 0;
        await using var viewModel = CreateViewModel(record, () => { launched++; return Task.CompletedTask; });
        SetCatalog(viewModel);

        await viewModel.LaunchGameCommand.ExecuteAsync(null);

        Assert.Equal(0, launched);
        Assert.False(viewModel.LaunchPreflight.CanLaunchNormally);
        Assert.NotEmpty(viewModel.LaunchPreflight.Issues);
        await viewModel.AcknowledgeLaunchWarningsCommand.ExecuteAsync(null);
        await viewModel.LaunchGameCommand.ExecuteAsync(null);
        Assert.Equal(1, launched);
        Assert.True(viewModel.LaunchPreflight.CanLaunchNormally);
    }

    [Fact]
    public async Task Launch_rechecks_game_fingerprint_after_an_external_update()
    {
        var record = CreateInstance() with { BuildId = "build-a" };
        await WriteLoaderAsync(record, verified: true);
        var launched = 0;
        await using var viewModel = CreateViewModel(record, () => { launched++; return Task.CompletedTask; });
        SetCatalog(viewModel, withBuilds: true);
        await viewModel.LaunchGameCommand.ExecuteAsync(null);
        Assert.Equal(1, launched);
        await File.WriteAllTextAsync(Path.Combine(record.RootPath, "hollow_knight.exe"), "updated-game");

        await viewModel.LaunchGameCommand.ExecuteAsync(null);

        Assert.Equal(1, launched);
        Assert.False(viewModel.LaunchPreflight.LoaderReady);
        Assert.Contains(viewModel.LaunchPreflight.Issues, issue => issue.Arguments.Contains("build-b"));
    }

    [Theory]
    [InlineData("hollow_knight_Data/globalgamemanagers")]
    [InlineData("UnityPlayer.dll")]
    public async Task Launch_rechecks_required_game_data_files(string missingFile)
    {
        var record = CreateInstance() with { BuildId = "build-a" };
        await File.WriteAllTextAsync(Path.Combine(record.RootPath, "UnityPlayer.dll"), "unity");
        var launched = 0;
        await using var viewModel = CreateViewModel(record, () => { launched++; return Task.CompletedTask; });
        SetCatalog(viewModel, withBuilds: true, requireUnityPlayer: true);
        await viewModel.LaunchGameCommand.ExecuteAsync(null);
        Assert.Equal(1, launched);
        File.Delete(Path.Combine(record.RootPath, missingFile));

        await viewModel.LaunchGameCommand.ExecuteAsync(null);

        Assert.Equal(1, launched);
        Assert.False(viewModel.LaunchPreflight.GameFilesReady);
        Assert.Contains(viewModel.LaunchPreflight.Issues, issue => issue.RelativeFilePath == missingFile);
    }

    private static async Task WriteLoaderAsync(InstanceRecord record, bool verified)
    {
        const string hook = "hollow_knight_Data/Managed/MMHOOK_Assembly-CSharp.dll";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(record.RootPath, hook))!);
        await File.WriteAllTextAsync(Path.Combine(record.RootPath, hook), "hook");
        await AtomicJsonStore.WriteAsync(Path.Combine(GetStateRoot(record), "loader.json"), new InstalledPackageReceipt
        {
            PackageId = "modding-api-77", LoaderState = LoaderState.ModdingApi, IsVerified = verified,
            Files = [new InstalledFileReceipt { RelativePath = hook, Sha256 = Hash("hook") }]
        });
    }

    private static void SetCatalog(MainViewModel viewModel, bool withBuilds = false, bool requireUnityPlayer = false) =>
        typeof(MainViewModel).GetField("catalog", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(viewModel,
            new GameCatalog
            {
                Builds = withBuilds ? [
                    new GameBuild
                    {
                        Id = "build-a", DisplayVersion = "A", ManifestId = "1",
                        ExecutableSha256 = Hash("game"), GlobalGameManagersSha256 = Hash("data"),
                        UnityPlayerSha256 = requireUnityPlayer ? Hash("unity") : null
                    },
                    new GameBuild
                    {
                        Id = "build-b", DisplayVersion = "B", ManifestId = "2",
                        ExecutableSha256 = Hash("updated-game"), GlobalGameManagersSha256 = Hash("data")
                    }] : [],
                Loaders = [new LoaderManifest
                {
                    Id = "modding-api-77", Name = "API 77", Version = "77",
                    DownloadUrl = "https://example.invalid/loader.zip", Sha256 = new string('A', 64),
                    SupportedBuildIds = ["build-a"]
                }]
            });

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private InstanceRecord CreateInstance()
    {
        var game = Path.Combine(root, "游戏 版本", "空洞骑士 测试");
        Directory.CreateDirectory(Path.Combine(game, "hollow_knight_Data"));
        File.WriteAllText(Path.Combine(game, "hollow_knight.exe"), "game");
        File.WriteAllText(Path.Combine(game, "hollow_knight_Data", "globalgamemanagers"), "data");
        var record = new InstanceRecord
        {
            Id = "test-instance", Name = "测试实例", RootPath = game, BuildId = "unknown", CreatedAt = DateTimeOffset.UtcNow
        };
        Directory.CreateDirectory(Path.Combine(GetStateRoot(record), "local-low"));
        return record;
    }

    private MainViewModel CreateViewModel(
        InstanceRecord record,
        Func<Task>? launch,
        Func<ProcessStartInfo, Process?>? processStarter = null)
    {
        var viewModel = new MainViewModel(
            Path.Combine(root, "app-data"),
            launchOverride: launch,
            gameProcessRunningOverride: () => false,
            sharedLocalLowPathOverride: Path.Combine(root, "shared-saves"),
            startGameProcessOverride: processStarter)
        {
            VersionRoot = Path.GetDirectoryName(record.RootPath)!
        };
        typeof(MainViewModel).GetField("<SelectedInstance>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(viewModel, new InstanceItemViewModel(record, record.BuildId, "Vanilla", 0));
        return viewModel;
    }

    private static string GetStateRoot(InstanceRecord record) =>
        Path.Combine(Path.GetDirectoryName(record.RootPath)!, ".crystalfly", "instances", record.Id);

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

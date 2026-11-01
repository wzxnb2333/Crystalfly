using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Crystalfly.Core.LocalLow;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Instances;
using Crystalfly.App.ViewModels;
using Crystalfly.Core.Models;
using Crystalfly.Core.Runtime;
using Crystalfly.Core.Serialization;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class LaunchReliabilityTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"crystalfly-launch-{Guid.NewGuid():N}");

    [Fact]
    public async Task Running_game_opens_its_bound_lss_without_waiting_for_game_exit()
    {
        var lss = Path.Combine(root, "splits.lss");
        var record = CreateInstance() with { Purpose = InstancePurpose.OfficialSpeedrun, SpeedrunLiveSplitPath = lss };
        await File.WriteAllTextAsync(lss, "<Run />");
        Process? child = null;
        var opened = new List<string>();
        await using var model = CreateViewModel(record, null, _ =>
        {
            var info = new ProcessStartInfo("pwsh.exe") { UseShellExecute = false, CreateNoWindow = true };
            info.ArgumentList.Add("-NoProfile"); info.ArgumentList.Add("-NonInteractive");
            info.ArgumentList.Add("-Command"); info.ArgumentList.Add("Start-Sleep -Seconds 30");
            return child = Process.Start(info)!;
        }, openLiveSplit: opened.Add);
        var run = model.LaunchGameCommand.ExecuteAsync(null);
        try
        {
            await WaitUntilAsync(() => opened.Count == 1);
            Assert.Equal(lss, opened[0]);
            Assert.True(model.IsGameRunning);
            Assert.False(run.IsCompleted);
            await model.LaunchGameCommand.ExecuteAsync(null);
            Assert.Single(opened);
        }
        finally
        {
            if (child is not null && !child.HasExited) child.Kill();
            await run.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Bound_lss_opens_only_after_a_successful_game_start(bool gameFails, bool force)
    {
        var lss = Path.Combine(root, "练习 分段.lss");
        var record = CreateInstance() with { Purpose = InstancePurpose.OfficialSpeedrun, SpeedrunLiveSplitPath = lss };
        await File.WriteAllTextAsync(lss, "<Run />");
        var actions = new List<string>();
        await using var model = CreateViewModel(record, () =>
        {
            actions.Add("game");
            if (gameFails) throw new Win32Exception(2);
            return Task.CompletedTask;
        }, openLiveSplit: path => actions.Add(path));
        await (force ? model.ForceLaunchGameCommand : model.LaunchGameCommand).ExecuteAsync(null);
        Assert.Equal(gameFails ? new[] { "game" } : new[] { "game", lss }, actions);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Missing_lss_or_missing_file_association_does_not_turn_game_start_into_failure(bool missing)
    {
        var lss = Path.Combine(root, "splits.lss");
        var record = CreateInstance() with { Purpose = InstancePurpose.OfficialSpeedrun, SpeedrunLiveSplitPath = lss };
        if (!missing) await File.WriteAllTextAsync(lss, "<Run />");
        var started = false;
        var openAttempts = 0;
        await using var model = CreateViewModel(record, () => { started = true; return Task.CompletedTask; },
            openLiveSplit: _ => { openAttempts++; throw new Win32Exception(1155); });
        await model.LaunchGameCommand.ExecuteAsync(null);
        Assert.True(started);
        Assert.Equal(missing ? 0 : 1, openAttempts);
        Assert.Contains("游戏已启动", model.ErrorMessage);
        Assert.False(model.IsBusy);
    }

    [Fact]
    public async Task LiveSplit_binding_saves_changes_rejects_other_extensions_and_unbinds()
    {
        var record = CreateInstance() with { Purpose = InstancePurpose.OfficialSpeedrun };
        await InstanceSidecar.SaveAsync(record);
        var first = Path.Combine(root, "first.lss");
        var second = Path.Combine(root, "second.lss");
        await File.WriteAllTextAsync(first, "<Run />");
        await File.WriteAllTextAsync(second, "<Run />");
        await using var model = CreateViewModel(record, () => Task.CompletedTask);
        await model.BindSpeedrunLiveSplitAsync(record, first);
        Assert.Null(model.ErrorMessage);
        Assert.Equal(first, (await InstanceSidecar.LoadAsync(record.RootPath))!.SpeedrunLiveSplitPath);
        await model.BindSpeedrunLiveSplitAsync(record, second);
        Assert.Equal(second, (await InstanceSidecar.LoadAsync(record.RootPath))!.SpeedrunLiveSplitPath);
        await model.BindSpeedrunLiveSplitAsync(record, Path.Combine(record.RootPath, "hollow_knight.exe"));
        Assert.NotNull(model.ErrorMessage);
        Assert.Equal(second, (await InstanceSidecar.LoadAsync(record.RootPath))!.SpeedrunLiveSplitPath);
        await model.BindSpeedrunLiveSplitAsync(record, null);
        Assert.Null(model.ErrorMessage);
        Assert.Null((await InstanceSidecar.LoadAsync(record.RootPath))!.SpeedrunLiveSplitPath);
    }

    [Fact]
    public async Task Different_instances_can_run_together_and_exiting_one_does_not_unlock_the_other()
    {
        var first = CreateInstance();
        var second = first with { Id = "second", Name = "Second", RootPath = Path.Combine(Path.GetDirectoryName(first.RootPath)!, "Second") };
        Directory.CreateDirectory(Path.Combine(second.RootPath, "hollow_knight_Data"));
        File.Copy(Path.Combine(first.RootPath, "hollow_knight.exe"), Path.Combine(second.RootPath, "hollow_knight.exe"));
        File.Copy(Path.Combine(first.RootPath, "hollow_knight_Data", "globalgamemanagers"), Path.Combine(second.RootPath, "hollow_knight_Data", "globalgamemanagers"));
        Directory.CreateDirectory(Path.Combine(GetStateRoot(second), "local-low"));
        var children = new List<Process>();
        await using var model = CreateViewModel(first, null, _ =>
        {
            var info = new ProcessStartInfo("pwsh.exe") { UseShellExecute = false, CreateNoWindow = true };
            info.ArgumentList.Add("-NoProfile"); info.ArgumentList.Add("-NonInteractive");
            info.ArgumentList.Add("-Command"); info.ArgumentList.Add("Start-Sleep -Seconds 60");
            var process = Process.Start(info)!;
            children.Add(process);
            return process;
        });
        Task? firstRun = null, secondRun = null;
        try
        {
            firstRun = model.LaunchGameCommand.ExecuteAsync(null);
            await WaitUntilAsync(() => model.IsGameRunning && !model.IsBusy);
            Assert.True(model.CanNavigate);
            Assert.False(model.CanAttemptLaunch);
            await model.LaunchGameCommand.ExecuteAsync(null);
            Assert.Single(children);
            model.SelectPageCommand.Execute("Speedrun");
            Assert.Equal("Speedrun", model.CurrentPage);
            typeof(MainViewModel).GetField("<SelectedInstance>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(model, new InstanceItemViewModel(second, second.BuildId, "Vanilla", 0));
            secondRun = model.LaunchGameCommand.ExecuteAsync(null);
            await WaitUntilAsync(() => children.Count == 2 && !model.IsBusy);
            Assert.True(model.IsSelectedInstanceRunning);
            children[0].Kill();
            await firstRun.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(model.IsGameRunning);
            Assert.True(model.IsSelectedInstanceRunning);
            Assert.False(model.CanAttemptLaunch);
            // Closing the launcher must not wait for or terminate the remaining game.
            var remainingId = children[1].Id;
            await model.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            await secondRun.WaitAsync(TimeSpan.FromSeconds(10));
            using var remaining = Process.GetProcessById(remainingId);
            Assert.False(remaining.HasExited);
            remaining.Kill();
            remaining.WaitForExit();
        }
        finally
        {
            foreach (var child in children)
            {
                try { if (!child.HasExited) child.Kill(); }
                catch (InvalidOperationException) { }
            }
            if (firstRun is not null) await firstRun;
            if (secondRun is not null) await secondRun;
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!condition()) await Task.Delay(20, timeout.Token);
    }

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
    public async Task Windows_start_failure_leaves_shared_and_instance_saves_untouched_and_allows_retry(int nativeError)
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
            Assert.Equal("original-save", File.ReadAllText(Path.Combine(shared, "user1.dat")));
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
    public async Task Taken_over_loader_launches_without_acknowledgement_of_unverified_compatibility()
    {
        var record = CreateInstance() with { BuildId = "build-a" };
        await WriteLoaderAsync(record, verified: false);
        var launched = 0;
        await using var viewModel = CreateViewModel(record, () => { launched++; return Task.CompletedTask; });
        SetCatalog(viewModel);

        await viewModel.LaunchGameCommand.ExecuteAsync(null);

        Assert.Equal(1, launched);
        Assert.True(viewModel.LaunchPreflight.CanLaunchNormally);
        Assert.False(viewModel.HasLaunchIssues);
        Assert.Empty(viewModel.CreateLaunchIssueItems());
    }

    [Fact]
    public async Task Silksong_unknown_build_keeps_declared_version_without_compatibility_prompt()
    {
        var record = await CreateSilksongInstanceAsync();
        var launched = 0;
        await using var viewModel = CreateViewModel(record, () => { launched++; return Task.CompletedTask; });
        await viewModel.LaunchGameCommand.ExecuteAsync(null);
        Assert.Empty(viewModel.CreateLaunchIssueItems());
        Assert.Equal(1, launched);
        Assert.True(viewModel.LaunchPreflight.CanLaunchNormally);

        var projected = (InstanceItemViewModel)typeof(MainViewModel)
            .GetMethod("ProjectInstanceItem", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(viewModel, [(record, LoaderState.BepInEx,
                new InstalledPackageReceipt { PackageId = "bepinex", IsVerified = false }, 0), new CrystalflySettings()])!;
        Assert.Equal("1.0.28324", projected.DisplayVersion);
        Assert.Equal("bepinex", projected.LoaderDisplay);

        Assert.False(viewModel.HasLaunchIssues);

        await File.AppendAllTextAsync(Path.Combine(record.RootPath, GameDefinition.Silksong.DataDirectoryName, "Managed", "Assembly-CSharp.dll"), "changed game code");
        await viewModel.LaunchGameCommand.ExecuteAsync(null);
        Assert.Equal(2, launched);
        Assert.False(viewModel.HasLaunchIssues);
        Assert.Empty(viewModel.LaunchPreflight.Issues);
    }

    [Fact]
    public async Task Failed_warning_persistence_keeps_mod_warning_unacknowledged()
    {
        var record = await CreateSilksongInstanceAsync();
        await using var viewModel = CreateViewModel(record, () => Task.CompletedTask);
        await viewModel.LaunchGameCommand.ExecuteAsync(null);
        viewModel.LaunchPreflight = new(true, true, true, true,
        [
            new LaunchPreflightIssue
            {
                Code = LaunchIssueCode.ModModifiedFile, Severity = LaunchIssueSeverity.Warning,
                SubjectModId = "example", CurrentFileSha256 = Hash("modified mod")
            }
        ]);
        var settingsPath = Path.Combine(root, "app-data", "settings.json");
        await CrystalflySettingsStore.SaveAsync(settingsPath, new CrystalflySettings());
        using var lockedSettings = new FileStream(settingsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        await viewModel.AcknowledgeLaunchWarningsCommand.ExecuteAsync(null);
        Assert.True(viewModel.HasLaunchIssues);
        Assert.False(Assert.Single(viewModel.LaunchPreflight.Issues).IsAcknowledged);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
    }

    private async Task<InstanceRecord> CreateSilksongInstanceAsync()
    {
        var game = Path.Combine(root, "versions", "silksong");
        var managed = Path.Combine(game, GameDefinition.Silksong.DataDirectoryName, "Managed");
        Directory.CreateDirectory(managed);
        File.Copy(typeof(LaunchReliabilityTests).Assembly.Location, Path.Combine(managed, "Assembly-CSharp.dll"));
        await File.WriteAllTextAsync(Path.Combine(game, GameDefinition.Silksong.ExecutableName), "game");
        await File.WriteAllTextAsync(Path.Combine(game, GameDefinition.Silksong.GlobalGameManagersPath), "data");
        var record = Assert.Single(await InstanceImportService.DiscoverAsync(Path.GetDirectoryName(game)!, new GameCatalog()));
        Directory.CreateDirectory(Path.Combine(GetStateRoot(record), "local-low"));
        Directory.CreateDirectory(Path.Combine(game, "BepInEx", "core"));
        await File.WriteAllTextAsync(Path.Combine(game, "BepInEx", "core", "BepInEx.dll"), "loader");
        var loaderReceiptPath = Path.Combine(Path.GetDirectoryName(InstanceSidecar.GetMetadataPath(game, record.Id))!, "loader.json");
        await AtomicJsonStore.WriteAsync(loaderReceiptPath, new InstalledPackageReceipt
        {
            GameId = GameId.Silksong, PackageId = "bepinex-silksong-5.4.23.4", LoaderState = LoaderState.BepInEx,
            IsVerified = false, Files = [new() { RelativePath = "BepInEx/core/BepInEx.dll", Sha256 = Hash("loader") }]
        });
        return record;
    }

    [Fact]
    public async Task Silksong_custom_speedrun_launch_opens_its_bound_LiveSplit_without_a_template()
    {
        var lss = Path.Combine(root, "silksong.lss");
        var record = await CreateSilksongInstanceAsync();
        await File.WriteAllTextAsync(lss, "<Run />");
        record = record with { Purpose = InstancePurpose.CustomSpeedrun, SpeedrunLiveSplitPath = lss };
        await InstanceSidecar.SaveAsync(record);
        var launched = false;
        string? opened = null;
        await using var vm = CreateViewModel(record, () => { launched = true; return Task.CompletedTask; },
            openLiveSplit: path => { Assert.True(launched); opened = path; });
        await vm.LaunchGameCommand.ExecuteAsync(null);
        Assert.True(launched);
        Assert.Equal(lss, opened);
        Assert.Null(vm.ErrorMessage);
        Assert.Null(vm.SpeedrunReportPath);
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
        Func<ProcessStartInfo, Process?>? processStarter = null,
        Action<string>? openLiveSplit = null)
    {
        var viewModel = new MainViewModel(
            Path.Combine(root, "app-data"),
            launchOverride: launch,
            gameProcessRunningOverride: () => false,
            sharedLocalLowPathOverride: Path.Combine(root, "shared-saves"),
            startGameProcessOverride: processStarter,
            openLiveSplitOverride: openLiveSplit)
        {
            VersionRoot = Path.GetDirectoryName(record.RootPath)!
        };
        if (record.GameId == GameId.Silksong)
            typeof(MainViewModel).GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(viewModel, new CrystalflySettings { SelectedGameId = GameId.Silksong, OfflineMode = true });
        typeof(MainViewModel).GetField("<SelectedInstance>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(viewModel, new InstanceItemViewModel(record, record.BuildId, "Vanilla", 0));
        return viewModel;
    }

    private static string GetStateRoot(InstanceRecord record) =>
        Path.Combine(Path.GetDirectoryName(record.RootPath)!, ".crystalfly",
            record.GameId == GameId.Silksong ? "games/silksong/instances" : "instances", record.Id);

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

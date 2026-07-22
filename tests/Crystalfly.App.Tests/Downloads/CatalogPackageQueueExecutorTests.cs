using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using Crystalfly.App.Downloads;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Loaders;
using Crystalfly.Core.Models;
using Crystalfly.Core.Mods;
using Crystalfly.Core.Packages;
using Crystalfly.Core.Serialization;

namespace Crystalfly.App.Tests.Downloads;

public sealed class CatalogPackageQueueExecutorTests : IDisposable
{
    private const string LoaderId = "modding-api-77";
    private const string BuildId = "1.5.78.11833";
    private readonly string root = Path.Combine(
        Path.GetTempPath(), "Crystalfly.Tests", Guid.NewGuid().ToString("N"));
    private readonly SemaphoreSlim networkGate = new(3, 3);

    [Fact]
    public async Task Installs_loader_recursive_dependency_and_requested_mod()
    {
        using var fixture = await CreateFixtureAsync();
        var progress = new List<PackageTransferProgress>();

        foreach (var item in fixture.Group.Items)
        {
            await fixture.Executor.TransferAsync(
                fixture.Group, item, new InlineProgress(progress.Add), networkGate, CancellationToken.None);
            await fixture.Executor.InstallAsync(fixture.Group, item, CancellationToken.None);
        }

        Assert.Equal(4, fixture.Handler.RequestCount);
        Assert.Contains(progress, value => value.CompletedBytes > 0 && value.TotalBytes > 0);
        Assert.True(File.Exists(Path.Combine(
            fixture.InstanceRoot, "hollow_knight_Data", "Managed", "MMHOOK_Assembly-CSharp.dll")));
        Assert.True(File.Exists(Path.Combine(
            fixture.InstanceRoot, "hollow_knight_Data", "Managed", "Mods", "Library", "library.dll")));
        Assert.True(File.Exists(Path.Combine(
            fixture.InstanceRoot, "hollow_knight_Data", "Managed", "Mods", "Bridge", "bridge.dll")));
        Assert.True(File.Exists(Path.Combine(
            fixture.InstanceRoot, "hollow_knight_Data", "Managed", "Mods", "Feature", "feature.dll")));

        var stateRoot = Path.Combine(fixture.VersionRoot, ".crystalfly", "instances", "practice");
        Assert.True(File.Exists(Path.Combine(stateRoot, "loader.json")));
        Assert.Equal(3, Directory.EnumerateFiles(Path.Combine(stateRoot, "mods"), "*.json").Count());
    }

    [Fact]
    public async Task Satisfied_loader_is_rechecked_as_loader_without_another_download()
    {
        using var fixture = await CreateFixtureAsync();
        var queuedLoader = fixture.Group.Items.Single(item => item.Kind == DownloadQueueItemKind.Loader);
        await fixture.Executor.TransferAsync(
            fixture.Group, queuedLoader, new InlineProgress(_ => { }), networkGate, CancellationToken.None);
        await fixture.Executor.InstallAsync(fixture.Group, queuedLoader, CancellationToken.None);
        var loader = queuedLoader with
        {
            IsSatisfied = true
        };
        var executor = new CatalogPackageQueueExecutor(
            () => fixture.Catalog with { Loaders = [] },
            fixture.Client,
            new InstanceOperationCoordinator());
        var requestCount = fixture.Handler.RequestCount;

        await executor.TransferAsync(
            fixture.Group, loader, new InlineProgress(_ => { }), networkGate, CancellationToken.None);
        await executor.InstallAsync(fixture.Group, loader, CancellationToken.None);

        Assert.Equal(requestCount, fixture.Handler.RequestCount);
        Assert.True(File.Exists(Path.Combine(
            fixture.InstanceRoot, "hollow_knight_Data", "Managed", "MMHOOK_Assembly-CSharp.dll")));
    }

    [Fact]
    public async Task Satisfied_loader_removed_after_enqueue_is_downloaded_again_before_install()
    {
        using var fixture = await CreateFixtureAsync();
        var queuedLoader = fixture.Group.Items.Single(item => item.Kind == DownloadQueueItemKind.Loader);
        await fixture.Executor.TransferAsync(
            fixture.Group, queuedLoader, new InlineProgress(_ => { }), networkGate, CancellationToken.None);
        await fixture.Executor.InstallAsync(fixture.Group, queuedLoader, CancellationToken.None);
        var loader = queuedLoader with
        {
            IsSatisfied = true
        };
        var stateRoot = Path.Combine(fixture.VersionRoot, ".crystalfly", "instances", "practice");
        var packageRoot = Path.Combine(fixture.VersionRoot, ".crystalfly", "packages");
        var loaderManager = new LoaderManager(
            fixture.InstanceRoot,
            Path.Combine(fixture.VersionRoot, ".crystalfly", "transactions"),
            Path.Combine(stateRoot, "loader.json"),
            packageRoot,
            fixture.Client);
        await loaderManager.UninstallAsync();
        File.Delete(Path.Combine(packageRoot, $"{fixture.Catalog.Loaders.Single().Sha256}.zip"));
        var requestCount = fixture.Handler.RequestCount;

        await fixture.Executor.TransferAsync(
            fixture.Group, loader, new InlineProgress(_ => { }), networkGate, CancellationToken.None);
        await fixture.Executor.InstallAsync(fixture.Group, loader, CancellationToken.None);

        Assert.Equal(requestCount + 1, fixture.Handler.RequestCount);
        Assert.True(File.Exists(Path.Combine(
            fixture.InstanceRoot, "hollow_knight_Data", "Managed", "MMHOOK_Assembly-CSharp.dll")));
    }

    [Fact]
    public async Task Requested_mod_reverifies_satisfied_prerequisite_files_before_writing()
    {
        using var fixture = await CreateFixtureAsync();
        foreach (var item in fixture.Group.Items)
        {
            await fixture.Executor.TransferAsync(
                fixture.Group, item, new InlineProgress(_ => { }), networkGate, CancellationToken.None);
        }
        foreach (var item in fixture.Group.Items.Take(3))
        {
            await fixture.Executor.InstallAsync(fixture.Group, item, CancellationToken.None);
        }
        await File.WriteAllTextAsync(Path.Combine(
            fixture.InstanceRoot,
            "hollow_knight_Data",
            "Managed",
            "Mods",
            "Library",
            "library.dll"), "drifted");
        var feature = fixture.Group.Items.Single(item => item.Kind == DownloadQueueItemKind.Mod);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Executor.InstallAsync(fixture.Group, feature, CancellationToken.None));

        Assert.Contains("drifted", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(
            fixture.InstanceRoot, "hollow_knight_Data", "Managed", "Mods", "Feature", "feature.dll")));
    }

    [Fact]
    public async Task Install_rechecks_entire_plan_and_does_not_write_when_state_changed()
    {
        using var fixture = await CreateFixtureAsync();
        foreach (var item in fixture.Group.Items)
        {
            await fixture.Executor.TransferAsync(
                fixture.Group, item, new InlineProgress(_ => { }), networkGate, CancellationToken.None);
        }
        foreach (var item in fixture.Group.Items.Take(3))
        {
            await fixture.Executor.InstallAsync(fixture.Group, item, CancellationToken.None);
        }
        var record = await InstanceSidecar.LoadAsync(fixture.InstanceRoot);
        await InstanceSidecar.SaveAsync(record with { Purpose = InstancePurpose.OfficialSpeedrun });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Executor.InstallAsync(fixture.Group, fixture.Group.Items[3], CancellationToken.None));

        Assert.Contains("cannot be modified", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(
            fixture.InstanceRoot, "hollow_knight_Data", "Managed", "Mods", "Feature", "feature.dll")));
        var receiptsRoot = Path.Combine(
            fixture.VersionRoot, ".crystalfly", "instances", "practice", "mods");
        Assert.Equal(2, Directory.EnumerateFiles(receiptsRoot, "*.json").Count());
    }

    [Fact]
    public async Task Transfer_reuses_verified_sha_cache()
    {
        using var fixture = await CreateFixtureAsync();
        var item = fixture.Group.Items.Single(candidate => candidate.Kind == DownloadQueueItemKind.Mod);
        var stages = new List<string>();

        await fixture.Executor.TransferAsync(
            fixture.Group, item, new InlineProgress(value => stages.Add(value.Stage)), networkGate, CancellationToken.None);
        await fixture.Executor.TransferAsync(
            fixture.Group, item, new InlineProgress(value => stages.Add(value.Stage)), networkGate, CancellationToken.None);

        Assert.Equal(1, fixture.Handler.RequestCount);
        Assert.Contains("Cached", stages);
        Assert.True(File.Exists(Path.Combine(
            fixture.VersionRoot, ".crystalfly", "packages", $"{fixture.Catalog.Mods.Single(mod => mod.Id == "feature").Sha256}.zip")));
    }

    [Fact]
    public async Task Satisfied_dependency_is_reverified_without_another_download()
    {
        using var fixture = await CreateFixtureAsync();
        foreach (var item in fixture.Group.Items)
        {
            await fixture.Executor.TransferAsync(
                fixture.Group, item, new InlineProgress(_ => { }), networkGate, CancellationToken.None);
            await fixture.Executor.InstallAsync(fixture.Group, item, CancellationToken.None);
        }
        var library = fixture.Group.Items.Single(item => item.PackageId == "library") with
        {
            IsSatisfied = true
        };
        var requestCount = fixture.Handler.RequestCount;
        await File.WriteAllTextAsync(Path.Combine(
            fixture.InstanceRoot,
            "hollow_knight_Data",
            "Managed",
            "Mods",
            "Library",
            "library.dll"), "drifted");

        await fixture.Executor.TransferAsync(
            fixture.Group, library, new InlineProgress(_ => { }), networkGate, CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Executor.InstallAsync(fixture.Group, library, CancellationToken.None));

        Assert.Equal(requestCount, fixture.Handler.RequestCount);
    }

    [Fact]
    public async Task Satisfied_mod_disabled_after_enqueue_is_downloaded_again_before_enable()
    {
        using var fixture = await CreateFixtureAsync();
        foreach (var item in fixture.Group.Items)
        {
            await fixture.Executor.TransferAsync(
                fixture.Group, item, new InlineProgress(_ => { }), networkGate, CancellationToken.None);
            await fixture.Executor.InstallAsync(fixture.Group, item, CancellationToken.None);
        }
        var feature = fixture.Group.Items.Single(item => item.PackageId == "feature") with
        {
            IsSatisfied = true
        };
        var featureManifest = fixture.Catalog.Mods.Single(mod => mod.Id == "feature");
        var stateRoot = Path.Combine(fixture.VersionRoot, ".crystalfly", "instances", "practice");
        var packageRoot = Path.Combine(fixture.VersionRoot, ".crystalfly", "packages");
        var modManager = new ModManager(
            fixture.InstanceRoot,
            Path.Combine(fixture.VersionRoot, ".crystalfly", "transactions"),
            Path.Combine(stateRoot, "mods"),
            packageRoot,
            fixture.Client);
        await modManager.SetEnabledAsync(feature.PackageId, enabled: false);
        File.Delete(Path.Combine(packageRoot, $"{featureManifest.Sha256}.zip"));
        var requestCount = fixture.Handler.RequestCount;

        await fixture.Executor.TransferAsync(
            fixture.Group, feature, new InlineProgress(_ => { }), networkGate, CancellationToken.None);
        await fixture.Executor.InstallAsync(fixture.Group, feature, CancellationToken.None);

        Assert.Equal(requestCount + 1, fixture.Handler.RequestCount);
        Assert.True((await modManager.GetInstalledAsync()).Single(mod => mod.Id == "feature").Enabled);
    }

    [Fact]
    public async Task Satisfied_dependency_removed_after_enqueue_is_downloaded_again_before_install()
    {
        using var fixture = await CreateFixtureAsync();
        foreach (var item in fixture.Group.Items)
        {
            await fixture.Executor.TransferAsync(
                fixture.Group, item, new InlineProgress(_ => { }), networkGate, CancellationToken.None);
            await fixture.Executor.InstallAsync(fixture.Group, item, CancellationToken.None);
        }
        var library = fixture.Group.Items.Single(item => item.PackageId == "library") with
        {
            IsSatisfied = true
        };
        var libraryManifest = fixture.Catalog.Mods.Single(mod => mod.Id == "library");
        var packageRoot = Path.Combine(fixture.VersionRoot, ".crystalfly", "packages");
        File.Delete(Path.Combine(packageRoot, $"{libraryManifest.Sha256}.zip"));
        File.Delete(Path.Combine(
            fixture.InstanceRoot,
            "hollow_knight_Data",
            "Managed",
            "Mods",
            "Library",
            "library.dll"));
        var receiptsRoot = Path.Combine(
            fixture.VersionRoot, ".crystalfly", "instances", "practice", "mods");
        foreach (var receiptPath in Directory.EnumerateFiles(receiptsRoot, "*.json"))
        {
            var receipt = await AtomicJsonStore.ReadAsync<InstalledModReceipt>(receiptPath);
            if (string.Equals(receipt.Id, "library", StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(receiptPath);
                File.Delete(receiptPath + ".bak");
                break;
            }
        }
        var requestCount = fixture.Handler.RequestCount;

        await fixture.Executor.TransferAsync(
            fixture.Group, library, new InlineProgress(_ => { }), networkGate, CancellationToken.None);
        await fixture.Executor.InstallAsync(fixture.Group, library, CancellationToken.None);

        Assert.Equal(requestCount + 1, fixture.Handler.RequestCount);
        Assert.True(File.Exists(Path.Combine(
            fixture.InstanceRoot,
            "hollow_knight_Data",
            "Managed",
            "Mods",
            "Library",
            "library.dll")));
    }

    [Fact]
    public async Task Install_rechecks_game_state_after_waiting_for_the_transaction_gate()
    {
        using var fixture = await CreateFixtureAsync();
        var coordinator = new InstanceOperationCoordinator();
        var gameRunning = false;
        var executor = new CatalogPackageQueueExecutor(
            () => fixture.Catalog,
            fixture.Client,
            coordinator,
            () => gameRunning,
            TimeSpan.FromMilliseconds(10));
        var loader = fixture.Group.Items.Single(item => item.Kind == DownloadQueueItemKind.Loader);
        var standalone = fixture.Group with
        {
            Kind = DownloadQueueGroupKind.LoaderInstall,
            Items = [loader]
        };
        await executor.TransferAsync(
            standalone, loader, new InlineProgress(_ => { }), networkGate, CancellationToken.None);
        var blockerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseBlocker = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocker = coordinator.RunAsync("other", async cancellationToken =>
        {
            blockerEntered.SetResult();
            await releaseBlocker.Task.WaitAsync(cancellationToken);
        });
        await blockerEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var install = executor.InstallAsync(standalone, loader, CancellationToken.None);
        gameRunning = true;
        releaseBlocker.SetResult();
        await blocker;
        await Task.Delay(50);
        Assert.False(install.IsCompleted);

        gameRunning = false;
        await install.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(File.Exists(Path.Combine(
            fixture.InstanceRoot, "hollow_knight_Data", "Managed", "MMHOOK_Assembly-CSharp.dll")));
    }

    [Fact]
    public async Task Dependency_repair_reenables_without_network_transfer()
    {
        using var fixture = await CreateFixtureAsync();
        var loader = fixture.Group.Items[0];
        var library = fixture.Group.Items[1];
        await fixture.Executor.TransferAsync(
            fixture.Group, loader, new InlineProgress(_ => { }), networkGate, CancellationToken.None);
        await fixture.Executor.InstallAsync(fixture.Group, loader, CancellationToken.None);
        await fixture.Executor.TransferAsync(
            fixture.Group, library, new InlineProgress(_ => { }), networkGate, CancellationToken.None);
        await fixture.Executor.InstallAsync(fixture.Group, library, CancellationToken.None);
        var manager = ModManager(fixture);
        await manager.SetEnabledAsync("library", enabled: false);
        var repair = RepairGroup(
            fixture,
            Item("library", DownloadQueueItemKind.DependencyReEnable, LoaderId));
        var requestCount = fixture.Handler.RequestCount;

        await fixture.Executor.TransferAsync(
            repair, repair.Items[0], new InlineProgress(_ => { }), networkGate, CancellationToken.None);
        await fixture.Executor.InstallAsync(repair, repair.Items[0], CancellationToken.None);

        Assert.Equal(requestCount, fixture.Handler.RequestCount);
        Assert.True((await manager.GetInstalledAsync()).Single(mod => mod.Id == "library").Enabled);
    }

    [Fact]
    public async Task Dependency_repair_downloads_and_installs_missing_dependency()
    {
        using var fixture = await CreateFixtureAsync();
        var loader = fixture.Group.Items[0];
        await fixture.Executor.TransferAsync(
            fixture.Group, loader, new InlineProgress(_ => { }), networkGate, CancellationToken.None);
        await fixture.Executor.InstallAsync(fixture.Group, loader, CancellationToken.None);
        var repair = RepairGroup(fixture, fixture.Group.Items[1]);

        await fixture.Executor.TransferAsync(
            repair, repair.Items[0], new InlineProgress(_ => { }), networkGate, CancellationToken.None);
        await fixture.Executor.InstallAsync(repair, repair.Items[0], CancellationToken.None);

        var receipt = Assert.Single(await ModManager(fixture).GetInstalledAsync());
        Assert.Equal("library", receipt.Id);
        Assert.True(receipt.Enabled);
    }

    [Fact]
    public async Task Dependency_repair_rechecks_instance_build_before_transfer()
    {
        using var fixture = await CreateFixtureAsync();
        var repair = RepairGroup(fixture, fixture.Group.Items[1]);
        var instance = await InstanceSidecar.LoadAsync(fixture.InstanceRoot);
        await InstanceSidecar.SaveAsync(instance with { BuildId = "changed" });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Executor.TransferAsync(
                repair, repair.Items[0], new InlineProgress(_ => { }), networkGate, CancellationToken.None));

        Assert.Contains("build", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, fixture.Handler.RequestCount);
    }

    [Fact]
    public async Task Dependency_repair_reenable_fails_when_receipt_disappears()
    {
        using var fixture = await CreateFixtureAsync();
        var loader = fixture.Group.Items[0];
        await fixture.Executor.TransferAsync(
            fixture.Group, loader, new InlineProgress(_ => { }), networkGate, CancellationToken.None);
        await fixture.Executor.InstallAsync(fixture.Group, loader, CancellationToken.None);
        var repair = RepairGroup(
            fixture,
            Item("library", DownloadQueueItemKind.DependencyReEnable, LoaderId));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            fixture.Executor.TransferAsync(
                repair, repair.Items[0], new InlineProgress(_ => { }), networkGate, CancellationToken.None));
    }

    [Fact]
    public async Task Preset_group_installs_missing_mods_in_plan_order()
    {
        using var fixture = await CreateFixtureAsync();
        var loader = fixture.Group.Items[0];
        await fixture.Executor.TransferAsync(
            fixture.Group, loader, new InlineProgress(_ => { }), networkGate, CancellationToken.None);
        await fixture.Executor.InstallAsync(fixture.Group, loader, CancellationToken.None);
        var instance = await InstanceSidecar.LoadAsync(fixture.InstanceRoot);
        var preset = Preset(instance, ModPresetApplyMode.Append);
        var plan = new PresetApplyPlan
        {
            Preset = preset,
            PreApplyStates = [],
            Steps =
            [
                PresetStep(PresetApplyStepKind.Install, "library"),
                PresetStep(PresetApplyStepKind.Install, "bridge"),
                PresetStep(PresetApplyStepKind.Install, "feature")
            ]
        };
        var group = ModPresetQueueGroupFactory.Create(plan, fixture.Catalog, instance);

        foreach (var item in group.Items)
        {
            await fixture.Executor.TransferAsync(
                group, item, new InlineProgress(_ => { }), networkGate, CancellationToken.None);
            await fixture.Executor.InstallAsync(group, item, CancellationToken.None);
        }

        Assert.Equal(["bridge", "feature", "library"],
            (await ModManager(fixture).GetInstalledAsync()).Select(receipt => receipt.Id));
    }

    [Fact]
    public async Task Preset_group_toggles_mod_without_network_and_rechecks_pinned_state()
    {
        using var fixture = await CreateFixtureAsync();
        foreach (var item in fixture.Group.Items)
        {
            await fixture.Executor.TransferAsync(
                fixture.Group, item, new InlineProgress(_ => { }), networkGate, CancellationToken.None);
            await fixture.Executor.InstallAsync(fixture.Group, item, CancellationToken.None);
        }
        var manager = ModManager(fixture);
        var instance = await InstanceSidecar.LoadAsync(fixture.InstanceRoot);
        var preset = Preset(instance, ModPresetApplyMode.Exact);
        var plan = new PresetApplyPlan
        {
            Preset = preset,
            PreApplyStates = [],
            Steps = [PresetStep(PresetApplyStepKind.Disable, "feature")]
        };
        var group = ModPresetQueueGroupFactory.Create(plan, fixture.Catalog, instance);
        var prepareItem = group.Items.Single(item => item.Kind == DownloadQueueItemKind.PresetPrepare);
        var presetItem = group.Items.Single(item => item.Kind == DownloadQueueItemKind.PresetDisable);
        var requestCount = fixture.Handler.RequestCount;

        await fixture.Executor.TransferAsync(
            group, prepareItem, new InlineProgress(_ => { }), networkGate, CancellationToken.None);
        await fixture.Executor.InstallAsync(group, prepareItem, CancellationToken.None);
        await fixture.Executor.TransferAsync(
            group, presetItem, new InlineProgress(_ => { }), networkGate, CancellationToken.None);
        await fixture.Executor.InstallAsync(group, presetItem, CancellationToken.None);

        Assert.Equal(requestCount, fixture.Handler.RequestCount);
        Assert.False((await manager.GetInstalledAsync()).Single(receipt => receipt.Id == "feature").Enabled);

        await manager.SetEnabledAsync("feature", enabled: true);
        await manager.SetPinnedAsync("feature", pinned: true);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Executor.InstallAsync(group, presetItem, CancellationToken.None));
        Assert.True((await manager.GetInstalledAsync()).Single(receipt => receipt.Id == "feature").Enabled);
    }

    [Fact]
    public async Task Preset_prepare_captures_state_when_execution_starts_instead_of_plan_creation()
    {
        using var fixture = await CreateFixtureAsync();
        foreach (var item in fixture.Group.Items)
        {
            await fixture.Executor.TransferAsync(
                fixture.Group, item, new InlineProgress(_ => { }), networkGate, CancellationToken.None);
            await fixture.Executor.InstallAsync(fixture.Group, item, CancellationToken.None);
        }
        var manager = ModManager(fixture);
        await manager.SetEnabledAsync("feature", enabled: false);
        var instance = await InstanceSidecar.LoadAsync(fixture.InstanceRoot);
        var plan = new PresetApplyPlan
        {
            Preset = Preset(instance, ModPresetApplyMode.Exact),
            PreApplyStates =
            [
                new PresetModState
                {
                    ModId = "feature",
                    WasInstalled = true,
                    Enabled = false
                }
            ],
            Steps = [PresetStep(PresetApplyStepKind.Disable, "feature")]
        };
        var group = ModPresetQueueGroupFactory.Create(plan, fixture.Catalog, instance);
        await manager.SetEnabledAsync("feature", enabled: true);
        var prepare = group.Items.Single(item => item.Kind == DownloadQueueItemKind.PresetPrepare);

        await fixture.Executor.TransferAsync(
            group, prepare, new InlineProgress(_ => { }), networkGate, CancellationToken.None);
        await fixture.Executor.InstallAsync(group, prepare, CancellationToken.None);
        await manager.DisableIgnoringDependentsAsync("feature");

        var dataRoot = Path.Combine(fixture.VersionRoot, ".crystalfly");
        var stateRoot = Path.Combine(dataRoot, "instances", "practice");
        var service = new ModPresetService(
            instance,
            fixture.Catalog.Mods,
            new LoaderManager(
                fixture.InstanceRoot,
                Path.Combine(dataRoot, "transactions"),
                Path.Combine(stateRoot, "loader.json"),
                Path.Combine(dataRoot, "packages"),
                fixture.Client),
            manager,
            Path.Combine(stateRoot, "presets"));
        await service.RestoreLastAsync();

        Assert.True((await manager.GetInstalledAsync()).Single(receipt => receipt.Id == "feature").Enabled);
    }

    [Fact]
    public async Task Concurrent_instances_share_one_cache_transfer_for_the_same_sha()
    {
        using var fixture = await CreateFixtureAsync();
        fixture.Handler.BlockResponses();
        var item = fixture.Group.Items.Single(candidate => candidate.Kind == DownloadQueueItemKind.Mod);
        var secondRoot = Path.Combine(fixture.VersionRoot, "secondary");
        Directory.CreateDirectory(secondRoot);
        var secondGroup = fixture.Group with
        {
            Id = "secondary-feature",
            TargetInstanceId = "secondary",
            TargetInstanceName = "Secondary",
            TargetInstanceRoot = secondRoot
        };

        var first = fixture.Executor.TransferAsync(
            fixture.Group, item, new InlineProgress(_ => { }), networkGate, CancellationToken.None);
        await fixture.Handler.FirstRequest.WaitAsync(TimeSpan.FromSeconds(5));
        var second = fixture.Executor.TransferAsync(
            secondGroup, item, new InlineProgress(_ => { }), networkGate, CancellationToken.None);
        await Task.Delay(100);
        fixture.Handler.ReleaseResponses();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, fixture.Handler.RequestCount);
    }

    [Fact]
    public async Task Cache_waiters_do_not_consume_network_slots_needed_by_other_packages()
    {
        using var fixture = await CreateFixtureAsync();
        fixture.Handler.BlockResponses();
        var shared = fixture.Group.Items.Single(candidate => candidate.Kind == DownloadQueueItemKind.Mod);
        var different = fixture.Group.Items.Single(candidate => candidate.PackageId == "bridge");

        var first = fixture.Executor.TransferAsync(
            fixture.Group, shared, new InlineProgress(_ => { }), networkGate, CancellationToken.None);
        await fixture.Handler.FirstRequest.WaitAsync(TimeSpan.FromSeconds(5));
        var second = fixture.Executor.TransferAsync(
            fixture.Group, shared, new InlineProgress(_ => { }), networkGate, CancellationToken.None);
        var third = fixture.Executor.TransferAsync(
            fixture.Group, shared, new InlineProgress(_ => { }), networkGate, CancellationToken.None);
        var fourth = fixture.Executor.TransferAsync(
            fixture.Group, different, new InlineProgress(_ => { }), networkGate, CancellationToken.None);

        try
        {
            await fixture.Handler.SecondRequest.WaitAsync(TimeSpan.FromSeconds(1));
        }
        finally
        {
            fixture.Handler.ReleaseResponses();
            await Task.WhenAll(first, second, third, fourth).WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.Equal(2, fixture.Handler.RequestCount);
    }

    [Fact]
    public async Task Queue_transfer_and_manual_uri_install_share_one_cache_transfer()
    {
        using var fixture = await CreateFixtureAsync();
        fixture.Handler.BlockResponses();
        var item = fixture.Group.Items.Single(candidate => candidate.Kind == DownloadQueueItemKind.Mod);
        var manifest = fixture.Catalog.Mods.Single(candidate => candidate.Id == item.PackageId);
        var cacheRoot = Path.Combine(fixture.VersionRoot, ".crystalfly", "packages");
        var manualTarget = Path.Combine(root, "manual-target");

        var queued = fixture.Executor.TransferAsync(
            fixture.Group, item, new InlineProgress(_ => { }), networkGate, CancellationToken.None);
        await fixture.Handler.FirstRequest.WaitAsync(TimeSpan.FromSeconds(5));
        var manual = PackageInstaller.InstallFromUriAsync(
            new Uri(manifest.DownloadUrl),
            manualTarget,
            Path.Combine(fixture.VersionRoot, ".crystalfly", "manual-transactions"),
            manifest.SizeBytes,
            manifest.Sha256,
            cacheRoot,
            fixture.Client);
        await Task.Delay(100);
        fixture.Handler.ReleaseResponses();
        await Task.WhenAll(queued, manual).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, fixture.Handler.RequestCount);
        Assert.Equal("feature", await File.ReadAllTextAsync(Path.Combine(manualTarget, "feature.dll")));
    }

    [Theory]
    [InlineData(InstancePurpose.OfficialSpeedrun, BuildId)]
    [InlineData(InstancePurpose.General, "unknown")]
    [InlineData(InstancePurpose.General, "unsupported-build")]
    public async Task Standalone_loader_rejects_restricted_or_unsupported_instances(
        InstancePurpose purpose,
        string buildId)
    {
        using var fixture = await CreateFixtureAsync();
        var record = await InstanceSidecar.LoadAsync(fixture.InstanceRoot);
        await InstanceSidecar.SaveAsync(record with { Purpose = purpose, BuildId = buildId });
        var item = fixture.Group.Items.Single(candidate => candidate.Kind == DownloadQueueItemKind.Loader);
        var group = fixture.Group with
        {
            Kind = DownloadQueueGroupKind.LoaderInstall,
            Items = [item]
        };
        await fixture.Executor.TransferAsync(
            group, item, new InlineProgress(_ => { }), networkGate, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Executor.InstallAsync(group, item, CancellationToken.None));

        Assert.False(File.Exists(Path.Combine(
            fixture.InstanceRoot, "hollow_knight_Data", "Managed", "MMHOOK_Assembly-CSharp.dll")));
        Assert.False(File.Exists(Path.Combine(
            fixture.VersionRoot, ".crystalfly", "instances", "practice", "loader.json")));
    }

    [Fact]
    public void Requires_game_exit_and_transient_classification_are_exact()
    {
        var executor = new CatalogPackageQueueExecutor(
            static () => new GameCatalog(),
            new HttpClient(),
            new InstanceOperationCoordinator());

        Assert.True(executor.RequiresGameExit(Item("loader", DownloadQueueItemKind.Loader, LoaderId)));
        Assert.True(executor.RequiresGameExit(Item("library", DownloadQueueItemKind.Dependency, LoaderId)));
        Assert.True(executor.RequiresGameExit(Item("feature", DownloadQueueItemKind.Mod, LoaderId)));
        Assert.True(executor.RequiresGameExit(Item("preset", DownloadQueueItemKind.PresetPrepare, LoaderId)));
        Assert.False(executor.RequiresGameExit(Item("asset", DownloadQueueItemKind.Asset, string.Empty)));
        Assert.True(executor.IsTransient(new HttpRequestException("network")));
        Assert.True(executor.IsTransient(new TimeoutException()));
        Assert.True(executor.IsTransient(new TaskCanceledException()));
        Assert.True(executor.IsTransient(new HttpRequestException(
            "timeout", null, HttpStatusCode.RequestTimeout)));
        Assert.True(executor.IsTransient(new HttpRequestException(
            "limited", null, HttpStatusCode.TooManyRequests)));
        Assert.True(executor.IsTransient(new HttpRequestException(
            "server", null, HttpStatusCode.BadGateway)));
        Assert.False(executor.IsTransient(new HttpRequestException(
            "missing", null, HttpStatusCode.NotFound)));
        Assert.False(executor.IsTransient(new InvalidDataException()));
        Assert.False(executor.IsTransient(new OperationCanceledException()));
    }

    private async Task<Fixture> CreateFixtureAsync()
    {
        var versionRoot = Path.Combine(root, "versions");
        var instanceRoot = Path.Combine(versionRoot, "practice");
        Directory.CreateDirectory(Path.Combine(instanceRoot, "hollow_knight_Data", "Managed"));
        var record = new InstanceRecord
        {
            Id = "practice",
            Name = "Practice",
            RootPath = instanceRoot,
            BuildId = BuildId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await InstanceSidecar.SaveAsync(record);

        var loaderPackage = CreateZip("loader.zip", ("MMHOOK_Assembly-CSharp.dll", "loader"));
        var libraryPackage = CreateZip("library.zip", ("library.dll", "library"));
        var bridgePackage = CreateZip("bridge.zip", ("bridge.dll", "bridge"));
        var featurePackage = CreateZip("feature.zip", ("feature.dll", "feature"));
        var loader = Loader(loaderPackage);
        var library = Mod("library", "Library", libraryPackage);
        var bridge = Mod("bridge", "Bridge", bridgePackage, ["library"]);
        var feature = Mod("feature", "Feature", featurePackage, ["bridge"]);
        var catalog = new GameCatalog { Loaders = [loader], Mods = [library, bridge, feature] };
        var handler = new PackageHandler(new Dictionary<string, byte[]>
        {
            [new Uri(loader.DownloadUrl).AbsolutePath] = await File.ReadAllBytesAsync(loaderPackage),
            [new Uri(library.DownloadUrl).AbsolutePath] = await File.ReadAllBytesAsync(libraryPackage),
            [new Uri(bridge.DownloadUrl).AbsolutePath] = await File.ReadAllBytesAsync(bridgePackage),
            [new Uri(feature.DownloadUrl).AbsolutePath] = await File.ReadAllBytesAsync(featurePackage)
        });
        var client = new HttpClient(handler);
        var executor = new CatalogPackageQueueExecutor(
            () => catalog, client, new InstanceOperationCoordinator());
        var group = new DownloadQueueGroup
        {
            Id = "install-feature",
            DeduplicationKey = "practice:feature",
            Kind = DownloadQueueGroupKind.ModInstall,
            Name = "Feature",
            TargetInstanceId = record.Id,
            TargetInstanceName = record.Name,
            TargetInstanceRoot = instanceRoot,
            CreatedAt = DateTimeOffset.UtcNow,
            Items =
            [
                Item(loader.Id, DownloadQueueItemKind.Loader, loader.Id, loader.Version),
                Item(library.Id, DownloadQueueItemKind.Dependency, library.LoaderId, library.Version),
                Item(bridge.Id, DownloadQueueItemKind.Dependency, bridge.LoaderId, bridge.Version),
                Item(feature.Id, DownloadQueueItemKind.Mod, feature.LoaderId, feature.Version)
            ]
        };
        return new Fixture(executor, handler, client, catalog, group, versionRoot, instanceRoot);
    }

    private LoaderManifest Loader(string packagePath) => new()
    {
        Id = LoaderId,
        Name = "Modding API",
        Version = "77",
        DownloadUrl = "https://packages.test/loader.zip",
        SizeBytes = new FileInfo(packagePath).Length,
        Sha256 = Sha256(packagePath),
        SupportedBuildIds = [BuildId]
    };

    private ModManifest Mod(
        string id,
        string name,
        string packagePath,
        IReadOnlyList<string>? dependencies = null) => new()
        {
            Id = id,
            Name = name,
            Version = "1.0.0",
            DownloadUrl = $"https://packages.test/{id}.zip",
            SizeBytes = new FileInfo(packagePath).Length,
            Sha256 = Sha256(packagePath),
            LoaderId = LoaderId,
            SupportedBuildIds = [BuildId],
            Dependencies = dependencies ?? []
        };

    private static DownloadQueueGroup RepairGroup(Fixture fixture, params DownloadQueueItem[] items) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        DeduplicationKey = "practice:repair:modding-api-77",
        Kind = DownloadQueueGroupKind.ModDependencyRepair,
        Name = "Repair dependencies",
        TargetInstanceId = "practice",
        TargetInstanceName = "Practice",
        TargetInstanceRoot = fixture.InstanceRoot,
        ExpectedBuildId = BuildId,
        ExpectedLoaderId = LoaderId,
        CreatedAt = DateTimeOffset.UtcNow,
        Items = items
    };

    private static ModPreset Preset(InstanceRecord instance, ModPresetApplyMode mode) => new()
    {
        Id = "preset",
        Name = "Preset",
        GameBuildId = instance.BuildId,
        LoaderId = LoaderId,
        ApplyMode = mode
    };

    private static PresetApplyStep PresetStep(PresetApplyStepKind kind, string id) => new()
    {
        Kind = kind,
        State = PresetApplyStepState.Pending,
        ModId = id,
        Version = "1.0.0",
        LoaderId = LoaderId,
        Reason = kind.ToString()
    };

    private static ModManager ModManager(Fixture fixture)
    {
        var dataRoot = Path.Combine(fixture.VersionRoot, ".crystalfly");
        return new ModManager(
            fixture.InstanceRoot,
            Path.Combine(dataRoot, "transactions"),
            Path.Combine(dataRoot, "instances", "practice", "mods"),
            Path.Combine(dataRoot, "packages"),
            fixture.Client);
    }

    private static DownloadQueueItem Item(
        string id,
        DownloadQueueItemKind kind,
        string loaderId,
        string version = "1.0.0") => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = kind,
            PackageId = id,
            Name = id,
            Version = version,
            LoaderId = loaderId
        };

    private string CreateZip(string name, params (string Path, string Content)[] entries)
    {
        var path = Path.Combine(root, "source", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var entry in entries)
        {
            using var writer = new StreamWriter(archive.CreateEntry(entry.Path).Open());
            writer.Write(entry.Content);
        }
        return path;
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
        networkGate.Dispose();
    }

    private sealed record Fixture(
        CatalogPackageQueueExecutor Executor,
        PackageHandler Handler,
        HttpClient Client,
        GameCatalog Catalog,
        DownloadQueueGroup Group,
        string VersionRoot,
        string InstanceRoot) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }

    private sealed class PackageHandler(IReadOnlyDictionary<string, byte[]> packages) : HttpMessageHandler
    {
        private readonly TaskCompletionSource firstRequest =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource secondRequest =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource releaseResponses =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int requestCount;
        private bool blockResponses;

        public int RequestCount => Volatile.Read(ref requestCount);

        public Task FirstRequest => firstRequest.Task;

        public Task SecondRequest => secondRequest.Task;

        public void BlockResponses() => blockResponses = true;

        public void ReleaseResponses() => releaseResponses.TrySetResult();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref requestCount) >= 2)
            {
                secondRequest.TrySetResult();
            }
            firstRequest.TrySetResult();
            if (blockResponses)
            {
                await releaseResponses.Task.WaitAsync(cancellationToken);
            }
            var bytes = packages[request.RequestUri!.AbsolutePath];
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            };
        }
    }

    private sealed class InlineProgress(Action<PackageTransferProgress> report)
        : IProgress<PackageTransferProgress>
    {
        public void Report(PackageTransferProgress value) => report(value);
    }
}

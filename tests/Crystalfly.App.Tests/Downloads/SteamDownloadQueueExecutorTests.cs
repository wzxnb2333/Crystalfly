using System.Collections.Concurrent;
using System.Text.Json;
using Crystalfly.App.Downloads;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;
using Crystalfly.Core.Packages;
using Crystalfly.Steam.Downloads;

namespace Crystalfly.App.Tests.Downloads;

public sealed class SteamDownloadQueueExecutorTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "Crystalfly.Tests",
        Guid.NewGuid().ToString("N"));
    private readonly SemaphoreSlim networkGate = new(3, 3);

    [Fact]
    public void Factory_creates_one_persistable_Steam_build_item()
    {
        Directory.CreateDirectory(root);
        var createdAt = new DateTimeOffset(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

        DownloadQueueGroup group = SteamDownloadQueueGroupFactory.Create(
            "1.5.78.11833",
            "1.5.78.11833",
            257781644874438846,
            root,
            "1578",
            createdAt);

        DownloadQueueItem item = Assert.Single(group.Items);
        Assert.Equal(DownloadQueueGroupKind.AssetInstall, group.Kind);
        Assert.Equal("1578", group.TargetInstanceName);
        Assert.Equal(Path.Combine(root, "1578"), group.TargetInstanceRoot);
        Assert.Equal(createdAt, group.CreatedAt);
        Assert.Equal(DownloadQueueItemKind.Asset, item.Kind);
        Assert.Equal("steam:1.5.78.11833", item.PackageId);
        Assert.Equal("257781644874438846", item.PackagePath);
        Assert.Equal("1.5.78.11833", item.Version);
    }

    [Fact]
    public void Different_instances_of_same_manifest_are_not_deduplicated()
    {
        Directory.CreateDirectory(root);

        DownloadQueueGroup first = SteamDownloadQueueGroupFactory.Create(
            "1.5.78.11833", "1.5.78.11833", 42, root, "first");
        DownloadQueueGroup second = SteamDownloadQueueGroupFactory.Create(
            "1.5.78.11833", "1.5.78.11833", 42, root, "second");

        Assert.NotEqual(first.DeduplicationKey, second.DeduplicationKey);
    }

    [Fact]
    public void Same_instance_with_different_manifests_is_deduplicated()
    {
        Directory.CreateDirectory(root);

        DownloadQueueGroup first = SteamDownloadQueueGroupFactory.Create(
            "build-1", "Build 1", 41, root, "target");
        DownloadQueueGroup second = SteamDownloadQueueGroupFactory.Create(
            "build-2", "Build 2", 42, root, "target");

        Assert.Equal(first.DeduplicationKey, second.DeduplicationKey);
    }

    [Fact]
    public async Task Transfer_and_install_publish_verified_download_as_instance()
    {
        Directory.CreateDirectory(root);
        SteamDownloadRequest? captured = null;
        var executor = CreateExecutor(async (request, report, cancellationToken) =>
        {
            captured = request;
            Directory.CreateDirectory(request.StagingDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(request.StagingDirectory, "hollow_knight.exe"),
                "game",
                cancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(request.StagingDirectory, "steam_appid.txt"),
                "367520",
                cancellationToken);
            report(new SteamDownloadProgress(4, 4, 1, "hollow_knight.exe")
            {
                BytesPerSecond = 1024
            });
            return new SteamDownloadResult(367520, 367521, 42, request.StagingDirectory,
                ["hollow_knight.exe", "steam_appid.txt"], 4);
        }, manifest => manifest == 42 ? "latest-known" : null);
        DownloadQueueGroup group = SteamDownloadQueueGroupFactory.Create(
            "public", "Steam public", null, root, "current");
        DownloadQueueItem item = Assert.Single(group.Items);
        var reports = new List<PackageTransferProgress>();

        await executor.TransferAsync(
            group, item, new ProgressCapture(reports.Add), networkGate, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Null(captured.ManifestId);
        Assert.False(Directory.Exists(group.TargetInstanceRoot));
        PackageTransferProgress progress = Assert.Single(reports);
        Assert.Equal((4L, 4L, 1024D, "hollow_knight.exe"),
            (progress.CompletedBytes, progress.TotalBytes, progress.BytesPerSecond, progress.Stage));

        await executor.InstallAsync(group, item, CancellationToken.None);

        InstanceRecord instance = await InstanceSidecar.LoadAsync(group.TargetInstanceRoot);
        Assert.Equal(group.TargetInstanceId, instance.Id);
        Assert.Equal("latest-known", instance.BuildId);
        Assert.Equal(InstanceProvisioningMode.Downloaded, instance.ProvisioningMode);
        Assert.Equal("367520", await File.ReadAllTextAsync(
            Path.Combine(group.TargetInstanceRoot, "steam_appid.txt")));
    }

    [Fact]
    public async Task Failed_or_canceled_transfer_removes_staging_and_never_creates_instance()
    {
        Directory.CreateDirectory(root);
        foreach (Exception failure in new Exception[]
                 {
                     new HttpRequestException("CDN unavailable"),
                     new OperationCanceledException(new CancellationToken(canceled: true))
                 })
        {
            DownloadQueueGroup group = SteamDownloadQueueGroupFactory.Create(
                "public", "Steam public", null, root, Guid.NewGuid().ToString("N"));
            var executor = CreateExecutor(async (request, _, cancellationToken) =>
            {
                Directory.CreateDirectory(request.StagingDirectory);
                await File.WriteAllTextAsync(
                    Path.Combine(request.StagingDirectory, "partial.bin"), "partial", cancellationToken);
                throw failure;
            });

            await Assert.ThrowsAsync(failure.GetType(), () => executor.TransferAsync(
                group,
                Assert.Single(group.Items),
                new ProgressCapture(_ => { }),
                networkGate,
                CancellationToken.None));

            Assert.False(Directory.Exists(group.TargetInstanceRoot));
            Assert.False(Directory.Exists(SteamDownloadQueueGroupFactory.GetStagingDirectory(group)));
        }
    }

    [Fact]
    public async Task Same_Steam_session_runs_only_one_depot_download_at_a_time()
    {
        Directory.CreateDirectory(root);
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new ConcurrentQueue<string>();
        var active = 0;
        var maxActive = 0;
        var executor = CreateExecutor(async (request, _, cancellationToken) =>
        {
            int current = Interlocked.Increment(ref active);
            InterlockedExtensions.Max(ref maxActive, current);
            calls.Enqueue(request.StagingDirectory);
            if (calls.Count == 1)
            {
                firstEntered.SetResult();
                await releaseFirst.Task.WaitAsync(cancellationToken);
            }
            Directory.CreateDirectory(request.StagingDirectory);
            Interlocked.Decrement(ref active);
            return new SteamDownloadResult(367520, 367521, 42, request.StagingDirectory, [], 0);
        });
        DownloadQueueGroup first = SteamDownloadQueueGroupFactory.Create(
            "public", "Steam public", null, root, "first");
        DownloadQueueGroup second = SteamDownloadQueueGroupFactory.Create(
            "public", "Steam public", null, root, "second");

        Task firstTask = executor.TransferAsync(
            first, Assert.Single(first.Items), new ProgressCapture(_ => { }), networkGate,
            CancellationToken.None);
        await firstEntered.Task;
        Task secondTask = executor.TransferAsync(
            second, Assert.Single(second.Items), new ProgressCapture(_ => { }), networkGate,
            CancellationToken.None);
        await Task.Yield();

        Assert.Single(calls);
        releaseFirst.SetResult();
        await Task.WhenAll(firstTask, secondTask);
        Assert.Equal(1, maxActive);
    }

    [Fact]
    public async Task Steam_login_is_rechecked_after_waiting_for_the_session_gate()
    {
        Directory.CreateDirectory(root);
        var loggedOn = true;
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waitingForLogin = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var executor = new SteamDownloadQueueExecutor(
            new RejectingFallbackExecutor(),
            async (request, _, cancellationToken) =>
            {
                var call = Interlocked.Increment(ref calls);
                Directory.CreateDirectory(request.StagingDirectory);
                if (call == 1)
                {
                    firstEntered.SetResult();
                    await releaseFirst.Task.WaitAsync(cancellationToken);
                }
                else
                {
                    secondStarted.SetResult();
                }
                return new SteamDownloadResult(
                    367520, 367521, 42, request.StagingDirectory, [], 0);
            },
            _ => null,
            () => loggedOn,
            TimeSpan.FromMilliseconds(10));
        DownloadQueueGroup first = SteamDownloadQueueGroupFactory.Create(
            "public", "Steam public", null, root, "first-recheck");
        DownloadQueueGroup second = SteamDownloadQueueGroupFactory.Create(
            "public", "Steam public", null, root, "second-recheck");

        var firstTransfer = executor.TransferAsync(
            first, Assert.Single(first.Items), new ProgressCapture(_ => { }), networkGate,
            CancellationToken.None);
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var secondTransfer = executor.TransferAsync(
            second,
            Assert.Single(second.Items),
            new ProgressCapture(progress =>
            {
                if (progress.Stage == "Waiting for Steam login")
                {
                    waitingForLogin.TrySetResult();
                }
            }),
            networkGate,
            CancellationToken.None);
        loggedOn = false;
        releaseFirst.SetResult();
        await firstTransfer.WaitAsync(TimeSpan.FromSeconds(5));

        var observed = await Task.WhenAny(waitingForLogin.Task, secondStarted.Task)
            .WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Same(waitingForLogin.Task, observed);
        Assert.Equal(1, Volatile.Read(ref calls));

        loggedOn = true;
        await secondTransfer.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Restored_transfer_waits_for_Steam_login_and_then_continues()
    {
        Directory.CreateDirectory(root);
        var loggedOn = false;
        var reports = new ConcurrentQueue<PackageTransferProgress>();
        var executor = new SteamDownloadQueueExecutor(
            new RejectingFallbackExecutor(),
            SuccessfulDownload(42),
            _ => null,
            () => loggedOn,
            TimeSpan.FromMilliseconds(10));
        DownloadQueueGroup group = SteamDownloadQueueGroupFactory.Create(
            "public", "Steam public", null, root, "restored");

        var transfer = executor.TransferAsync(
            group,
            Assert.Single(group.Items),
            new ProgressCapture(reports.Enqueue),
            networkGate,
            CancellationToken.None);
        await WaitUntilAsync(() => reports.Any(report => report.Stage == "Waiting for Steam login"));
        Assert.False(transfer.IsCompleted);

        loggedOn = true;
        await transfer.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(Directory.Exists(SteamDownloadQueueGroupFactory.GetStagingDirectory(group)));
    }

    [Fact]
    public async Task Waiting_for_Steam_login_does_not_take_a_network_slot()
    {
        Directory.CreateDirectory(root);
        var loggedOn = false;
        var reports = new ConcurrentQueue<PackageTransferProgress>();
        var executor = new SteamDownloadQueueExecutor(
            new RejectingFallbackExecutor(),
            SuccessfulDownload(42),
            _ => null,
            () => loggedOn,
            TimeSpan.FromMilliseconds(10));
        DownloadQueueGroup group = SteamDownloadQueueGroupFactory.Create(
            "public", "Steam public", null, root, "waiting");

        var transfer = executor.TransferAsync(
            group,
            Assert.Single(group.Items),
            new ProgressCapture(reports.Enqueue),
            networkGate,
            CancellationToken.None);
        await WaitUntilAsync(() => reports.Any(report => report.Stage == "Waiting for Steam login"));

        Assert.True(await networkGate.WaitAsync(0));
        networkGate.Release();

        loggedOn = true;
        await transfer.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Explicit_build_is_rejected_when_actual_manifest_maps_to_another_build()
    {
        Directory.CreateDirectory(root);
        var executor = CreateExecutor(
            SuccessfulDownload(42),
            _ => "different-build");
        DownloadQueueGroup group = SteamDownloadQueueGroupFactory.Create(
            "expected-build", "Expected", 42, root, "target");
        DownloadQueueItem item = Assert.Single(group.Items);
        await executor.TransferAsync(
            group, item, new ProgressCapture(_ => { }), networkGate, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => executor.InstallAsync(group, item, CancellationToken.None));

        Assert.Contains("does not match", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(group.TargetInstanceRoot));
        Assert.False(Directory.Exists(SteamDownloadQueueGroupFactory.GetStagingDirectory(group)));
    }

    [Fact]
    public async Task Unknown_public_manifest_uses_unverified_build_id()
    {
        Directory.CreateDirectory(root);
        var executor = CreateExecutor(SuccessfulDownload(42));
        DownloadQueueGroup group = SteamDownloadQueueGroupFactory.Create(
            "public", "Steam public", null, root, "target");
        DownloadQueueItem item = Assert.Single(group.Items);

        await executor.TransferAsync(
            group, item, new ProgressCapture(_ => { }), networkGate, CancellationToken.None);
        await executor.InstallAsync(group, item, CancellationToken.None);

        InstanceRecord instance = await InstanceSidecar.LoadAsync(group.TargetInstanceRoot);
        Assert.Equal("steam-public-42", instance.BuildId);
    }

    [Fact]
    public async Task Existing_target_is_preserved_and_download_staging_is_removed()
    {
        Directory.CreateDirectory(root);
        var executor = CreateExecutor(SuccessfulDownload(42), _ => "expected-build");
        DownloadQueueGroup group = SteamDownloadQueueGroupFactory.Create(
            "expected-build", "Expected", 42, root, "target");
        DownloadQueueItem item = Assert.Single(group.Items);
        await executor.TransferAsync(
            group, item, new ProgressCapture(_ => { }), networkGate, CancellationToken.None);
        Directory.CreateDirectory(group.TargetInstanceRoot);
        string marker = Path.Combine(group.TargetInstanceRoot, "keep.txt");
        await File.WriteAllTextAsync(marker, "keep");

        await Assert.ThrowsAsync<IOException>(
            () => executor.InstallAsync(group, item, CancellationToken.None));

        Assert.Equal("keep", await File.ReadAllTextAsync(marker));
        Assert.False(Directory.Exists(SteamDownloadQueueGroupFactory.GetStagingDirectory(group)));
    }

    [Fact]
    public async Task Matching_published_instance_makes_repeated_install_idempotent()
    {
        Directory.CreateDirectory(root);
        var executor = CreateExecutor(SuccessfulDownload(42), _ => "expected-build");
        DownloadQueueGroup group = SteamDownloadQueueGroupFactory.Create(
            "expected-build", "Expected", 42, root, "target");
        DownloadQueueItem item = Assert.Single(group.Items);
        await executor.TransferAsync(
            group, item, new ProgressCapture(_ => { }), networkGate, CancellationToken.None);
        await executor.InstallAsync(group, item, CancellationToken.None);

        await executor.TransferAsync(
            group, item, new ProgressCapture(_ => { }), networkGate, CancellationToken.None);
        await executor.InstallAsync(group, item, CancellationToken.None);

        InstanceRecord instance = await InstanceSidecar.LoadAsync(group.TargetInstanceRoot);
        Assert.Equal(group.TargetInstanceId, instance.Id);
        Assert.Equal(group.TargetInstanceName, instance.Name);
        Assert.Equal("expected-build", instance.BuildId);
        Assert.Equal(group.CreatedAt, instance.CreatedAt);
        Assert.False(Directory.Exists(SteamDownloadQueueGroupFactory.GetStagingDirectory(group)));
        Assert.False(File.Exists(Path.Combine(
            group.TargetInstanceRoot,
            InstanceDirectory.PendingDownloadMarkerFileName)));
    }

    [Fact]
    public async Task Destination_transfer_state_recovers_publish_after_move_crash()
    {
        Directory.CreateDirectory(root);
        var executor = CreateExecutor(SuccessfulDownload(42), _ => "expected-build");
        DownloadQueueGroup group = SteamDownloadQueueGroupFactory.Create(
            "expected-build", "Expected", 42, root, "target");
        DownloadQueueItem item = Assert.Single(group.Items);
        await executor.TransferAsync(
            group, item, new ProgressCapture(_ => { }), networkGate, CancellationToken.None);
        string staging = SteamDownloadQueueGroupFactory.GetStagingDirectory(group);
        Directory.Move(staging, group.TargetInstanceRoot);

        await executor.TransferAsync(
            group, item, new ProgressCapture(_ => { }), networkGate, CancellationToken.None);
        await executor.InstallAsync(group, item, CancellationToken.None);

        InstanceRecord instance = await InstanceSidecar.LoadAsync(group.TargetInstanceRoot);
        Assert.Equal(group.TargetInstanceId, instance.Id);
        Assert.Equal(group.TargetInstanceName, instance.Name);
        Assert.Equal("expected-build", instance.BuildId);
        Assert.Equal(group.CreatedAt, instance.CreatedAt);
        Assert.Equal(InstanceProvisioningMode.Downloaded, instance.ProvisioningMode);
        Assert.False(Directory.Exists(staging));
        Assert.False(File.Exists(Path.Combine(
            group.TargetInstanceRoot,
            InstanceDirectory.PendingDownloadMarkerFileName)));
    }

    [Fact]
    public async Task Pending_publish_is_skipped_by_discovery_then_recovers_with_queue_instance_id()
    {
        Directory.CreateDirectory(root);
        var executor = CreateExecutor(async (request, _, cancellationToken) =>
        {
            Directory.CreateDirectory(Path.Combine(request.StagingDirectory, "hollow_knight_Data"));
            await File.WriteAllTextAsync(
                Path.Combine(request.StagingDirectory, "hollow_knight.exe"),
                "game",
                cancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(request.StagingDirectory, "hollow_knight_Data", "globalgamemanagers"),
                "global",
                cancellationToken);
            return new SteamDownloadResult(
                367520,
                367521,
                42,
                request.StagingDirectory,
                ["hollow_knight.exe", "hollow_knight_Data/globalgamemanagers"],
                10);
        }, _ => "expected-build");
        DownloadQueueGroup group = SteamDownloadQueueGroupFactory.Create(
            "expected-build", "Expected", 42, root, "target");
        DownloadQueueItem item = Assert.Single(group.Items);
        await executor.TransferAsync(
            group, item, new ProgressCapture(_ => { }), networkGate, CancellationToken.None);
        string staging = SteamDownloadQueueGroupFactory.GetStagingDirectory(group);
        Directory.Move(staging, group.TargetInstanceRoot);

        IReadOnlyList<InstanceRecord> beforeRecovery = await InstanceImportService.DiscoverAsync(
            root,
            new GameCatalog());

        Assert.Empty(beforeRecovery);
        Assert.False(File.Exists(InstanceSidecar.GetMarkerPath(group.TargetInstanceRoot)));
        Assert.True(File.Exists(Path.Combine(
            group.TargetInstanceRoot,
            InstanceDirectory.PendingDownloadMarkerFileName)));

        await executor.TransferAsync(
            group, item, new ProgressCapture(_ => { }), networkGate, CancellationToken.None);
        await executor.InstallAsync(group, item, CancellationToken.None);
        IReadOnlyList<InstanceRecord> afterRecovery = await InstanceImportService.DiscoverAsync(
            root,
            new GameCatalog());

        InstanceRecord recovered = Assert.Single(afterRecovery);
        Assert.Equal(group.TargetInstanceId, recovered.Id);
        Assert.False(File.Exists(Path.Combine(
            group.TargetInstanceRoot,
            InstanceDirectory.PendingDownloadMarkerFileName)));
    }

    [Fact]
    public async Task Steam_publish_waits_for_shared_instance_operation_gate()
    {
        Directory.CreateDirectory(root);
        var coordinator = new InstanceOperationCoordinator();
        var executor = new SteamDownloadQueueExecutor(
            new RejectingFallbackExecutor(),
            SuccessfulDownload(42),
            _ => "expected-build",
            operationCoordinator: coordinator);
        DownloadQueueGroup group = SteamDownloadQueueGroupFactory.Create(
            "expected-build", "Expected", 42, root, "target");
        DownloadQueueItem item = Assert.Single(group.Items);
        await executor.TransferAsync(
            group, item, new ProgressCapture(_ => { }), networkGate, CancellationToken.None);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task blocker = coordinator.RunAsync("refresh", async _ =>
        {
            entered.SetResult();
            await release.Task;
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Task publish = executor.InstallAsync(group, item, CancellationToken.None);
        try
        {
            await Task.Delay(100);
            Assert.False(publish.IsCompleted);
            Assert.False(Directory.Exists(group.TargetInstanceRoot));
        }
        finally
        {
            release.TrySetResult();
            await blocker;
        }

        await publish.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(Directory.Exists(group.TargetInstanceRoot));
    }

    [Theory]
    [InlineData(41UL, "same", "manifest")]
    [InlineData(42UL, "other", "target")]
    public async Task Mismatched_destination_transfer_state_is_not_adopted(
        ulong manifestId,
        string instanceId,
        string expectedError)
    {
        Directory.CreateDirectory(root);
        var executor = CreateExecutor(SuccessfulDownload(42), _ => "expected-build");
        DownloadQueueGroup group = SteamDownloadQueueGroupFactory.Create(
            "expected-build", "Expected", 42, root, "target");
        DownloadQueueItem item = Assert.Single(group.Items);
        await executor.TransferAsync(
            group, item, new ProgressCapture(_ => { }), networkGate, CancellationToken.None);
        string staging = SteamDownloadQueueGroupFactory.GetStagingDirectory(group);
        Directory.Move(staging, group.TargetInstanceRoot);
        await File.WriteAllTextAsync(
            Path.Combine(group.TargetInstanceRoot, InstanceDirectory.PendingDownloadMarkerFileName),
            JsonSerializer.Serialize(new
            {
                manifestId,
                instanceId = instanceId == "same" ? group.TargetInstanceId : instanceId
            }));
        string marker = Path.Combine(group.TargetInstanceRoot, "keep.txt");
        await File.WriteAllTextAsync(marker, "keep");
        await executor.TransferAsync(
            group, item, new ProgressCapture(_ => { }), networkGate, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => executor.InstallAsync(group, item, CancellationToken.None));

        Assert.Contains(expectedError, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("keep", await File.ReadAllTextAsync(marker));
        Assert.False(File.Exists(InstanceSidecar.GetMarkerPath(group.TargetInstanceRoot)));
        Assert.False(Directory.Exists(staging));
    }

    [Theory]
    [InlineData(DownloadQueueItemKind.Mod)]
    [InlineData(DownloadQueueItemKind.DependencyReEnable)]
    public async Task Non_Steam_items_are_forwarded_to_fallback_executor(DownloadQueueItemKind kind)
    {
        var fallback = new RecordingFallbackExecutor();
        var executor = new SteamDownloadQueueExecutor(
            fallback,
            (_, _, _) => throw new InvalidOperationException("Steam downloader was called."),
            _ => null);
        var group = new DownloadQueueGroup();
        var item = new DownloadQueueItem
        {
            Kind = kind,
            PackageId = "hkmod:DebugMod"
        };

        Assert.True(executor.RequiresGameExit(item));
        await executor.TransferAsync(
            group, item, new ProgressCapture(_ => { }), networkGate, CancellationToken.None);
        await executor.InstallAsync(group, item, CancellationToken.None);

        Assert.Equal(1, fallback.TransferCalls);
        Assert.Equal(1, fallback.InstallCalls);
    }

    private static SteamDownloadQueueExecutor CreateExecutor(
        SteamQueueDownloadAsync download,
        Func<ulong, string?>? resolveBuildId = null) => new(
            new RejectingFallbackExecutor(),
            download,
            resolveBuildId ?? (_ => null));

    private static SteamQueueDownloadAsync SuccessfulDownload(ulong manifestId) =>
        async (request, _, cancellationToken) =>
        {
            Directory.CreateDirectory(request.StagingDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(request.StagingDirectory, "steam_appid.txt"),
                "367520",
                cancellationToken);
            return new SteamDownloadResult(
                367520,
                367521,
                manifestId,
                request.StagingDirectory,
                ["steam_appid.txt"],
                6);
        };

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    public void Dispose()
    {
        networkGate.Dispose();
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class ProgressCapture(Action<PackageTransferProgress> capture)
        : IProgress<PackageTransferProgress>
    {
        public void Report(PackageTransferProgress value) => capture(value);
    }

    private sealed class RejectingFallbackExecutor : IDownloadQueueExecutor
    {
        public bool RequiresGameExit(DownloadQueueItem item) =>
            throw new InvalidOperationException("Fallback was called.");

        public bool IsTransient(Exception exception) => false;

        public Task TransferAsync(
            DownloadQueueGroup group,
            DownloadQueueItem item,
            IProgress<PackageTransferProgress> progress,
            SemaphoreSlim networkGate,
            CancellationToken cancellationToken) => throw new InvalidOperationException("Fallback was called.");

        public Task InstallAsync(
            DownloadQueueGroup group,
            DownloadQueueItem item,
            CancellationToken cancellationToken) => throw new InvalidOperationException("Fallback was called.");
    }

    private sealed class RecordingFallbackExecutor : IDownloadQueueExecutor
    {
        public int TransferCalls { get; private set; }

        public int InstallCalls { get; private set; }

        public bool RequiresGameExit(DownloadQueueItem item) => true;

        public bool IsTransient(Exception exception) => false;

        public Task TransferAsync(
            DownloadQueueGroup group,
            DownloadQueueItem item,
            IProgress<PackageTransferProgress> progress,
            SemaphoreSlim networkGate,
            CancellationToken cancellationToken)
        {
            TransferCalls++;
            return Task.CompletedTask;
        }

        public Task InstallAsync(
            DownloadQueueGroup group,
            DownloadQueueItem item,
            CancellationToken cancellationToken)
        {
            InstallCalls++;
            return Task.CompletedTask;
        }
    }

    private static class InterlockedExtensions
    {
        public static void Max(ref int target, int value)
        {
            int current;
            while (value > (current = Volatile.Read(ref target))
                   && Interlocked.CompareExchange(ref target, value, current) != current)
            {
            }
        }
    }
}

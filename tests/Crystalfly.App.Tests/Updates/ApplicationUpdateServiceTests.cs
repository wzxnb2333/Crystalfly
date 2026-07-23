using System.Net;
using System.Security.Cryptography;
using Crystalfly.App.Updates;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Networking;
using Crystalfly.Core.Updates;

namespace Crystalfly.App.Tests.Updates;

public sealed class ApplicationUpdateServiceTests : IDisposable
{
    private static readonly Uri ManifestUri = new("https://github.com/wzxnb2333/Crystalfly/releases/latest/download/update.json");
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), $"Crystalfly-update-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task CheckAsync_skips_without_request_when_offline()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("Request should not be sent."));
        using var client = new HttpClient(handler);
        using var policy = new NetworkPolicy(isOffline: true);
        var service = CreateService(client, policy, CreateManifest("0.6.1"));

        var result = await service.CheckAsync(new CrystalflySettings());

        Assert.Equal(ApplicationUpdateCheckStatus.Offline, result.Status);
        Assert.Null(result.Manifest);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_uses_persisted_check_time_after_service_restart()
    {
        var handler = new StubHandler(_ => JsonResponse("signed-manifest"));
        using var client = new HttpClient(handler);
        using var policy = new NetworkPolicy();
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 7, 23, 8, 0, 0, TimeSpan.Zero));
        var service = CreateService(client, policy, CreateManifest("0.6.1"), timeProvider: timeProvider);

        var first = await service.CheckAsync(new CrystalflySettings());
        var restartedService = CreateService(
            client,
            policy,
            CreateManifest("0.6.1"),
            timeProvider: timeProvider);
        var second = await restartedService.CheckAsync(new CrystalflySettings
        {
            LastUpdateCheckAt = first.CheckedAt
        });
        timeProvider.Advance(TimeSpan.FromDays(1));
        var third = await restartedService.CheckAsync(new CrystalflySettings
        {
            LastUpdateCheckAt = first.CheckedAt
        });

        Assert.Equal(ApplicationUpdateCheckStatus.UpdateAvailable, first.Status);
        Assert.Equal(timeProvider.GetUtcNow() - TimeSpan.FromDays(1), first.CheckedAt);
        Assert.Equal(ApplicationUpdateCheckStatus.NotDue, second.Status);
        Assert.Null(second.CheckedAt);
        Assert.Equal(ApplicationUpdateCheckStatus.UpdateAvailable, third.Status);
        Assert.Equal(timeProvider.GetUtcNow(), third.CheckedAt);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_skips_when_automatic_checks_are_disabled()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("Request should not be sent."));
        using var client = new HttpClient(handler);
        using var policy = new NetworkPolicy();
        var service = CreateService(client, policy, CreateManifest("0.6.1"));

        var result = await service.CheckAsync(new CrystalflySettings
        {
            CheckForUpdates = false
        });

        Assert.Equal(ApplicationUpdateCheckStatus.Disabled, result.Status);
        Assert.Null(result.CheckedAt);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_force_ignores_interval()
    {
        var handler = new StubHandler(_ => JsonResponse("signed-manifest"));
        using var client = new HttpClient(handler);
        using var policy = new NetworkPolicy();
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 7, 23, 8, 0, 0, TimeSpan.Zero));
        var service = CreateService(client, policy, CreateManifest("0.6.1"), timeProvider: timeProvider);

        var result = await service.CheckAsync(
            new CrystalflySettings
            {
                CheckForUpdates = false,
                LastUpdateCheckAt = timeProvider.GetUtcNow()
            },
            force: true);

        Assert.Equal(ApplicationUpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal(timeProvider.GetUtcNow(), result.CheckedAt);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_force_still_skips_when_offline()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("Request should not be sent."));
        using var client = new HttpClient(handler);
        using var policy = new NetworkPolicy(isOffline: true);
        var service = CreateService(client, policy, CreateManifest("0.6.1"));

        var result = await service.CheckAsync(
            new CrystalflySettings
            {
                LastUpdateCheckAt = DateTimeOffset.UtcNow
            },
            force: true);

        Assert.Equal(ApplicationUpdateCheckStatus.Offline, result.Status);
        Assert.Null(result.CheckedAt);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_does_not_treat_future_persisted_time_as_a_recent_check()
    {
        var handler = new StubHandler(_ => JsonResponse("signed-manifest"));
        using var client = new HttpClient(handler);
        using var policy = new NetworkPolicy();
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 7, 23, 8, 0, 0, TimeSpan.Zero));
        var service = CreateService(client, policy, CreateManifest("0.6.1"), timeProvider: timeProvider);

        var result = await service.CheckAsync(new CrystalflySettings
        {
            LastUpdateCheckAt = timeProvider.GetUtcNow().AddDays(30)
        });

        Assert.Equal(ApplicationUpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_force_allows_user_to_reconsider_a_skipped_version()
    {
        var handler = new StubHandler(_ => JsonResponse("signed-manifest"));
        using var client = new HttpClient(handler);
        using var policy = new NetworkPolicy();
        var service = CreateService(client, policy, CreateManifest("0.6.1"));

        var result = await service.CheckAsync(
            new CrystalflySettings { SkippedUpdateVersion = "0.6.1" },
            force: true);

        Assert.Equal(ApplicationUpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.NotNull(result.Manifest);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_coalesces_concurrent_automatic_checks()
    {
        var handler = new DelayedHandler();
        using var client = new HttpClient(handler);
        using var policy = new NetworkPolicy();
        var service = CreateService(client, policy, CreateManifest("0.6.1"));
        var settings = new CrystalflySettings();

        Task<ApplicationUpdateCheckResult> first = service.CheckAsync(settings);
        await handler.RequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Task<ApplicationUpdateCheckResult> second = service.CheckAsync(settings);
        await Task.Delay(50);

        Assert.Equal(1, handler.RequestCount);
        handler.ReleaseResponse.TrySetResult();
        Assert.Equal(ApplicationUpdateCheckStatus.UpdateAvailable, (await first).Status);
        Assert.Equal(ApplicationUpdateCheckStatus.NotDue, (await second).Status);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_reports_version_skipped_by_settings()
    {
        using var client = new HttpClient(new StubHandler(_ => JsonResponse("signed-manifest")));
        using var policy = new NetworkPolicy();
        var service = CreateService(client, policy, CreateManifest("0.6.1"));

        var result = await service.CheckAsync(new CrystalflySettings
        {
            SkippedUpdateVersion = "0.6.1"
        });

        Assert.Equal(ApplicationUpdateCheckStatus.VersionSkipped, result.Status);
        Assert.Null(result.Manifest);
        Assert.NotNull(result.CheckedAt);
    }

    [Theory]
    [InlineData("0.6.0")]
    [InlineData("0.5.9")]
    public async Task CheckAsync_reports_up_to_date_for_non_newer_version(string manifestVersion)
    {
        using var client = new HttpClient(new StubHandler(_ => JsonResponse("signed-manifest")));
        using var policy = new NetworkPolicy();
        var service = CreateService(client, policy, CreateManifest(manifestVersion));

        var result = await service.CheckAsync(new CrystalflySettings());

        Assert.Equal(ApplicationUpdateCheckStatus.UpToDate, result.Status);
        Assert.Null(result.Manifest);
        Assert.NotNull(result.CheckedAt);
    }

    [Fact]
    public void CrystalflySettings_enables_update_checks_by_default()
    {
        var settings = new CrystalflySettings();

        Assert.True(settings.CheckForUpdates);
        Assert.Null(settings.LastUpdateCheckAt);
        Assert.Null(settings.SkippedUpdateVersion);
    }

    [Fact]
    public async Task DownloadAssetAsync_streams_verified_asset_to_temporary_file()
    {
        byte[] content = [1, 2, 3, 4, 5];
        var asset = CreateAsset(content);
        using var client = new HttpClient(new StubHandler(_ => BinaryResponse(content)));
        using var policy = new NetworkPolicy();
        var service = CreateService(client, policy, CreateManifest("0.6.1", asset));

        string path = await service.DownloadAssetAsync(asset, tempDirectory);

        Assert.StartsWith(Path.GetFullPath(tempDirectory), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(".exe", Path.GetExtension(path));
        Assert.Equal(content, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task Manifest_and_asset_requests_use_their_separate_clients()
    {
        byte[] content = [7, 8, 9];
        var manifestHandler = new StubHandler(_ => JsonResponse("signed-manifest"));
        var assetHandler = new StubHandler(_ => BinaryResponse(content));
        using var manifestClient = new HttpClient(manifestHandler);
        using var assetClient = new HttpClient(assetHandler);
        using var policy = new NetworkPolicy();
        var asset = CreateAsset(content);
        var service = new ApplicationUpdateService(
            manifestClient,
            assetClient,
            policy,
            ManifestUri,
            new Version(0, 6, 0),
            _ => CreateManifest("0.6.1", asset),
            TimeProvider.System);

        await service.CheckAsync(new CrystalflySettings());
        string path = await service.DownloadAssetAsync(asset, tempDirectory);

        Assert.Equal(1, manifestHandler.RequestCount);
        Assert.Equal(1, assetHandler.RequestCount);
        File.Delete(path);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DownloadAssetAsync_deletes_temporary_file_when_validation_fails(bool wrongSize)
    {
        byte[] content = [1, 2, 3, 4, 5];
        var valid = CreateAsset(content);
        var asset = valid with
        {
            Size = wrongSize ? valid.Size + 1 : valid.Size,
            Sha256 = wrongSize ? valid.Sha256 : new string('0', 64)
        };
        using var client = new HttpClient(new StubHandler(_ => BinaryResponse(content)));
        using var policy = new NetworkPolicy();
        var service = CreateService(client, policy, CreateManifest("0.6.1", asset));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.DownloadAssetAsync(asset, tempDirectory));

        Assert.Empty(Directory.EnumerateFiles(tempDirectory));
    }

    [Fact]
    public async Task DownloadAssetAsync_deletes_temporary_file_when_cancelled()
    {
        byte[] content = [1, 2, 3, 4, 5];
        var asset = CreateAsset(content);
        using var cancellation = new CancellationTokenSource();
        using var stream = new CancelAfterFirstReadStream(content, cancellation);
        using var client = new HttpClient(new StubHandler(_ => BinaryResponse(stream)));
        using var policy = new NetworkPolicy();
        var service = CreateService(client, policy, CreateManifest("0.6.1", asset));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.DownloadAssetAsync(asset, tempDirectory, cancellation.Token));

        Assert.Empty(Directory.EnumerateFiles(tempDirectory));
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static ApplicationUpdateService CreateService(
        HttpClient client,
        INetworkPolicy policy,
        UpdateManifest manifest,
        TimeProvider? timeProvider = null) =>
        new(
            client,
            policy,
            ManifestUri,
            new Version(0, 6, 0),
            _ => manifest,
            timeProvider ?? TimeProvider.System);

    private static UpdateManifest CreateManifest(string version, UpdateAsset? asset = null) => new()
    {
        Channel = "stable",
        Version = version,
        PublishedAt = new DateTimeOffset(2026, 7, 23, 0, 0, 0, TimeSpan.Zero),
        NotesMarkdown = "notes",
        Assets = asset is null ? [] : [asset]
    };

    private static UpdateAsset CreateAsset(byte[] content) => new()
    {
        Kind = UpdateAssetKind.Installer,
        Runtime = "win-x64",
        Url = "https://github.com/wzxnb2333/Crystalfly/releases/download/v0.6.1/Crystalfly.exe",
        Size = content.LongLength,
        Sha256 = Convert.ToHexString(SHA256.HashData(content))
    };

    private static HttpResponseMessage JsonResponse(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content)
    };

    private static HttpResponseMessage BinaryResponse(byte[] content) => new(HttpStatusCode.OK)
    {
        Content = new ByteArrayContent(content)
    };

    private static HttpResponseMessage BinaryResponse(Stream content) => new(HttpStatusCode.OK)
    {
        Content = new StreamContent(content)
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(responseFactory(request));
        }
    }

    private sealed class DelayedHandler : HttpMessageHandler
    {
        private int requestCount;

        public int RequestCount => Volatile.Read(ref requestCount);

        public TaskCompletionSource RequestStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseResponse { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref requestCount);
            RequestStarted.TrySetResult();
            await ReleaseResponse.Task.WaitAsync(cancellationToken);
            return JsonResponse("signed-manifest");
        }
    }

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public void Advance(TimeSpan value) => utcNow += value;
    }

    private sealed class CancelAfterFirstReadStream(
        byte[] content,
        CancellationTokenSource cancellation) : MemoryStream(content)
    {
        private bool read;

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (!read)
            {
                read = true;
                int count = await base.ReadAsync(buffer[..1], cancellationToken);
                cancellation.Cancel();
                return count;
            }

            cancellationToken.ThrowIfCancellationRequested();
            return await base.ReadAsync(buffer, cancellationToken);
        }
    }
}

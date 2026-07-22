using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Crystalfly.Core.Networking;
using Crystalfly.Core.Packages;

namespace Crystalfly.Core.Tests.Packages;

public sealed class PackageInstallerTests
{
    [Fact]
    public async Task InstallFromFile_verifies_and_replaces_package_files()
    {
        using var test = new TestDirectory();
        var package = test.CreateZip(("mod.dll", "new"), ("docs/readme.txt", "docs"));
        var target = test.CreateDirectory("target");
        await File.WriteAllTextAsync(Path.Combine(target, "mod.dll"), "old");

        var result = await PackageInstaller.InstallFromFileAsync(
            package, target, test.CreateDirectory("transactions"), new FileInfo(package).Length, FileSha256(package));

        Assert.Equal("new", await File.ReadAllTextAsync(Path.Combine(target, "mod.dll")));
        Assert.Equal("docs", await File.ReadAllTextAsync(Path.Combine(target, "docs", "readme.txt")));
        Assert.Equal(2, result.Changes.Count);
    }

    [Fact]
    public async Task InstallFromFile_rejects_wrong_size_or_hash_without_changing_target()
    {
        using var test = new TestDirectory();
        var package = test.CreateZip(("mod.dll", "new"));
        var target = test.CreateDirectory("target");
        await File.WriteAllTextAsync(Path.Combine(target, "mod.dll"), "old");
        var transactions = test.CreateDirectory("transactions");

        await Assert.ThrowsAsync<InvalidDataException>(() => PackageInstaller.InstallFromFileAsync(
            package, target, transactions, new FileInfo(package).Length + 1, FileSha256(package)));
        await Assert.ThrowsAsync<InvalidDataException>(() => PackageInstaller.InstallFromFileAsync(
            package, target, transactions, new FileInfo(package).Length, new string('0', 64)));

        Assert.Equal("old", await File.ReadAllTextAsync(Path.Combine(target, "mod.dll")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(transactions));
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("/absolute.txt")]
    [InlineData("C:/drive.txt")]
    [InlineData("folder/../../outside.txt")]
    [InlineData("mod.dll:payload")]
    [InlineData("mod.dll.")]
    [InlineData("mod.dll ")]
    public async Task InstallFromFile_rejects_unsafe_zip_paths(string entryName)
    {
        using var test = new TestDirectory();
        var package = test.CreateZip((entryName, "bad"));
        var target = test.CreateDirectory("target");

        await Assert.ThrowsAsync<InvalidDataException>(() => PackageInstaller.InstallFromFileAsync(
            package,
            target,
            test.CreateDirectory("transactions"),
            new FileInfo(package).Length,
            FileSha256(package)));

        Assert.Empty(Directory.EnumerateFileSystemEntries(target));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("file-directory-conflict")]
    public async Task InstallFromFile_rejects_ambiguous_zip_targets(string kind)
    {
        using var test = new TestDirectory();
        var package = kind == "duplicate"
            ? test.CreateZip(("Mod.dll", "one"), ("mod.dll", "two"))
            : test.CreateZip(("mods", "file"), ("mods/debug.dll", "nested"));
        var target = test.CreateDirectory("target");

        await Assert.ThrowsAsync<InvalidDataException>(() => PackageInstaller.InstallFromFileAsync(
            package,
            target,
            test.CreateDirectory("transactions"),
            new FileInfo(package).Length,
            FileSha256(package)));

        Assert.Empty(Directory.EnumerateFileSystemEntries(target));
    }

    [Fact]
    public async Task InstallFromUri_requires_https_before_downloading()
    {
        using var test = new TestDirectory();

        await Assert.ThrowsAsync<ArgumentException>(() => PackageInstaller.InstallFromUriAsync(
            new Uri("http://example.invalid/mod.zip"),
            test.CreateDirectory("target"),
            test.CreateDirectory("transactions"),
            1,
            new string('0', 64)));
    }

    [Fact]
    public async Task AcquireVerifiedFileFromFile_caches_verified_raw_file()
    {
        using var test = new TestDirectory();
        var source = Path.Combine(test.CreateDirectory("source"), "tool.dll");
        await File.WriteAllTextAsync(source, "tool-binary");
        var cache = test.CreateDirectory("cache");

        var acquired = await PackageInstaller.AcquireVerifiedFileFromFileAsync(
            source,
            test.CreateDirectory("transactions"),
            new FileInfo(source).Length,
            FileSha256(source),
            cache);

        Assert.StartsWith(Path.GetFullPath(cache), Path.GetFullPath(acquired), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("tool-binary", await File.ReadAllTextAsync(acquired));
    }

    [Fact]
    public async Task AcquireVerifiedFileFromUri_returns_valid_cache_without_redownloading()
    {
        using var test = new TestDirectory();
        var source = Path.Combine(test.CreateDirectory("source"), "tool.dll");
        await File.WriteAllTextAsync(source, "tool-binary");
        var bytes = await File.ReadAllBytesAsync(source);
        var hash = FileSha256(source);
        var cache = test.CreateDirectory("cache");
        var handler = new StubHandler(_ => Response(bytes));
        using var client = new HttpClient(handler);

        var first = await PackageInstaller.AcquireVerifiedFileFromUriAsync(
            new Uri("https://example.invalid/tool.dll"),
            test.CreateDirectory("transactions"),
            expectedSize: null,
            hash,
            cache,
            client);
        var second = await PackageInstaller.AcquireVerifiedFileFromUriAsync(
            new Uri("https://example.invalid/tool.dll"),
            test.CreateDirectory("transactions-2"),
            expectedSize: null,
            hash,
            cache,
            new HttpClient(new StubHandler(_ => throw new InvalidOperationException("Network should not be used."))));

        Assert.Equal(first, second);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(second));
    }

    [Fact]
    public async Task AcquireVerifiedFileFromUri_uses_valid_cache_while_offline()
    {
        using var test = new TestDirectory();
        var source = Path.Combine(test.CreateDirectory("source"), "tool.dll");
        await File.WriteAllTextAsync(source, "cached-tool");
        var bytes = await File.ReadAllBytesAsync(source);
        var hash = FileSha256(source);
        var cache = test.CreateDirectory("cache");
        using var onlineClient = new HttpClient(new StubHandler(_ => Response(bytes)));
        await PackageInstaller.AcquireVerifiedFileFromUriAsync(
            new Uri("https://example.invalid/tool.dll"),
            test.CreateDirectory("online-transactions"),
            bytes.Length,
            hash,
            cache,
            onlineClient);
        var offlineHandler = new StubHandler(_ => throw new InvalidOperationException("Network used."));
        using var offlineClient = new HttpClient(offlineHandler);
        var policy = new NetworkPolicy(isOffline: true);

        var acquired = await PackageInstaller.AcquireVerifiedFileFromUriAsync(
            new Uri("https://example.invalid/tool.dll"),
            test.CreateDirectory("offline-transactions"),
            bytes.Length,
            hash,
            cache,
            offlineClient,
            networkPolicy: policy);

        Assert.Equal(bytes, await File.ReadAllBytesAsync(acquired));
        Assert.Equal(0, offlineHandler.RequestCount);
    }

    [Fact]
    public async Task AcquireVerifiedFileFromUri_blocks_uncached_download_while_offline()
    {
        using var test = new TestDirectory();
        var handler = new StubHandler(_ => throw new InvalidOperationException("Network used."));
        using var client = new HttpClient(handler);
        var policy = new NetworkPolicy(isOffline: true);

        await Assert.ThrowsAsync<OfflineModeException>(() =>
            PackageInstaller.AcquireVerifiedFileFromUriAsync(
                new Uri("https://example.invalid/tool.dll"),
                test.CreateDirectory("transactions"),
                1,
                new string('A', 64),
                test.CreateDirectory("cache"),
                client,
                networkPolicy: policy));

        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task Concurrent_acquire_and_install_share_one_cache_transfer()
    {
        using var test = new TestDirectory();
        var package = test.CreateZip(("mod.dll", "shared"));
        var bytes = await File.ReadAllBytesAsync(package);
        var hash = FileSha256(package);
        var cache = test.CreateDirectory("cache");
        var handler = new BlockingHandler(bytes);
        using var client = new HttpClient(handler);

        var acquire = PackageInstaller.AcquireVerifiedFileFromUriAsync(
            new Uri("https://example.invalid/mod.zip"),
            test.CreateDirectory("acquire-transactions"),
            bytes.Length,
            hash,
            cache,
            client);
        await handler.FirstRequest.WaitAsync(TimeSpan.FromSeconds(5));
        var install = PackageInstaller.InstallFromUriAsync(
            new Uri("https://example.invalid/mod.zip"),
            test.CreateDirectory("target"),
            test.CreateDirectory("install-transactions"),
            bytes.Length,
            hash,
            cache,
            client);
        await Task.Delay(100);
        handler.ReleaseResponses();
        await Task.WhenAll(acquire, install).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(cache, $"{hash}.zip")));
        Assert.Equal("shared", await File.ReadAllTextAsync(Path.Combine(test.CreateDirectory("target"), "mod.dll")));
    }

    [Fact]
    public async Task AcquireVerifiedFileFromUri_reports_monotonic_transfer_progress()
    {
        using var test = new TestDirectory();
        var source = Path.Combine(test.CreateDirectory("source"), "tool.dll");
        await File.WriteAllTextAsync(source, new string('x', 32_000));
        var bytes = await File.ReadAllBytesAsync(source);
        var reports = new List<PackageTransferProgress>();
        using var client = new HttpClient(new StubHandler(_ => Response(bytes)));

        await PackageInstaller.AcquireVerifiedFileFromUriAsync(
            new Uri("https://example.invalid/tool.dll"),
            test.CreateDirectory("transactions"),
            bytes.Length,
            FileSha256(source),
            test.CreateDirectory("cache"),
            client,
            new Progress<PackageTransferProgress>(reports.Add));

        Assert.NotEmpty(reports);
        Assert.Equal(bytes.Length, reports[^1].CompletedBytes);
        Assert.Equal(bytes.Length, reports[^1].TotalBytes);
        Assert.True(reports.Zip(reports.Skip(1), (before, after) => after.CompletedBytes >= before.CompletedBytes).All(value => value));
    }

    [Fact]
    public async Task AcquireVerifiedFileFromUri_releases_network_slot_when_canceled()
    {
        using var test = new TestDirectory();
        var handler = new BlockingHandler([1]);
        using var client = new HttpClient(handler);
        using var networkGate = new SemaphoreSlim(1, 1);
        using var cancellation = new CancellationTokenSource();

        var acquire = PackageInstaller.AcquireVerifiedFileFromUriAsync(
            new Uri("https://example.invalid/tool.dll"),
            test.CreateDirectory("transactions"),
            1,
            new string('0', 64),
            test.CreateDirectory("cache"),
            client,
            cancellationToken: cancellation.Token,
            networkGate: networkGate);
        await handler.FirstRequest.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, networkGate.CurrentCount);

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => acquire);
        Assert.Equal(1, networkGate.CurrentCount);
    }

    [Fact]
    public async Task AcquireVerifiedFileFromUri_releases_network_slot_when_request_fails()
    {
        using var test = new TestDirectory();
        using var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(
            HttpStatusCode.InternalServerError)));
        using var networkGate = new SemaphoreSlim(1, 1);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            PackageInstaller.AcquireVerifiedFileFromUriAsync(
                new Uri("https://example.invalid/tool.dll"),
                test.CreateDirectory("transactions"),
                1,
                new string('0', 64),
                test.CreateDirectory("cache"),
                client,
                networkGate: networkGate));

        Assert.Equal(1, networkGate.CurrentCount);
    }

    [Fact]
    public async Task InstallFromUri_without_declared_size_uses_content_length_and_populates_cache()
    {
        using var test = new TestDirectory();
        var package = test.CreateZip(("mod.dll", "cached"));
        var bytes = await File.ReadAllBytesAsync(package);
        var handler = new StubHandler(_ => Response(bytes));
        using var client = new HttpClient(handler);
        var cache = test.CreateDirectory("cache");
        var target = test.CreateDirectory("target");

        await PackageInstaller.InstallFromUriAsync(
            new Uri("https://example.invalid/mod.zip"),
            target,
            test.CreateDirectory("transactions"),
            expectedSize: null,
            FileSha256(package),
            cache,
            client);

        Assert.Equal("cached", await File.ReadAllTextAsync(Path.Combine(target, "mod.dll")));
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(cache, $"{FileSha256(package)}.zip")));
    }

    [Fact]
    public async Task InstallFromUri_uses_valid_cache_without_network_request()
    {
        using var test = new TestDirectory();
        var package = test.CreateZip(("mod.dll", "cached"));
        var hash = FileSha256(package);
        var cache = test.CreateDirectory("cache");
        File.Copy(package, Path.Combine(cache, $"{hash}.zip"));
        var handler = new StubHandler(_ => throw new InvalidOperationException("Network should not be used."));
        using var client = new HttpClient(handler);

        await PackageInstaller.InstallFromUriAsync(
            new Uri("https://example.invalid/mod.zip"),
            test.CreateDirectory("target"),
            test.CreateDirectory("transactions"),
            expectedSize: null,
            hash,
            cache,
            client);

        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task InstallFromUri_redownloads_corrupt_cache_before_installing()
    {
        using var test = new TestDirectory();
        var package = test.CreateZip(("mod.dll", "fresh"));
        var bytes = await File.ReadAllBytesAsync(package);
        var hash = FileSha256(package);
        var cache = test.CreateDirectory("cache");
        await File.WriteAllTextAsync(Path.Combine(cache, $"{hash}.zip"), "corrupt");
        var handler = new StubHandler(_ => Response(bytes));
        using var client = new HttpClient(handler);
        var target = test.CreateDirectory("target");

        await PackageInstaller.InstallFromUriAsync(
            new Uri("https://example.invalid/mod.zip"),
            target,
            test.CreateDirectory("transactions"),
            expectedSize: null,
            hash,
            cache,
            client);

        Assert.Equal("fresh", await File.ReadAllTextAsync(Path.Combine(target, "mod.dll")));
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(cache, $"{hash}.zip")));
    }

    [Fact]
    public async Task InstallFromUri_rejects_bad_redownload_without_touching_cache_or_target()
    {
        using var test = new TestDirectory();
        var expectedPackage = test.CreateZip(("mod.dll", "expected"));
        var badPackage = test.CreateZip(("mod.dll", "bad"));
        var hash = FileSha256(expectedPackage);
        var cache = test.CreateDirectory("cache");
        var cachePath = Path.Combine(cache, $"{hash}.zip");
        await File.WriteAllTextAsync(cachePath, "corrupt-cache");
        var cachedBytes = await File.ReadAllBytesAsync(cachePath);
        var handler = new StubHandler(_ => Response(File.ReadAllBytes(badPackage)));
        using var client = new HttpClient(handler);
        var target = test.CreateDirectory("target");
        await File.WriteAllTextAsync(Path.Combine(target, "existing.txt"), "keep");

        await Assert.ThrowsAsync<InvalidDataException>(() => PackageInstaller.InstallFromUriAsync(
            new Uri("https://example.invalid/mod.zip"),
            target,
            test.CreateDirectory("transactions"),
            expectedSize: null,
            hash,
            cache,
            client));

        Assert.Equal(cachedBytes, await File.ReadAllBytesAsync(cachePath));
        Assert.Equal("keep", await File.ReadAllTextAsync(Path.Combine(target, "existing.txt")));
        Assert.False(File.Exists(Path.Combine(target, "mod.dll")));
    }

    [Fact]
    public async Task InstallFromUri_without_declared_size_rejects_missing_content_length()
    {
        using var test = new TestDirectory();
        var package = test.CreateZip(("mod.dll", "new"));
        var bytes = await File.ReadAllBytesAsync(package);
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new UnknownLengthContent(bytes)
        });
        using var client = new HttpClient(handler);
        var target = test.CreateDirectory("target");

        await Assert.ThrowsAsync<InvalidDataException>(() => PackageInstaller.InstallFromUriAsync(
            new Uri("https://example.invalid/mod.zip"),
            target,
            test.CreateDirectory("transactions"),
            expectedSize: null,
            FileSha256(package),
            cacheRoot: null,
            client));

        Assert.Empty(Directory.EnumerateFileSystemEntries(target));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public async Task InstallFromUri_rejects_response_length_mismatch_without_changing_target(int delta)
    {
        using var test = new TestDirectory();
        var package = test.CreateZip(("mod.dll", "new"));
        var bytes = await File.ReadAllBytesAsync(package);
        var response = Response(bytes);
        response.Content.Headers.ContentLength = bytes.Length + delta;
        var handler = new StubHandler(_ => response);
        using var client = new HttpClient(handler);
        var target = test.CreateDirectory("target");

        await Assert.ThrowsAsync<InvalidDataException>(() => PackageInstaller.InstallFromUriAsync(
            new Uri("https://example.invalid/mod.zip"),
            target,
            test.CreateDirectory("transactions"),
            expectedSize: null,
            FileSha256(package),
            cacheRoot: null,
            client));

        Assert.Empty(Directory.EnumerateFileSystemEntries(target));
    }

    [Fact]
    public async Task InstallFromUri_rejects_content_length_over_remote_limit()
    {
        using var test = new TestDirectory();
        var response = Response([]);
        response.Content.Headers.ContentLength = 256L * 1024 * 1024 + 1;
        var handler = new StubHandler(_ => response);
        using var client = new HttpClient(handler);

        await Assert.ThrowsAsync<InvalidDataException>(() => PackageInstaller.InstallFromUriAsync(
            new Uri("https://example.invalid/mod.zip"),
            test.CreateDirectory("target"),
            test.CreateDirectory("transactions"),
            expectedSize: null,
            new string('0', 64),
            cacheRoot: null,
            client));
    }

    private static HttpResponseMessage Response(byte[] content) => new(HttpStatusCode.OK)
    {
        Content = new ByteArrayContent(content)
    };

    private static string FileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(respond(request));
        }
    }

    private sealed class BlockingHandler(byte[] content) : HttpMessageHandler
    {
        private readonly TaskCompletionSource firstRequest =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource releaseResponses =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int requestCount;

        public int RequestCount => Volatile.Read(ref requestCount);

        public Task FirstRequest => firstRequest.Task;

        public void ReleaseResponses() => releaseResponses.TrySetResult();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref requestCount);
            firstRequest.TrySetResult();
            await releaseResponses.Task.WaitAsync(cancellationToken);
            return Response(content);
        }
    }

    private sealed class UnknownLengthContent(byte[] content) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(content).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class TestDirectory : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "Crystalfly.Tests", Guid.NewGuid().ToString("N"));

        public string CreateDirectory(params string[] parts)
        {
            var path = parts.Aggregate(_root, Path.Combine);
            Directory.CreateDirectory(path);
            return path;
        }

        public string CreateZip(params (string Name, string Content)[] entries)
        {
            Directory.CreateDirectory(_root);
            var path = Path.Combine(_root, $"{Guid.NewGuid():N}.zip");
            using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
            foreach (var item in entries)
            {
                var entry = archive.CreateEntry(item.Name);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8, leaveOpen: false);
                writer.Write(item.Content);
            }
            return path;
        }

        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}

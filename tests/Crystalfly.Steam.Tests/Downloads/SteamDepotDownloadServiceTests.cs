using System.Collections.Concurrent;
using System.Security.Cryptography;
using Crystalfly.Steam.Downloads;

namespace Crystalfly.Steam.Tests.Downloads;

public sealed class SteamDepotDownloadServiceTests : IDisposable
{
    private readonly string _staging = Path.Combine(Path.GetTempPath(), $"crystalfly-depot-{Guid.NewGuid():N}");

    [Fact]
    public async Task ChunkDownloadsNeverExceedSixteenConcurrentRequests()
    {
        byte[][] chunks = CreateChunks(17);
        var source = new ControlledContentClient(CreateManifest(chunks), chunks);
        var downloader = new SteamDepotDownloadService(source);
        Task<SteamDownloadResult> download = downloader.DownloadAsync(new SteamDownloadRequest(_staging, 123));

        try
        {
            await source.WaitForStartedAsync(16).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(16, source.StartedCount);
            Assert.Equal(16, source.ActiveCount);
            Assert.Equal(16, source.MaxActiveCount);

            source.ReleaseChunk(source.StartedChunkIds[0]);
            await source.WaitForStartedAsync(17).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(16, source.MaxActiveCount);
            source.ReleaseAll();
            await download;
        }
        finally
        {
            source.ReleaseAll();
            await IgnoreFailureAsync(download);
        }
    }

    [Fact]
    public async Task ConcurrentChunksWriteAtTheirManifestOffsetsWhenTheyFinishOutOfOrder()
    {
        byte[] first = "abc"u8.ToArray();
        byte[] second = "def"u8.ToArray();
        var source = new ControlledContentClient(CreateManifest(first, second), first, second);
        var secondReported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var downloader = new SteamDepotDownloadService(source, progress =>
        {
            if (progress.CompletedBytes == second.Length)
                secondReported.TrySetResult();
        });
        Task<SteamDownloadResult> download = downloader.DownloadAsync(new SteamDownloadRequest(_staging, 123));

        try
        {
            await source.WaitForStartedAsync(2).WaitAsync(TimeSpan.FromSeconds(5));
            source.ReleaseChunk("chunk-1");
            await secondReported.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.False(download.IsCompleted);
            source.ReleaseChunk("chunk-0");
            await download;

            Assert.Equal("abcdef", await File.ReadAllTextAsync(Path.Combine(_staging, "game.dat")));
        }
        finally
        {
            source.ReleaseAll();
            await IgnoreFailureAsync(download);
        }
    }

    [Fact]
    public async Task CancellationDoesNotStartSeventeenthChunkAndCleansPartialFile()
    {
        byte[][] chunks = CreateChunks(17);
        var source = new ControlledContentClient(CreateManifest(chunks), chunks);
        using var cancellation = new CancellationTokenSource();
        var downloader = new SteamDepotDownloadService(source);
        Task<SteamDownloadResult> download = downloader.DownloadAsync(
            new SteamDownloadRequest(_staging, 123),
            cancellation.Token);

        try
        {
            await source.WaitForStartedAsync(16).WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            source.ReleaseAll();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => download.WaitAsync(TimeSpan.FromSeconds(5)));

            Assert.Equal(16, source.StartedCount);
            Assert.Equal(0, source.ActiveCount);
            Assert.False(File.Exists(Path.Combine(_staging, "game.dat")));
            Assert.False(File.Exists(Path.Combine(_staging, "game.dat.crystalfly-part")));
        }
        finally
        {
            source.ReleaseAll();
            await IgnoreFailureAsync(download);
        }
    }

    [Fact]
    public async Task ChunkFailureWaitsForInflightChunksAndCleansPartialFile()
    {
        byte[][] chunks = CreateChunks(17);
        var source = new ControlledContentClient(CreateManifest(chunks), chunks);
        var downloader = new SteamDepotDownloadService(source);
        Task<SteamDownloadResult> download = downloader.DownloadAsync(new SteamDownloadRequest(_staging, 123));

        try
        {
            await source.WaitForStartedAsync(16).WaitAsync(TimeSpan.FromSeconds(5));
            string failedChunk = source.StartedChunkIds[0];
            var failure = new IOException("chunk failed");
            source.FailChunk(failedChunk, failure);
            await source.WaitForFinishedAsync(failedChunk).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.False(download.IsCompleted);
            source.ReleaseAll();
            IOException actual = await Assert.ThrowsAsync<IOException>(() => download);

            Assert.Same(failure, actual);
            Assert.Equal(16, source.StartedCount);
            Assert.Equal(0, source.ActiveCount);
            Assert.False(File.Exists(Path.Combine(_staging, "game.dat")));
            Assert.False(File.Exists(Path.Combine(_staging, "game.dat.crystalfly-part")));
        }
        finally
        {
            source.ReleaseAll();
            await IgnoreFailureAsync(download);
        }
    }

    [Fact]
    public async Task DownloadWritesAndVerifiesEveryFile()
    {
        byte[] first = "abc"u8.ToArray();
        byte[] second = "def"u8.ToArray();
        var source = new MemoryContentClient(CreateManifest(first, second), first, second);
        var downloader = new SteamDepotDownloadService(source);

        SteamDownloadResult result = await downloader.DownloadAsync(new SteamDownloadRequest(_staging, 123));

        Assert.Equal(SteamProduct.HollowKnightAppId, source.AppId);
        Assert.Equal(SteamProduct.HollowKnightWindowsDepotId, source.DepotId);
        Assert.Equal("public", source.Branch);
        Assert.Equal(123UL, source.RequestedManifestId);
        Assert.Equal("abcdef", await File.ReadAllTextAsync(Path.Combine(_staging, "game.dat")));
        Assert.Equal((123UL, 6L, 1), (result.ManifestId, result.TotalBytes, result.Files.Count));
    }

    [Fact]
    public async Task NestedFileDownloadCreatesParentBeforeCleaningPartial()
    {
        const string relativePath = "hollow_knight_Data/Plugins/x86_64/D3D12Core.dll";
        byte[] content = "d3d12"u8.ToArray();
        var manifest = new SteamDepotManifest(
            123,
            [new SteamDepotFile(
                relativePath,
                content.Length,
                Convert.ToHexString(SHA1.HashData(content)),
                [new SteamDepotChunk("chunk-0", 0, content.Length)])]);
        var downloader = new SteamDepotDownloadService(new MemoryContentClient(manifest, content));

        SteamDownloadResult result = await downloader.DownloadAsync(new SteamDownloadRequest(_staging, 123));

        string target = Path.Combine(_staging, "hollow_knight_Data", "Plugins", "x86_64", "D3D12Core.dll");
        Assert.Equal(content, await File.ReadAllBytesAsync(target));
        Assert.Equal([relativePath], result.Files);
        Assert.False(File.Exists(target + ".crystalfly-part"));
    }

    [Fact]
    public async Task DownloadCreatesSteamAppIdFileForDirectLaunch()
    {
        byte[] content = "game"u8.ToArray();
        var downloader = new SteamDepotDownloadService(
            new MemoryContentClient(CreateManifest(content), content));

        await downloader.DownloadAsync(new SteamDownloadRequest(_staging, 123));

        string appIdPath = Path.Combine(_staging, "steam_appid.txt");
        Assert.Equal("367520", await File.ReadAllTextAsync(appIdPath));
        Assert.Equal(6, new FileInfo(appIdPath).Length);
        Assert.False(File.Exists(appIdPath + ".crystalfly-part"));
    }

    [Fact]
    public async Task ManifestSteamAppIdIsVerifiedAndNotOverwritten()
    {
        byte[] content = "manifest-value"u8.ToArray();
        var manifest = new SteamDepotManifest(
            123,
            [new SteamDepotFile(
                "steam_appid.txt",
                content.Length,
                Convert.ToHexString(SHA1.HashData(content)),
                [new SteamDepotChunk("chunk-0", 0, content.Length)])]);
        var downloader = new SteamDepotDownloadService(new MemoryContentClient(manifest, content));

        SteamDownloadResult result = await downloader.DownloadAsync(new SteamDownloadRequest(_staging, 123));

        Assert.Equal(content, await File.ReadAllBytesAsync(Path.Combine(_staging, "steam_appid.txt")));
        Assert.Equal(["steam_appid.txt"], result.Files);
    }

    [Fact]
    public async Task HashMismatchDoesNotPublishFile()
    {
        byte[] content = "abc"u8.ToArray();
        SteamDepotManifest manifest = CreateManifest(content) with
        {
            Files =
            [
                new SteamDepotFile("game.dat", content.Length, new string('0', 40),
                    [new SteamDepotChunk("chunk-0", 0, content.Length)])
            ]
        };
        var downloader = new SteamDepotDownloadService(new MemoryContentClient(manifest, content));

        await Assert.ThrowsAsync<InvalidDataException>(
            () => downloader.DownloadAsync(new SteamDownloadRequest(_staging, 123)));

        Assert.False(File.Exists(Path.Combine(_staging, "game.dat")));
    }

    [Fact]
    public async Task InvalidChunkLayoutIsRejectedBeforeDownloadAndRemovesStalePartialFile()
    {
        byte[] content = "abcd"u8.ToArray();
        var manifest = new SteamDepotManifest(
            123,
            [new SteamDepotFile(
                "game.dat",
                content.Length,
                Convert.ToHexString(SHA1.HashData(content)),
                [
                    new SteamDepotChunk("chunk-0", 0, 3),
                    new SteamDepotChunk("chunk-1", 2, 2)
                ])]);
        var source = new MemoryContentClient(manifest, "abc"u8.ToArray(), "cd"u8.ToArray());
        string partial = Path.Combine(_staging, "game.dat.crystalfly-part");
        Directory.CreateDirectory(_staging);
        await File.WriteAllTextAsync(partial, "stale");
        var downloader = new SteamDepotDownloadService(source);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => downloader.DownloadAsync(new SteamDownloadRequest(_staging, 123)));

        Assert.Equal(0, source.DownloadedChunkCount);
        Assert.False(File.Exists(Path.Combine(_staging, "game.dat")));
        Assert.False(File.Exists(partial));
    }

    [Fact]
    public async Task AllChunkLayoutsAreValidatedBeforePublishingAnyFile()
    {
        byte[] first = "ok"u8.ToArray();
        byte[] second = "bad!"u8.ToArray();
        var manifest = new SteamDepotManifest(
            123,
            [
                new SteamDepotFile(
                    "first.dat",
                    first.Length,
                    Convert.ToHexString(SHA1.HashData(first)),
                    [new SteamDepotChunk("chunk-0", 0, first.Length)]),
                new SteamDepotFile(
                    "second.dat",
                    second.Length,
                    Convert.ToHexString(SHA1.HashData(second)),
                    [
                        new SteamDepotChunk("chunk-1", 0, 3),
                        new SteamDepotChunk("chunk-2", 2, 2)
                    ])
            ]);
        var source = new MemoryContentClient(
            manifest,
            first,
            "bad"u8.ToArray(),
            "d!"u8.ToArray());
        var downloader = new SteamDepotDownloadService(source);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => downloader.DownloadAsync(new SteamDownloadRequest(_staging, 123)));

        Assert.Equal(0, source.DownloadedChunkCount);
        Assert.False(File.Exists(Path.Combine(_staging, "first.dat")));
        Assert.False(File.Exists(Path.Combine(_staging, "second.dat")));
    }

    [Fact]
    public async Task ChunkPastFileEndIsRejectedBeforeDownload()
    {
        byte[] content = "abcd"u8.ToArray();
        var manifest = new SteamDepotManifest(
            123,
            [new SteamDepotFile(
                "game.dat",
                content.Length,
                Convert.ToHexString(SHA1.HashData(content)),
                [new SteamDepotChunk("chunk-0", 3, 2)])]);
        var source = new MemoryContentClient(manifest, "de"u8.ToArray());
        var downloader = new SteamDepotDownloadService(source);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => downloader.DownloadAsync(new SteamDownloadRequest(_staging, 123)));

        Assert.Equal(0, source.DownloadedChunkCount);
        Assert.False(File.Exists(Path.Combine(_staging, "game.dat")));
    }

    private static SteamDepotManifest CreateManifest(params byte[][] chunks)
    {
        byte[] content = chunks.SelectMany(static chunk => chunk).ToArray();
        long offset = 0;
        var descriptors = chunks.Select((chunk, index) =>
        {
            var descriptor = new SteamDepotChunk($"chunk-{index}", offset, chunk.Length);
            offset += chunk.Length;
            return descriptor;
        }).ToArray();
        return new SteamDepotManifest(
            123,
            [new SteamDepotFile("game.dat", content.Length, Convert.ToHexString(SHA1.HashData(content)), descriptors)]);
    }

    private static byte[][] CreateChunks(int count) => Enumerable.Range(0, count)
        .Select(static index => new[] { (byte)index })
        .ToArray();

    private static async Task IgnoreFailureAsync(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_staging))
            Directory.Delete(_staging, recursive: true);
    }

    private class MemoryContentClient(SteamDepotManifest manifest, params byte[][] chunks) : ISteamContentDeliveryClient
    {
        private int _downloadedChunkCount;
        private readonly Dictionary<string, byte[]> _chunks = chunks
            .Select((chunk, index) => KeyValuePair.Create($"chunk-{index}", chunk))
            .ToDictionary();

        public uint AppId { get; private set; }
        public uint DepotId { get; private set; }
        public string? Branch { get; private set; }
        public ulong? RequestedManifestId { get; private set; }
        public int DownloadedChunkCount => Volatile.Read(ref _downloadedChunkCount);

        public Task<SteamDepotManifest> GetManifestAsync(
            uint appId,
            uint depotId,
            string branch,
            ulong? manifestId,
            CancellationToken cancellationToken)
        {
            AppId = appId;
            DepotId = depotId;
            Branch = branch;
            RequestedManifestId = manifestId;
            return Task.FromResult(manifest);
        }

        public virtual Task<ReadOnlyMemory<byte>> DownloadChunkAsync(
            SteamDepotChunk chunk,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _downloadedChunkCount);
            return Task.FromResult<ReadOnlyMemory<byte>>(_chunks[chunk.Id]);
        }
    }

    private sealed class ControlledContentClient : ISteamContentDeliveryClient
    {
        private readonly SteamDepotManifest _manifest;
        private readonly Dictionary<string, ChunkControl> _chunks;
        private readonly ConcurrentQueue<string> _startedChunkIds = new();
        private readonly TaskCompletionSource[] _startedCounts;
        private int _activeCount;
        private int _maxActiveCount;
        private int _startedCount;

        public ControlledContentClient(SteamDepotManifest manifest, params byte[][] chunks)
        {
            _manifest = manifest;
            _chunks = chunks
                .Select((chunk, index) => KeyValuePair.Create($"chunk-{index}", new ChunkControl(chunk)))
                .ToDictionary();
            _startedCounts = Enumerable.Range(0, chunks.Length + 1)
                .Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously))
                .ToArray();
            _startedCounts[0].TrySetResult();
        }

        public int ActiveCount => Volatile.Read(ref _activeCount);
        public int MaxActiveCount => Volatile.Read(ref _maxActiveCount);
        public int StartedCount => Volatile.Read(ref _startedCount);
        public string[] StartedChunkIds => _startedChunkIds.ToArray();

        public Task<SteamDepotManifest> GetManifestAsync(
            uint appId,
            uint depotId,
            string branch,
            ulong? manifestId,
            CancellationToken cancellationToken) => Task.FromResult(_manifest);

        public async Task<ReadOnlyMemory<byte>> DownloadChunkAsync(
            SteamDepotChunk chunk,
            CancellationToken cancellationToken)
        {
            ChunkControl control = _chunks[chunk.Id];
            int active = Interlocked.Increment(ref _activeCount);
            RecordMaxActive(active);
            _startedChunkIds.Enqueue(chunk.Id);
            int started = Interlocked.Increment(ref _startedCount);
            _startedCounts[started].TrySetResult();

            try
            {
                return await control.Completion.Task;
            }
            finally
            {
                Interlocked.Decrement(ref _activeCount);
                control.Finished.TrySetResult();
            }
        }

        public Task WaitForStartedAsync(int count) => StartedCount >= count
            ? Task.CompletedTask
            : _startedCounts[count].Task;

        public Task WaitForFinishedAsync(string chunkId) => _chunks[chunkId].Finished.Task;

        public void ReleaseChunk(string chunkId) =>
            _chunks[chunkId].Completion.TrySetResult(_chunks[chunkId].Bytes);

        public void FailChunk(string chunkId, Exception exception) =>
            _chunks[chunkId].Completion.TrySetException(exception);

        public void ReleaseStarted()
        {
            foreach (string chunkId in StartedChunkIds)
                ReleaseChunk(chunkId);
        }

        public void ReleaseAll()
        {
            foreach (string chunkId in _chunks.Keys)
                ReleaseChunk(chunkId);
        }

        private void RecordMaxActive(int active)
        {
            int current = Volatile.Read(ref _maxActiveCount);
            while (active > current)
            {
                int observed = Interlocked.CompareExchange(ref _maxActiveCount, active, current);
                if (observed == current)
                    return;
                current = observed;
            }
        }

        private sealed class ChunkControl(byte[] bytes)
        {
            public ReadOnlyMemory<byte> Bytes { get; } = bytes;
            public TaskCompletionSource<ReadOnlyMemory<byte>> Completion { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource Finished { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}

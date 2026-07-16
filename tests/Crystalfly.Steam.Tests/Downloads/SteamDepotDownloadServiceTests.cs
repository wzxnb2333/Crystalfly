using System.Security.Cryptography;
using Crystalfly.Steam.Downloads;

namespace Crystalfly.Steam.Tests.Downloads;

public sealed class SteamDepotDownloadServiceTests : IDisposable
{
    private readonly string _staging = Path.Combine(Path.GetTempPath(), $"crystalfly-depot-{Guid.NewGuid():N}");

    [Fact]
    public async Task CancellationWaitsForCurrentChunkBoundary()
    {
        byte[] first = "abc"u8.ToArray();
        byte[] second = "def"u8.ToArray();
        var source = new BlockingContentClient(CreateManifest(first, second), first, second);
        using var cancellation = new CancellationTokenSource();
        var downloader = new SteamDepotDownloadService(source);

        Task download = downloader.DownloadAsync(new SteamDownloadRequest(_staging, 123), cancellation.Token);
        await source.FirstChunkStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        Assert.False(download.IsCompleted);
        source.ReleaseFirstChunk.TrySetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => download);
        Assert.Equal(1, source.DownloadedChunkCount);
        Assert.False(File.Exists(Path.Combine(_staging, "game.dat")));
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

    public void Dispose()
    {
        if (Directory.Exists(_staging))
            Directory.Delete(_staging, recursive: true);
    }

    private class MemoryContentClient(SteamDepotManifest manifest, params byte[][] chunks) : ISteamContentDeliveryClient
    {
        private readonly Dictionary<string, byte[]> _chunks = chunks
            .Select((chunk, index) => KeyValuePair.Create($"chunk-{index}", chunk))
            .ToDictionary();

        public uint AppId { get; private set; }
        public uint DepotId { get; private set; }
        public string? Branch { get; private set; }
        public ulong? RequestedManifestId { get; private set; }
        public int DownloadedChunkCount { get; protected set; }

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
            DownloadedChunkCount++;
            return Task.FromResult<ReadOnlyMemory<byte>>(_chunks[chunk.Id]);
        }
    }

    private sealed class BlockingContentClient(SteamDepotManifest manifest, params byte[][] chunks)
        : MemoryContentClient(manifest, chunks)
    {
        public TaskCompletionSource FirstChunkStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirstChunk { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task<ReadOnlyMemory<byte>> DownloadChunkAsync(
            SteamDepotChunk chunk,
            CancellationToken cancellationToken)
        {
            if (DownloadedChunkCount == 0)
            {
                FirstChunkStarted.TrySetResult();
                await ReleaseFirstChunk.Task;
            }

            return await base.DownloadChunkAsync(chunk, cancellationToken);
        }
    }
}

using System.Security.Cryptography;
using Crystalfly.Steam.Downloads;

namespace Crystalfly.Steam.Tests.Downloads;

public sealed class SteamChunkCacheTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        $"crystalfly-steam-chunks-{Guid.NewGuid():N}");

    [Fact]
    public async Task VerifiedChunkIsReusedWithoutAnotherDownload()
    {
        byte[] content = "cached chunk"u8.ToArray();
        SteamDepotChunk chunk = Chunk(content);
        var cache = new SteamChunkCache(root);
        var downloads = 0;

        SteamChunkCacheResult first = await cache.GetOrDownloadAsync(
            chunk,
            _ =>
            {
                downloads++;
                return Task.FromResult<ReadOnlyMemory<byte>>(content);
            },
            CancellationToken.None);
        SteamChunkCacheResult second = await cache.GetOrDownloadAsync(
            chunk,
            _ =>
            {
                downloads++;
                return Task.FromResult<ReadOnlyMemory<byte>>(content);
            },
            CancellationToken.None);

        Assert.False(first.FromCache);
        Assert.True(second.FromCache);
        Assert.Equal(content, second.Bytes.ToArray());
        Assert.Equal(1, downloads);
    }

    [Fact]
    public async Task CorruptChunkIsDeletedAndDownloadedAgain()
    {
        byte[] content = "healthy"u8.ToArray();
        SteamDepotChunk chunk = Chunk(content);
        var cache = new SteamChunkCache(root);
        await cache.GetOrDownloadAsync(
            chunk,
            _ => Task.FromResult<ReadOnlyMemory<byte>>(content),
            CancellationToken.None);
        await File.WriteAllTextAsync(cache.GetCachePath(chunk.Id), "broken");
        var downloads = 0;

        SteamChunkCacheResult result = await cache.GetOrDownloadAsync(
            chunk,
            _ =>
            {
                downloads++;
                return Task.FromResult<ReadOnlyMemory<byte>>(content);
            },
            CancellationToken.None);

        Assert.False(result.FromCache);
        Assert.Equal(content, result.Bytes.ToArray());
        Assert.Equal(1, downloads);
    }

    [Fact]
    public async Task PruneRemovesLeastRecentlyUsedChunksUntilUnderLimit()
    {
        byte[] oldest = "old"u8.ToArray();
        byte[] newest = "new"u8.ToArray();
        var cache = new SteamChunkCache(root, maximumBytes: newest.Length);
        SteamDepotChunk oldestChunk = Chunk(oldest);
        SteamDepotChunk newestChunk = Chunk(newest);
        await cache.GetOrDownloadAsync(
            oldestChunk,
            _ => Task.FromResult<ReadOnlyMemory<byte>>(oldest),
            CancellationToken.None);
        File.SetLastWriteTimeUtc(cache.GetCachePath(oldestChunk.Id), DateTime.UtcNow.AddMinutes(-1));
        await cache.GetOrDownloadAsync(
            newestChunk,
            _ => Task.FromResult<ReadOnlyMemory<byte>>(newest),
            CancellationToken.None);

        await cache.PruneAsync(CancellationToken.None);
        SteamChunkCacheSnapshot snapshot = await cache.GetSnapshotAsync(CancellationToken.None);

        Assert.False(File.Exists(cache.GetCachePath(oldestChunk.Id)));
        Assert.True(File.Exists(cache.GetCachePath(newestChunk.Id)));
        Assert.Equal((newest.Length, 1), (snapshot.SizeBytes, snapshot.EntryCount));
    }

    [Fact]
    public async Task ClearRemovesChunksButKeepsCacheRoot()
    {
        byte[] content = "clear me"u8.ToArray();
        var cache = new SteamChunkCache(root);
        await cache.GetOrDownloadAsync(
            Chunk(content),
            _ => Task.FromResult<ReadOnlyMemory<byte>>(content),
            CancellationToken.None);

        await cache.ClearAsync(CancellationToken.None);
        SteamChunkCacheSnapshot snapshot = await cache.GetSnapshotAsync(CancellationToken.None);

        Assert.True(Directory.Exists(root));
        Assert.Equal((0L, 0), (snapshot.SizeBytes, snapshot.EntryCount));
    }

    [Fact]
    public async Task InvalidChunkIdIsRejectedBeforeDownload()
    {
        var cache = new SteamChunkCache(root);
        var downloads = 0;

        await Assert.ThrowsAsync<InvalidDataException>(() => cache.GetOrDownloadAsync(
            new SteamDepotChunk("../outside", 0, 1),
            _ =>
            {
                downloads++;
                return Task.FromResult<ReadOnlyMemory<byte>>(new byte[] { 1 });
            },
            CancellationToken.None));

        Assert.Equal(0, downloads);
    }

    private static SteamDepotChunk Chunk(byte[] content) => new(
        Convert.ToHexString(SHA1.HashData(content)),
        0,
        content.Length);

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}

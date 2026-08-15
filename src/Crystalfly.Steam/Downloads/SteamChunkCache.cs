using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Crystalfly.Steam.Downloads;

public sealed record SteamChunkCacheSnapshot(long SizeBytes, int EntryCount, long MaximumBytes);

internal sealed record SteamChunkCacheResult(ReadOnlyMemory<byte> Bytes, bool FromCache);

public sealed class SteamChunkCache
{
    public const long DefaultMaximumBytes = 20L * 1024 * 1024 * 1024;

    private readonly ConcurrentDictionary<string, SemaphoreSlim> gates =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly string root;
    private readonly long maximumBytes;

    public SteamChunkCache(string root, long maximumBytes = DefaultMaximumBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        this.root = Path.GetFullPath(root);
        this.maximumBytes = maximumBytes;
    }

    public async Task<SteamChunkCacheSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureRootIsSafe();
        if (!Directory.Exists(root))
            return new SteamChunkCacheSnapshot(0, 0, maximumBytes);

        return await Task.Run(() =>
        {
            long size = 0;
            int count = 0;
            foreach (string path in EnumerateChunks())
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    size = checked(size + new FileInfo(path).Length);
                    count++;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                }
            }
            return new SteamChunkCacheSnapshot(size, count, maximumBytes);
        }, cancellationToken);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        EnsureRootIsSafe();
        await Task.Run(() =>
        {
            if (Directory.Exists(root))
                DeleteContents(root, cancellationToken);
            Directory.CreateDirectory(root);
        }, cancellationToken);
    }

    internal async Task<SteamChunkCacheResult> GetOrDownloadAsync(
        SteamDepotChunk chunk,
        Func<CancellationToken, Task<ReadOnlyMemory<byte>>> download,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(download);
        byte[] expectedHash = ParseChunkId(chunk.Id);
        string path = GetCachePath(chunk.Id);
        SemaphoreSlim gate = gates.GetOrAdd(path, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            ReadOnlyMemory<byte>? cached = await TryReadAsync(
                path,
                chunk.UncompressedLength,
                expectedHash,
                cancellationToken);
            if (cached is not null)
            {
                TryTouch(path);
                return new SteamChunkCacheResult(cached.Value, FromCache: true);
            }

            ReadOnlyMemory<byte> bytes = await download(cancellationToken);
            Validate(bytes.Span, chunk.UncompressedLength, expectedHash);
            await WriteAsync(path, bytes, CancellationToken.None);
            return new SteamChunkCacheResult(bytes, FromCache: false);
        }
        finally
        {
            gate.Release();
        }
    }

    internal async Task PruneAsync(CancellationToken cancellationToken = default)
    {
        EnsureRootIsSafe();
        if (!Directory.Exists(root))
            return;

        var entries = await Task.Run(() => EnumerateChunks()
            .Select(path => new FileInfo(path))
            .OrderBy(static file => file.LastWriteTimeUtc)
            .ToArray(), cancellationToken);
        long total = 0;
        foreach (FileInfo entry in entries)
            total = checked(total + entry.Length);

        foreach (FileInfo entry in entries)
        {
            if (total <= maximumBytes)
                break;
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                long length = entry.Length;
                entry.Delete();
                total -= length;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    internal string GetCachePath(string chunkId)
    {
        string normalized = Convert.ToHexString(ParseChunkId(chunkId));
        EnsureRootIsSafe();
        string bucket = Path.Combine(root, normalized[..2]);
        EnsureNotReparsePoint(bucket);
        return Path.Combine(bucket, $"{normalized}.chunk");
    }

    private static async Task<ReadOnlyMemory<byte>?> TryReadAsync(
        string path,
        int expectedLength,
        byte[] expectedHash,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            Validate(bytes, expectedLength, expectedHash);
            return bytes;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException)
        {
            TryDelete(path);
            return null;
        }
    }

    private static async Task WriteAsync(
        string path,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var output = new FileStream(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                131072,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await output.WriteAsync(bytes, cancellationToken);
                await output.FlushAsync(cancellationToken);
                output.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    private static void Validate(ReadOnlySpan<byte> bytes, int expectedLength, byte[] expectedHash)
    {
        if (bytes.Length != expectedLength)
            throw new InvalidDataException("Steam chunk length does not match its manifest entry.");
        byte[] actualHash = SHA1.HashData(bytes);
        if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
            throw new InvalidDataException("Steam chunk SHA-1 does not match its manifest ID.");
    }

    private static byte[] ParseChunkId(string chunkId)
    {
        if (chunkId is null || chunkId.Length != 40)
            throw new InvalidDataException("Steam chunk ID must be a SHA-1 hash.");
        try
        {
            return Convert.FromHexString(chunkId);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("Steam chunk ID must be a SHA-1 hash.", exception);
        }
    }

    private IEnumerable<string> EnumerateChunks() => Directory.EnumerateFiles(
        root,
        "*.chunk",
        new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        });

    private void EnsureRootIsSafe()
    {
        string? parent = Path.GetDirectoryName(root);
        if (parent is not null)
            EnsureNotReparsePoint(parent);
        EnsureNotReparsePoint(root);
    }

    private static void EnsureNotReparsePoint(string path)
    {
        if (Directory.Exists(path)
            && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Steam chunk cache path cannot be a reparse point: {path}");
        }
    }

    private static void DeleteContents(string directory, CancellationToken cancellationToken)
    {
        foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileAttributes attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.Directory) == 0)
            {
                File.Delete(entry);
                continue;
            }
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                Directory.Delete(entry);
                continue;
            }
            DeleteContents(entry, cancellationToken);
            Directory.Delete(entry);
        }
    }

    private static void TryTouch(string path)
    {
        try
        {
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}

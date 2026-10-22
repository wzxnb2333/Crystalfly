using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Avalonia.Media.Imaging;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Thunderstore;

namespace Crystalfly.App.Services;

public sealed class ThunderstorePackageIconLoader(HttpClient client, string cacheRoot, Func<bool> isOffline)
{
    private const int MaximumBytes = 1024 * 1024;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(12);
    private readonly string fullCacheRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(cacheRoot));
    private readonly SemaphoreSlim requests = new(4, 4);

    public async Task<Bitmap?> LoadAsync(string iconUrl, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(iconUrl, UriKind.Absolute, out var uri)) return null;
        try { uri = ThunderstoreCatalogSource.TrustedUri(uri.AbsoluteUri); }
        catch (InvalidDataException) { return null; }

        var cachePath = TryCachePath(uri);
        if (cachePath is not null && await ReadCacheAsync(cachePath, cancellationToken) is { } cached)
            return cached;
        if (isOffline()) return null;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);
        try
        {
            await requests.WaitAsync(timeout.Token);
            try
            {
                if (isOffline()) return null;
                if (cachePath is not null && await ReadCacheAsync(cachePath, timeout.Token) is { } cachedAfterGate)
                    return cachedAfterGate;

                using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                _ = ThunderstoreCatalogSource.TrustedUri(response.RequestMessage?.RequestUri?.AbsoluteUri ?? uri.AbsoluteUri);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > MaximumBytes) return null;
                await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
                var bytes = await ReadBoundedAsync(stream, timeout.Token);
                var bitmap = Decode(bytes);
                if (bitmap is null) return null;
                try
                {
                    if (cachePath is not null) await WriteCacheAsync(cachePath, bytes, timeout.Token);
                }
                catch
                {
                    bitmap.Dispose();
                    throw;
                }
                return bitmap;
            }
            finally { requests.Release(); }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception error) when (error is OperationCanceledException or HttpRequestException or IOException
            or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private string? TryCachePath(Uri uri)
    {
        try
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri)));
            var path = Path.GetFullPath(Path.Combine(fullCacheRoot, hash + ".png"));
            if (!path.StartsWith(fullCacheRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return null;
            InstanceDirectory.RejectReparseAncestors(path);
            return path;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException) { return null; }
    }

    private static async Task<Bitmap?> ReadCacheAsync(string path, CancellationToken token)
    {
        if (!File.Exists(path)) return null;
        byte[] bytes;
        try
        {
            InstanceDirectory.RejectReparseAncestors(path);
            await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            bytes = await ReadBoundedAsync(input, token);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException) { return null; }

        var bitmap = Decode(bytes);
        if (bitmap is not null) return bitmap;
        try
        {
            InstanceDirectory.RejectReparseAncestors(path);
            File.Delete(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException) { }
        return null;
    }

    private static async Task WriteCacheAsync(string path, byte[] bytes, CancellationToken token)
    {
        var directory = Path.GetDirectoryName(path)!;
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".part";
        try
        {
            InstanceDirectory.RejectReparseAncestors(path);
            Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(temporary, bytes, token);
            InstanceDirectory.RejectReparseAncestors(path);
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException) { }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException) { }
        }
    }

    private static Bitmap? Decode(byte[] bytes)
    {
        if (bytes.Length is < 24 or > MaximumBytes
            || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            || BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4)) != 256
            || BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4)) != 256)
            return null;
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            var bitmap = new Bitmap(stream);
            if (bitmap.PixelSize.Width == 256 && bitmap.PixelSize.Height == 256) return bitmap;
            bitmap.Dispose();
        }
        catch (Exception error) when (error is not OutOfMemoryException) { }
        return null;
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, CancellationToken token)
    {
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(), token)) > 0)
        {
            if (output.Length + read > MaximumBytes) throw new InvalidDataException("Package icon exceeds the size limit.");
            await output.WriteAsync(buffer.AsMemory(0, read), token);
        }
        return output.ToArray();
    }
}

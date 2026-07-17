using System.Text.Json;

namespace Crystalfly.Core.Serialization;

public static class AtomicJsonStore
{
    public static async Task WriteAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken = default)
    {
        var targetPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(targetPath)
            ?? throw new ArgumentException("Path must include a directory.", nameof(path));
        Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, value, CrystalflyJson.Options, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            if (File.Exists(targetPath))
            {
                File.Replace(temporaryPath, targetPath, targetPath + ".bak", ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, targetPath);
            }
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    public static async Task<T> ReadAsync<T>(
        string path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await ReadFileAsync<T>(path, cancellationToken);
        }
        catch (JsonException) when (File.Exists(path + ".bak"))
        {
            return await ReadFileAsync<T>(path + ".bak", cancellationToken);
        }
    }

    private static async Task<T> ReadFileAsync<T>(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await JsonSerializer.DeserializeAsync<T>(stream, CrystalflyJson.Options, cancellationToken)
            ?? throw new JsonException($"JSON did not contain a {typeof(T).Name} value.");
    }
}

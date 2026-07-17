using System.Text;

namespace Crystalfly.Core.Logs;

public sealed record InstanceLogFile(string Name, string Path, long SizeBytes, DateTimeOffset LastWriteTime);

public static class InstanceLogService
{
    private const int DefaultMaxBytes = 128 * 1024;

    public static IReadOnlyList<InstanceLogFile> Discover(string instanceRoot, string sharedLocalLowPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedLocalLowPath);

        string instance = Path.GetFullPath(instanceRoot);
        string localLow = Path.GetFullPath(sharedLocalLowPath);
        (string Name, string Path)[] candidates =
        [
            ("BepInEx", Path.Combine(instance, "BepInEx", "LogOutput.log")),
            ("Modding API", Path.Combine(instance, "hollow_knight_Data", "Managed", "Mods", "ModLog.txt")),
            ("Modding API", Path.Combine(instance, "hollow_knight_Data", "Managed", "ModLog.txt")),
            ("Player.log", Path.Combine(localLow, "Player.log")),
            ("Modding API", Path.Combine(localLow, "ModLog.txt")),
            ("Modding API", Path.Combine(localLow, "ModLog.log"))
        ];

        return candidates
            .Where(candidate => File.Exists(candidate.Path))
            .DistinctBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
            .Select(candidate =>
            {
                var file = new FileInfo(candidate.Path);
                return new InstanceLogFile(
                    candidate.Name,
                    file.FullName,
                    file.Length,
                    file.LastWriteTimeUtc);
            })
            .ToArray();
    }

    public static async Task<string> ReadTailAsync(
        string path,
        int maxBytes = DefaultMaxBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        long start = Math.Max(0, stream.Length - maxBytes);
        bool discardPartialLine = false;
        if (start > 0)
        {
            stream.Position = start - 1;
            discardPartialLine = stream.ReadByte() != '\n';
        }
        stream.Position = start;

        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: false);
        if (discardPartialLine)
        {
            _ = await reader.ReadLineAsync(cancellationToken);
        }
        return await reader.ReadToEndAsync(cancellationToken);
    }
}

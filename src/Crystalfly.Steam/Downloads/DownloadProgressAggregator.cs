namespace Crystalfly.Steam.Downloads;

public sealed class DownloadProgressAggregator
{
    private static readonly TimeSpan SpeedWindow = TimeSpan.FromSeconds(3);
    private readonly long _totalBytes;
    private readonly Action<SteamDownloadProgress>? _report;
    private readonly TimeProvider _timeProvider;
    private readonly List<(long Timestamp, long Bytes)> _samples = [];
    private readonly object _sync = new();
    private long _completedBytes;

    public DownloadProgressAggregator(long totalBytes, Action<SteamDownloadProgress>? report = null)
        : this(totalBytes, report, TimeProvider.System)
    {
    }

    internal DownloadProgressAggregator(
        long totalBytes,
        Action<SteamDownloadProgress>? report,
        TimeProvider timeProvider)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalBytes);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _totalBytes = totalBytes;
        _report = report;
        _timeProvider = timeProvider;
        _samples.Add((_timeProvider.GetTimestamp(), 0));
    }

    public SteamDownloadProgress CompleteChunk(int byteCount, string currentFile)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(byteCount);
        lock (_sync)
        {
            long completed = checked(_completedBytes + byteCount);
            if (completed > _totalBytes)
                throw new InvalidDataException("Downloaded bytes exceed the manifest size.");

            _completedBytes = completed;
            long now = _timeProvider.GetTimestamp();
            _samples.Add((now, completed));
            long cutoff = now - checked((long)(SpeedWindow.TotalSeconds * _timeProvider.TimestampFrequency));
            int removeCount = 0;
            while (removeCount + 1 < _samples.Count && _samples[removeCount + 1].Timestamp <= cutoff)
                removeCount++;
            if (removeCount > 0)
                _samples.RemoveRange(0, removeCount);

            (long timestamp, long bytes) = _samples[0];
            double elapsedSeconds = _timeProvider.GetElapsedTime(timestamp, now).TotalSeconds;
            double bytesPerSecond = elapsedSeconds > 0
                ? (completed - bytes) / elapsedSeconds
                : 0;
            var progress = new SteamDownloadProgress(
                completed,
                _totalBytes,
                _totalBytes == 0 ? 1 : (double)completed / _totalBytes,
                currentFile)
            {
                BytesPerSecond = bytesPerSecond
            };
            _report?.Invoke(progress);
            return progress;
        }
    }
}

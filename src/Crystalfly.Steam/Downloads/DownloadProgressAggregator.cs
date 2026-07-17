namespace Crystalfly.Steam.Downloads;

public sealed class DownloadProgressAggregator
{
    private readonly long _totalBytes;
    private readonly Action<SteamDownloadProgress>? _report;
    private long _completedBytes;

    public DownloadProgressAggregator(long totalBytes, Action<SteamDownloadProgress>? report = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalBytes);
        _totalBytes = totalBytes;
        _report = report;
    }

    public SteamDownloadProgress CompleteChunk(int byteCount, string currentFile)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(byteCount);
        long completed = checked(_completedBytes + byteCount);
        if (completed > _totalBytes)
            throw new InvalidDataException("Downloaded bytes exceed the manifest size.");

        _completedBytes = completed;
        var progress = new SteamDownloadProgress(
            completed,
            _totalBytes,
            _totalBytes == 0 ? 1 : (double)completed / _totalBytes,
            currentFile);
        _report?.Invoke(progress);
        return progress;
    }
}

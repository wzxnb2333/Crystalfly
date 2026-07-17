using Crystalfly.Steam.Downloads;

namespace Crystalfly.Steam.Tests.Downloads;

public sealed class DownloadProgressAggregatorTests
{
    [Fact]
    public void CompletedChunksAreAggregatedAcrossFiles()
    {
        var reports = new List<SteamDownloadProgress>();
        var aggregator = new DownloadProgressAggregator(300, reports.Add);

        aggregator.CompleteChunk(75, "first.dat");
        SteamDownloadProgress final = aggregator.CompleteChunk(225, "second.dat");

        Assert.Collection(
            reports,
            first => Assert.Equal((75L, 300L, 0.25, "first.dat"),
                (first.CompletedBytes, first.TotalBytes, first.Fraction, first.CurrentFile)),
            second => Assert.Equal((300L, 300L, 1d, "second.dat"),
                (second.CompletedBytes, second.TotalBytes, second.Fraction, second.CurrentFile)));
        Assert.Equal(reports[^1], final);
    }

    [Fact]
    public void ReportsSmoothedBytesPerSecondOverRecentWindow()
    {
        var clock = new ManualTimeProvider();
        var reports = new List<SteamDownloadProgress>();
        var aggregator = new DownloadProgressAggregator(1000, reports.Add, clock);
        clock.Advance(TimeSpan.FromSeconds(1));
        aggregator.CompleteChunk(100, "first.dat");
        clock.Advance(TimeSpan.FromSeconds(1));
        aggregator.CompleteChunk(200, "second.dat");
        clock.Advance(TimeSpan.FromSeconds(3));
        SteamDownloadProgress latest = aggregator.CompleteChunk(300, "third.dat");

        Assert.Equal(100d, reports[0].BytesPerSecond, precision: 3);
        Assert.Equal(150d, reports[1].BytesPerSecond, precision: 3);
        Assert.Equal(100d, latest.BytesPerSecond, precision: 3);
    }

    [Fact]
    public void ReportsZeroSpeedWhenNoTimeHasElapsed()
    {
        var aggregator = new DownloadProgressAggregator(
            100,
            report: null,
            new ManualTimeProvider());

        SteamDownloadProgress progress = aggregator.CompleteChunk(10, "first.dat");

        Assert.Equal(0d, progress.BytesPerSecond);
        Assert.False(double.IsNaN(progress.BytesPerSecond));
        Assert.False(double.IsInfinity(progress.BytesPerSecond));
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => timestamp;

        public void Advance(TimeSpan elapsed) =>
            timestamp += elapsed.Ticks;
    }
}

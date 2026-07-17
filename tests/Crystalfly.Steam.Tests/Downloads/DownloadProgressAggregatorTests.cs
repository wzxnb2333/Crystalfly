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
}

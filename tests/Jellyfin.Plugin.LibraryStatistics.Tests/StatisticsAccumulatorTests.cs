using Jellyfin.Plugin.LibraryStatistics.Models;
using Jellyfin.Plugin.LibraryStatistics.Services;
using Xunit;

namespace Jellyfin.Plugin.LibraryStatistics.Tests;

public sealed class StatisticsAccumulatorTests
{
    [Fact]
    public void ApplyTo_AggregatesCountsBytesAndBreakdowns()
    {
        var accumulator = new StatisticsAccumulator();
        accumulator.AddMedia(1_000, MediaKind.Movie);
        accumulator.AddMedia(2_000, MediaKind.Episode);
        accumulator.AddContainer("MKV", 1_000);
        accumulator.AddContainer("mkv", 2_000);
        accumulator.AddVideoCodec("HEVC", 1_000);

        var summary = new StatisticsSummary();
        accumulator.ApplyTo(summary);

        Assert.Equal(2, summary.MediaFileCount);
        Assert.Equal(3_000, summary.TotalSizeBytes);
        Assert.Equal(1, summary.MovieCount);
        Assert.Equal(1, summary.EpisodeCount);
        var container = Assert.Single(summary.Containers);
        Assert.Equal("MKV", container.Label, ignoreCase: true);
        Assert.Equal(2, container.Count);
        Assert.Equal(3_000, container.SizeBytes);
    }
}

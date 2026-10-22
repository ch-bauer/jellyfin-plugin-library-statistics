using Jellyfin.Plugin.LibraryStatistics.Services;
using Xunit;

namespace Jellyfin.Plugin.LibraryStatistics.Tests;

public sealed class GrowthTimelineTests
{
    [Fact]
    public void BuildGrowthTimeline_GroupsDaysAndAccumulatesMediaFiles()
    {
        var entries = new[]
        {
            new LibraryStatisticsService.MediaGrowthEntry(new DateTime(2026, 5, 31, 22, 0, 0, DateTimeKind.Utc), 100),
            new LibraryStatisticsService.MediaGrowthEntry(new DateTime(2026, 5, 31, 23, 0, 0, DateTimeKind.Utc), 200),
            new LibraryStatisticsService.MediaGrowthEntry(new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc), 400)
        };

        var timeline = LibraryStatisticsService.BuildGrowthTimeline(entries);

        Assert.Collection(
            timeline,
            first =>
            {
                Assert.Equal(new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc), first.DateUtc);
                Assert.Equal(300, first.CumulativeSizeBytes);
                Assert.Equal(2, first.CumulativeFileCount);
            },
            second =>
            {
                Assert.Equal(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), second.DateUtc);
                Assert.Equal(700, second.CumulativeSizeBytes);
                Assert.Equal(3, second.CumulativeFileCount);
            });
    }
}

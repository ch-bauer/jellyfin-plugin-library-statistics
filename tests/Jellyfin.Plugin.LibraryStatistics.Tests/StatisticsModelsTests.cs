using System.Text.Json;
using Jellyfin.Plugin.LibraryStatistics.Models;
using Xunit;

namespace Jellyfin.Plugin.LibraryStatistics.Tests;

public sealed class StatisticsModelsTests
{
    [Fact]
    public void DuplicateGroups_RoundTripPathsAndMediaInfo()
    {
        var expected = new StatisticsResult
        {
            DuplicateGroups =
            [
                new DuplicateGroup
                {
                    ItemId = Guid.NewGuid(),
                    MediaType = "Movie",
                    Title = "Example",
                    LibraryName = "Movies",
                    IsMergedVersion = true,
                    Versions =
                    [
                        new MediaVersionDetail
                        {
                            Name = "4K",
                            Path = @"D:\Movies\Example\Example.2160p.mkv",
                            Container = "MKV",
                            SizeBytes = 42,
                            Streams =
                            [
                                new MediaStreamDetail
                                {
                                    Type = "Video",
                                    Codec = "HEVC",
                                    Width = 3840,
                                    Height = 2160,
                                    VideoRange = "HDR10"
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var json = JsonSerializer.Serialize(expected, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var actual = JsonSerializer.Deserialize<StatisticsResult>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var group = Assert.Single(actual!.DuplicateGroups);
        var version = Assert.Single(group.Versions);
        var stream = Assert.Single(version.Streams);
        Assert.Equal(expected.DuplicateGroups[0].ItemId, group.ItemId);
        Assert.True(group.IsMergedVersion);
        Assert.Equal(@"D:\Movies\Example\Example.2160p.mkv", version.Path);
        Assert.Equal("HEVC", stream.Codec);
        Assert.Equal(3840, stream.Width);
        Assert.Equal("HDR10", stream.VideoRange);
    }

    [Fact]
    public void TrendsAndInsights_RoundTripTimelineAndSeriesTotals()
    {
        var expected = new StatisticsResult
        {
            GrowthTimeline =
            [
                new GrowthPoint
                {
                    DateUtc = new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc),
                    CumulativeSizeBytes = 4_096,
                    CumulativeFileCount = 3
                }
            ],
            LargestSeries =
            [
                new MediaInsight
                {
                    ItemId = Guid.NewGuid(),
                    MediaType = "Series",
                    Title = "Example series",
                    LibraryName = "Shows",
                    SizeBytes = 4_096,
                    FileCount = 3,
                    Path = "/media/shows/example/s01e01.mkv",
                    DateCreatedUtc = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc)
                }
            ]
        };

        var json = JsonSerializer.Serialize(expected, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var actual = JsonSerializer.Deserialize<StatisticsResult>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var point = Assert.Single(actual!.GrowthTimeline);
        var series = Assert.Single(actual.LargestSeries);
        Assert.Equal(4_096, point.CumulativeSizeBytes);
        Assert.Equal(3, point.CumulativeFileCount);
        Assert.Equal("Example series", series.Title);
        Assert.Equal(3, series.FileCount);
    }
}

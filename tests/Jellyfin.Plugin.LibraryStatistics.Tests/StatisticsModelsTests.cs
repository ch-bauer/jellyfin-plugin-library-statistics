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
            Total = new StatisticsSummary
            {
                TotalRunTimeTicks = TimeSpan.FromDays(365).Ticks
            },
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
        Assert.Equal(TimeSpan.FromDays(365).Ticks, actual.Total.TotalRunTimeTicks);
        Assert.Equal(4_096, point.CumulativeSizeBytes);
        Assert.Equal(3, point.CumulativeFileCount);
        Assert.Equal("Example series", series.Title);
        Assert.Equal(3, series.FileCount);
    }

    [Fact]
    public void LanguageFiles_RoundTripPerVideoLanguageSets()
    {
        var expected = new StatisticsResult
        {
            LanguageFiles =
            [
                new MediaLanguageDetail
                {
                    Title = "Example · S01E02 · Episode",
                    Path = "/media/example/s01e02.mkv",
                    LibraryName = "Shows",
                    MediaType = "Episode",
                    OriginalLanguage = "JPN",
                    AuditGroupKey = "season:example:1",
                    AuditGroupTitle = "Example — Season 1",
                    AuditGroupKind = "Season",
                    AuditParentGroupKey = "series:example",
                    AuditParentGroupTitle = "Example",
                    AuditParentGroupKind = "Series",
                    AudioLanguages = ["DEU", "ENG", "Unknown"],
                    SubtitleLanguages = ["ENG"]
                }
            ]
        };

        var json = JsonSerializer.Serialize(expected, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var actual = JsonSerializer.Deserialize<StatisticsResult>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var file = Assert.Single(actual!.LanguageFiles);
        Assert.Equal("/media/example/s01e02.mkv", file.Path);
        Assert.Equal("Episode", file.MediaType);
        Assert.Equal("JPN", file.OriginalLanguage);
        Assert.Equal("Example — Season 1", file.AuditGroupTitle);
        Assert.Equal("Example", file.AuditParentGroupTitle);
        Assert.Equal(["DEU", "ENG", "Unknown"], file.AudioLanguages);
        Assert.Equal(["ENG"], file.SubtitleLanguages);
    }

    [Fact]
    public void AvailabilityItems_RoundTripAllIndexedQualities()
    {
        var expected = new StatisticsResult
        {
            AvailabilityItems =
            [
                new MediaAvailabilityDetail
                {
                    ReviewKey = "review-key",
                    Title = "Example",
                    LibraryName = "Movies",
                    MediaType = "Movie",
                    AuditGroupKey = "collection:example",
                    AuditGroupTitle = "Example Collection",
                    AuditGroupKind = "Collection",
                    AuditParentGroupKey = string.Empty,
                    IsOnly4K = false,
                    Versions =
                    [
                        new MediaAvailabilityVersion { Path = "/media/example-4k.mkv", Resolution = "4K" },
                        new MediaAvailabilityVersion { Path = "/media/example-720p.mkv", Resolution = "720p" }
                    ]
                }
            ]
        };

        string json = JsonSerializer.Serialize(expected, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        StatisticsResult? actual = JsonSerializer.Deserialize<StatisticsResult>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        MediaAvailabilityDetail item = Assert.Single(actual!.AvailabilityItems);
        Assert.Equal("review-key", item.ReviewKey);
        Assert.Equal("Example Collection", item.AuditGroupTitle);
        Assert.Equal(["4K", "720p"], item.Versions.Select(version => version.Resolution));
        Assert.False(item.IsOnly4K);
    }
}

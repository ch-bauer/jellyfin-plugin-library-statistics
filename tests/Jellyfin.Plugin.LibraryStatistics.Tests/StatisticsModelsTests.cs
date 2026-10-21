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
        Assert.Equal(@"D:\Movies\Example\Example.2160p.mkv", version.Path);
        Assert.Equal("HEVC", stream.Codec);
        Assert.Equal(3840, stream.Width);
        Assert.Equal("HDR10", stream.VideoRange);
    }
}

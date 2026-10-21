using Jellyfin.Plugin.LibraryStatistics.Services;
using Xunit;

namespace Jellyfin.Plugin.LibraryStatistics.Tests;

public sealed class MediaClassificationTests
{
    [Theory]
    [InlineData(7680, 4320, "8K")]
    [InlineData(3840, 1600, "4K")]
    [InlineData(1920, 800, "1080p")]
    [InlineData(1280, 720, "720p")]
    [InlineData(1024, 576, "576p")]
    [InlineData(720, 480, "480p")]
    [InlineData(640, 360, "SD")]
    [InlineData(null, null, "Unknown")]
    public void ResolutionTier_ClassifiesCommonDimensions(int? width, int? height, string expected)
    {
        Assert.Equal(expected, MediaClassification.ResolutionTier(width, height));
    }

    [Theory]
    [InlineData("movie.mkv", "MKV")]
    [InlineData("/media/Movie.Name.2026.mp4", "MP4")]
    [InlineData("no-extension", "Unknown")]
    public void ContainerFromPath_NormalizesExtensions(string path, string expected)
    {
        Assert.Equal(expected, MediaClassification.ContainerFromPath(path));
    }

    [Theory]
    [InlineData("DolbyVision", "Dolby Vision")]
    [InlineData("HDR10", "HDR10")]
    [InlineData("Unknown", "Unknown")]
    [InlineData(null, "Unknown")]
    public void DynamicRange_NormalizesDisplayLabels(string? value, string expected)
    {
        Assert.Equal(expected, MediaClassification.DynamicRange(value));
    }

    [Theory]
    [InlineData("h265", "HEVC")]
    [InlineData("avc", "H.264")]
    [InlineData("av1", "AV1")]
    public void VideoCodec_NormalizesAliases(string value, string expected)
    {
        Assert.Equal(expected, MediaClassification.VideoCodec(value));
    }

    [Theory]
    [InlineData(1_000_000, "< 2 Mbps")]
    [InlineData(6_000_000, "4–8 Mbps")]
    [InlineData(70_000_000, "> 60 Mbps")]
    public void VideoBitrateTier_GroupsRates(int bitrate, string expected)
    {
        Assert.Equal(expected, MediaClassification.VideoBitrateTier(bitrate, 0, null));
    }
}

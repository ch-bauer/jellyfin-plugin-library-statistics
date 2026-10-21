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
    [InlineData(720, 576, "SD")]
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
}

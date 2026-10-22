using Jellyfin.Plugin.LibraryStatistics.Models;
using Jellyfin.Plugin.LibraryStatistics.Services;
using Xunit;

namespace Jellyfin.Plugin.LibraryStatistics.Tests;

public sealed class DuplicateReviewClassifierTests
{
    [Fact]
    public void AutoAllow_RequiresExactlyOne4KAndOne1080PVersion()
    {
        MediaVersionDetail fullHd = Version(1920, 1080, "/media/full-hd.mkv");
        MediaVersionDetail ultraHd = Version(3840, 2160, "/media/ultra-hd.mkv");
        MediaVersionDetail secondFullHd = Version(1920, 1080, "/media/second-full-hd.mkv");

        Assert.True(DuplicateReviewClassifier.IsExactly4KAnd1080P([fullHd, ultraHd]));
        Assert.False(DuplicateReviewClassifier.IsExactly4KAnd1080P([fullHd, ultraHd, secondFullHd]));
        Assert.False(DuplicateReviewClassifier.IsExactly4KAnd1080P([fullHd, secondFullHd]));
    }

    [Theory]
    [InlineData("Der Wurm (1) / Brennendes Inferno (2)")]
    [InlineData("Part 1 and Part 2")]
    public void AmbiguousEpisode_DetectsMultipartMetadataTitles(string title)
    {
        Assert.True(DuplicateReviewClassifier.IsAmbiguousEpisode(title, null));
    }

    [Fact]
    public void VersionSignature_IsOrderIndependentButChangesWithVersionSet()
    {
        MediaVersionDetail first = Version(1920, 1080, "/media/a.mkv");
        MediaVersionDetail second = Version(3840, 2160, "/media/b.mkv");
        Assert.Equal(
            DuplicateReviewClassifier.VersionSignature([first, second]),
            DuplicateReviewClassifier.VersionSignature([second, first]));
        Assert.NotEqual(
            DuplicateReviewClassifier.VersionSignature([first]),
            DuplicateReviewClassifier.VersionSignature([first, second]));
    }

    private static MediaVersionDetail Version(int width, int height, string path) => new()
    {
        Path = path,
        Streams = [new MediaStreamDetail { Type = "Video", Width = width, Height = height }]
    };
}

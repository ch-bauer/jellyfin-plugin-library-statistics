using Jellyfin.Plugin.LibraryStatistics.Services;
using Xunit;

namespace Jellyfin.Plugin.LibraryStatistics.Tests;

public sealed class MediaSourceIdentityTests
{
    [Fact]
    public void SourceSetKey_IgnoresSourceOrderCaseAndSlashStyle()
    {
        string first = MediaSourceIdentity.SourceSetKey(
            "Movie",
            [@"/media/Rio 2/Rio 2 - 1080P.mkv", @"/media/Rio 2/Rio 2.mkv"]);
        string second = MediaSourceIdentity.SourceSetKey(
            "movie",
            [@"\media\rio 2\rio 2.mkv", @"\media\rio 2\rio 2 - 1080p.mkv"]);

        Assert.Equal(first, second);
    }

    [Fact]
    public void SourceSetKey_SeparatesDifferentVersionSets()
    {
        string first = MediaSourceIdentity.SourceSetKey("Movie", ["/media/movie.mkv", "/media/movie-4k.mkv"]);
        string second = MediaSourceIdentity.SourceSetKey("Movie", ["/media/movie.mkv", "/media/movie-1080p.mkv"]);

        Assert.NotEqual(first, second);
    }
}

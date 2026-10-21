using Jellyfin.Plugin.LibraryStatistics.Services;
using Xunit;

namespace Jellyfin.Plugin.LibraryStatistics.Tests;

public sealed class DuplicateIdentityTests
{
    [Fact]
    public void MovieKey_PrefersProviderIdentityAcrossDifferentTitles()
    {
        var first = DuplicateIdentity.MovieKey("Alien", 1979, new Dictionary<string, string> { ["Imdb"] = "tt0078748" });
        var second = DuplicateIdentity.MovieKey("Alien - Director's Cut", 2003, new Dictionary<string, string> { ["IMDB"] = "TT0078748" });

        Assert.Equal(first, second);
    }

    [Fact]
    public void MovieKey_FallsBackToNormalizedTitleAndYear()
    {
        Assert.Equal(
            DuplicateIdentity.MovieKey("Blade Runner", 1982, null),
            DuplicateIdentity.MovieKey("Blade-Runner!", 1982, null));
        Assert.NotEqual(
            DuplicateIdentity.MovieKey("Blade Runner", 1982, null),
            DuplicateIdentity.MovieKey("Blade Runner", 2049, null));
    }

    [Fact]
    public void EpisodeKey_UsesSeriesAndEpisodeCoordinatesWithoutProviderIds()
    {
        Assert.Equal(
            DuplicateIdentity.EpisodeKey("Dark", 1, 1, "Secrets", null, null),
            DuplicateIdentity.EpisodeKey("DARK", 1, 1, "Secrets", null, null));
        Assert.NotEqual(
            DuplicateIdentity.EpisodeKey("Dark", 1, 1, "Secrets", null, null),
            DuplicateIdentity.EpisodeKey("Dark", 1, 2, "Lies", null, null));
    }
}

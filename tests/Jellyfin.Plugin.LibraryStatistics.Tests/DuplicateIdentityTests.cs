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

    [Fact]
    public void EpisodeKey_DoesNotCollapseDifferentEpisodesWithSharedProviderId()
    {
        var sharedEpisodeIds = new Dictionary<string, string> { ["Imdb"] = "tt0206512" };
        var seriesIds = new Dictionary<string, string> { ["Imdb"] = "tt0206512" };

        var episodeTwo = DuplicateIdentity.EpisodeKey(
            "SpongeBob SquarePants", 3, 2, "Episode 2", sharedEpisodeIds, seriesIds);
        var episodeTwentyTwo = DuplicateIdentity.EpisodeKey(
            "SpongeBob SquarePants", 3, 22, "Episode 22", sharedEpisodeIds, seriesIds);

        Assert.NotEqual(episodeTwo, episodeTwentyTwo);
    }

    [Fact]
    public void EpisodeKey_RequiresMatchingTitleAlongsideCoordinates()
    {
        var seriesIds = new Dictionary<string, string> { ["Tvdb"] = "101" };

        var first = DuplicateIdentity.EpisodeKey("Example", 1, 23, "First title", null, seriesIds);
        var second = DuplicateIdentity.EpisodeKey("Example", 1, 23, "Different title", null, seriesIds);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void EpisodeKey_DoesNotCollapseSameEpisodeNumberAcrossSeasons()
    {
        var seriesIds = new Dictionary<string, string> { ["Tvdb"] = "355567" };

        var seasonOne = DuplicateIdentity.EpisodeKey("The Boys", 1, 23, "Folge 23", null, seriesIds);
        var seasonTwo = DuplicateIdentity.EpisodeKey("The Boys", 2, 23, "Folge 23", null, seriesIds);

        Assert.NotEqual(seasonOne, seasonTwo);
    }
}

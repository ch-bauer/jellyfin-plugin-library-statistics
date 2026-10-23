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
    public void EpisodeKey_UsesSeriesEpisodeCoordinatesAndProviderIdentity()
    {
        var provider = new Dictionary<string, string> { ["Tvdb"] = "12345" };
        Assert.Equal(
            DuplicateIdentity.EpisodeKey("Dark", 1, 1, "Secrets", provider, null),
            DuplicateIdentity.EpisodeKey("DARK", 1, 1, "Secrets", provider, null));
        Assert.NotEqual(
            DuplicateIdentity.EpisodeKey("Dark", 1, 1, "Secrets", provider, null),
            DuplicateIdentity.EpisodeKey("Dark", 1, 2, "Lies", provider, null));
        Assert.NotEqual(
            DuplicateIdentity.EpisodeKey("Dark", 1, 1, "Secrets", provider, null),
            DuplicateIdentity.EpisodeKey("Dark", 1, 1, "Secrets", new Dictionary<string, string> { ["Tvdb"] = "67890" }, null));
    }

    [Fact]
    public void ProviderIdentity_RequiresARecognizedNonEmptyProvider()
    {
        Assert.True(DuplicateIdentity.HasProviderIdentity(new Dictionary<string, string> { ["Tvdb"] = "12345" }));
        Assert.False(DuplicateIdentity.HasProviderIdentity(new Dictionary<string, string> { ["Tvdb"] = "" }));
        Assert.False(DuplicateIdentity.HasProviderIdentity(null));
    }

    [Fact]
    public void SeriesKey_UnifiesAlternateSeriesDisplayNamesWithTheSameProviderIdentity()
    {
        var provider = new Dictionary<string, string> { ["Tvdb"] = "273181" };

        Assert.Equal(
            DuplicateIdentity.SeriesKey("Better Call Saul", provider),
            DuplicateIdentity.SeriesKey("Better Call Saul (4K)", provider));
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

    [Fact]
    public void EpisodeKey_DoesNotUseEpisodeProviderIdAloneWhenCoordinatesAreIncomplete()
    {
        var sharedIds = new Dictionary<string, string> { ["Imdb"] = "tt0206512" };
        var first = DuplicateIdentity.EpisodeKey("SpongeBob", 3, null, "Gefängnisstrafe", sharedIds, sharedIds);
        var second = DuplicateIdentity.EpisodeKey("SpongeBob", 3, null, "Punkt, Punkt, Komma, Strich", sharedIds, sharedIds);

        Assert.NotEqual(first, second);
    }
}

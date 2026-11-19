using Jellyfin.Plugin.LibraryStatistics.Services;
using Xunit;

namespace Jellyfin.Plugin.LibraryStatistics.Tests;

public sealed class LanguageAuditTests
{
    [Fact]
    public void OriginalLanguage_PrefersAnExplicitOriginalTrack()
    {
        List<(string? Language, bool IsDefault, string? Title)> streams =
        [
            Audio("DEU", isDefault: true),
            Audio("JPN", title: "Original audio"),
            Audio("ENG")
        ];

        Assert.Equal("JPN", LibraryStatisticsService.InferOriginalLanguage(streams));
    }

    [Fact]
    public void OriginalLanguage_PrefersKnownNonGermanDefaultThenNonGermanTrack()
    {
        Assert.Equal(
            "FRA",
            LibraryStatisticsService.InferOriginalLanguage([Audio("DEU"), Audio("FRA", isDefault: true)]));
        Assert.Equal(
            "JPN",
            LibraryStatisticsService.InferOriginalLanguage([Audio("DEU", isDefault: true), Audio("JPN")]));
    }

    [Fact]
    public void OriginalLanguage_AllowsGermanAsTheOriginalLanguage()
    {
        Assert.Equal("DEU", LibraryStatisticsService.InferOriginalLanguage([Audio("DEU")]));
    }

    [Fact]
    public void AuditGroup_TreatsSeasonZeroAsSpecials()
    {
        Assert.Equal("Specials", LibraryStatisticsService.AuditSeasonTitle(0));
        Assert.Equal("Season 2", LibraryStatisticsService.AuditSeasonTitle(2));
        Assert.Equal("Unknown season", LibraryStatisticsService.AuditSeasonTitle(null));
    }

    [Fact]
    public void MediaFormats_IncludeOnlyMoviesAndEpisodes()
    {
        Assert.True(LibraryStatisticsService.TracksMediaFormats(MediaKind.Movie));
        Assert.True(LibraryStatisticsService.TracksMediaFormats(MediaKind.Episode));
        Assert.False(LibraryStatisticsService.TracksMediaFormats(MediaKind.OtherVideo));
        Assert.False(LibraryStatisticsService.TracksMediaFormats(MediaKind.Audio));
    }

    private static (string? Language, bool IsDefault, string? Title) Audio(
        string language,
        bool isDefault = false,
        string? title = null) => (language, isDefault, title);
}

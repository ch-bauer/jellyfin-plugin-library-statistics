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

    private static (string? Language, bool IsDefault, string? Title) Audio(
        string language,
        bool isDefault = false,
        string? title = null) => (language, isDefault, title);
}

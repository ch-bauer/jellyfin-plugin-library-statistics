using Jellyfin.Plugin.LibraryStatistics.Models;
using Jellyfin.Plugin.LibraryStatistics.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.LibraryStatistics.Tests;

public sealed class StatisticsDatabaseTests
{
    [Fact]
    public void Database_PersistsSnapshotsAndExactVersionReviews()
    {
        string directory = Path.Combine(Path.GetTempPath(), "library-statistics-db-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var database = new StatisticsDatabase(directory, NullLogger<StatisticsDatabase>.Instance);
            var result = new StatisticsResult
            {
                ScannedAtUtc = new DateTime(2026, 10, 22, 10, 0, 0, DateTimeKind.Utc),
                Total = new StatisticsSummary { TotalSizeBytes = 1234, MediaFileCount = 12 }
            };
            database.SaveSnapshot(result);
            GrowthPoint point = Assert.Single(database.LoadTimeline());
            Assert.Equal(1234, point.CumulativeSizeBytes);
            Assert.Equal(12, point.CumulativeFileCount);

            DuplicateGroup group = Group("/media/a.mkv", "/media/b.mkv");
            database.SaveReview(group.GroupKey, DuplicateReviewClassifier.VersionSignature(group.Versions), true, "keep both");
            database.ApplyReviews([group], false);
            Assert.Equal(DuplicateReviewStates.Allowed, group.ReviewState);
            Assert.Equal("keep both", group.ReviewNote);

            group.Versions.Add(new MediaVersionDetail { Path = "/media/c.mkv" });
            database.ApplyReviews([group], false);
            Assert.Equal(DuplicateReviewStates.Unreviewed, group.ReviewState);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static DuplicateGroup Group(params string[] paths) => new()
    {
        GroupKey = "group",
        Versions = paths.Select(path => new MediaVersionDetail { Path = path }).ToList()
    };
}

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
            var reloadedDatabase = new StatisticsDatabase(directory, NullLogger<StatisticsDatabase>.Instance);
            reloadedDatabase.ApplyReviews([group], false);
            Assert.Equal(DuplicateReviewStates.Allowed, group.ReviewState);
            Assert.Equal("keep both", group.ReviewNote);

            group.Versions.Add(new MediaVersionDetail { Path = "/media/c.mkv" });
            reloadedDatabase.ApplyReviews([group], false);
            Assert.Equal(DuplicateReviewStates.Unreviewed, group.ReviewState);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Database_RecoversThePreviousTransactionFromBackup()
    {
        string directory = Path.Combine(Path.GetTempPath(), "library-statistics-db-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var database = new StatisticsDatabase(directory, NullLogger<StatisticsDatabase>.Instance);
            database.SaveSnapshot(Result(100));
            database.SaveSnapshot(Result(200));
            File.WriteAllText(Path.Combine(directory, "library-statistics-state.json"), "not json");

            var recoveredDatabase = new StatisticsDatabase(directory, NullLogger<StatisticsDatabase>.Instance);
            GrowthPoint recovered = Assert.Single(recoveredDatabase.LoadTimeline());
            Assert.Equal(100, recovered.CumulativeSizeBytes);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Database_SavesBatchReviewsAndClearsAllManualDecisions()
    {
        string directory = Path.Combine(Path.GetTempPath(), "library-statistics-db-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var database = new StatisticsDatabase(directory, NullLogger<StatisticsDatabase>.Instance);
            DuplicateGroup first = Group("/media/s01e01-a.mkv", "/media/s01e01-b.mkv");
            DuplicateGroup second = Group("/media/s01e02-a.mkv", "/media/s01e02-b.mkv");
            first.GroupKey = "episode-one";
            second.GroupKey = "episode-two";

            database.SaveReviews(
                [
                    (first.GroupKey, DuplicateReviewClassifier.VersionSignature(first.Versions)),
                    (second.GroupKey, DuplicateReviewClassifier.VersionSignature(second.Versions))
                ],
                true,
                "whole series");
            database.ApplyReviews([first, second], false);
            Assert.All([first, second], group => Assert.Equal(DuplicateReviewStates.Allowed, group.ReviewState));

            Assert.Equal(2, database.ClearReviews());
            database.ApplyReviews([first, second], false);
            Assert.All([first, second], group => Assert.Equal(DuplicateReviewStates.Unreviewed, group.ReviewState));
            Assert.Equal(0, database.ClearReviews());
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static StatisticsResult Result(long size) => new()
    {
        ScannedAtUtc = new DateTime(2026, 10, 22, 10, 0, 0, DateTimeKind.Utc),
        Total = new StatisticsSummary { TotalSizeBytes = size, MediaFileCount = 12 }
    };

    private static DuplicateGroup Group(params string[] paths) => new()
    {
        GroupKey = "group",
        Versions = paths.Select(path => new MediaVersionDetail { Path = path }).ToList()
    };
}

using System.Globalization;
using Jellyfin.Plugin.LibraryStatistics.Models;
using MediaBrowser.Common.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.LibraryStatistics.Services;

/// <summary>Transactional storage for real scan snapshots and duplicate reviews.</summary>
public sealed class StatisticsDatabase
{
    private readonly string _connectionString;
    private readonly ILogger<StatisticsDatabase> _logger;
    private readonly object _sync = new();

    /// <summary>Initializes a new instance of the <see cref="StatisticsDatabase"/> class.</summary>
    public StatisticsDatabase(IApplicationPaths applicationPaths, ILogger<StatisticsDatabase> logger)
        : this(applicationPaths.DataPath, logger)
    {
    }

    internal StatisticsDatabase(string dataPath, ILogger<StatisticsDatabase> logger)
    {
        string path = Path.Combine(dataPath, "library-statistics.db");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false,
            DefaultTimeout = 5
        }.ToString();
        _logger = logger;
        Initialize();
    }

    /// <summary>Saves one successful scan snapshot.</summary>
    public void SaveSnapshot(StatisticsResult result)
    {
        lock (_sync)
        {
            using SqliteConnection connection = Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO scan_snapshots(scan_day, scanned_at_utc, total_size_bytes, media_file_count)
                VALUES ($day, $at, $bytes, $files)
                ON CONFLICT(scan_day) DO UPDATE SET
                    scanned_at_utc = excluded.scanned_at_utc,
                    total_size_bytes = excluded.total_size_bytes,
                    media_file_count = excluded.media_file_count;
                """;
            command.Parameters.AddWithValue("$day", result.ScannedAtUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$at", result.ScannedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$bytes", result.Total.TotalSizeBytes);
            command.Parameters.AddWithValue("$files", result.Total.MediaFileCount);
            command.ExecuteNonQuery();
        }
    }

    /// <summary>Loads the actual successful-scan history.</summary>
    public List<GrowthPoint> LoadTimeline()
    {
        lock (_sync)
        {
            List<GrowthPoint> points = [];
            using SqliteConnection connection = Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT scanned_at_utc, total_size_bytes, media_file_count FROM scan_snapshots ORDER BY scanned_at_utc;";
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                points.Add(new GrowthPoint
                {
                    DateUtc = DateTime.Parse(reader.GetString(0), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                    CumulativeSizeBytes = reader.GetInt64(1),
                    CumulativeFileCount = reader.GetInt32(2)
                });
            }

            return points;
        }
    }

    /// <summary>Stores an explicit administrator review for one exact version set.</summary>
    public void SaveReview(string groupKey, string versionSignature, bool allowed, string? note)
    {
        lock (_sync)
        {
            using SqliteConnection connection = Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO duplicate_reviews(group_key, version_signature, allowed, note, updated_at_utc)
                VALUES ($key, $signature, $allowed, $note, $at)
                ON CONFLICT(group_key) DO UPDATE SET
                    version_signature = excluded.version_signature,
                    allowed = excluded.allowed,
                    note = excluded.note,
                    updated_at_utc = excluded.updated_at_utc;
                """;
            command.Parameters.AddWithValue("$key", groupKey);
            command.Parameters.AddWithValue("$signature", versionSignature);
            command.Parameters.AddWithValue("$allowed", allowed ? 1 : 0);
            command.Parameters.AddWithValue("$note", (note ?? string.Empty).Trim());
            command.Parameters.AddWithValue("$at", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();
        }
    }

    /// <summary>Applies saved and automatic review states to duplicate groups.</summary>
    public void ApplyReviews(IList<DuplicateGroup> groups, bool autoAllow4KAnd1080P)
    {
        Dictionary<string, ReviewRecord> reviews = LoadReviews();
        foreach (DuplicateGroup group in groups)
        {
            string signature = DuplicateReviewClassifier.VersionSignature(group.Versions);
            if (reviews.TryGetValue(group.GroupKey, out ReviewRecord? review)
                && review.VersionSignature.Equals(signature, StringComparison.Ordinal))
            {
                group.ReviewState = review.Allowed
                    ? DuplicateReviewStates.Allowed
                    : group.IsAmbiguous ? DuplicateReviewStates.Ambiguous : DuplicateReviewStates.Unreviewed;
                group.ReviewNote = review.Note;
            }
            else if (group.IsAmbiguous)
            {
                group.ReviewState = DuplicateReviewStates.Ambiguous;
            }
            else if (autoAllow4KAnd1080P && DuplicateReviewClassifier.IsExactly4KAnd1080P(group.Versions))
            {
                group.ReviewState = DuplicateReviewStates.AutoAllowed;
            }
            else
            {
                group.ReviewState = DuplicateReviewStates.Unreviewed;
            }
        }
    }

    private Dictionary<string, ReviewRecord> LoadReviews()
    {
        Dictionary<string, ReviewRecord> reviews = new(StringComparer.Ordinal);
        lock (_sync)
        {
            using SqliteConnection connection = Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT group_key, version_signature, allowed, note FROM duplicate_reviews;";
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                reviews[reader.GetString(0)] = new ReviewRecord(reader.GetString(1), reader.GetInt32(2) != 0, reader.GetString(3));
            }
        }

        return reviews;
    }

    private void Initialize()
    {
        lock (_sync)
        {
            try
            {
                using SqliteConnection connection = Open();
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = """
                    PRAGMA journal_mode=WAL;
                    PRAGMA synchronous=FULL;
                    PRAGMA busy_timeout=5000;
                    CREATE TABLE IF NOT EXISTS scan_snapshots(
                        scan_day TEXT PRIMARY KEY,
                        scanned_at_utc TEXT NOT NULL,
                        total_size_bytes INTEGER NOT NULL,
                        media_file_count INTEGER NOT NULL);
                    CREATE TABLE IF NOT EXISTS duplicate_reviews(
                        group_key TEXT PRIMARY KEY,
                        version_signature TEXT NOT NULL,
                        allowed INTEGER NOT NULL,
                        note TEXT NOT NULL DEFAULT '',
                        updated_at_utc TEXT NOT NULL);
                    """;
                command.ExecuteNonQuery();
            }
            catch (SqliteException ex)
            {
                _logger.LogError(ex, "Library Statistics: could not initialize the statistics database.");
                throw;
            }
        }
    }

    private SqliteConnection Open()
    {
        SqliteConnection connection = new(_connectionString);
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA synchronous=FULL; PRAGMA busy_timeout=5000;";
        command.ExecuteNonQuery();
        return connection;
    }

    private sealed record ReviewRecord(string VersionSignature, bool Allowed, string Note);
}

using System.Globalization;
using System.Text.Json;
using Jellyfin.Plugin.LibraryStatistics.Models;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.LibraryStatistics.Services;

/// <summary>Transactional storage for real scan snapshots and duplicate reviews.</summary>
public sealed class StatisticsDatabase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _path;
    private readonly string _backupPath;
    private readonly ILogger<StatisticsDatabase> _logger;
    private readonly object _sync = new();

    /// <summary>Initializes a new instance of the <see cref="StatisticsDatabase"/> class.</summary>
    public StatisticsDatabase(IApplicationPaths applicationPaths, ILogger<StatisticsDatabase> logger)
        : this(applicationPaths.DataPath, logger)
    {
    }

    internal StatisticsDatabase(string dataPath, ILogger<StatisticsDatabase> logger)
    {
        Directory.CreateDirectory(dataPath);
        _path = Path.Combine(dataPath, "library-statistics-state.json");
        _backupPath = _path + ".bak";
        _logger = logger;
    }

    /// <summary>Saves one successful scan snapshot.</summary>
    public void SaveSnapshot(StatisticsResult result)
    {
        lock (_sync)
        {
            DatabaseState state = LoadState();
            string day = result.ScannedAtUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            ScanSnapshot snapshot = new()
            {
                Day = day,
                ScannedAtUtc = result.ScannedAtUtc,
                TotalSizeBytes = result.Total.TotalSizeBytes,
                MediaFileCount = result.Total.MediaFileCount
            };
            int existing = state.Snapshots.FindIndex(item => item.Day.Equals(day, StringComparison.Ordinal));
            if (existing >= 0) state.Snapshots[existing] = snapshot;
            else state.Snapshots.Add(snapshot);
            SaveState(state);
        }
    }

    /// <summary>Loads the actual successful-scan history.</summary>
    public List<GrowthPoint> LoadTimeline()
    {
        lock (_sync)
        {
            return LoadState().Snapshots
                .OrderBy(item => item.ScannedAtUtc)
                .Select(item => new GrowthPoint
                {
                    DateUtc = item.ScannedAtUtc,
                    CumulativeSizeBytes = item.TotalSizeBytes,
                    CumulativeFileCount = item.MediaFileCount
                })
                .ToList();
        }
    }

    /// <summary>Stores an explicit administrator review for one exact version set.</summary>
    public void SaveReview(string groupKey, string versionSignature, bool allowed, string? note)
    {
        lock (_sync)
        {
            DatabaseState state = LoadState();
            state.Reviews[groupKey] = new ReviewRecord
            {
                VersionSignature = versionSignature,
                Allowed = allowed,
                Note = (note ?? string.Empty).Trim(),
                UpdatedAtUtc = DateTime.UtcNow
            };
            SaveState(state);
        }
    }

    /// <summary>Applies saved and automatic review states to duplicate groups.</summary>
    public void ApplyReviews(IList<DuplicateGroup> groups, bool autoAllow4KAnd1080P)
    {
        Dictionary<string, ReviewRecord> reviews;
        lock (_sync) reviews = LoadState().Reviews;
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

    private DatabaseState LoadState()
    {
        DatabaseState? state = TryLoad(_path);
        if (state is not null) return Normalize(state);

        state = TryLoad(_backupPath);
        if (state is not null)
        {
            _logger.LogWarning("Library Statistics: recovered review state from its backup file.");
            return Normalize(state);
        }

        return new DatabaseState();
    }

    private DatabaseState? TryLoad(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return JsonSerializer.Deserialize<DatabaseState>(stream, JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogError(ex, "Library Statistics: could not read persistent state from {Path}.", path);
            return null;
        }
    }

    private void SaveState(DatabaseState state)
    {
        string temporaryPath = _path + ".tmp";
        try
        {
            using (FileStream stream = new(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, state, JsonOptions);
                stream.Flush(true);
            }

            if (File.Exists(_path)) File.Copy(_path, _backupPath, true);
            File.Move(temporaryPath, _path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static DatabaseState Normalize(DatabaseState state)
    {
        state.Snapshots ??= [];
        state.Reviews = state.Reviews is null
            ? new Dictionary<string, ReviewRecord>(StringComparer.Ordinal)
            : new Dictionary<string, ReviewRecord>(state.Reviews, StringComparer.Ordinal);
        return state;
    }

    private sealed class DatabaseState
    {
        public List<ScanSnapshot> Snapshots { get; set; } = [];

        public Dictionary<string, ReviewRecord> Reviews { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class ScanSnapshot
    {
        public string Day { get; set; } = string.Empty;

        public DateTime ScannedAtUtc { get; set; }

        public long TotalSizeBytes { get; set; }

        public int MediaFileCount { get; set; }
    }

    private sealed class ReviewRecord
    {
        public string VersionSignature { get; set; } = string.Empty;

        public bool Allowed { get; set; }

        public string Note { get; set; } = string.Empty;

        public DateTime UpdatedAtUtc { get; set; }
    }
}

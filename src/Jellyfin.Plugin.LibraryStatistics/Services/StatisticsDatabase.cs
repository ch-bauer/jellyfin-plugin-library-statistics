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
        SaveReviews([(groupKey, versionSignature)], allowed, note);
    }

    /// <summary>Stores administrator reviews for several exact version sets in one transaction.</summary>
    internal void SaveReviews(
        IEnumerable<(string GroupKey, string VersionSignature)> reviews,
        bool allowed,
        string? note)
    {
        lock (_sync)
        {
            DatabaseState state = LoadState();
            string cleanNote = (note ?? string.Empty).Trim();
            DateTime updatedAtUtc = DateTime.UtcNow;
            foreach ((string groupKey, string versionSignature) in reviews)
            {
                state.Reviews[groupKey] = new ReviewRecord
                {
                    VersionSignature = versionSignature,
                    Allowed = allowed,
                    Note = cleanNote,
                    UpdatedAtUtc = updatedAtUtc
                };
            }

            SaveState(state);
        }
    }

    /// <summary>Clears all persisted manual duplicate review decisions.</summary>
    public int ClearReviews()
    {
        lock (_sync)
        {
            DatabaseState state = LoadState();
            int removed = state.Reviews.Count;
            if (removed == 0) return 0;
            state.Reviews.Clear();
            SaveState(state);
            return removed;
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

    /// <summary>Stores an explicit language review for one exact stream-language set.</summary>
    public void SaveLanguageReview(string reviewKey, string signature, bool allowed, string? note)
    {
        SaveAuditReview(state => state.LanguageReviews, reviewKey, signature, allowed, note);
    }

    /// <summary>Clears all persisted language review decisions.</summary>
    public int ClearLanguageReviews() => ClearAuditReviews(state => state.LanguageReviews);

    /// <summary>Applies saved review states to per-file language metadata.</summary>
    public void ApplyLanguageReviews(IList<MediaLanguageDetail> files)
    {
        Dictionary<string, ReviewRecord> reviews;
        lock (_sync) reviews = LoadState().LanguageReviews;
        foreach (MediaLanguageDetail file in files)
        {
            string signature = LanguageReviewClassifier.Signature(file);
            if (reviews.TryGetValue(file.ReviewKey, out ReviewRecord? review)
                && review.VersionSignature.Equals(signature, StringComparison.Ordinal))
            {
                file.ReviewState = review.Allowed ? LanguageReviewStates.Allowed : LanguageReviewStates.Unreviewed;
                file.ReviewNote = review.Note;
            }
            else
            {
                file.ReviewState = LanguageReviewStates.Unreviewed;
                file.ReviewNote = string.Empty;
            }
        }
    }

    /// <summary>Stores an explicit resolution-availability review for one exact version set.</summary>
    public void SaveAvailabilityReview(string reviewKey, string signature, bool allowed, string? note)
    {
        SaveAuditReview(state => state.AvailabilityReviews, reviewKey, signature, allowed, note);
    }

    /// <summary>Clears all persisted resolution-availability review decisions.</summary>
    public int ClearAvailabilityReviews() => ClearAuditReviews(state => state.AvailabilityReviews);

    /// <summary>Applies saved review states to logical media availability records.</summary>
    public void ApplyAvailabilityReviews(IList<MediaAvailabilityDetail> items)
    {
        Dictionary<string, ReviewRecord> reviews;
        lock (_sync) reviews = LoadState().AvailabilityReviews;
        foreach (MediaAvailabilityDetail item in items)
        {
            string signature = AvailabilityReviewClassifier.Signature(item);
            if (reviews.TryGetValue(item.ReviewKey, out ReviewRecord? review)
                && review.VersionSignature.Equals(signature, StringComparison.Ordinal))
            {
                item.ReviewState = review.Allowed ? AvailabilityReviewStates.Allowed : AvailabilityReviewStates.Unreviewed;
                item.ReviewNote = review.Note;
            }
            else
            {
                item.ReviewState = AvailabilityReviewStates.Unreviewed;
                item.ReviewNote = string.Empty;
            }
        }
    }

    private void SaveAuditReview(
        Func<DatabaseState, Dictionary<string, ReviewRecord>> selectReviews,
        string reviewKey,
        string signature,
        bool allowed,
        string? note)
    {
        lock (_sync)
        {
            DatabaseState state = LoadState();
            selectReviews(state)[reviewKey] = new ReviewRecord
            {
                VersionSignature = signature,
                Allowed = allowed,
                Note = (note ?? string.Empty).Trim(),
                UpdatedAtUtc = DateTime.UtcNow
            };
            SaveState(state);
        }
    }

    private int ClearAuditReviews(Func<DatabaseState, Dictionary<string, ReviewRecord>> selectReviews)
    {
        lock (_sync)
        {
            DatabaseState state = LoadState();
            Dictionary<string, ReviewRecord> reviews = selectReviews(state);
            int removed = reviews.Count;
            if (removed == 0) return 0;
            reviews.Clear();
            SaveState(state);
            return removed;
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
        state.LanguageReviews = state.LanguageReviews is null
            ? new Dictionary<string, ReviewRecord>(StringComparer.Ordinal)
            : new Dictionary<string, ReviewRecord>(state.LanguageReviews, StringComparer.Ordinal);
        state.AvailabilityReviews = state.AvailabilityReviews is null
            ? new Dictionary<string, ReviewRecord>(StringComparer.Ordinal)
            : new Dictionary<string, ReviewRecord>(state.AvailabilityReviews, StringComparer.Ordinal);
        return state;
    }

    private sealed class DatabaseState
    {
        public List<ScanSnapshot> Snapshots { get; set; } = [];

        public Dictionary<string, ReviewRecord> Reviews { get; set; } = new(StringComparer.Ordinal);

        public Dictionary<string, ReviewRecord> LanguageReviews { get; set; } = new(StringComparer.Ordinal);

        public Dictionary<string, ReviewRecord> AvailabilityReviews { get; set; } = new(StringComparer.Ordinal);
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

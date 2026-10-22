namespace Jellyfin.Plugin.LibraryStatistics.Models;

/// <summary>A selectable Jellyfin media library.</summary>
public sealed class LibraryOption
{
    /// <summary>Gets or sets the virtual-folder id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the Jellyfin collection type.</summary>
    public string CollectionType { get; set; } = "mixed";

    /// <summary>Gets or sets a value indicating whether this library is excluded.</summary>
    public bool Excluded { get; set; }
}

/// <summary>A count and attributed byte total for one media characteristic.</summary>
public sealed class BreakdownEntry
{
    /// <summary>Gets or sets the characteristic label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of matching media items.</summary>
    public int Count { get; set; }

    /// <summary>Gets or sets the total bytes of matching media items.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Gets or sets the distinct files represented by this entry.</summary>
    public List<string> Paths { get; set; } = [];
}

/// <summary>Common counters for a scan or one library.</summary>
public class StatisticsSummary
{
    /// <summary>Gets or sets the total number of file-backed media items.</summary>
    public int MediaFileCount { get; set; }

    /// <summary>Gets or sets the total measured file size.</summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>Gets or sets the movie count.</summary>
    public int MovieCount { get; set; }

    /// <summary>Gets or sets the episode count.</summary>
    public int EpisodeCount { get; set; }

    /// <summary>Gets or sets the other video count.</summary>
    public int OtherVideoCount { get; set; }

    /// <summary>Gets or sets the audio-track count.</summary>
    public int AudioCount { get; set; }

    /// <summary>Gets or sets measured movie bytes.</summary>
    public long MovieSizeBytes { get; set; }

    /// <summary>Gets or sets measured episode bytes.</summary>
    public long EpisodeSizeBytes { get; set; }

    /// <summary>Gets or sets measured other-video bytes.</summary>
    public long OtherVideoSizeBytes { get; set; }

    /// <summary>Gets or sets measured audio bytes.</summary>
    public long AudioSizeBytes { get; set; }

    /// <summary>Gets or sets the total number of scanned media and sidecar files.</summary>
    public int TotalFileCount { get; set; }

    /// <summary>Gets or sets external subtitle bytes.</summary>
    public long SubtitleSizeBytes { get; set; }

    /// <summary>Gets or sets the external subtitle-file count.</summary>
    public int SubtitleFileCount { get; set; }

    /// <summary>Gets or sets image bytes.</summary>
    public long ImageSizeBytes { get; set; }

    /// <summary>Gets or sets the image-file count.</summary>
    public int ImageFileCount { get; set; }

    /// <summary>Gets or sets alongside-media trickplay bytes.</summary>
    public long TrickplaySizeBytes { get; set; }

    /// <summary>Gets or sets the trickplay-folder count.</summary>
    public int TrickplayFolderCount { get; set; }

    /// <summary>Gets or sets NFO/XML metadata bytes.</summary>
    public long MetadataSizeBytes { get; set; }

    /// <summary>Gets or sets the metadata-file count.</summary>
    public int MetadataFileCount { get; set; }

    /// <summary>Gets or sets bytes for remaining sidecar files.</summary>
    public long OtherFileSizeBytes { get; set; }

    /// <summary>Gets or sets the remaining sidecar-file count.</summary>
    public int OtherFileCount { get; set; }

    /// <summary>Gets or sets container statistics.</summary>
    public List<BreakdownEntry> Containers { get; set; } = [];

    /// <summary>Gets or sets resolution statistics.</summary>
    public List<BreakdownEntry> Resolutions { get; set; } = [];

    /// <summary>Gets or sets video-codec statistics.</summary>
    public List<BreakdownEntry> VideoCodecs { get; set; } = [];

    /// <summary>Gets or sets audio-codec statistics.</summary>
    public List<BreakdownEntry> AudioCodecs { get; set; } = [];

    /// <summary>Gets or sets audio-codec statistics for video files.</summary>
    public List<BreakdownEntry> VideoAudioCodecs { get; set; } = [];

    /// <summary>Gets or sets audio-codec statistics for music files.</summary>
    public List<BreakdownEntry> MusicAudioCodecs { get; set; } = [];

    /// <summary>Gets or sets dynamic-range statistics.</summary>
    public List<BreakdownEntry> DynamicRanges { get; set; } = [];

    /// <summary>Gets or sets video-bitrate tier statistics.</summary>
    public List<BreakdownEntry> VideoBitrates { get; set; } = [];

    /// <summary>Gets or sets distinct audio-language statistics per file.</summary>
    public List<BreakdownEntry> AudioLanguages { get; set; } = [];

    /// <summary>Gets or sets distinct embedded-subtitle-language statistics per file.</summary>
    public List<BreakdownEntry> SubtitleLanguages { get; set; } = [];
}

/// <summary>Statistics for one included Jellyfin library.</summary>
public sealed class LibraryScanStatistics : StatisticsSummary
{
    /// <summary>Gets or sets the virtual-folder id.</summary>
    public string LibraryId { get; set; } = string.Empty;

    /// <summary>Gets or sets the library name.</summary>
    public string LibraryName { get; set; } = string.Empty;

    /// <summary>Gets or sets the library collection type.</summary>
    public string CollectionType { get; set; } = "mixed";
}

/// <summary>One video, audio, or subtitle stream belonging to a media version.</summary>
public sealed class MediaStreamDetail
{
    /// <summary>Gets or sets the stream type.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Gets or sets the stream codec.</summary>
    public string Codec { get; set; } = "Unknown";

    /// <summary>Gets or sets Jellyfin's display title.</summary>
    public string DisplayTitle { get; set; } = string.Empty;

    /// <summary>Gets or sets the language code.</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>Gets or sets the video width.</summary>
    public int? Width { get; set; }

    /// <summary>Gets or sets the video height.</summary>
    public int? Height { get; set; }

    /// <summary>Gets or sets the channel count.</summary>
    public int? Channels { get; set; }

    /// <summary>Gets or sets the stream bitrate.</summary>
    public int? BitRate { get; set; }

    /// <summary>Gets or sets the video dynamic-range label.</summary>
    public string VideoRange { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the stream is the default.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Gets or sets a value indicating whether the stream is forced.</summary>
    public bool IsForced { get; set; }
}

/// <summary>One Jellyfin media source inside a merged-version item.</summary>
public sealed class MediaVersionDetail
{
    /// <summary>Gets or sets Jellyfin's version name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the exact indexed filesystem path.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Gets or sets the media container.</summary>
    public string Container { get; set; } = "Unknown";

    /// <summary>Gets or sets the measured or indexed file size.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Gets or sets the runtime in ticks.</summary>
    public long? RunTimeTicks { get; set; }

    /// <summary>Gets or sets the relevant MediaInfo streams.</summary>
    public List<MediaStreamDetail> Streams { get; set; } = [];
}

/// <summary>A Jellyfin item with more than one merged media version.</summary>
public sealed class DuplicateGroup
{
    /// <summary>Gets or sets the stable metadata-derived group key.</summary>
    public string GroupKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the Jellyfin item id.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Gets or sets Movie or Series.</summary>
    public string MediaType { get; set; } = string.Empty;

    /// <summary>Gets or sets the movie or series title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the episode label for a series result.</summary>
    public string Subtitle { get; set; } = string.Empty;

    /// <summary>Gets or sets the production year.</summary>
    public int? ProductionYear { get; set; }

    /// <summary>Gets or sets the containing library.</summary>
    public string LibraryName { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether Jellyfin exposes multiple media sources for the item.</summary>
    public bool IsMergedVersion { get; set; }

    /// <summary>Gets or sets a value indicating whether the sources may represent episode parts rather than quality versions.</summary>
    public bool IsAmbiguous { get; set; }

    /// <summary>Gets or sets the persisted review state.</summary>
    public string ReviewState { get; set; } = DuplicateReviewStates.Unreviewed;

    /// <summary>Gets or sets an optional reviewer note.</summary>
    public string ReviewNote { get; set; } = string.Empty;

    /// <summary>Gets or sets the Jellyfin season number for series entries.</summary>
    public int? SeasonNumber { get; set; }

    /// <summary>Gets or sets the Jellyfin episode number for series entries.</summary>
    public int? EpisodeNumber { get; set; }

    /// <summary>Gets or sets Jellyfin's merged media versions.</summary>
    public List<MediaVersionDetail> Versions { get; set; } = [];
}

/// <summary>Known duplicate review states.</summary>
public static class DuplicateReviewStates
{
    /// <summary>The group requires review.</summary>
    public const string Unreviewed = "Unreviewed";

    /// <summary>An administrator explicitly allowed the group.</summary>
    public const string Allowed = "Allowed";

    /// <summary>The configured 4K plus 1080p rule allowed the group.</summary>
    public const string AutoAllowed = "AutoAllowed";

    /// <summary>The group may contain episode parts rather than duplicate versions.</summary>
    public const string Ambiguous = "Ambiguous";
}

/// <summary>A request to change one duplicate review.</summary>
public sealed class DuplicateReviewRequest
{
    /// <summary>Gets or sets a value indicating whether the duplicate is allowed.</summary>
    public bool Allowed { get; set; }

    /// <summary>Gets or sets an optional review note.</summary>
    public string Note { get; set; } = string.Empty;
}

/// <summary>Current state of a background library scan.</summary>
public sealed class ScanProgress
{
    /// <summary>Gets or sets the scan identifier.</summary>
    public Guid ScanId { get; set; }

    /// <summary>Gets or sets the machine-readable status.</summary>
    public string Status { get; set; } = "Idle";

    /// <summary>Gets or sets the current phase.</summary>
    public string Phase { get; set; } = string.Empty;

    /// <summary>Gets or sets the current library.</summary>
    public string LibraryName { get; set; } = string.Empty;

    /// <summary>Gets or sets the completed percentage.</summary>
    public int Percent { get; set; }

    /// <summary>Gets or sets the number of processed media items.</summary>
    public int ProcessedItems { get; set; }

    /// <summary>Gets or sets the total number of media items when known.</summary>
    public int TotalItems { get; set; }

    /// <summary>Gets or sets an error message for failed scans.</summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the scan includes filesystem sidecars.</summary>
    public bool FullStorageScan { get; set; }
}

/// <summary>Options for starting a scan.</summary>
public sealed class StartScanRequest
{
    /// <summary>Gets or sets whether to include the slower filesystem scan.</summary>
    public bool FullStorageScan { get; set; }
}

/// <summary>One cumulative point in the media-library growth timeline.</summary>
public sealed class GrowthPoint
{
    /// <summary>Gets or sets the UTC day represented by this point.</summary>
    public DateTime DateUtc { get; set; }

    /// <summary>Gets or sets the cumulative measured bytes at this point.</summary>
    public long CumulativeSizeBytes { get; set; }

    /// <summary>Gets or sets the cumulative number of media files at this point.</summary>
    public int CumulativeFileCount { get; set; }
}

/// <summary>One movie or series entry used by the largest/latest explorer.</summary>
public sealed class MediaInsight
{
    /// <summary>Gets or sets the Jellyfin item or series id.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Gets or sets Movie or Series.</summary>
    public string MediaType { get; set; } = string.Empty;

    /// <summary>Gets or sets the display title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the containing library.</summary>
    public string LibraryName { get; set; } = string.Empty;

    /// <summary>Gets or sets the total measured bytes for this movie or series.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Gets or sets the newest indexed creation date represented by the entry.</summary>
    public DateTime DateCreatedUtc { get; set; }

    /// <summary>Gets or sets an indexed media path represented by the entry.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of distinct media files represented by the entry.</summary>
    public int FileCount { get; set; }
}

/// <summary>The complete result of a library scan.</summary>
public sealed class StatisticsResult
{
    /// <summary>Gets or sets the UTC completion time.</summary>
    public DateTime ScannedAtUtc { get; set; }

    /// <summary>Gets or sets the elapsed scan time in milliseconds.</summary>
    public long DurationMilliseconds { get; set; }

    /// <summary>Gets or sets aggregate statistics.</summary>
    public StatisticsSummary Total { get; set; } = new();

    /// <summary>Gets or sets per-library statistics.</summary>
    public List<LibraryScanStatistics> Libraries { get; set; } = [];

    /// <summary>Gets or sets movie and series-episode items with merged media versions.</summary>
    public List<DuplicateGroup> DuplicateGroups { get; set; } = [];

    /// <summary>Gets or sets cumulative growth points reconstructed from indexed media dates.</summary>
    public List<GrowthPoint> GrowthTimeline { get; set; } = [];

    /// <summary>Gets or sets movies ordered by measured size.</summary>
    public List<MediaInsight> LargestMovies { get; set; } = [];

    /// <summary>Gets or sets movies ordered by indexed creation date.</summary>
    public List<MediaInsight> LatestMovies { get; set; } = [];

    /// <summary>Gets or sets series ordered by aggregate episode-media size.</summary>
    public List<MediaInsight> LargestSeries { get; set; } = [];

    /// <summary>Gets or sets series ordered by newest indexed episode date.</summary>
    public List<MediaInsight> LatestSeries { get; set; } = [];
}

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

    /// <summary>Gets or sets container statistics.</summary>
    public List<BreakdownEntry> Containers { get; set; } = [];

    /// <summary>Gets or sets resolution statistics.</summary>
    public List<BreakdownEntry> Resolutions { get; set; } = [];

    /// <summary>Gets or sets video-codec statistics.</summary>
    public List<BreakdownEntry> VideoCodecs { get; set; } = [];

    /// <summary>Gets or sets audio-codec statistics.</summary>
    public List<BreakdownEntry> AudioCodecs { get; set; } = [];

    /// <summary>Gets or sets dynamic-range statistics.</summary>
    public List<BreakdownEntry> DynamicRanges { get; set; } = [];
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
}

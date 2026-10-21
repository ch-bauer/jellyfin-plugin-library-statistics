using System.Diagnostics;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.LibraryStatistics.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.LibraryStatistics.Services;

/// <summary>Builds read-only statistics from Jellyfin's indexed media and merged-version metadata.</summary>
public sealed class LibraryStatisticsService
{
    private readonly ILibraryManager _libraryManager;
    private readonly IMediaSourceManager _mediaSourceManager;
    private readonly StatisticsStore _store;
    private readonly ILogger<LibraryStatisticsService> _logger;
    private readonly SemaphoreSlim _scanLock = new(1, 1);

    /// <summary>Initializes a new instance of the <see cref="LibraryStatisticsService"/> class.</summary>
    public LibraryStatisticsService(
        ILibraryManager libraryManager,
        IMediaSourceManager mediaSourceManager,
        StatisticsStore store,
        ILogger<LibraryStatisticsService> logger)
    {
        _libraryManager = libraryManager;
        _mediaSourceManager = mediaSourceManager;
        _store = store;
        _logger = logger;
    }

    /// <summary>Gets the available libraries and their exclusion state.</summary>
    public IReadOnlyList<LibraryOption> GetLibraries()
    {
        HashSet<string> excluded = new(
            Plugin.Instance?.Configuration.ExcludedLibraryIds ?? [],
            StringComparer.OrdinalIgnoreCase);

        return _libraryManager.GetVirtualFolders()
            .Where(folder => !string.IsNullOrWhiteSpace(folder.ItemId) && !string.IsNullOrWhiteSpace(folder.Name))
            .Select(folder => new LibraryOption
            {
                Id = folder.ItemId,
                Name = folder.Name,
                CollectionType = folder.CollectionType?.ToString() ?? "mixed",
                Excluded = excluded.Contains(folder.ItemId)
            })
            .OrderBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Runs one scan, or returns <see langword="null"/> when another scan is active.</summary>
    public async Task<StatisticsResult?> TryScanAsync(CancellationToken cancellationToken)
    {
        if (!await _scanLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        try
        {
            return await Task.Run(() => Scan(cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _scanLock.Release();
        }
    }

    private StatisticsResult Scan(CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        StatisticsResult result = new();
        StatisticsAccumulator total = new();
        HashSet<string> duplicateSourceSets = new(StringComparer.Ordinal);
        Dictionary<Guid, MediaInsightAccumulator> movieInsights = [];
        Dictionary<Guid, MediaInsightAccumulator> seriesInsights = [];
        List<TimelineSource> timelineSources = [];
        HashSet<string> excluded = new(
            Plugin.Instance?.Configuration.ExcludedLibraryIds ?? [],
            StringComparer.OrdinalIgnoreCase);

        var folders = _libraryManager.GetVirtualFolders()
            .Where(folder => !string.IsNullOrWhiteSpace(folder.ItemId)
                && !excluded.Contains(folder.ItemId)
                && Guid.TryParse(folder.ItemId, out _))
            .ToList();

        _logger.LogInformation("Library Statistics: scanning {Count} included libraries.", folders.Count);

        foreach (var folder in folders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Guid libraryId = Guid.Parse(folder.ItemId);
            string libraryName = folder.Name ?? "Unknown";
            LibraryScanStatistics stats = new()
            {
                LibraryId = folder.ItemId,
                LibraryName = libraryName,
                CollectionType = folder.CollectionType?.ToString() ?? "mixed"
            };
            StatisticsAccumulator accumulator = new();
            HashSet<string> analyzedPaths = new(StringComparer.Ordinal);

            IReadOnlyList<BaseItem> items = _libraryManager.GetItemList(new InternalItemsQuery
            {
                AncestorIds = [libraryId],
                Recursive = true,
                IsFolder = false,
                MediaTypes = [MediaType.Video, MediaType.Audio]
            });

            foreach (BaseItem item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AnalyzeItem(
                    item,
                    libraryName,
                    accumulator,
                    analyzedPaths,
                    duplicateSourceSets,
                    result.DuplicateGroups,
                    movieInsights,
                    seriesInsights,
                    timelineSources);
            }

            accumulator.ApplyTo(stats);
            accumulator.MergeInto(total);
            result.Libraries.Add(stats);
        }

        result.DuplicateGroups = result.DuplicateGroups
            .OrderBy(group => group.MediaType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Subtitle, StringComparer.OrdinalIgnoreCase)
            .ToList();
        total.ApplyTo(result.Total);
        result.GrowthTimeline = BuildTimeline(timelineSources);
        result.LargestMovies = BuildInsights(movieInsights.Values, latest: false);
        result.LatestMovies = BuildInsights(movieInsights.Values, latest: true);
        result.LargestSeries = BuildInsights(seriesInsights.Values, latest: false);
        result.LatestSeries = BuildInsights(seriesInsights.Values, latest: true);
        stopwatch.Stop();
        result.DurationMilliseconds = stopwatch.ElapsedMilliseconds;
        result.ScannedAtUtc = DateTime.UtcNow;
        _store.Save(result);

        _logger.LogInformation(
            "Library Statistics: scan complete with {Files} media files, {Bytes} measured bytes, and {Duplicates} merged-version groups in {Elapsed} ms.",
            result.Total.MediaFileCount,
            result.Total.TotalSizeBytes,
            result.DuplicateGroups.Count,
            result.DurationMilliseconds);
        return result;
    }

    private void AnalyzeItem(
        BaseItem item,
        string libraryName,
        StatisticsAccumulator accumulator,
        ISet<string> analyzedPaths,
        ISet<string> duplicateSourceSets,
        ICollection<DuplicateGroup> duplicateGroups,
        IDictionary<Guid, MediaInsightAccumulator> movieInsights,
        IDictionary<Guid, MediaInsightAccumulator> seriesInsights,
        ICollection<TimelineSource> timelineSources)
    {
        MediaKind kind = item switch
        {
            Movie => MediaKind.Movie,
            Episode => MediaKind.Episode,
            Audio => MediaKind.Audio,
            _ => MediaKind.OtherVideo
        };

        List<MediaSourceInfo> sources = GetMediaSources(item)
            .Where(source => !string.IsNullOrWhiteSpace(source.Path))
            .GroupBy(source => source.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (sources.Count == 0)
        {
            if (analyzedPaths.Add(MediaSourceIdentity.PathKey(item.Path)))
            {
                long size = AnalyzeFallback(item, kind, accumulator);
                TrackMedia(item, libraryName, item.Path, size, movieInsights, seriesInsights, timelineSources);
            }

            return;
        }

        foreach (MediaSourceInfo source in sources)
        {
            if (!analyzedPaths.Add(MediaSourceIdentity.PathKey(source.Path)))
            {
                continue;
            }

            long size = source.Size.GetValueOrDefault() > 0
                ? source.Size.GetValueOrDefault()
                : MeasureFile(source.Path);
            IReadOnlyList<MediaStream> streams = source.MediaStreams ?? [];
            AnalyzeSource(source.Path, source.Container, size, streams, kind, accumulator);
            TrackMedia(item, libraryName, source.Path, size, movieInsights, seriesInsights, timelineSources);
        }

        if (sources.Count > 1 && item is Movie or Episode)
        {
            string mediaType = item is Movie ? "Movie" : "Series";
            string sourceSetKey = MediaSourceIdentity.SourceSetKey(
                mediaType,
                sources.Select(source => source.Path));
            if (duplicateSourceSets.Add(sourceSetKey))
            {
                duplicateGroups.Add(BuildDuplicateGroup(item, libraryName, sources));
            }
        }
    }

    private long AnalyzeFallback(BaseItem item, MediaKind kind, StatisticsAccumulator accumulator)
    {
        if (string.IsNullOrWhiteSpace(item.Path))
        {
            return 0;
        }

        long size = MeasureFile(item.Path);
        AnalyzeSource(item.Path, null, size, ReadStreams(item.Id), kind, accumulator);
        return size;
    }

    private void TrackMedia(
        BaseItem item,
        string libraryName,
        string? path,
        long size,
        IDictionary<Guid, MediaInsightAccumulator> movieInsights,
        IDictionary<Guid, MediaInsightAccumulator> seriesInsights,
        ICollection<TimelineSource> timelineSources)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        DateTime createdUtc = NormalizeUtc(item.DateCreated);
        timelineSources.Add(new TimelineSource(createdUtc, Math.Max(0, size)));

        if (item is not Movie && item is not Episode)
        {
            return;
        }

        if (item is Movie)
        {
            AddInsight(movieInsights, item.Id, "Movie", item.Name ?? "Untitled", libraryName, path, size, createdUtc);
            return;
        }

        var episode = (Episode)item;
        Guid seriesId = episode.SeriesId != Guid.Empty ? episode.SeriesId : episode.Id;
        BaseItem? series = episode.SeriesId != Guid.Empty ? _libraryManager.GetItemById(episode.SeriesId) : null;
        AddInsight(
            seriesInsights,
            seriesId,
            "Series",
            series?.Name ?? episode.SeriesName ?? "Unknown series",
            libraryName,
            path,
            size,
            createdUtc);
    }

    private static void AddInsight(
        IDictionary<Guid, MediaInsightAccumulator> target,
        Guid itemId,
        string mediaType,
        string title,
        string libraryName,
        string? path,
        long size,
        DateTime createdUtc)
    {
        if (!target.TryGetValue(itemId, out MediaInsightAccumulator? insight))
        {
            insight = new MediaInsightAccumulator(itemId, mediaType, title, libraryName);
            target[itemId] = insight;
        }

        insight.SizeBytes += Math.Max(0, size);
        insight.FileCount++;
        if (createdUtc > insight.DateCreatedUtc)
        {
            insight.DateCreatedUtc = createdUtc;
        }

        if (string.IsNullOrWhiteSpace(insight.Path) && !string.IsNullOrWhiteSpace(path))
        {
            insight.Path = path;
        }
    }

    private static List<GrowthPoint> BuildTimeline(IEnumerable<TimelineSource> sources)
    {
        long cumulativeSize = 0;
        int cumulativeFiles = 0;
        List<GrowthPoint> points = [];
        foreach (var day in sources
            .GroupBy(source => source.DateCreatedUtc.Date)
            .OrderBy(group => group.Key))
        {
            cumulativeSize += day.Sum(source => source.SizeBytes);
            cumulativeFiles += day.Count();
            points.Add(new GrowthPoint
            {
                DateUtc = DateTime.SpecifyKind(day.Key, DateTimeKind.Utc),
                CumulativeSizeBytes = cumulativeSize,
                CumulativeFileCount = cumulativeFiles
            });
        }

        return points;
    }

    private static List<MediaInsight> BuildInsights(IEnumerable<MediaInsightAccumulator> source, bool latest)
    {
        IOrderedEnumerable<MediaInsightAccumulator> ordered = latest
            ? source.OrderByDescending(item => item.DateCreatedUtc).ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            : source.OrderByDescending(item => item.SizeBytes).ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase);

        return ordered.Take(100).Select(item => new MediaInsight
        {
            ItemId = item.ItemId,
            MediaType = item.MediaType,
            Title = item.Title,
            LibraryName = item.LibraryName,
            SizeBytes = item.SizeBytes,
            DateCreatedUtc = item.DateCreatedUtc,
            Path = item.Path,
            FileCount = item.FileCount
        }).ToList();
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        if (value == default)
        {
            return DateTime.UtcNow;
        }

        return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    }

    private static void AnalyzeSource(
        string? path,
        string? container,
        long size,
        IReadOnlyList<MediaStream> streams,
        MediaKind kind,
        StatisticsAccumulator accumulator)
    {
        accumulator.AddMedia(size, kind);
        accumulator.AddContainer(
            string.IsNullOrWhiteSpace(container)
                ? MediaClassification.ContainerFromPath(path)
                : container.ToUpperInvariant(),
            size);

        MediaStream? videoStream = streams.FirstOrDefault(stream => stream.Type == MediaStreamType.Video);
        if (videoStream is not null)
        {
            accumulator.AddResolution(MediaClassification.ResolutionTier(videoStream.Width, videoStream.Height), size);
            accumulator.AddVideoCodec(videoStream.Codec ?? "Unknown", size);
            accumulator.AddDynamicRange(MediaClassification.DynamicRange(videoStream.VideoRangeType.ToString()), size);
        }

        foreach (string? codec in streams
            .Where(stream => stream.Type == MediaStreamType.Audio)
            .Select(stream => string.IsNullOrWhiteSpace(stream.Codec) ? "Unknown" : stream.Codec)
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            accumulator.AddAudioCodec(codec!, size);
        }
    }

    private DuplicateGroup BuildDuplicateGroup(BaseItem item, string libraryName, IEnumerable<MediaSourceInfo> sources)
    {
        string title = item.Name ?? "Untitled";
        string subtitle = string.Empty;
        string mediaType = "Movie";
        int? productionYear = item.ProductionYear;

        if (item is Episode episode)
        {
            mediaType = "Series";
            BaseItem? series = episode.SeriesId != Guid.Empty ? _libraryManager.GetItemById(episode.SeriesId) : null;
            title = series?.Name ?? episode.SeriesName ?? "Unknown series";
            productionYear = series?.ProductionYear;
            string number = episode.ParentIndexNumber.HasValue && episode.IndexNumber.HasValue
                ? $"S{episode.ParentIndexNumber:00}E{episode.IndexNumber:00}"
                : "Episode";
            subtitle = $"{number} · {episode.Name ?? "Untitled"}";
        }

        return new DuplicateGroup
        {
            ItemId = item.Id,
            MediaType = mediaType,
            Title = title,
            Subtitle = subtitle,
            ProductionYear = productionYear,
            LibraryName = libraryName,
            Versions = sources.Select(source => new MediaVersionDetail
            {
                Name = source.Name ?? string.Empty,
                Path = source.Path ?? string.Empty,
                Container = string.IsNullOrWhiteSpace(source.Container)
                    ? MediaClassification.ContainerFromPath(source.Path)
                    : source.Container.ToUpperInvariant(),
                SizeBytes = source.Size.GetValueOrDefault() > 0
                    ? source.Size.GetValueOrDefault()
                    : MeasureFile(source.Path),
                RunTimeTicks = source.RunTimeTicks ?? item.RunTimeTicks,
                Streams = (source.MediaStreams ?? [])
                    .Where(IsRelevantStream)
                    .Select(ToStreamDetail)
                    .ToList()
            }).ToList()
        };
    }

    private List<MediaSourceInfo> GetMediaSources(BaseItem item)
    {
        try
        {
            return _mediaSourceManager.GetStaticMediaSources(item, false, null).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogDebug(ex, "Library Statistics: media sources unavailable for {ItemId}.", item.Id);
            return [];
        }
    }

    private List<MediaStream> ReadStreams(Guid itemId)
    {
        try
        {
            return _mediaSourceManager.GetMediaStreams(itemId).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogDebug(ex, "Library Statistics: stream metadata unavailable for {ItemId}.", itemId);
            return [];
        }
    }

    private static bool IsRelevantStream(MediaStream stream) =>
        stream.Type is MediaStreamType.Video or MediaStreamType.Audio or MediaStreamType.Subtitle;

    private static MediaStreamDetail ToStreamDetail(MediaStream stream) => new()
    {
        Type = stream.Type.ToString(),
        Codec = string.IsNullOrWhiteSpace(stream.Codec) ? "Unknown" : stream.Codec,
        DisplayTitle = stream.DisplayTitle ?? string.Empty,
        Language = stream.Language ?? string.Empty,
        Width = stream.Width,
        Height = stream.Height,
        Channels = stream.Channels,
        BitRate = stream.BitRate,
        VideoRange = stream.Type == MediaStreamType.Video
            ? MediaClassification.DynamicRange(stream.VideoRangeType.ToString())
            : string.Empty,
        IsDefault = stream.IsDefault,
        IsForced = stream.IsForced
    };

    private long MeasureFile(string? path)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? new FileInfo(path).Length : 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            _logger.LogDebug(ex, "Library Statistics: file size unavailable for {Path}.", path);
            return 0;
        }
    }

    private sealed class MediaInsightAccumulator(Guid itemId, string mediaType, string title, string libraryName)
    {
        internal Guid ItemId { get; } = itemId;

        internal string MediaType { get; } = mediaType;

        internal string Title { get; } = title;

        internal string LibraryName { get; } = libraryName;

        internal long SizeBytes { get; set; }

        internal DateTime DateCreatedUtc { get; set; }

        internal string Path { get; set; } = string.Empty;

        internal int FileCount { get; set; }
    }

    private readonly record struct TimelineSource(DateTime DateCreatedUtc, long SizeBytes);
}

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
                AnalyzeItem(item, libraryName, accumulator, result.DuplicateGroups);
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
        ICollection<DuplicateGroup> duplicateGroups)
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
            AnalyzeFallback(item, kind, accumulator);
            return;
        }

        foreach (MediaSourceInfo source in sources)
        {
            long size = source.Size.GetValueOrDefault() > 0
                ? source.Size.GetValueOrDefault()
                : MeasureFile(source.Path);
            IReadOnlyList<MediaStream> streams = source.MediaStreams ?? [];
            AnalyzeSource(source.Path, source.Container, size, streams, kind, accumulator);
        }

        if (sources.Count > 1 && item is Movie or Episode)
        {
            duplicateGroups.Add(BuildDuplicateGroup(item, libraryName, sources));
        }
    }

    private void AnalyzeFallback(BaseItem item, MediaKind kind, StatisticsAccumulator accumulator)
    {
        if (string.IsNullOrWhiteSpace(item.Path))
        {
            return;
        }

        long size = MeasureFile(item.Path);
        AnalyzeSource(item.Path, null, size, ReadStreams(item.Id), kind, accumulator);
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
}

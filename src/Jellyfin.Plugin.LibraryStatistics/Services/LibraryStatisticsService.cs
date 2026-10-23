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
    private readonly LibraryStorageScanner _storageScanner;
    private readonly StatisticsDatabase _database;
    private readonly ILogger<LibraryStatisticsService> _logger;
    private readonly SemaphoreSlim _scanLock = new(1, 1);

    /// <summary>Initializes a new instance of the <see cref="LibraryStatisticsService"/> class.</summary>
    public LibraryStatisticsService(
        ILibraryManager libraryManager,
        IMediaSourceManager mediaSourceManager,
        StatisticsStore store,
        LibraryStorageScanner storageScanner,
        StatisticsDatabase database,
        ILogger<LibraryStatisticsService> logger)
    {
        _libraryManager = libraryManager;
        _mediaSourceManager = mediaSourceManager;
        _store = store;
        _storageScanner = storageScanner;
        _database = database;
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
    public async Task<StatisticsResult?> TryScanAsync(
        bool fullStorageScan,
        Action<ScanProgress>? reportProgress,
        CancellationToken cancellationToken)
    {
        if (!await _scanLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        try
        {
            return await Task.Run(
                () => Scan(fullStorageScan, reportProgress, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _scanLock.Release();
        }
    }

    /// <summary>Loads the latest result with current review states and real scan history.</summary>
    public StatisticsResult? GetLatest()
    {
        StatisticsResult? result = _store.Load();
        if (result is null) return null;
        ApplyStoredState(result);
        return result;
    }

    /// <summary>Changes the explicit review state for one duplicate group.</summary>
    public DuplicateGroup? SetDuplicateReview(string groupKey, DuplicateReviewRequest request)
    {
        StatisticsResult? result = _store.Load();
        DuplicateGroup? group = result?.DuplicateGroups.FirstOrDefault(item =>
            item.GroupKey.Equals(groupKey, StringComparison.Ordinal));
        if (result is null || group is null) return null;

        _database.SaveReview(
            group.GroupKey,
            DuplicateReviewClassifier.VersionSignature(group.Versions),
            request.Allowed,
            request.Note);
        ApplyStoredState(result);
        _store.Save(result);
        return result.DuplicateGroups.First(item => item.GroupKey.Equals(groupKey, StringComparison.Ordinal));
    }

    /// <summary>Changes the explicit review state for several exact duplicate groups.</summary>
    public IReadOnlyList<DuplicateGroup>? SetDuplicateReviews(DuplicateReviewBatchRequest request)
    {
        StatisticsResult? result = _store.Load();
        if (result is null) return null;

        HashSet<string> keys = new(
            request.GroupKeys.Where(key => !string.IsNullOrWhiteSpace(key)),
            StringComparer.Ordinal);
        List<DuplicateGroup> groups = result.DuplicateGroups
            .Where(group => keys.Contains(group.GroupKey))
            .ToList();
        if (keys.Count == 0 || groups.Count != keys.Count) return null;

        _database.SaveReviews(
            groups.Select(group => (
                group.GroupKey,
                DuplicateReviewClassifier.VersionSignature(group.Versions))),
            request.Allowed,
            request.Note);
        ApplyStoredState(result);
        _store.Save(result);
        return groups;
    }

    /// <summary>Clears every persisted manual duplicate review decision.</summary>
    public DuplicateReviewResetResult ResetDuplicateReviews()
    {
        int removed = _database.ClearReviews();
        StatisticsResult? result = _store.Load();
        if (result is not null)
        {
            ApplyStoredState(result);
            _store.Save(result);
        }

        return new DuplicateReviewResetResult { RemovedCount = removed };
    }

    private StatisticsResult Scan(
        bool fullStorageScan,
        Action<ScanProgress>? reportProgress,
        CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        StatisticsResult? previousResult = fullStorageScan ? null : _store.Load();
        StatisticsResult result = new();
        StatisticsAccumulator total = new();
        Dictionary<string, DuplicateAccumulator> duplicateCandidates = new(StringComparer.Ordinal);
        Dictionary<Guid, MediaInsightAccumulator> movieInsights = [];
        Dictionary<Guid, MediaInsightAccumulator> seriesInsights = [];
        List<MediaGrowthEntry> growthEntries = [];
        Dictionary<Guid, BaseItem?> entityCache = [];
        HashSet<string> excluded = new(
            Plugin.Instance?.Configuration.ExcludedLibraryIds ?? [],
            StringComparer.OrdinalIgnoreCase);

        var folders = _libraryManager.GetVirtualFolders()
            .Where(folder => !string.IsNullOrWhiteSpace(folder.ItemId)
                && !excluded.Contains(folder.ItemId)
                && Guid.TryParse(folder.ItemId, out _))
            .ToList();

        _logger.LogInformation("Library Statistics: scanning {Count} included libraries.", folders.Count);
        int processedItems = 0;
        int discoveredItems = 0;
        Report(reportProgress, "Scanning", "Preparing libraries", string.Empty, 2, 0, 0, fullStorageScan);

        for (int folderIndex = 0; folderIndex < folders.Count; folderIndex++)
        {
            var folder = folders[folderIndex];
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
            discoveredItems += items.Count;
            Report(reportProgress, "Scanning", "Reading Jellyfin metadata", libraryName,
                ScanPercent(folderIndex, folders.Count, 0), processedItems, discoveredItems, fullStorageScan);

            for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
            {
                BaseItem item = items[itemIndex];
                cancellationToken.ThrowIfCancellationRequested();
                AnalyzeItem(
                    item,
                    libraryName,
                    accumulator,
                    analyzedPaths,
                    duplicateCandidates,
                    movieInsights,
                    seriesInsights,
                    growthEntries,
                    entityCache);
                processedItems++;
                if (itemIndex % 25 == 0 || itemIndex == items.Count - 1)
                {
                    Report(reportProgress, "Scanning", "Reading Jellyfin metadata", libraryName,
                        ScanPercent(folderIndex, folders.Count, items.Count == 0 ? 1 : (itemIndex + 1d) / items.Count),
                        processedItems, discoveredItems, fullStorageScan);
                }
            }

            if (fullStorageScan)
            {
                Report(reportProgress, "Scanning", "Measuring sidecar storage", libraryName,
                    Math.Min(94, ScanPercent(folderIndex + 1, folders.Count, 0)), processedItems, discoveredItems, true);
                _storageScanner.Scan(folder.Locations, accumulator, cancellationToken);
            }

            accumulator.ApplyTo(stats);
            if (!fullStorageScan)
            {
                LibraryScanStatistics? previousLibrary = previousResult?.Libraries.FirstOrDefault(item =>
                    item.LibraryId.Equals(stats.LibraryId, StringComparison.OrdinalIgnoreCase));
                if (previousLibrary is not null) RestoreAuxiliary(previousLibrary, stats);
            }
            accumulator.MergeInto(total);
            result.Libraries.Add(stats);
        }

        Report(reportProgress, "Scanning", "Classifying duplicates", string.Empty, 95, processedItems, discoveredItems, fullStorageScan);
        result.DuplicateGroups = BuildDuplicateGroups(duplicateCandidates.Values)
            .OrderBy(group => group.MediaType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Subtitle, StringComparer.OrdinalIgnoreCase)
            .ToList();
        total.ApplyTo(result.Total);
        if (!fullStorageScan && previousResult is not null) AggregateAuxiliary(result.Libraries, result.Total);
        result.LargestMovies = BuildInsights(movieInsights.Values, latest: false);
        result.LatestMovies = BuildInsights(movieInsights.Values, latest: true);
        result.LargestSeries = BuildInsights(seriesInsights.Values, latest: false);
        result.LatestSeries = BuildInsights(seriesInsights.Values, latest: true);
        result.GrowthTimeline = BuildGrowthTimeline(growthEntries);
        result.GrowthTimelineSource = "MediaDates";
        stopwatch.Stop();
        result.DurationMilliseconds = stopwatch.ElapsedMilliseconds;
        result.ScannedAtUtc = DateTime.UtcNow;
        _database.ApplyReviews(result.DuplicateGroups, Plugin.Instance?.Configuration.AutoAllow4KAnd1080P ?? false);
        _database.SaveSnapshot(result);
        if (result.GrowthTimeline.Count == 0)
        {
            result.GrowthTimeline = _database.LoadTimeline();
            result.GrowthTimelineSource = "ScanSnapshots";
        }
        _store.Save(result);
        Report(reportProgress, "Completed", "Scan complete", string.Empty, 100, processedItems, discoveredItems, fullStorageScan);

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
        IDictionary<string, DuplicateAccumulator> duplicateCandidates,
        IDictionary<Guid, MediaInsightAccumulator> movieInsights,
        IDictionary<Guid, MediaInsightAccumulator> seriesInsights,
        ICollection<MediaGrowthEntry> growthEntries,
        IDictionary<Guid, BaseItem?> entityCache)
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

        TrackDuplicateCandidate(item, libraryName, sources, duplicateCandidates, entityCache);

        if (sources.Count == 0)
        {
            if (analyzedPaths.Add(MediaSourceIdentity.PathKey(item.Path)))
            {
                long size = AnalyzeFallback(item, kind, accumulator);
                TrackMedia(item, libraryName, item.Path, size, movieInsights, seriesInsights, growthEntries, entityCache);
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
            AnalyzeSource(source.Path, source.Container, size, source.RunTimeTicks ?? item.RunTimeTicks, streams, kind, accumulator);
            TrackMedia(item, libraryName, source.Path, size, movieInsights, seriesInsights, growthEntries, entityCache);
        }

    }

    private long AnalyzeFallback(BaseItem item, MediaKind kind, StatisticsAccumulator accumulator)
    {
        if (string.IsNullOrWhiteSpace(item.Path))
        {
            return 0;
        }

        long size = MeasureFile(item.Path);
        AnalyzeSource(item.Path, null, size, item.RunTimeTicks, ReadStreams(item.Id), kind, accumulator);
        return size;
    }

    private void TrackMedia(
        BaseItem item,
        string libraryName,
        string? path,
        long size,
        IDictionary<Guid, MediaInsightAccumulator> movieInsights,
        IDictionary<Guid, MediaInsightAccumulator> seriesInsights,
        ICollection<MediaGrowthEntry> growthEntries,
        IDictionary<Guid, BaseItem?> entityCache)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        DateTime createdUtc = NormalizeUtc(item.DateCreated);
        growthEntries.Add(new MediaGrowthEntry(createdUtc, Math.Max(0, size)));
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
        BaseItem? series = GetCachedItem(episode.SeriesId, entityCache);
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

    internal static List<GrowthPoint> BuildGrowthTimeline(IEnumerable<MediaGrowthEntry> entries)
    {
        long cumulativeSize = 0;
        int cumulativeFiles = 0;
        List<GrowthPoint> timeline = [];

        foreach (IGrouping<DateTime, MediaGrowthEntry> day in entries
            .GroupBy(entry => entry.DateUtc.Date)
            .OrderBy(group => group.Key))
        {
            cumulativeSize += day.Sum(entry => entry.SizeBytes);
            cumulativeFiles += day.Count();
            timeline.Add(new GrowthPoint
            {
                DateUtc = DateTime.SpecifyKind(day.Key, DateTimeKind.Utc),
                CumulativeSizeBytes = cumulativeSize,
                CumulativeFileCount = cumulativeFiles
            });
        }

        return timeline;
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
        long? runTimeTicks,
        IReadOnlyList<MediaStream> streams,
        MediaKind kind,
        StatisticsAccumulator accumulator)
    {
        accumulator.AddMedia(size, kind);
        accumulator.AddContainer(
            string.IsNullOrWhiteSpace(container)
                ? MediaClassification.ContainerFromPath(path)
                : container.ToUpperInvariant(),
            size,
            path);

        MediaStream? videoStream = streams.FirstOrDefault(stream => stream.Type == MediaStreamType.Video);
        if (videoStream is not null)
        {
            accumulator.AddResolution(MediaClassification.ResolutionTier(videoStream.Width, videoStream.Height), size, path);
            accumulator.AddVideoCodec(MediaClassification.VideoCodec(videoStream.Codec), size, path);
            accumulator.AddDynamicRange(MediaClassification.DynamicRange(videoStream.VideoRangeType.ToString()), size, path);
            accumulator.AddVideoBitrate(MediaClassification.VideoBitrateTier(videoStream.BitRate, size, runTimeTicks), size, path);
        }

        foreach (string codec in streams
            .Where(stream => stream.Type == MediaStreamType.Audio)
            .Select(stream => MediaClassification.AudioCodec(stream.Codec, stream.Profile))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            accumulator.AddAudioCodec(codec, size, path);
            if (kind == MediaKind.Audio) accumulator.AddMusicAudioCodec(codec, size, path);
            else accumulator.AddVideoAudioCodec(codec, size, path);
        }

        foreach (string language in streams
            .Where(stream => stream.Type == MediaStreamType.Audio)
            .Select(stream => MediaClassification.Language(stream.Language))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            accumulator.AddAudioLanguage(language, size, path);
        }

        foreach (string language in streams
            .Where(stream => stream.Type == MediaStreamType.Subtitle)
            .Select(stream => MediaClassification.Language(stream.Language))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            accumulator.AddSubtitleLanguage(language, size, path);
        }
    }

    private void TrackDuplicateCandidate(
        BaseItem item,
        string libraryName,
        IReadOnlyCollection<MediaSourceInfo> sources,
        IDictionary<string, DuplicateAccumulator> candidates,
        IDictionary<Guid, BaseItem?> entityCache)
    {
        if (item is not Movie && item is not Episode)
        {
            return;
        }

        string title = item.Name ?? "Untitled";
        string subtitle = string.Empty;
        string mediaType = "Movie";
        int? productionYear = item.ProductionYear;
        int? duplicateSeasonNumber = null;
        int? duplicateEpisodeNumber = null;
        string seriesGroupKey = string.Empty;
        string identity;

        if (item is Episode episode)
        {
            mediaType = "Series";
            BaseItem? series = GetCachedItem(episode.SeriesId, entityCache);
            BaseItem? season = GetCachedItem(episode.SeasonId, entityCache);
            int? seasonNumber = season?.IndexNumber ?? episode.ParentIndexNumber;
            duplicateSeasonNumber = seasonNumber;
            duplicateEpisodeNumber = episode.IndexNumber;
            title = series?.Name ?? episode.SeriesName ?? "Unknown series";
            seriesGroupKey = DuplicateIdentity.SeriesKey(title, series?.ProviderIds);
            productionYear = series?.ProductionYear;
            string number = seasonNumber.HasValue && episode.IndexNumber.HasValue
                ? $"S{seasonNumber:00}E{episode.IndexNumber:00}"
                : "Episode";
            subtitle = $"{number} · {episode.Name ?? "Untitled"}";
            identity = DuplicateIdentity.EpisodeKey(
                title,
                seasonNumber,
                episode.IndexNumber,
                episode.Name,
                episode.ProviderIds,
                series?.ProviderIds);
            bool hasMultipleJellyfinSources = sources
                .Where(source => !string.IsNullOrWhiteSpace(source.Path))
                .Select(source => MediaSourceIdentity.PathKey(source.Path))
                .Distinct(StringComparer.Ordinal)
                .Skip(1)
                .Any();
            if (!seasonNumber.HasValue
                || !episode.IndexNumber.HasValue
                || !DuplicateIdentity.HasProviderIdentity(episode.ProviderIds)
                || hasMultipleJellyfinSources)
            {
                // Cross-item episode matching requires complete coordinates and a real
                // episode provider id. Jellyfin-merged sources remain grouped per item.
                identity += ":jellyfin-item:" + item.Id.ToString("N");
            }
        }
        else
        {
            identity = DuplicateIdentity.MovieKey(title, productionYear, item.ProviderIds);
        }

        if (!candidates.TryGetValue(identity, out DuplicateAccumulator? candidate))
        {
            candidate = new DuplicateAccumulator(
                item.Id,
                identity,
                mediaType,
                title,
                subtitle,
                productionYear,
                duplicateSeasonNumber,
                duplicateEpisodeNumber,
                seriesGroupKey,
                item is Episode ambiguousEpisode
                    && DuplicateReviewClassifier.IsAmbiguousEpisode(ambiguousEpisode.Name, ambiguousEpisode.IndexNumberEnd));
            candidates[identity] = candidate;
        }

        candidate.LibraryNames.Add(libraryName);
        if (sources
            .Where(source => !string.IsNullOrWhiteSpace(source.Path))
            .Select(source => MediaSourceIdentity.PathKey(source.Path))
            .Distinct(StringComparer.Ordinal)
            .Skip(1)
            .Any())
        {
            candidate.IsMergedVersion = true;
        }

        if (sources.Count > 0)
        {
            foreach (MediaSourceInfo source in sources)
            {
                if (!string.IsNullOrWhiteSpace(source.Path))
                {
                    candidate.Versions.TryAdd(
                        MediaSourceIdentity.PathKey(source.Path),
                        ToVersionDetail(item, source));
                }
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(item.Path))
        {
            return;
        }

        candidate.Versions.TryAdd(
            MediaSourceIdentity.PathKey(item.Path),
            new MediaVersionDetail
            {
                Name = item.Name ?? string.Empty,
                Path = item.Path,
                Container = MediaClassification.ContainerFromPath(item.Path),
                SizeBytes = MeasureFile(item.Path),
                RunTimeTicks = item.RunTimeTicks,
                Streams = ReadStreams(item.Id)
                    .Where(IsRelevantStream)
                    .Select(ToStreamDetail)
                    .ToList()
            });
    }

    private MediaVersionDetail ToVersionDetail(BaseItem item, MediaSourceInfo source) => new()
    {
        Name = string.IsNullOrWhiteSpace(source.Name) ? item.Name ?? string.Empty : source.Name,
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
    };

    private static List<DuplicateGroup> BuildDuplicateGroups(IEnumerable<DuplicateAccumulator> candidates)
    {
        return candidates
            .Where(candidate => candidate.Versions.Count > 1
                && (candidate.MediaType != "Series"
                    || candidate.IsMergedVersion
                    || DuplicateReviewClassifier.HaveComparableEpisodeRuntimes(candidate.Versions.Values)))
            .Select(candidate => new DuplicateGroup
            {
                GroupKey = DuplicateReviewClassifier.Hash(candidate.Identity),
                SeriesGroupKey = candidate.SeriesGroupKey,
                ItemId = candidate.ItemId,
                MediaType = candidate.MediaType,
                Title = candidate.Title,
                Subtitle = candidate.Subtitle,
                ProductionYear = candidate.ProductionYear,
                LibraryName = string.Join(", ", candidate.LibraryNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)),
                IsMergedVersion = candidate.IsMergedVersion,
                IsAmbiguous = candidate.IsAmbiguous,
                SeasonNumber = candidate.SeasonNumber,
                EpisodeNumber = candidate.EpisodeNumber,
                Versions = candidate.Versions.Values
                    .OrderBy(version => version.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(version => version.Path, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .ToList();
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

    private BaseItem? GetCachedItem(Guid itemId, IDictionary<Guid, BaseItem?> cache)
    {
        if (itemId == Guid.Empty) return null;
        if (cache.TryGetValue(itemId, out BaseItem? item)) return item;
        item = _libraryManager.GetItemById(itemId);
        cache[itemId] = item;
        return item;
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

    internal sealed record MediaGrowthEntry(DateTime DateUtc, long SizeBytes);

    private sealed class DuplicateAccumulator(
        Guid itemId,
        string identity,
        string mediaType,
        string title,
        string subtitle,
        int? productionYear,
        int? seasonNumber,
        int? episodeNumber,
        string seriesGroupKey,
        bool isAmbiguous)
    {
        internal Guid ItemId { get; } = itemId;

        internal string Identity { get; } = identity;

        internal string MediaType { get; } = mediaType;

        internal string Title { get; } = title;

        internal string Subtitle { get; } = subtitle;

        internal int? ProductionYear { get; } = productionYear;

        internal int? SeasonNumber { get; } = seasonNumber;

        internal int? EpisodeNumber { get; } = episodeNumber;

        internal string SeriesGroupKey { get; } = seriesGroupKey;

        internal bool IsAmbiguous { get; } = isAmbiguous;

        internal HashSet<string> LibraryNames { get; } = new(StringComparer.OrdinalIgnoreCase);

        internal bool IsMergedVersion { get; set; }

        internal Dictionary<string, MediaVersionDetail> Versions { get; } = new(StringComparer.Ordinal);
    }

    private void ApplyStoredState(StatisticsResult result)
    {
        foreach (DuplicateGroup group in result.DuplicateGroups.Where(group => string.IsNullOrWhiteSpace(group.GroupKey)))
        {
            string legacyIdentity = string.Join(
                ":",
                group.MediaType,
                group.Title,
                group.ProductionYear?.ToString() ?? "unknown",
                group.SeasonNumber?.ToString() ?? "unknown",
                group.EpisodeNumber?.ToString() ?? "unknown",
                group.Subtitle);
            group.GroupKey = DuplicateReviewClassifier.Hash(legacyIdentity.ToLowerInvariant());
        }

        _database.ApplyReviews(result.DuplicateGroups, Plugin.Instance?.Configuration.AutoAllow4KAnd1080P ?? false);
        if (!string.Equals(result.GrowthTimelineSource, "MediaDates", StringComparison.Ordinal)
            || result.GrowthTimeline.Count == 0)
        {
            result.GrowthTimeline = _database.LoadTimeline();
            result.GrowthTimelineSource = "ScanSnapshots";
        }
    }

    private static void RestoreAuxiliary(StatisticsSummary previous, StatisticsSummary current)
    {
        current.SubtitleSizeBytes = previous.SubtitleSizeBytes;
        current.SubtitleFileCount = previous.SubtitleFileCount;
        current.ImageSizeBytes = previous.ImageSizeBytes;
        current.ImageFileCount = previous.ImageFileCount;
        current.TrickplaySizeBytes = previous.TrickplaySizeBytes;
        current.TrickplayFolderCount = previous.TrickplayFolderCount;
        current.MetadataSizeBytes = previous.MetadataSizeBytes;
        current.MetadataFileCount = previous.MetadataFileCount;
        current.OtherFileSizeBytes = previous.OtherFileSizeBytes;
        current.OtherFileCount = previous.OtherFileCount;
        int auxiliaryFileCount = Math.Max(0, previous.TotalFileCount - previous.MediaFileCount);
        current.TotalFileCount = current.MediaFileCount + auxiliaryFileCount;
        current.TotalSizeBytes = current.MovieSizeBytes
            + current.EpisodeSizeBytes
            + current.OtherVideoSizeBytes
            + current.AudioSizeBytes
            + current.SubtitleSizeBytes
            + current.ImageSizeBytes
            + current.TrickplaySizeBytes
            + current.MetadataSizeBytes
            + current.OtherFileSizeBytes;
    }

    private static void AggregateAuxiliary(IEnumerable<LibraryScanStatistics> libraries, StatisticsSummary total)
    {
        List<LibraryScanStatistics> values = libraries.ToList();
        total.SubtitleSizeBytes = values.Sum(item => item.SubtitleSizeBytes);
        total.SubtitleFileCount = values.Sum(item => item.SubtitleFileCount);
        total.ImageSizeBytes = values.Sum(item => item.ImageSizeBytes);
        total.ImageFileCount = values.Sum(item => item.ImageFileCount);
        total.TrickplaySizeBytes = values.Sum(item => item.TrickplaySizeBytes);
        total.TrickplayFolderCount = values.Sum(item => item.TrickplayFolderCount);
        total.MetadataSizeBytes = values.Sum(item => item.MetadataSizeBytes);
        total.MetadataFileCount = values.Sum(item => item.MetadataFileCount);
        total.OtherFileSizeBytes = values.Sum(item => item.OtherFileSizeBytes);
        total.OtherFileCount = values.Sum(item => item.OtherFileCount);
        total.TotalFileCount = total.MediaFileCount + values.Sum(item => Math.Max(0, item.TotalFileCount - item.MediaFileCount));
        total.TotalSizeBytes = total.MovieSizeBytes
            + total.EpisodeSizeBytes
            + total.OtherVideoSizeBytes
            + total.AudioSizeBytes
            + total.SubtitleSizeBytes
            + total.ImageSizeBytes
            + total.TrickplaySizeBytes
            + total.MetadataSizeBytes
            + total.OtherFileSizeBytes;
    }

    private static int ScanPercent(int libraryIndex, int libraryCount, double libraryFraction)
    {
        if (libraryCount <= 0) return 90;
        double completed = Math.Clamp((libraryIndex + libraryFraction) / libraryCount, 0, 1);
        return 5 + (int)Math.Round(completed * 85);
    }

    private static void Report(
        Action<ScanProgress>? report,
        string status,
        string phase,
        string libraryName,
        int percent,
        int processed,
        int total,
        bool fullStorageScan)
    {
        report?.Invoke(new ScanProgress
        {
            Status = status,
            Phase = phase,
            LibraryName = libraryName,
            Percent = Math.Clamp(percent, 0, 100),
            ProcessedItems = processed,
            TotalItems = total,
            FullStorageScan = fullStorageScan
        });
    }
}

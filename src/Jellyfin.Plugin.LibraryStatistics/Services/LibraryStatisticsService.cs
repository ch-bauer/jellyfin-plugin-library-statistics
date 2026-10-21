using System.Diagnostics;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.LibraryStatistics.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.LibraryStatistics.Services;

/// <summary>Builds read-only statistics from Jellyfin's indexed media and stream metadata.</summary>
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
        var excluded = new HashSet<string>(
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
        var stopwatch = Stopwatch.StartNew();
        var result = new StatisticsResult();
        var total = new StatisticsAccumulator();
        var excluded = new HashSet<string>(
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
            var libraryId = Guid.Parse(folder.ItemId);
            var stats = new LibraryScanStatistics
            {
                LibraryId = folder.ItemId,
                LibraryName = folder.Name ?? "Unknown",
                CollectionType = folder.CollectionType?.ToString() ?? "mixed"
            };
            var accumulator = new StatisticsAccumulator();

            var items = _libraryManager.GetItemList(new InternalItemsQuery
            {
                AncestorIds = [libraryId],
                Recursive = true,
                IsFolder = false,
                MediaTypes = [MediaType.Video, MediaType.Audio]
            });

            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AnalyzeItem(item, accumulator);
            }

            accumulator.ApplyTo(stats);
            accumulator.MergeInto(total);
            result.Libraries.Add(stats);
        }

        total.ApplyTo(result.Total);
        stopwatch.Stop();
        result.DurationMilliseconds = stopwatch.ElapsedMilliseconds;
        result.ScannedAtUtc = DateTime.UtcNow;
        _store.Save(result);

        _logger.LogInformation(
            "Library Statistics: scan complete with {Files} media files and {Bytes} measured bytes in {Elapsed} ms.",
            result.Total.MediaFileCount,
            result.Total.TotalSizeBytes,
            result.DurationMilliseconds);
        return result;
    }

    private void AnalyzeItem(BaseItem item, StatisticsAccumulator accumulator)
    {
        if (string.IsNullOrWhiteSpace(item.Path))
        {
            return;
        }

        var kind = item switch
        {
            Movie => MediaKind.Movie,
            Episode => MediaKind.Episode,
            Audio => MediaKind.Audio,
            _ => MediaKind.OtherVideo
        };
        var size = MeasureFile(item.Path);
        accumulator.AddMedia(size, kind);
        accumulator.AddContainer(MediaClassification.ContainerFromPath(item.Path), size);

        try
        {
            var streams = _mediaSourceManager.GetMediaStreams(item.Id);
            var videoStream = streams.FirstOrDefault(stream => stream.Type == MediaStreamType.Video);
            if (videoStream is not null)
            {
                accumulator.AddResolution(MediaClassification.ResolutionTier(videoStream.Width, videoStream.Height), size);
                accumulator.AddVideoCodec(videoStream.Codec ?? "Unknown", size);
                accumulator.AddDynamicRange(MediaClassification.DynamicRange(videoStream.VideoRangeType.ToString()), size);
            }

            foreach (var codec in streams
                .Where(stream => stream.Type == MediaStreamType.Audio)
                .Select(stream => string.IsNullOrWhiteSpace(stream.Codec) ? "Unknown" : stream.Codec)
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                accumulator.AddAudioCodec(codec!, size);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogDebug(ex, "Library Statistics: stream metadata unavailable for {ItemId}.", item.Id);
        }
    }

    private long MeasureFile(string path)
    {
        try
        {
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            _logger.LogDebug(ex, "Library Statistics: file size unavailable for {Path}.", path);
            return 0;
        }
    }
}

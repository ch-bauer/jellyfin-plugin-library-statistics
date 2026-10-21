using Jellyfin.Plugin.LibraryStatistics.Models;

namespace Jellyfin.Plugin.LibraryStatistics.Services;

/// <summary>Mutable aggregation helpers used during a scan.</summary>
internal sealed class StatisticsAccumulator
{
    private readonly Dictionary<string, Bucket> _containers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Bucket> _resolutions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Bucket> _videoCodecs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Bucket> _audioCodecs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Bucket> _videoAudioCodecs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Bucket> _musicAudioCodecs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Bucket> _dynamicRanges = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Bucket> _videoBitrates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Bucket> _audioLanguages = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Bucket> _subtitleLanguages = new(StringComparer.OrdinalIgnoreCase);

    internal int MediaFileCount { get; private set; }
    internal int TotalFileCount { get; private set; }
    internal long TotalSizeBytes { get; private set; }
    internal int MovieCount { get; private set; }
    internal int EpisodeCount { get; private set; }
    internal int OtherVideoCount { get; private set; }
    internal int AudioCount { get; private set; }
    internal long MovieSizeBytes { get; private set; }
    internal long EpisodeSizeBytes { get; private set; }
    internal long OtherVideoSizeBytes { get; private set; }
    internal long AudioSizeBytes { get; private set; }
    internal long SubtitleSizeBytes { get; private set; }
    internal int SubtitleFileCount { get; private set; }
    internal long ImageSizeBytes { get; private set; }
    internal int ImageFileCount { get; private set; }
    internal long TrickplaySizeBytes { get; private set; }
    internal int TrickplayFolderCount { get; private set; }
    internal long MetadataSizeBytes { get; private set; }
    internal int MetadataFileCount { get; private set; }
    internal long OtherFileSizeBytes { get; private set; }
    internal int OtherFileCount { get; private set; }

    internal void AddMedia(long sizeBytes, MediaKind kind)
    {
        long size = Math.Max(0, sizeBytes);
        MediaFileCount++;
        TotalFileCount++;
        TotalSizeBytes += size;
        switch (kind)
        {
            case MediaKind.Movie: MovieCount++; MovieSizeBytes += size; break;
            case MediaKind.Episode: EpisodeCount++; EpisodeSizeBytes += size; break;
            case MediaKind.Audio: AudioCount++; AudioSizeBytes += size; break;
            default: OtherVideoCount++; OtherVideoSizeBytes += size; break;
        }
    }

    internal void AddAuxiliary(long sizeBytes, AuxiliaryKind kind, int count = 1)
    {
        long size = Math.Max(0, sizeBytes);
        int safeCount = Math.Max(0, count);
        TotalSizeBytes += size;
        TotalFileCount += safeCount;
        switch (kind)
        {
            case AuxiliaryKind.Subtitle: SubtitleSizeBytes += size; SubtitleFileCount += safeCount; break;
            case AuxiliaryKind.Image: ImageSizeBytes += size; ImageFileCount += safeCount; break;
            case AuxiliaryKind.Trickplay: TrickplaySizeBytes += size; TrickplayFolderCount++; break;
            case AuxiliaryKind.Metadata: MetadataSizeBytes += size; MetadataFileCount += safeCount; break;
            default: OtherFileSizeBytes += size; OtherFileCount += safeCount; break;
        }
    }

    internal void AddContainer(string value, long sizeBytes, string? path = null) => Add(_containers, value, sizeBytes, path);
    internal void AddResolution(string value, long sizeBytes, string? path = null) => Add(_resolutions, value, sizeBytes, path);
    internal void AddVideoCodec(string value, long sizeBytes, string? path = null) => Add(_videoCodecs, value, sizeBytes, path);
    internal void AddAudioCodec(string value, long sizeBytes, string? path = null) => Add(_audioCodecs, value, sizeBytes, path);
    internal void AddVideoAudioCodec(string value, long sizeBytes, string? path = null) => Add(_videoAudioCodecs, value, sizeBytes, path);
    internal void AddMusicAudioCodec(string value, long sizeBytes, string? path = null) => Add(_musicAudioCodecs, value, sizeBytes, path);
    internal void AddDynamicRange(string value, long sizeBytes, string? path = null) => Add(_dynamicRanges, value, sizeBytes, path);
    internal void AddVideoBitrate(string value, long sizeBytes, string? path = null) => Add(_videoBitrates, value, sizeBytes, path);
    internal void AddAudioLanguage(string value, long sizeBytes, string? path = null) => Add(_audioLanguages, value, sizeBytes, path);
    internal void AddSubtitleLanguage(string value, long sizeBytes, string? path = null) => Add(_subtitleLanguages, value, sizeBytes, path);

    internal void MergeInto(StatisticsAccumulator target)
    {
        target.MediaFileCount += MediaFileCount;
        target.TotalFileCount += TotalFileCount;
        target.TotalSizeBytes += TotalSizeBytes;
        target.MovieCount += MovieCount;
        target.EpisodeCount += EpisodeCount;
        target.OtherVideoCount += OtherVideoCount;
        target.AudioCount += AudioCount;
        target.MovieSizeBytes += MovieSizeBytes;
        target.EpisodeSizeBytes += EpisodeSizeBytes;
        target.OtherVideoSizeBytes += OtherVideoSizeBytes;
        target.AudioSizeBytes += AudioSizeBytes;
        target.SubtitleSizeBytes += SubtitleSizeBytes;
        target.SubtitleFileCount += SubtitleFileCount;
        target.ImageSizeBytes += ImageSizeBytes;
        target.ImageFileCount += ImageFileCount;
        target.TrickplaySizeBytes += TrickplaySizeBytes;
        target.TrickplayFolderCount += TrickplayFolderCount;
        target.MetadataSizeBytes += MetadataSizeBytes;
        target.MetadataFileCount += MetadataFileCount;
        target.OtherFileSizeBytes += OtherFileSizeBytes;
        target.OtherFileCount += OtherFileCount;
        Merge(_containers, target._containers);
        Merge(_resolutions, target._resolutions);
        Merge(_videoCodecs, target._videoCodecs);
        Merge(_audioCodecs, target._audioCodecs);
        Merge(_videoAudioCodecs, target._videoAudioCodecs);
        Merge(_musicAudioCodecs, target._musicAudioCodecs);
        Merge(_dynamicRanges, target._dynamicRanges);
        Merge(_videoBitrates, target._videoBitrates);
        Merge(_audioLanguages, target._audioLanguages);
        Merge(_subtitleLanguages, target._subtitleLanguages);
    }

    internal void ApplyTo(StatisticsSummary summary)
    {
        summary.MediaFileCount = MediaFileCount;
        summary.TotalFileCount = TotalFileCount;
        summary.TotalSizeBytes = TotalSizeBytes;
        summary.MovieCount = MovieCount;
        summary.EpisodeCount = EpisodeCount;
        summary.OtherVideoCount = OtherVideoCount;
        summary.AudioCount = AudioCount;
        summary.MovieSizeBytes = MovieSizeBytes;
        summary.EpisodeSizeBytes = EpisodeSizeBytes;
        summary.OtherVideoSizeBytes = OtherVideoSizeBytes;
        summary.AudioSizeBytes = AudioSizeBytes;
        summary.SubtitleSizeBytes = SubtitleSizeBytes;
        summary.SubtitleFileCount = SubtitleFileCount;
        summary.ImageSizeBytes = ImageSizeBytes;
        summary.ImageFileCount = ImageFileCount;
        summary.TrickplaySizeBytes = TrickplaySizeBytes;
        summary.TrickplayFolderCount = TrickplayFolderCount;
        summary.MetadataSizeBytes = MetadataSizeBytes;
        summary.MetadataFileCount = MetadataFileCount;
        summary.OtherFileSizeBytes = OtherFileSizeBytes;
        summary.OtherFileCount = OtherFileCount;
        summary.Containers = Convert(_containers);
        summary.Resolutions = Convert(_resolutions);
        summary.VideoCodecs = Convert(_videoCodecs);
        summary.AudioCodecs = Convert(_audioCodecs);
        summary.VideoAudioCodecs = Convert(_videoAudioCodecs);
        summary.MusicAudioCodecs = Convert(_musicAudioCodecs);
        summary.DynamicRanges = Convert(_dynamicRanges);
        summary.VideoBitrates = Convert(_videoBitrates);
        summary.AudioLanguages = Convert(_audioLanguages);
        summary.SubtitleLanguages = Convert(_subtitleLanguages);
    }

    private static void Add(Dictionary<string, Bucket> target, string value, long sizeBytes, string? path)
    {
        string key = string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
        if (!target.TryGetValue(key, out Bucket? bucket))
        {
            bucket = new Bucket();
            target[key] = bucket;
        }

        bucket.Count++;
        bucket.Size += Math.Max(0, sizeBytes);
        if (!string.IsNullOrWhiteSpace(path)) bucket.Paths.Add(path);
    }

    private static void Merge(Dictionary<string, Bucket> source, Dictionary<string, Bucket> target)
    {
        foreach (var entry in source)
        {
            if (!target.TryGetValue(entry.Key, out Bucket? bucket))
            {
                bucket = new Bucket();
                target[entry.Key] = bucket;
            }

            bucket.Count += entry.Value.Count;
            bucket.Size += entry.Value.Size;
            bucket.Paths.UnionWith(entry.Value.Paths);
        }
    }

    private static List<BreakdownEntry> Convert(Dictionary<string, Bucket> source) => source
        .OrderByDescending(entry => entry.Value.Count)
        .ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
        .Select(entry => new BreakdownEntry
        {
            Label = entry.Key,
            Count = entry.Value.Count,
            SizeBytes = entry.Value.Size,
            Paths = entry.Value.Paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList()
        })
        .ToList();

    private sealed class Bucket
    {
        internal int Count { get; set; }
        internal long Size { get; set; }
        internal HashSet<string> Paths { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}

internal enum MediaKind { Movie, Episode, OtherVideo, Audio }

internal enum AuxiliaryKind { Subtitle, Image, Trickplay, Metadata, Other }

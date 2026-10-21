using Jellyfin.Plugin.LibraryStatistics.Models;

namespace Jellyfin.Plugin.LibraryStatistics.Services;

/// <summary>Mutable aggregation helpers used during a scan.</summary>
internal sealed class StatisticsAccumulator
{
    private readonly Dictionary<string, (int Count, long Size)> _containers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (int Count, long Size)> _resolutions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (int Count, long Size)> _videoCodecs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (int Count, long Size)> _audioCodecs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (int Count, long Size)> _dynamicRanges = new(StringComparer.OrdinalIgnoreCase);

    internal int MediaFileCount { get; private set; }

    internal long TotalSizeBytes { get; private set; }

    internal int MovieCount { get; private set; }

    internal int EpisodeCount { get; private set; }

    internal int OtherVideoCount { get; private set; }

    internal int AudioCount { get; private set; }

    internal void AddMedia(long sizeBytes, MediaKind kind)
    {
        MediaFileCount++;
        TotalSizeBytes += Math.Max(0, sizeBytes);
        switch (kind)
        {
            case MediaKind.Movie:
                MovieCount++;
                break;
            case MediaKind.Episode:
                EpisodeCount++;
                break;
            case MediaKind.Audio:
                AudioCount++;
                break;
            default:
                OtherVideoCount++;
                break;
        }
    }

    internal void AddContainer(string value, long sizeBytes) => Add(_containers, value, sizeBytes);

    internal void AddResolution(string value, long sizeBytes) => Add(_resolutions, value, sizeBytes);

    internal void AddVideoCodec(string value, long sizeBytes) => Add(_videoCodecs, value, sizeBytes);

    internal void AddAudioCodec(string value, long sizeBytes) => Add(_audioCodecs, value, sizeBytes);

    internal void AddDynamicRange(string value, long sizeBytes) => Add(_dynamicRanges, value, sizeBytes);

    internal void MergeInto(StatisticsAccumulator target)
    {
        target.MediaFileCount += MediaFileCount;
        target.TotalSizeBytes += TotalSizeBytes;
        target.MovieCount += MovieCount;
        target.EpisodeCount += EpisodeCount;
        target.OtherVideoCount += OtherVideoCount;
        target.AudioCount += AudioCount;
        Merge(_containers, target._containers);
        Merge(_resolutions, target._resolutions);
        Merge(_videoCodecs, target._videoCodecs);
        Merge(_audioCodecs, target._audioCodecs);
        Merge(_dynamicRanges, target._dynamicRanges);
    }

    internal void ApplyTo(StatisticsSummary summary)
    {
        summary.MediaFileCount = MediaFileCount;
        summary.TotalSizeBytes = TotalSizeBytes;
        summary.MovieCount = MovieCount;
        summary.EpisodeCount = EpisodeCount;
        summary.OtherVideoCount = OtherVideoCount;
        summary.AudioCount = AudioCount;
        summary.Containers = Convert(_containers);
        summary.Resolutions = Convert(_resolutions);
        summary.VideoCodecs = Convert(_videoCodecs);
        summary.AudioCodecs = Convert(_audioCodecs);
        summary.DynamicRanges = Convert(_dynamicRanges);
    }

    private static void Add(Dictionary<string, (int Count, long Size)> target, string value, long sizeBytes)
    {
        var key = string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
        var current = target.GetValueOrDefault(key);
        target[key] = (current.Count + 1, current.Size + Math.Max(0, sizeBytes));
    }

    private static void Merge(
        Dictionary<string, (int Count, long Size)> source,
        Dictionary<string, (int Count, long Size)> target)
    {
        foreach (var entry in source)
        {
            var current = target.GetValueOrDefault(entry.Key);
            target[entry.Key] = (current.Count + entry.Value.Count, current.Size + entry.Value.Size);
        }
    }

    private static List<BreakdownEntry> Convert(Dictionary<string, (int Count, long Size)> source)
    {
        return source
            .OrderByDescending(entry => entry.Value.Size)
            .ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new BreakdownEntry
            {
                Label = entry.Key,
                Count = entry.Value.Count,
                SizeBytes = entry.Value.Size
            })
            .ToList();
    }
}

internal enum MediaKind
{
    Movie,
    Episode,
    OtherVideo,
    Audio
}

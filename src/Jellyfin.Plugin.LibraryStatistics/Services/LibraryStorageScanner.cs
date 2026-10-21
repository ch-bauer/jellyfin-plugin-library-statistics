using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.LibraryStatistics.Services;

/// <summary>Measures non-media files stored alongside a Jellyfin library.</summary>
public sealed class LibraryStorageScanner
{
    private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".3g2", ".3gp", ".aac", ".aiff", ".alac", ".ape", ".asf", ".avi", ".divx", ".dvr-ms",
        ".f4v", ".flac", ".flv", ".m2ts", ".m4a", ".m4v", ".mka", ".mkv", ".mov", ".mp2",
        ".mp3", ".mp4", ".mpeg", ".mpg", ".mts", ".ogg", ".ogm", ".ogv", ".opus", ".rm",
        ".rmvb", ".ts", ".vob", ".wav", ".webm", ".wma", ".wmv"
    };
    private static readonly HashSet<string> SubtitleExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ass", ".idx", ".js", ".jss", ".mpl", ".pjs", ".rt", ".sami", ".smi", ".srt",
        ".ssa", ".sub", ".sup", ".ttml", ".vtt"
    };
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".avif", ".bmp", ".gif", ".heic", ".heif", ".jpeg", ".jpg", ".png", ".tif", ".tiff", ".webp"
    };
    private static readonly HashSet<string> MetadataExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".json", ".nfo", ".xml"
    };

    private readonly ILogger<LibraryStorageScanner> _logger;

    /// <summary>Initializes a new instance of the <see cref="LibraryStorageScanner"/> class.</summary>
    public LibraryStorageScanner(ILogger<LibraryStorageScanner> logger)
    {
        _logger = logger;
    }

    internal void Scan(IEnumerable<string>? roots, StatisticsAccumulator accumulator, CancellationToken cancellationToken)
    {
        var pending = new Stack<string>((roots ?? []).Where(path => !string.IsNullOrWhiteSpace(path)));
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string directory = pending.Pop();
            string normalized;
            try
            {
                normalized = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!visited.Add(normalized) || !Directory.Exists(normalized)) continue;
                if ((File.GetAttributes(normalized) & FileAttributes.ReparsePoint) != 0) continue;

                if (Path.GetFileName(normalized).Equals(".trickplay", StringComparison.OrdinalIgnoreCase))
                {
                    MeasureTrickplay(normalized, accumulator, cancellationToken);
                    continue;
                }

                foreach (string child in Directory.EnumerateDirectories(normalized)) pending.Push(child);
                foreach (string file in Directory.EnumerateFiles(normalized)) AddSidecar(file, accumulator);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                _logger.LogDebug(ex, "Library Statistics: auxiliary storage unavailable for {Directory}.", directory);
            }
        }
    }

    private static void AddSidecar(string path, StatisticsAccumulator accumulator)
    {
        string extension = Path.GetExtension(path);
        if (MediaExtensions.Contains(extension)) return;
        long size = SafeLength(path);
        AuxiliaryKind kind = SubtitleExtensions.Contains(extension) ? AuxiliaryKind.Subtitle
            : ImageExtensions.Contains(extension) ? AuxiliaryKind.Image
            : MetadataExtensions.Contains(extension) ? AuxiliaryKind.Metadata
            : AuxiliaryKind.Other;
        accumulator.AddAuxiliary(size, kind);
    }

    private static void MeasureTrickplay(string directory, StatisticsAccumulator accumulator, CancellationToken cancellationToken)
    {
        long size = 0;
        int count = 0;
        try
        {
            foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                size += SafeLength(file);
                count++;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // Retain the measurable subset when a subdirectory becomes unavailable mid-scan.
        }

        accumulator.AddAuxiliary(size, AuxiliaryKind.Trickplay, count);
    }

    private static long SafeLength(string path)
    {
        try { return new FileInfo(path).Length; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { return 0; }
    }
}

namespace Jellyfin.Plugin.LibraryStatistics.Services;

/// <summary>Creates stable identities for media paths and merged source sets.</summary>
internal static class MediaSourceIdentity
{
    /// <summary>Normalizes a media path for scan-level de-duplication.</summary>
    public static string PathKey(string? path) =>
        (path ?? string.Empty)
            .Trim()
            .Replace('\\', '/')
            .TrimEnd('/')
            .ToUpperInvariant();

    /// <summary>Creates an order-independent key for one merged-version source set.</summary>
    public static string SourceSetKey(string mediaType, IEnumerable<string?> paths) =>
        mediaType.ToUpperInvariant() + "\u001F" + string.Join(
            '\u001E',
            paths.Select(PathKey)
                .Where(path => path.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal));
}

namespace Jellyfin.Plugin.LibraryStatistics.Services;

/// <summary>Pure media classification helpers.</summary>
internal static class MediaClassification
{
    internal static string ContainerFromPath(string? path)
    {
        var extension = Path.GetExtension(path ?? string.Empty).TrimStart('.');
        return string.IsNullOrWhiteSpace(extension) ? "Unknown" : extension.ToUpperInvariant();
    }

    internal static string ResolutionTier(int? width, int? height)
    {
        var w = width.GetValueOrDefault();
        var h = height.GetValueOrDefault();
        if (w >= 7000 || h >= 3800)
        {
            return "8K";
        }

        if (w >= 3000 || h >= 1800)
        {
            return "4K";
        }

        if (w >= 1800 || h >= 1000)
        {
            return "1080p";
        }

        if (w >= 1200 || h >= 700)
        {
            return "720p";
        }

        if (w > 0 || h > 0)
        {
            return "SD";
        }

        return "Unknown";
    }

    internal static string DynamicRange(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return "Unknown";
        }

        return value.Replace("DolbyVision", "Dolby Vision", StringComparison.OrdinalIgnoreCase);
    }
}

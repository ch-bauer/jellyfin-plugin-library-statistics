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
        int longEdge = Math.Max(width.GetValueOrDefault(), height.GetValueOrDefault());
        int shortEdge = Math.Min(width.GetValueOrDefault(), height.GetValueOrDefault());
        if (longEdge <= 0)
        {
            return "Unknown";
        }

        if (longEdge >= 6000 || shortEdge >= 3200)
        {
            return "8K";
        }

        if (longEdge >= 3200 || shortEdge >= 1700)
        {
            return "4K";
        }

        if (longEdge >= 1600 || shortEdge >= 900)
        {
            return "1080p";
        }

        if (longEdge >= 1120 || shortEdge >= 620)
        {
            return "720p";
        }

        if (longEdge >= 940 || shortEdge >= 528)
        {
            return "576p";
        }

        if (longEdge >= 720 || shortEdge >= 400)
        {
            return "480p";
        }

        return "SD";
    }

    internal static string VideoCodec(string? codec)
    {
        return (codec ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "hevc" or "h265" or "x265" => "HEVC",
            "h264" or "avc" or "x264" => "H.264",
            "av1" => "AV1",
            "vp9" => "VP9",
            "mpeg2video" or "mpeg2" => "MPEG-2",
            "mpeg4" => "MPEG-4",
            "vc1" => "VC-1",
            var value when value.Length > 0 => value.ToUpperInvariant(),
            _ => "Unknown"
        };
    }

    internal static string AudioCodec(string? codec, string? profile)
    {
        string normalized = (codec ?? string.Empty).Trim().ToLowerInvariant();
        string detail = (profile ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "truehd" when detail.Contains("atmos", StringComparison.Ordinal) => "TrueHD Atmos",
            "truehd" => "TrueHD",
            "eac3" when detail.Contains("atmos", StringComparison.Ordinal) => "E-AC-3 Atmos",
            "eac3" => "E-AC-3",
            "ac3" => "AC-3",
            "dts" when detail.Contains("ma", StringComparison.Ordinal) => "DTS-HD MA",
            "dts" when detail.Contains("hra", StringComparison.Ordinal) => "DTS-HD HRA",
            "dts" => "DTS",
            "aac" => "AAC",
            "flac" => "FLAC",
            "opus" => "Opus",
            "mp3" => "MP3",
            var value when value.Length > 0 => value.ToUpperInvariant(),
            _ => "Unknown"
        };
    }

    internal static string VideoBitrateTier(int? streamBitrate, long sizeBytes, long? runTimeTicks)
    {
        long bitrate = streamBitrate.GetValueOrDefault();
        if (bitrate <= 0 && sizeBytes > 0 && runTimeTicks.GetValueOrDefault() > 0)
        {
            double seconds = TimeSpan.FromTicks(runTimeTicks!.Value).TotalSeconds;
            bitrate = seconds > 0 ? (long)Math.Round(sizeBytes * 8d / seconds) : 0;
        }

        double mbps = bitrate / 1_000_000d;
        if (bitrate <= 0) return "Unknown";
        if (mbps < 2) return "< 2 Mbps";
        if (mbps < 4) return "2–4 Mbps";
        if (mbps < 8) return "4–8 Mbps";
        if (mbps < 16) return "8–16 Mbps";
        if (mbps < 32) return "16–32 Mbps";
        if (mbps < 60) return "32–60 Mbps";
        return "> 60 Mbps";
    }

    internal static string Language(string? language)
    {
        return string.IsNullOrWhiteSpace(language) ? "Unknown" : language.Trim().ToUpperInvariant();
    }

    internal static string DynamicRange(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return "Unknown";
        }

        string normalized = value.Replace("DolbyVision", "Dolby Vision", StringComparison.OrdinalIgnoreCase);
        if (normalized.Contains("DOVI", StringComparison.OrdinalIgnoreCase)) return "Dolby Vision";
        if (normalized.Contains("HDR10+", StringComparison.OrdinalIgnoreCase)) return "HDR10+";
        if (normalized.Contains("HDR", StringComparison.OrdinalIgnoreCase)) return "HDR10";
        if (normalized.Contains("HLG", StringComparison.OrdinalIgnoreCase)) return "HLG";
        if (normalized.Contains("SDR", StringComparison.OrdinalIgnoreCase)) return "SDR";
        return normalized;
    }
}

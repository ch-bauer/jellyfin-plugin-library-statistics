using System.Security.Cryptography;
using System.Text;
using Jellyfin.Plugin.LibraryStatistics.Models;

namespace Jellyfin.Plugin.LibraryStatistics.Services;

/// <summary>Creates stable duplicate fingerprints and applies conservative automatic review rules.</summary>
internal static class DuplicateReviewClassifier
{
    internal static string Hash(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    internal static string VersionSignature(IEnumerable<MediaVersionDetail> versions) => Hash(string.Join(
        "\n",
        versions.Select(version => MediaSourceIdentity.PathKey(version.Path))
            .OrderBy(path => path, StringComparer.Ordinal)));

    internal static bool IsExactly4KAnd1080P(IReadOnlyCollection<MediaVersionDetail> versions)
    {
        if (versions.Count != 2) return false;
        string[] tiers = versions.Select(ResolutionTier).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        return tiers.SequenceEqual(new[] { "1080p", "4K" }, StringComparer.Ordinal);
    }

    internal static bool IsAmbiguousEpisode(string? title, int? indexNumberEnd)
    {
        if (indexNumberEnd.HasValue) return true;
        string value = title ?? string.Empty;
        return value.Contains(" / ", StringComparison.Ordinal)
            || value.Contains(" | ", StringComparison.Ordinal)
            || value.Contains("Teil 1", StringComparison.OrdinalIgnoreCase)
                && value.Contains("Teil 2", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Part 1", StringComparison.OrdinalIgnoreCase)
                && value.Contains("Part 2", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolutionTier(MediaVersionDetail version)
    {
        MediaStreamDetail? video = version.Streams.FirstOrDefault(stream =>
            stream.Type.Equals("Video", StringComparison.OrdinalIgnoreCase));
        return MediaClassification.ResolutionTier(video?.Width, video?.Height);
    }
}

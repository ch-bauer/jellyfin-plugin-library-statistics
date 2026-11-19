using Jellyfin.Plugin.LibraryStatistics.Models;

namespace Jellyfin.Plugin.LibraryStatistics.Services;

/// <summary>Creates stable keys and exact version signatures for availability reviews.</summary>
internal static class AvailabilityReviewClassifier
{
    internal static string ReviewKey(string logicalMediaKey) => DuplicateReviewClassifier.Hash(logicalMediaKey);

    internal static string Signature(MediaAvailabilityDetail item) => DuplicateReviewClassifier.Hash(string.Join(
        "\n",
        item.Versions
            .Select(version => version.Path.Trim() + "\u001f" + version.Resolution)
            .Order(StringComparer.Ordinal)));
}

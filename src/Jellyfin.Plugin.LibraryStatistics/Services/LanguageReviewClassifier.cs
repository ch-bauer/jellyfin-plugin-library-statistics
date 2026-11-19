using Jellyfin.Plugin.LibraryStatistics.Models;

namespace Jellyfin.Plugin.LibraryStatistics.Services;

/// <summary>Creates stable keys and exact metadata signatures for language reviews.</summary>
internal static class LanguageReviewClassifier
{
    internal static string ReviewKey(string path) => DuplicateReviewClassifier.Hash(path.Trim());

    internal static string Signature(MediaLanguageDetail file) => DuplicateReviewClassifier.Hash(string.Join(
        "\n",
        string.Join('\u001f', file.AudioLanguages.Order(StringComparer.OrdinalIgnoreCase)),
        string.Join('\u001f', file.SubtitleLanguages.Order(StringComparer.OrdinalIgnoreCase))));
}

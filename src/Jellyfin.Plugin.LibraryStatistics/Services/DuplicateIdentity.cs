namespace Jellyfin.Plugin.LibraryStatistics.Services;

/// <summary>Builds conservative cross-item identities for duplicate detection.</summary>
internal static class DuplicateIdentity
{
    private static readonly string[] ProviderPriority = ["Imdb", "Tmdb", "Tvdb"];

    internal static string MovieKey(
        string? title,
        int? productionYear,
        IReadOnlyDictionary<string, string>? providerIds)
    {
        string provider = ProviderKey(providerIds);
        if (provider.Length > 0)
        {
            return "movie:provider:" + provider;
        }

        return "movie:title:" + Normalize(title) + ":year:" + (productionYear?.ToString() ?? "unknown");
    }

    internal static string EpisodeKey(
        string? seriesTitle,
        int? seasonNumber,
        int? episodeNumber,
        string? episodeTitle,
        IReadOnlyDictionary<string, string>? episodeProviderIds,
        IReadOnlyDictionary<string, string>? seriesProviderIds)
    {
        string episodeProvider = ProviderKey(episodeProviderIds);
        if (episodeProvider.Length > 0)
        {
            return "episode:provider:" + episodeProvider;
        }

        string seriesProvider = ProviderKey(seriesProviderIds);
        string series = seriesProvider.Length > 0 ? "provider:" + seriesProvider : "title:" + Normalize(seriesTitle);
        string titleFallback = seasonNumber.HasValue && episodeNumber.HasValue
            ? string.Empty
            : Normalize(episodeTitle);
        return "episode:series:" + series
            + ":season:" + (seasonNumber?.ToString() ?? "unknown")
            + ":episode:" + (episodeNumber?.ToString() ?? "unknown")
            + ":title:" + titleFallback;
    }

    private static string ProviderKey(IReadOnlyDictionary<string, string>? providerIds)
    {
        if (providerIds is null)
        {
            return string.Empty;
        }

        foreach (string providerName in ProviderPriority)
        {
            var match = providerIds.FirstOrDefault(pair =>
                pair.Key.Equals(providerName, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(pair.Value));
            if (!string.IsNullOrWhiteSpace(match.Value))
            {
                return providerName.ToLowerInvariant() + ":" + match.Value.Trim().ToLowerInvariant();
            }
        }

        return string.Empty;
    }

    private static string Normalize(string? value)
    {
        return new string((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }
}

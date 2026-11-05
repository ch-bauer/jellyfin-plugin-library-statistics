namespace Jellyfin.Plugin.LibraryStatistics.Services;

/// <summary>Aggregates one representative runtime for each logical media item.</summary>
internal sealed class UniqueRuntimeAccumulator
{
    private readonly Dictionary<string, long> _runtimes = new(StringComparer.Ordinal);

    internal long TotalTicks => _runtimes.Values.Sum();

    internal void Add(MediaKind kind, string identity, IEnumerable<long?> runtimeCandidates)
    {
        if (kind == MediaKind.Audio || string.IsNullOrWhiteSpace(identity))
        {
            return;
        }

        long runtime = runtimeCandidates
            .Select(value => value.GetValueOrDefault())
            .Where(value => value > 0)
            .DefaultIfEmpty()
            .Max();
        if (runtime <= 0)
        {
            return;
        }

        if (!_runtimes.TryGetValue(identity, out long current) || runtime > current)
        {
            _runtimes[identity] = runtime;
        }
    }
}

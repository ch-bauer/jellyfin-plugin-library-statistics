using Jellyfin.Plugin.LibraryStatistics.Services;
using Xunit;

namespace Jellyfin.Plugin.LibraryStatistics.Tests;

public sealed class UniqueRuntimeAccumulatorTests
{
    [Fact]
    public void TotalTicks_KeepsOnlyLongestRuntimeForDuplicateIdentity()
    {
        var accumulator = new UniqueRuntimeAccumulator();

        accumulator.Add("movie:alien", [TimeSpan.FromMinutes(116).Ticks]);
        accumulator.Add("movie:alien", [TimeSpan.FromMinutes(117).Ticks]);
        accumulator.Add("episode:dark:s01e01", [null, 0, TimeSpan.FromMinutes(52).Ticks]);

        Assert.Equal(TimeSpan.FromMinutes(169).Ticks, accumulator.TotalTicks);
    }

    [Fact]
    public void Add_UsesLongestRuntimeAmongMergedVersions()
    {
        var accumulator = new UniqueRuntimeAccumulator();

        accumulator.Add(
            "movie:blade-runner",
            [TimeSpan.FromMinutes(117).Ticks, TimeSpan.FromMinutes(118).Ticks]);

        Assert.Equal(TimeSpan.FromMinutes(118).Ticks, accumulator.TotalTicks);
    }
}

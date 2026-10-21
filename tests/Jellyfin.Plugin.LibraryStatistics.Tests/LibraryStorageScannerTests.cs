using Jellyfin.Plugin.LibraryStatistics.Models;
using Jellyfin.Plugin.LibraryStatistics.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.LibraryStatistics.Tests;

public sealed class LibraryStorageScannerTests
{
    [Fact]
    public void Scan_CountsSidecarsAndDoesNotCountIndexedMediaExtensionsAgain()
    {
        string root = Path.Combine(Path.GetTempPath(), "library-statistics-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllBytes(Path.Combine(root, "movie.mkv"), new byte[11]);
            File.WriteAllBytes(Path.Combine(root, "movie.srt"), new byte[7]);
            File.WriteAllBytes(Path.Combine(root, "poster.jpg"), new byte[5]);
            File.WriteAllBytes(Path.Combine(root, "movie.nfo"), new byte[3]);
            string trickplay = Path.Combine(root, ".trickplay");
            Directory.CreateDirectory(trickplay);
            File.WriteAllBytes(Path.Combine(trickplay, "tile.jpg"), new byte[13]);

            var accumulator = new StatisticsAccumulator();
            new LibraryStorageScanner(NullLogger<LibraryStorageScanner>.Instance)
                .Scan([root], accumulator, CancellationToken.None);
            var summary = new StatisticsSummary();
            accumulator.ApplyTo(summary);

            Assert.Equal(4, summary.TotalFileCount);
            Assert.Equal(7, summary.SubtitleSizeBytes);
            Assert.Equal(5, summary.ImageSizeBytes);
            Assert.Equal(3, summary.MetadataSizeBytes);
            Assert.Equal(13, summary.TrickplaySizeBytes);
            Assert.Equal(1, summary.TrickplayFolderCount);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}

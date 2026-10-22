using Jellyfin.Plugin.LibraryStatistics.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.LibraryStatistics.Services;

/// <summary>Runs one persistent background scan and exposes its progress.</summary>
public sealed class ScanCoordinator
{
    private readonly LibraryStatisticsService _statisticsService;
    private readonly ILogger<ScanCoordinator> _logger;
    private readonly object _sync = new();
    private ScanProgress _progress = new();
    private CancellationTokenSource? _cancellation;

    /// <summary>Initializes a new instance of the <see cref="ScanCoordinator"/> class.</summary>
    public ScanCoordinator(LibraryStatisticsService statisticsService, ILogger<ScanCoordinator> logger)
    {
        _statisticsService = statisticsService;
        _logger = logger;
    }

    /// <summary>Starts a scan, or returns the currently active scan.</summary>
    public ScanProgress Start(bool fullStorageScan)
    {
        lock (_sync)
        {
            if (_progress.Status == "Scanning") return Clone(_progress);
            _cancellation?.Dispose();
            _cancellation = new CancellationTokenSource();
            _progress = new ScanProgress
            {
                ScanId = Guid.NewGuid(),
                Status = "Scanning",
                Phase = "Queued",
                Percent = 0,
                FullStorageScan = fullStorageScan
            };
            Guid scanId = _progress.ScanId;
            CancellationToken token = _cancellation.Token;
            _ = Task.Run(() => RunAsync(scanId, fullStorageScan, token), CancellationToken.None);
            return Clone(_progress);
        }
    }

    /// <summary>Returns the most recent scan state.</summary>
    public ScanProgress GetProgress()
    {
        lock (_sync) return Clone(_progress);
    }

    /// <summary>Requests cancellation of the active scan.</summary>
    public bool Cancel(Guid scanId)
    {
        lock (_sync)
        {
            if (_progress.ScanId != scanId || _progress.Status != "Scanning") return false;
            _progress.Phase = "Cancelling";
            _cancellation?.Cancel();
            return true;
        }
    }

    private async Task RunAsync(Guid scanId, bool fullStorageScan, CancellationToken cancellationToken)
    {
        try
        {
            StatisticsResult? result = await _statisticsService.TryScanAsync(
                fullStorageScan,
                update => Update(scanId, update),
                cancellationToken).ConfigureAwait(false);
            if (result is null) Fail(scanId, "Another scan is already running.");
        }
        catch (OperationCanceledException)
        {
            lock (_sync)
            {
                if (_progress.ScanId == scanId)
                {
                    _progress.Status = "Cancelled";
                    _progress.Phase = "Scan cancelled";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Library Statistics: background scan failed.");
            Fail(scanId, "The scan failed. Check the Jellyfin server log.");
        }
    }

    private void Update(Guid scanId, ScanProgress update)
    {
        lock (_sync)
        {
            if (_progress.ScanId != scanId) return;
            update.ScanId = scanId;
            _progress = Clone(update);
        }
    }

    private void Fail(Guid scanId, string message)
    {
        lock (_sync)
        {
            if (_progress.ScanId != scanId) return;
            _progress.Status = "Failed";
            _progress.Phase = "Scan failed";
            _progress.Error = message;
        }
    }

    private static ScanProgress Clone(ScanProgress value) => new()
    {
        ScanId = value.ScanId,
        Status = value.Status,
        Phase = value.Phase,
        LibraryName = value.LibraryName,
        Percent = value.Percent,
        ProcessedItems = value.ProcessedItems,
        TotalItems = value.TotalItems,
        Error = value.Error,
        FullStorageScan = value.FullStorageScan
    };
}

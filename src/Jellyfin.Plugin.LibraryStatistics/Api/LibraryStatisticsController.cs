using System.Net.Mime;
using Jellyfin.Plugin.LibraryStatistics.Models;
using Jellyfin.Plugin.LibraryStatistics.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.LibraryStatistics.Api;

/// <summary>Admin-only API for library discovery and statistics scans.</summary>
[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("LibraryStatistics")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class LibraryStatisticsController : ControllerBase
{
    private readonly LibraryStatisticsService _statisticsService;
    private readonly ScanCoordinator _scanCoordinator;

    /// <summary>Initializes a new instance of the <see cref="LibraryStatisticsController"/> class.</summary>
    public LibraryStatisticsController(
        LibraryStatisticsService statisticsService,
        ScanCoordinator scanCoordinator)
    {
        _statisticsService = statisticsService;
        _scanCoordinator = scanCoordinator;
    }

    /// <summary>Lists all virtual folders and their current exclusion state.</summary>
    [HttpGet("Libraries")]
    [ProducesResponseType(typeof(IReadOnlyList<LibraryOption>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<LibraryOption>> GetLibraries() => Ok(_statisticsService.GetLibraries());

    /// <summary>Returns the latest persisted scan without starting a new one.</summary>
    [HttpGet("Latest")]
    [ProducesResponseType(typeof(StatisticsResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult<StatisticsResult> GetLatest()
    {
        var result = _statisticsService.GetLatest();
        return result is null ? NoContent() : Ok(result);
    }

    /// <summary>Starts a background scan of all non-excluded libraries.</summary>
    [HttpPost("Scan")]
    [ProducesResponseType(typeof(ScanProgress), StatusCodes.Status202Accepted)]
    public ActionResult<ScanProgress> Scan([FromBody] StartScanRequest? request)
    {
        ScanProgress progress = _scanCoordinator.Start(request?.FullStorageScan ?? false);
        return Accepted(progress);
    }

    /// <summary>Returns progress for the current or most recent scan.</summary>
    [HttpGet("Scan/Progress")]
    [ProducesResponseType(typeof(ScanProgress), StatusCodes.Status200OK)]
    public ActionResult<ScanProgress> GetScanProgress() => Ok(_scanCoordinator.GetProgress());

    /// <summary>Cancels the active scan.</summary>
    [HttpPost("Scan/{scanId:guid}/Cancel")]
    [ProducesResponseType(typeof(ScanProgress), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ScanProgress> CancelScan(Guid scanId)
    {
        if (!_scanCoordinator.Cancel(scanId))
        {
            return NotFound();
        }

        return Ok(_scanCoordinator.GetProgress());
    }

    /// <summary>Marks one exact duplicate version set as allowed or unreviewed.</summary>
    [HttpPost("Duplicates/{groupKey}/Review")]
    [ProducesResponseType(typeof(DuplicateGroup), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<DuplicateGroup> ReviewDuplicate(string groupKey, [FromBody] DuplicateReviewRequest request)
    {
        DuplicateGroup? group = _statisticsService.SetDuplicateReview(groupKey, request);
        return group is null ? NotFound() : Ok(group);
    }

    /// <summary>Marks several exact duplicate version sets as allowed or unreviewed.</summary>
    [HttpPost("Duplicates/ReviewBatch")]
    [ProducesResponseType(typeof(IReadOnlyList<DuplicateGroup>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<IReadOnlyList<DuplicateGroup>> ReviewDuplicates([FromBody] DuplicateReviewBatchRequest request)
    {
        IReadOnlyList<DuplicateGroup>? groups = _statisticsService.SetDuplicateReviews(request);
        return groups is null ? NotFound() : Ok(groups);
    }

    /// <summary>Clears all persisted manual duplicate review decisions.</summary>
    [HttpPost("Duplicates/Reviews/Reset")]
    [ProducesResponseType(typeof(DuplicateReviewResetResult), StatusCodes.Status200OK)]
    public ActionResult<DuplicateReviewResetResult> ResetDuplicateReviews() =>
        Ok(_statisticsService.ResetDuplicateReviews());
}

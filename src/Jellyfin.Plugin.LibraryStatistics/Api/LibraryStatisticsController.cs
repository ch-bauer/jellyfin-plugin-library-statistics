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
    private readonly StatisticsStore _store;

    /// <summary>Initializes a new instance of the <see cref="LibraryStatisticsController"/> class.</summary>
    public LibraryStatisticsController(LibraryStatisticsService statisticsService, StatisticsStore store)
    {
        _statisticsService = statisticsService;
        _store = store;
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
        var result = _store.Load();
        return result is null ? NoContent() : Ok(result);
    }

    /// <summary>Scans all non-excluded libraries.</summary>
    [HttpPost("Scan")]
    [ProducesResponseType(typeof(StatisticsResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StatisticsResult>> Scan(CancellationToken cancellationToken)
    {
        var result = await _statisticsService.TryScanAsync(cancellationToken).ConfigureAwait(false);
        return result is null
            ? Conflict(new { message = "A library statistics scan is already running." })
            : Ok(result);
    }
}

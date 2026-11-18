using Microsoft.AspNetCore.Mvc;
using UpstoxAiScanner.Application.Common;
using UpstoxAiScanner.Application.DTOs.Instruments;
using UpstoxAiScanner.Application.Interfaces;

namespace UpstoxAiScanner.Api.Controllers;

/// <summary>
/// Endpoints for managing stock universe constituents and synchronizing with Upstox instrument master.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class InstrumentsController : ControllerBase
{
    private readonly IInstrumentService _instrumentService;
    private readonly ILogger<InstrumentsController> _logger;

    public InstrumentsController(
        IInstrumentService instrumentService,
        ILogger<InstrumentsController> logger)
    {
        _instrumentService = instrumentService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves paginated and filtered instruments in the stock universe.
    /// </summary>
    /// <param name="universe">Universe name, e.g. "ALL", "NSE", "BSE", "NIFTY500". Default is "ALL".</param>
    /// <param name="isActive">Filter by active status (true/false).</param>
    /// <param name="query">Search term matching symbol or company name.</param>
    /// <param name="page">1-based page index (default: 1).</param>
    /// <param name="pageSize">Number of instruments per page (default: 50, max: 500).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<InstrumentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInstruments(
        [FromQuery] string? universe = "ALL",
        [FromQuery] bool? isActive = null,
        [FromQuery] string? query = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var filter = new InstrumentFilterDto
        {
            Universe = universe,
            IsActive = isActive,
            Query = query,
            Page = page,
            PageSize = pageSize
        };

        var result = await _instrumentService.GetInstrumentsAsync(filter, cancellationToken);
        return Ok(ApiResponse<PagedResult<InstrumentDto>>.Ok(result));
    }

    /// <summary>
    /// Retrieves a single instrument by its trading symbol or instrument key.
    /// </summary>
    /// <param name="symbol">Trading symbol (e.g. "RELIANCE") or instrument key (e.g. "NSE_EQ|INE002A01018").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("{symbol}")]
    [ProducesResponseType(typeof(ApiResponse<InstrumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InstrumentDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySymbol(string symbol, CancellationToken cancellationToken = default)
    {
        InstrumentDto? instrument = null;

        if (symbol.Contains('|'))
        {
            instrument = await _instrumentService.GetByInstrumentKeyAsync(symbol, cancellationToken);
        }
        else
        {
            instrument = await _instrumentService.GetBySymbolAsync(symbol, cancellationToken);
        }

        if (instrument == null)
        {
            return NotFound(ApiResponse<InstrumentDto>.Fail($"Instrument '{symbol}' was not found in the database."));
        }

        return Ok(ApiResponse<InstrumentDto>.Ok(instrument));
    }

    /// <summary>
    /// Synchronizes the specified stock universe (e.g. ALL, NSE, BSE, NIFTY500) with Upstox's official instrument master.
    /// Downloads the latest compressed instrument master, matches symbols/ISINs, and upserts into SQL Server.
    /// </summary>
    /// <param name="universe">Universe to synchronize (default: "ALL").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("sync")]
    [ProducesResponseType(typeof(ApiResponse<InstrumentSyncSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InstrumentSyncSummaryDto>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SyncInstruments(
        [FromQuery] string universe = "ALL",
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Triggered manual sync for universe {Universe}", universe);
            var summary = await _instrumentService.SyncUniverseInstrumentsAsync(universe, cancellationToken);
            return Ok(ApiResponse<InstrumentSyncSummaryDto>.Ok(summary, message: summary.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to synchronize instruments for universe {Universe}", universe);
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<InstrumentSyncSummaryDto>.Fail($"Sync failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Updates the active/inactive status of an instrument.
    /// </summary>
    /// <param name="symbol">Trading symbol (e.g. "RELIANCE").</param>
    /// <param name="isActive">Active status flag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPatch("{symbol}/status")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetActiveStatus(
        string symbol,
        [FromQuery] bool isActive,
        CancellationToken cancellationToken = default)
    {
        var updated = await _instrumentService.SetActiveStatusAsync(symbol, isActive, cancellationToken);
        if (!updated)
        {
            return NotFound(ApiResponse<bool>.Fail($"Instrument '{symbol}' was not found."));
        }

        return Ok(ApiResponse<bool>.Ok(true, message: $"Instrument '{symbol}' status updated to {(isActive ? "Active" : "Inactive")}."));
    }
}

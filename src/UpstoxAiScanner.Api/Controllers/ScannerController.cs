using Microsoft.AspNetCore.Mvc;
using UpstoxAiScanner.Application.Common;
using UpstoxAiScanner.Application.DTOs.Scanner;
using UpstoxAiScanner.Application.Interfaces;

namespace UpstoxAiScanner.Api.Controllers;

/// <summary>
/// Endpoints for Intraday and Swing trading scanners, rule-based scoring (0-100), and stock rankings.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ScannerController : ControllerBase
{
    private readonly IScannerService _scannerService;
    private readonly ILogger<ScannerController> _logger;

    public ScannerController(
        IScannerService scannerService,
        ILogger<ScannerController> logger)
    {
        _scannerService = scannerService;
        _logger = logger;
    }

    /// <summary>
    /// Executes or retrieves ranked daily Swing trading opportunities scored from 0 to 100 based on
    /// EMA 20/50/200 alignment, 20/50-day high breakouts, MACD crossovers, RSI oversold recovery, and volume surges.
    /// </summary>
    /// <param name="universe">Universe name (default: "NIFTY500").</param>
    /// <param name="signal">Filter by signal ("Strong Bullish", "Bullish", "Neutral", "Bearish", "Strong Bearish").</param>
    /// <param name="minScore">Minimum score threshold.</param>
    /// <param name="maxScore">Maximum score threshold.</param>
    /// <param name="limit">Max number of top ranked stocks to return (default: 20).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("swing")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ScanResultDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSwingOpportunities(
        [FromQuery] string universe = "NIFTY500",
        [FromQuery] string? signal = null,
        [FromQuery] decimal? minScore = null,
        [FromQuery] decimal? maxScore = null,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var results = await _scannerService.RunScanAsync("Swing", universe, limit, cancellationToken);

            // Apply in-memory filters if specified
            var filtered = results.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(signal))
            {
                filtered = filtered.Where(r => r.Signal.Equals(signal, StringComparison.OrdinalIgnoreCase));
            }
            if (minScore.HasValue)
            {
                filtered = filtered.Where(r => r.TechnicalScore >= minScore.Value);
            }
            if (maxScore.HasValue)
            {
                filtered = filtered.Where(r => r.TechnicalScore <= maxScore.Value);
            }

            var list = filtered.ToList();
            return Ok(ApiResponse<IReadOnlyList<ScanResultDto>>.Ok(list, message: $"Found {list.Count} swing trading opportunities."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing swing scan: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<IReadOnlyList<ScanResultDto>>.Fail($"Swing scan failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Executes or retrieves ranked 5-minute Intraday momentum opportunities scored from 0 to 100 based on
    /// 5m MACD crossovers, fast EMA trends, intraday breakout detection, and volume surges.
    /// </summary>
    /// <param name="universe">Universe name (default: "NIFTY500").</param>
    /// <param name="signal">Filter by signal ("Strong Bullish", "Bullish", "Neutral", "Bearish", "Strong Bearish").</param>
    /// <param name="minScore">Minimum score threshold.</param>
    /// <param name="maxScore">Maximum score threshold.</param>
    /// <param name="limit">Max number of top ranked stocks to return (default: 20).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("intraday")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ScanResultDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIntradayOpportunities(
        [FromQuery] string universe = "NIFTY500",
        [FromQuery] string? signal = null,
        [FromQuery] decimal? minScore = null,
        [FromQuery] decimal? maxScore = null,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var results = await _scannerService.RunScanAsync("Intraday", universe, limit, cancellationToken);

            var filtered = results.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(signal))
            {
                filtered = filtered.Where(r => r.Signal.Equals(signal, StringComparison.OrdinalIgnoreCase));
            }
            if (minScore.HasValue)
            {
                filtered = filtered.Where(r => r.TechnicalScore >= minScore.Value);
            }
            if (maxScore.HasValue)
            {
                filtered = filtered.Where(r => r.TechnicalScore <= maxScore.Value);
            }

            var list = filtered.ToList();
            return Ok(ApiResponse<IReadOnlyList<ScanResultDto>>.Ok(list, message: $"Found {list.Count} intraday momentum opportunities."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing intraday scan: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<IReadOnlyList<ScanResultDto>>.Fail($"Intraday scan failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Runs an on-demand scan and technical scoring evaluation for a single stock symbol.
    /// </summary>
    /// <param name="symbol">Stock symbol (e.g. "RELIANCE") or Upstox instrument key.</param>
    /// <param name="scanType">"Swing" (daily) or "Intraday" (5m).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("stock/{symbol}")]
    [ProducesResponseType(typeof(ApiResponse<ScanResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ScanResultDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ScanStock(
        string symbol,
        [FromQuery] string scanType = "Swing",
        CancellationToken cancellationToken = default)
    {
        var result = await _scannerService.ScanSingleInstrumentAsync(symbol, scanType, cancellationToken);
        if (result == null)
        {
            return NotFound(ApiResponse<ScanResultDto>.Fail($"Stock '{symbol}' could not be scanned or has insufficient candle data."));
        }

        return Ok(ApiResponse<ScanResultDto>.Ok(result, message: $"{scanType} scan complete for {symbol}. Score: {result.TechnicalScore} ({result.Signal})."));
    }

    /// <summary>
    /// Triggers an on-demand universe scan and persists all scored results to SQL Server.
    /// </summary>
    /// <param name="scanType">"Swing" or "Intraday".</param>
    /// <param name="universe">Universe name (e.g. "NIFTY500").</param>
    /// <param name="limit">Max number of top stocks to return (default: 20).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("run")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ScanResultDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> TriggerScan(
        [FromQuery] string scanType = "Swing",
        [FromQuery] string universe = "NIFTY500",
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var results = await _scannerService.RunScanAsync(scanType, universe, limit, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ScanResultDto>>.Ok(results, message: $"Scan completed. Found {results.Count} ranked candidates."));
    }

    /// <summary>
    /// Retrieves historical persisted scan results from the database.
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ScanResultDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetScanHistory(
        [FromQuery] string scanType = "Swing",
        [FromQuery] string? universe = "NIFTY500",
        [FromQuery] string? signal = null,
        [FromQuery] decimal? minScore = null,
        [FromQuery] decimal? maxScore = null,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var filter = new ScannerFilterDto
        {
            Universe = universe,
            Signal = signal,
            MinScore = minScore,
            MaxScore = maxScore,
            Limit = limit
        };

        var history = await _scannerService.GetLatestScanResultsAsync(scanType, filter, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ScanResultDto>>.Ok(history));
    }
}

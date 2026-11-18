using Microsoft.AspNetCore.Mvc;
using UpstoxAiScanner.Application.Common;
using UpstoxAiScanner.Application.DTOs.Ai;
using UpstoxAiScanner.Application.DTOs.MarketData;
using UpstoxAiScanner.Application.DTOs.Scanner;
using UpstoxAiScanner.Application.Interfaces;

namespace UpstoxAiScanner.Api.Controllers;

/// <summary>
/// Endpoints for Intraday and Swing trading scanners, rule-based scoring (0-100), market hours, background services, and AI explanations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ScannerController : ControllerBase
{
    private readonly IScannerService _scannerService;
    private readonly IMarketHoursService _marketHoursService;
    private readonly IBackgroundScannerControllerService _backgroundService;
    private readonly IUpstoxWebSocketClient _webSocketClient;
    private readonly IAiExplanationService _aiExplanationService;
    private readonly ILogger<ScannerController> _logger;

    public ScannerController(
        IScannerService scannerService,
        IMarketHoursService marketHoursService,
        IBackgroundScannerControllerService backgroundService,
        IUpstoxWebSocketClient webSocketClient,
        IAiExplanationService aiExplanationService,
        ILogger<ScannerController> logger)
    {
        _scannerService = scannerService;
        _marketHoursService = marketHoursService;
        _backgroundService = backgroundService;
        _webSocketClient = webSocketClient;
        _aiExplanationService = aiExplanationService;
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
    /// Generates grounded AI technical analysis and market commentary for a specific stock on-demand.
    /// Strictly grounded in actual quantitative indicators; zero price hallucination.
    /// </summary>
    /// <param name="symbol">Stock symbol (e.g. "RELIANCE") or instrument key.</param>
    /// <param name="scanType">"Swing" (daily) or "Intraday" (5m).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("stock/{symbol}/ai-explain")]
    [ProducesResponseType(typeof(ApiResponse<AiExplanationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AiExplanationDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExplainStock(
        string symbol,
        [FromQuery] string scanType = "Swing",
        CancellationToken cancellationToken = default)
    {
        var explanation = await _aiExplanationService.ExplainStockAsync(symbol, scanType, cancellationToken);
        if (explanation == null)
        {
            return NotFound(ApiResponse<AiExplanationDto>.Fail($"Stock '{symbol}' could not be scanned or explained."));
        }

        return Ok(ApiResponse<AiExplanationDto>.Ok(explanation, message: $"AI technical explanation generated for {symbol} ({explanation.Provider})."));
    }

    /// <summary>
    /// Generates grounded AI technical analysis and market commentary for a pre-computed scan result.
    /// </summary>
    /// <param name="scanResult">Pre-computed scan result payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("ai-explain")]
    [ProducesResponseType(typeof(ApiResponse<AiExplanationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AiExplanationDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExplainScanResult(
        [FromBody] ScanResultDto scanResult,
        CancellationToken cancellationToken = default)
    {
        if (scanResult == null || string.IsNullOrWhiteSpace(scanResult.Symbol))
        {
            return BadRequest(ApiResponse<AiExplanationDto>.Fail("A valid ScanResultDto with symbol is required."));
        }

        var explanation = await _aiExplanationService.ExplainScanResultAsync(scanResult, cancellationToken);
        return Ok(ApiResponse<AiExplanationDto>.Ok(explanation, message: $"AI technical explanation generated for {scanResult.Symbol} ({explanation.Provider})."));
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

    /// <summary>
    /// Returns the real-time Indian stock market session phase (PreOpen, RegularOpen, PostClose, Closed),
    /// current IST time, and countdowns to open/close.
    /// </summary>
    [HttpGet("market-status")]
    [ProducesResponseType(typeof(ApiResponse<MarketSessionStatus>), StatusCodes.Status200OK)]
    public IActionResult GetMarketStatus()
    {
        var status = _marketHoursService.GetSessionStatus();
        return Ok(ApiResponse<MarketSessionStatus>.Ok(status, message: status.Message));
    }

    /// <summary>
    /// Returns telemetry and execution statistics of the hosted background scanner service.
    /// </summary>
    [HttpGet("background/status")]
    [ProducesResponseType(typeof(ApiResponse<BackgroundScannerStatusDto>), StatusCodes.Status200OK)]
    public IActionResult GetBackgroundScannerStatus()
    {
        var status = _backgroundService.GetStatus();
        return Ok(ApiResponse<BackgroundScannerStatusDto>.Ok(status));
    }

    /// <summary>
    /// Dynamically enables or pauses the scheduled background scanning loop.
    /// </summary>
    [HttpPost("background/toggle")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public IActionResult ToggleBackgroundScanning([FromQuery] bool enabled)
    {
        _backgroundService.SetEnabled(enabled);
        return Ok(ApiResponse<bool>.Ok(enabled, message: $"Background scanning {(enabled ? "enabled" : "paused")}."));
    }

    /// <summary>
    /// Overrides market hours checking, allowing automated background scanning to run 24/7 for testing and demonstrations.
    /// </summary>
    [HttpPost("background/ignore-market-hours")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public IActionResult SetIgnoreMarketHours([FromQuery] bool ignore)
    {
        _backgroundService.SetIgnoreMarketHours(ignore);
        return Ok(ApiResponse<bool>.Ok(ignore, message: $"Market hours override set to {ignore}."));
    }

    /// <summary>
    /// Returns the list of instrument keys actively subscribed to the real-time WebSocket market data feed.
    /// </summary>
    [HttpGet("websocket/subscriptions")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<string>>), StatusCodes.Status200OK)]
    public IActionResult GetWebSocketSubscriptions()
    {
        var subscriptions = _webSocketClient.Subscriptions;
        return Ok(ApiResponse<IReadOnlyCollection<string>>.Ok(subscriptions, message: $"{subscriptions.Count} active WebSocket subscriptions."));
    }

    /// <summary>
    /// Subscribes instrument keys to the real-time WebSocket market data feed.
    /// </summary>
    [HttpPost("websocket/subscribe")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SubscribeWebSocket(
        [FromQuery] string instrumentKeys,
        [FromQuery] string mode = "full",
        CancellationToken cancellationToken = default)
    {
        var keys = instrumentKeys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (keys.Length == 0)
        {
            return BadRequest(ApiResponse<bool>.Fail("At least one instrument key must be specified."));
        }

        await _webSocketClient.SubscribeAsync(keys, mode, cancellationToken);
        return Ok(ApiResponse<bool>.Ok(true, message: $"Subscribed {keys.Length} instruments to WebSocket feed ({mode})."));
    }
}

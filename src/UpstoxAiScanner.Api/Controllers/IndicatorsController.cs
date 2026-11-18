using Microsoft.AspNetCore.Mvc;
using UpstoxAiScanner.Application.Common;
using UpstoxAiScanner.Application.DTOs.TechnicalAnalysis;
using UpstoxAiScanner.Application.Interfaces;
using UpstoxAiScanner.Domain.Models;

namespace UpstoxAiScanner.Api.Controllers;

/// <summary>
/// Technical indicator calculations and quantitative momentum analysis endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class IndicatorsController : ControllerBase
{
    private readonly ITechnicalAnalysisService _technicalAnalysisService;
    private readonly ILogger<IndicatorsController> _logger;

    public IndicatorsController(
        ITechnicalAnalysisService technicalAnalysisService,
        ILogger<IndicatorsController> logger)
    {
        _technicalAnalysisService = technicalAnalysisService;
        _logger = logger;
    }

    /// <summary>
    /// Calculates complete technical indicators (RSI 14, MACD 12/26/9, EMA 20/50/200, Breakouts, and Volume Analysis)
    /// for a given stock symbol or Upstox instrument key.
    /// </summary>
    /// <param name="symbol">Stock symbol (e.g. "RELIANCE") or instrument key (e.g. "NSE_EQ|INE002A01018").</param>
    /// <param name="interval">Candle interval: "1d" for daily swing analysis (default), "5m" for intraday momentum.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("{symbol}")]
    [ProducesResponseType(typeof(ApiResponse<TechnicalAnalysisDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TechnicalAnalysisDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetIndicators(
        string symbol,
        [FromQuery] string interval = "1d",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return BadRequest(ApiResponse<TechnicalAnalysisDto>.Fail("Symbol cannot be empty.", "INVALID_INPUT"));
        }

        try
        {
            var analysis = await _technicalAnalysisService.AnalyzeInstrumentAsync(symbol, interval, cancellationToken);
            if (analysis == null)
            {
                return NotFound(ApiResponse<TechnicalAnalysisDto>.Fail($"Insufficient candle data available for '{symbol}'.", "DATA_NOT_FOUND"));
            }

            return Ok(ApiResponse<TechnicalAnalysisDto>.Ok(analysis));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing indicators for {Symbol}: {Message}", symbol, ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<TechnicalAnalysisDto>.Fail($"Analysis failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Computes technical indicators directly from an arbitrary array of OHLCV candles (useful for backtesting or client-supplied candles).
    /// </summary>
    /// <param name="request">Candle collection and symbol parameters.</param>
    [HttpPost("calculate")]
    [ProducesResponseType(typeof(ApiResponse<TechnicalAnalysisDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TechnicalAnalysisDto>), StatusCodes.Status400BadRequest)]
    public IActionResult CalculateIndicators([FromBody] CalculateIndicatorsRequest request)
    {
        if (request == null || request.Candles == null || request.Candles.Count == 0)
        {
            return BadRequest(ApiResponse<TechnicalAnalysisDto>.Fail("Candle data is required.", "INVALID_INPUT"));
        }

        var analysis = _technicalAnalysisService.AnalyzeCandles(
            request.Symbol ?? "CUSTOM",
            request.InstrumentKey ?? "CUSTOM",
            request.Interval ?? "1d",
            request.Candles);

        return Ok(ApiResponse<TechnicalAnalysisDto>.Ok(analysis));
    }
}

public class CalculateIndicatorsRequest
{
    public string? Symbol { get; set; } = "CUSTOM";
    public string? InstrumentKey { get; set; } = "CUSTOM";
    public string? Interval { get; set; } = "1d";
    public List<Candle> Candles { get; set; } = new();
}

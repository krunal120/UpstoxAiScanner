namespace UpstoxAiScanner.Application.DTOs.Scanner;

/// <summary>
/// Technical scan result DTO representing scored and classified trading opportunities.
/// </summary>
public class ScanResultDto
{
    public long Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string InstrumentKey { get; set; } = string.Empty;
    public string ScanType { get; set; } = "Swing"; // "Swing" or "Intraday"
    public DateTimeOffset Timestamp { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal TechnicalScore { get; set; }
    public string Signal { get; set; } = "Neutral";
    public string? BreakoutStatus { get; set; }
    public string? VolumeStatus { get; set; }
    public string? Explanation { get; set; }

    // Quick indicator snapshot metrics
    public decimal? Rsi { get; set; }
    public decimal? Macd { get; set; }
    public decimal? Ema20 { get; set; }
    public decimal? Ema50 { get; set; }
    public decimal? Ema200 { get; set; }
    public decimal? VolumeRatio { get; set; }
}

/// <summary>
/// Query filters for retrieving ranked scan results.
/// </summary>
public class ScannerFilterDto
{
    /// <summary>
    /// Stock universe (default: "NIFTY500").
    /// </summary>
    public string? Universe { get; set; } = "NIFTY500";

    /// <summary>
    /// Filter by signal classification ("Strong Bullish", "Bullish", "Neutral", "Bearish", "Strong Bearish").
    /// </summary>
    public string? Signal { get; set; }

    /// <summary>
    /// Minimum technical score (0 to 100).
    /// </summary>
    public decimal? MinScore { get; set; }

    /// <summary>
    /// Maximum technical score (0 to 100).
    /// </summary>
    public decimal? MaxScore { get; set; }

    /// <summary>
    /// Maximum number of ranked results to return (default: 20, max: 200).
    /// </summary>
    public int Limit { get; set; } = 20;
}

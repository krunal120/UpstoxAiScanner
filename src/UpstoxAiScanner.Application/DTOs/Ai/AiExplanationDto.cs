namespace UpstoxAiScanner.Application.DTOs.Ai;

/// <summary>
/// Structured institutional-grade market commentary explaining a stock's technical score and setup.
/// Grounded strictly in actual mathematically computed indicators without hallucination.
/// </summary>
public class AiExplanationDto
{
    public string Symbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string ScanType { get; set; } = "Swing";
    public decimal TechnicalScore { get; set; }
    public string Signal { get; set; } = "Neutral";
    public decimal CurrentPrice { get; set; }

    /// <summary>
    /// Executive 2-sentence summary of the market situation.
    /// </summary>
    public string ExecutiveSummary { get; set; } = string.Empty;

    /// <summary>
    /// Key technical forces driving the score (EMA alignment, RSI momentum, Breakout status, Volume surge).
    /// </summary>
    public IReadOnlyList<string> KeyDrivers { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Analysis of key price levels, resistance hurdles, and support floors.
    /// </summary>
    public string SupportResistanceLevels { get; set; } = string.Empty;

    /// <summary>
    /// Critical risk factors, potential exhaustion signals, and invalidation criteria.
    /// </summary>
    public string RiskFactors { get; set; } = string.Empty;

    /// <summary>
    /// Comprehensive multi-paragraph market commentary.
    /// </summary>
    public string FullCommentary { get; set; } = string.Empty;

    /// <summary>
    /// Provider used: "OpenAI" (Live LLM) or "RuleBasedFinancialEngine" (Deterministic Fallback).
    /// </summary>
    public string Provider { get; set; } = "RuleBasedFinancialEngine";

    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Request parameters for triggering on-demand technical explanations.
/// </summary>
public class ExplainStockRequestDto
{
    public string Symbol { get; set; } = string.Empty;
    public string ScanType { get; set; } = "Swing";
}

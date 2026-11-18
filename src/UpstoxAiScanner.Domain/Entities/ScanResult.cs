namespace UpstoxAiScanner.Domain.Entities;

/// <summary>
/// Persisted result of an automated intraday or swing technical scan.
/// </summary>
public class ScanResult
{
    public long Id { get; set; }
    public int InstrumentId { get; set; }
    public Instrument? Instrument { get; set; }

    /// <summary>
    /// Scan type: "Intraday" or "Swing"
    /// </summary>
    public required string ScanType { get; set; }

    public DateTimeOffset Timestamp { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal TechnicalScore { get; set; }

    /// <summary>
    /// Signal: "Bullish", "Bearish", "Neutral"
    /// </summary>
    public required string Signal { get; set; }

    public string? BreakoutStatus { get; set; }
    public string? VolumeStatus { get; set; }
    public string? Explanation { get; set; }
}

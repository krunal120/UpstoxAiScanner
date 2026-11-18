namespace UpstoxAiScanner.Application.DTOs.MarketData;

/// <summary>
/// Status and metrics of the current Indian stock market session (NSE/BSE).
/// </summary>
public class MarketSessionStatus
{
    public bool IsMarketOpen { get; set; }
    public string SessionPhase { get; set; } = "Closed"; // "PreOpen", "RegularOpen", "PostClose", "Closed"
    public DateTimeOffset CurrentTimeUtc { get; set; }
    public DateTimeOffset CurrentTimeIst { get; set; }
    public DateTimeOffset MarketOpenTimeTodayIst { get; set; }
    public DateTimeOffset MarketCloseTimeTodayIst { get; set; }
    public TimeSpan? TimeUntilOpen { get; set; }
    public TimeSpan? TimeUntilClose { get; set; }
    public bool IsWeekend { get; set; }
    public string Message { get; set; } = string.Empty;
}

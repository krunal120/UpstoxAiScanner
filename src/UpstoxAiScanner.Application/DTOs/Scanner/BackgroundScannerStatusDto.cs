namespace UpstoxAiScanner.Application.DTOs.Scanner;

/// <summary>
/// Status and execution telemetry for the automated background scanning service.
/// </summary>
public class BackgroundScannerStatusDto
{
    public bool IsEnabled { get; set; }
    public bool IsMarketOpen { get; set; }
    public bool IgnoreMarketHours { get; set; }
    public string MarketSessionPhase { get; set; } = "Closed";
    public DateTimeOffset? LastScanTimeUtc { get; set; }
    public DateTimeOffset? NextScheduledScanTimeUtc { get; set; }
    public int TotalScansExecuted { get; set; }
    public int IntradayScanIntervalSeconds { get; set; }
    public string Universe { get; set; } = "NIFTY500";
    public bool IsWebSocketConnected { get; set; }
    public int WebSocketActiveSubscriptionsCount { get; set; }
    public IReadOnlyCollection<string> WebSocketSubscriptions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<ScanResultDto> LatestTopSignals { get; set; } = Array.Empty<ScanResultDto>();
}

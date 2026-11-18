namespace UpstoxAiScanner.Application.Configuration;

/// <summary>
/// Configuration options for automated background scanning and WebSocket streaming.
/// </summary>
public class ScannerBackgroundOptions
{
    public const string SectionName = "Scanner";

    /// <summary>
    /// Enables or disables the automated background scanning loop.
    /// </summary>
    public bool EnableBackgroundScanning { get; set; } = true;

    /// <summary>
    /// Intraday scan interval in seconds (default: 300 = 5 minutes).
    /// </summary>
    public int IntradayScanIntervalSeconds { get; set; } = 300;

    /// <summary>
    /// Swing scan interval in minutes (default: 60 minutes).
    /// </summary>
    public int SwingScanIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// When true, allows background scanning to execute outside NSE market hours (useful for development, backtesting, and demo testing).
    /// </summary>
    public bool IgnoreMarketHours { get; set; } = false;

    /// <summary>
    /// Default stock universe to scan (default: "NIFTY500").
    /// </summary>
    public string Universe { get; set; } = "NIFTY500";

    /// <summary>
    /// Maximum number of constituents evaluated per background scan run.
    /// </summary>
    public int MaxConstituentsPerRun { get; set; } = 50;

    /// <summary>
    /// Whether to enable live/simulated WebSocket price streaming.
    /// </summary>
    public bool EnableWebSocketStreaming { get; set; } = true;

    /// <summary>
    /// WebSocket feed endpoint URL.
    /// </summary>
    public string WebSocketUrl { get; set; } = "wss://api.upstox.com/v3/feed/market-data-feed";
}

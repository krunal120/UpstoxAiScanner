using UpstoxAiScanner.Application.DTOs.MarketData;

namespace UpstoxAiScanner.Application.Interfaces;

/// <summary>
/// Service contract for Indian stock market hours, trading session calculations, and holiday validation.
/// </summary>
public interface IMarketHoursService
{
    /// <summary>
    /// Checks whether the regular trading session (09:15 to 15:30 IST on weekdays) is currently open.
    /// </summary>
    bool IsMarketOpen(DateTimeOffset? utcNow = null);

    /// <summary>
    /// Returns detailed session phase, IST timestamp, and time to open/close.
    /// </summary>
    MarketSessionStatus GetSessionStatus(DateTimeOffset? utcNow = null);

    /// <summary>
    /// Converts a UTC timestamp to Indian Standard Time (UTC+05:30).
    /// </summary>
    DateTimeOffset GetIstTime(DateTimeOffset? utcNow = null);
}

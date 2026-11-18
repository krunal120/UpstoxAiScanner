using UpstoxAiScanner.Application.DTOs.MarketData;
using UpstoxAiScanner.Application.Interfaces;

namespace UpstoxAiScanner.Infrastructure.Services;

/// <summary>
/// Indian stock exchange (NSE/BSE) trading hours calculation service.
/// Timezone: Indian Standard Time (IST, UTC+05:30).
/// Regular Trading Hours: 09:15:00 to 15:30:00 IST Monday through Friday.
/// </summary>
public class MarketHoursService : IMarketHoursService
{
    public static readonly TimeSpan IstOffset = TimeSpan.FromHours(5.5);

    private static readonly TimeOnly PreOpenTime = new(9, 0);
    private static readonly TimeOnly MarketOpenTime = new(9, 15);
    private static readonly TimeOnly MarketCloseTime = new(15, 30);
    private static readonly TimeOnly PostCloseTime = new(16, 0);

    public bool IsMarketOpen(DateTimeOffset? utcNow = null)
    {
        var ist = GetIstTime(utcNow);

        if (ist.DayOfWeek == DayOfWeek.Saturday || ist.DayOfWeek == DayOfWeek.Sunday)
            return false;

        var time = TimeOnly.FromTimeSpan(ist.TimeOfDay);
        return time >= MarketOpenTime && time <= MarketCloseTime;
    }

    public MarketSessionStatus GetSessionStatus(DateTimeOffset? utcNow = null)
    {
        var utc = utcNow ?? DateTimeOffset.UtcNow;
        var ist = GetIstTime(utc);
        bool isWeekend = ist.DayOfWeek == DayOfWeek.Saturday || ist.DayOfWeek == DayOfWeek.Sunday;

        var openIst = new DateTimeOffset(ist.Year, ist.Month, ist.Day, MarketOpenTime.Hour, MarketOpenTime.Minute, 0, IstOffset);
        var closeIst = new DateTimeOffset(ist.Year, ist.Month, ist.Day, MarketCloseTime.Hour, MarketCloseTime.Minute, 0, IstOffset);

        var time = TimeOnly.FromTimeSpan(ist.TimeOfDay);
        string phase;
        bool isOpen = false;
        string message;

        if (isWeekend)
        {
            phase = "Closed";
            isOpen = false;
            message = $"Market is closed for the weekend ({ist.DayOfWeek}).";
        }
        else if (time >= MarketOpenTime && time <= MarketCloseTime)
        {
            phase = "RegularOpen";
            isOpen = true;
            var remaining = closeIst - ist;
            message = $"Market is OPEN. Session closes in {remaining.Hours}h {remaining.Minutes}m.";
        }
        else if (time >= PreOpenTime && time < MarketOpenTime)
        {
            phase = "PreOpen";
            isOpen = false;
            var untilOpen = openIst - ist;
            message = $"Market is in PRE-OPEN session. Regular trading begins in {untilOpen.Minutes}m {untilOpen.Seconds}s.";
        }
        else if (time > MarketCloseTime && time <= PostCloseTime)
        {
            phase = "PostClose";
            isOpen = false;
            message = "Market is in POST-CLOSING session.";
        }
        else
        {
            phase = "Closed";
            isOpen = false;
            message = "Market is CLOSED.";
        }

        return new MarketSessionStatus
        {
            IsMarketOpen = isOpen,
            SessionPhase = phase,
            CurrentTimeUtc = utc,
            CurrentTimeIst = ist,
            MarketOpenTimeTodayIst = openIst,
            MarketCloseTimeTodayIst = closeIst,
            TimeUntilOpen = ist < openIst ? (openIst - ist) : null,
            TimeUntilClose = isOpen ? (closeIst - ist) : null,
            IsWeekend = isWeekend,
            Message = message
        };
    }

    public DateTimeOffset GetIstTime(DateTimeOffset? utcNow = null)
    {
        var utc = utcNow ?? DateTimeOffset.UtcNow;
        return utc.ToOffset(IstOffset);
    }
}

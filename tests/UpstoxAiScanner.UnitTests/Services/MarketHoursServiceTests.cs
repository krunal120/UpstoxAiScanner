using FluentAssertions;
using UpstoxAiScanner.Infrastructure.Services;
using Xunit;

namespace UpstoxAiScanner.UnitTests.Services;

public class MarketHoursServiceTests
{
    private readonly MarketHoursService _service = new();

    [Fact]
    public void GetIstTime_ConvertsUtcToIstCorrectly()
    {
        // 2026-03-18 04:00:00 UTC should be 2026-03-18 09:30:00 IST (+5:30)
        var utc = new DateTimeOffset(2026, 3, 18, 4, 0, 0, TimeSpan.Zero);
        var ist = _service.GetIstTime(utc);

        ist.Offset.Should().Be(TimeSpan.FromHours(5.5));
        ist.Hour.Should().Be(9);
        ist.Minute.Should().Be(30);
    }

    [Fact]
    public void IsMarketOpen_DuringRegularTradingHoursOnWeekday_ReturnsTrue()
    {
        // Wednesday 11:30 AM IST -> 06:00 AM UTC
        var utc = new DateTimeOffset(2026, 3, 18, 6, 0, 0, TimeSpan.Zero);
        bool isOpen = _service.IsMarketOpen(utc);

        isOpen.Should().BeTrue();

        var session = _service.GetSessionStatus(utc);
        session.IsMarketOpen.Should().BeTrue();
        session.SessionPhase.Should().Be("RegularOpen");
    }

    [Fact]
    public void IsMarketOpen_OnWeekend_ReturnsFalse()
    {
        // Saturday 11:30 AM IST
        var saturdayUtc = new DateTimeOffset(2026, 3, 21, 6, 0, 0, TimeSpan.Zero);
        bool isOpen = _service.IsMarketOpen(saturdayUtc);

        isOpen.Should().BeFalse();

        var session = _service.GetSessionStatus(saturdayUtc);
        session.IsMarketOpen.Should().BeFalse();
        session.IsWeekend.Should().BeTrue();
        session.SessionPhase.Should().Be("Closed");
    }

    [Fact]
    public void GetSessionStatus_DuringPreOpen_IdentifiesPreOpenPhase()
    {
        // Wednesday 09:05 AM IST -> 03:35 AM UTC
        var preOpenUtc = new DateTimeOffset(2026, 3, 18, 3, 35, 0, TimeSpan.Zero);
        var session = _service.GetSessionStatus(preOpenUtc);

        session.IsMarketOpen.Should().BeFalse();
        session.SessionPhase.Should().Be("PreOpen");
        session.TimeUntilOpen.Should().NotBeNull();
        session.TimeUntilOpen!.Value.TotalMinutes.Should().BeApproximately(10, 0.5);
    }

    [Fact]
    public void GetSessionStatus_DuringPostClose_IdentifiesPostClosePhase()
    {
        // Wednesday 15:45 PM IST -> 10:15 AM UTC
        var postCloseUtc = new DateTimeOffset(2026, 3, 18, 10, 15, 0, TimeSpan.Zero);
        var session = _service.GetSessionStatus(postCloseUtc);

        session.IsMarketOpen.Should().BeFalse();
        session.SessionPhase.Should().Be("PostClose");
    }

    [Fact]
    public void GetSessionStatus_AtNight_IdentifiesClosed()
    {
        // Wednesday 22:00 PM IST -> 16:30 PM UTC
        var nightUtc = new DateTimeOffset(2026, 3, 18, 16, 30, 0, TimeSpan.Zero);
        var session = _service.GetSessionStatus(nightUtc);

        session.IsMarketOpen.Should().BeFalse();
        session.SessionPhase.Should().Be("Closed");
    }
}

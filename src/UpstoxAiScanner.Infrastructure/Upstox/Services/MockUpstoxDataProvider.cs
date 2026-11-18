using UpstoxAiScanner.Application.DTOs.MarketData;

namespace UpstoxAiScanner.Infrastructure.Upstox.Services;

/// <summary>
/// Provides realistic deterministic mock market data for local development and testing when Upstox credentials are not present.
/// </summary>
public class MockUpstoxDataProvider
{
    private static readonly Dictionary<string, (string Symbol, decimal BasePrice, long BaseVolume)> KnownStocks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NSE_EQ|INE002A01018"] = ("RELIANCE", 2980.50m, 5200000),
        ["NSE_EQ|INE467B01029"] = ("TCS", 4120.00m, 2100000),
        ["NSE_EQ|INE009A01021"] = ("INFY", 1880.25m, 6800000),
        ["NSE_EQ|INE040A01034"] = ("HDFCBANK", 1680.75m, 11400000),
        ["NSE_EQ|INE090A01021"] = ("ICICIBANK", 1250.40m, 9300000),
        ["NSE_EQ|INE155A01022"] = ("TATAMOTORS", 995.60m, 8200000),
        ["NSE_EQ|INE062A01020"] = ("SBIN", 845.20m, 14200000),
        ["NSE_EQ|INE848E01016"] = ("NHPC", 98.40m, 23000000),
        ["NSE_INDEX|Nifty 50"] = ("NIFTY 50", 25250.00m, 250000000),
        ["NSE_INDEX|Nifty Bank"] = ("NIFTY BANK", 51800.00m, 180000000)
    };

    public IReadOnlyDictionary<string, MarketQuoteDto> GetQuotes(IEnumerable<string> instrumentKeys)
    {
        var result = new Dictionary<string, MarketQuoteDto>(StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow;

        foreach (var key in instrumentKeys.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var (symbol, basePrice, baseVolume) = ResolveStock(key);
            var (open, high, low, close, volume) = GenerateDayOhlcv(basePrice, baseVolume);
            var prevClose = Math.Round(basePrice * 0.995m, 2);
            var netChange = close - prevClose;
            var pctChange = prevClose != 0 ? Math.Round((netChange / prevClose) * 100, 2) : 0m;

            var depth = new MarketDepthDto
            {
                Buy = new List<MarketDepthItemDto>
                {
                    new() { Quantity = 250, Price = close - 0.10m, Orders = 3 },
                    new() { Quantity = 500, Price = close - 0.25m, Orders = 5 },
                    new() { Quantity = 1200, Price = close - 0.50m, Orders = 8 },
                    new() { Quantity = 2500, Price = close - 0.75m, Orders = 12 },
                    new() { Quantity = 4000, Price = close - 1.00m, Orders = 18 }
                },
                Sell = new List<MarketDepthItemDto>
                {
                    new() { Quantity = 300, Price = close + 0.10m, Orders = 4 },
                    new() { Quantity = 650, Price = close + 0.25m, Orders = 6 },
                    new() { Quantity = 1100, Price = close + 0.50m, Orders = 9 },
                    new() { Quantity = 2200, Price = close + 0.75m, Orders = 14 },
                    new() { Quantity = 3800, Price = close + 1.00m, Orders = 20 }
                }
            };

            var quote = new MarketQuoteDto
            {
                InstrumentKey = key,
                Symbol = symbol,
                LastPrice = close,
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = volume,
                AveragePrice = Math.Round((open + high + low + close) / 4, 2),
                NetChange = netChange,
                PercentageChange = pctChange,
                TotalBuyQuantity = 8450,
                TotalSellQuantity = 8050,
                LowerCircuitLimit = Math.Round(prevClose * 0.90m, 2),
                UpperCircuitLimit = Math.Round(prevClose * 1.10m, 2),
                PrevClosePrice = prevClose,
                YearHigh = Math.Round(basePrice * 1.25m, 2),
                YearLow = Math.Round(basePrice * 0.78m, 2),
                OpenInterest = 0,
                LastTradeTime = now.AddSeconds(-5),
                Timestamp = now,
                Depth = depth
            };

            result[key] = quote;
        }

        return result;
    }

    public IReadOnlyDictionary<string, LtpQuoteDto> GetLtpQuotes(IEnumerable<string> instrumentKeys)
    {
        var result = new Dictionary<string, LtpQuoteDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in instrumentKeys.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var (_, basePrice, baseVolume) = ResolveStock(key);
            var (_, _, _, close, volume) = GenerateDayOhlcv(basePrice, baseVolume);
            var prevClose = Math.Round(basePrice * 0.995m, 2);
            var change = close - prevClose;
            var changePct = prevClose != 0 ? Math.Round((change / prevClose) * 100, 2) : 0m;

            result[key] = new LtpQuoteDto
            {
                InstrumentKey = key,
                LastPrice = close,
                LastTradedQuantity = 50,
                Volume = volume,
                ClosePrice = prevClose,
                Change = change,
                ChangePercentage = changePct
            };
        }

        return result;
    }

    public IReadOnlyDictionary<string, OhlcQuoteDto> GetOhlcQuotes(IEnumerable<string> instrumentKeys, string interval)
    {
        var result = new Dictionary<string, OhlcQuoteDto>(StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow;

        foreach (var key in instrumentKeys.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var (_, basePrice, baseVolume) = ResolveStock(key);
            var (open, high, low, close, volume) = GenerateDayOhlcv(basePrice, baseVolume);
            var prevBase = basePrice * 0.992m;
            var (prevOpen, prevHigh, prevLow, prevClose, prevVol) = GenerateDayOhlcv(prevBase, (long)(baseVolume * 0.95));

            result[key] = new OhlcQuoteDto
            {
                InstrumentKey = key,
                LastPrice = close,
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = volume,
                Timestamp = now,
                PrevOpen = prevOpen,
                PrevHigh = prevHigh,
                PrevLow = prevLow,
                PrevClose = prevClose,
                PrevVolume = prevVol
            };
        }

        return result;
    }

    public IReadOnlyList<CandleDto> GetHistoricalCandles(string instrumentKey, string unit, int interval, DateOnly toDate, DateOnly fromDate)
    {
        var (_, basePrice, baseVolume) = ResolveStock(instrumentKey);
        var candles = new List<CandleDto>();

        var currentDate = fromDate;
        var runningPrice = basePrice * 0.88m; // start historical series slightly lower
        var random = new Random(Math.Abs(instrumentKey.GetHashCode()));

        while (currentDate <= toDate)
        {
            // Skip weekends
            if (currentDate.DayOfWeek != DayOfWeek.Saturday && currentDate.DayOfWeek != DayOfWeek.Sunday)
            {
                var changePercent = (decimal)(random.NextDouble() * 0.04 - 0.019); // -1.9% to +2.1%
                var open = Math.Round(runningPrice, 2);
                var close = Math.Round(open * (1 + changePercent), 2);
                var highOffset = (decimal)(random.NextDouble() * 0.015);
                var lowOffset = (decimal)(random.NextDouble() * 0.015);
                var high = Math.Round(Math.Max(open, close) * (1 + highOffset), 2);
                var low = Math.Round(Math.Min(open, close) * (1 - lowOffset), 2);
                var volumeMultiplier = (decimal)(0.7 + random.NextDouble() * 0.8);
                var volume = (long)(baseVolume * volumeMultiplier);

                var candleTime = new DateTimeOffset(currentDate.ToDateTime(new TimeOnly(15, 30)), TimeSpan.FromHours(5.5)); // IST

                candles.Add(new CandleDto
                {
                    Timestamp = candleTime,
                    Open = open,
                    High = high,
                    Low = low,
                    Close = close,
                    Volume = volume,
                    OpenInterest = 0
                });

                runningPrice = close;
            }

            currentDate = currentDate.AddDays(1);
        }

        // Return most recent first to align with Upstox chronological ordering
        candles.Reverse();
        return candles;
    }

    public IReadOnlyList<CandleDto> GetIntradayCandles(string instrumentKey, string unit, int interval)
    {
        var (_, basePrice, baseVolume) = ResolveStock(instrumentKey);
        var candles = new List<CandleDto>();
        var random = new Random(Math.Abs(instrumentKey.GetHashCode()) ^ 42);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var marketOpenTime = new TimeOnly(9, 15);
        var currentTime = TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5)); // Convert to IST
        var endTime = currentTime < new TimeOnly(15, 30) ? currentTime : new TimeOnly(15, 30);
        if (endTime < marketOpenTime)
        {
            endTime = new TimeOnly(15, 30); // Use full trading day if off-market hours
        }

        var runningTime = marketOpenTime;
        var runningPrice = basePrice * 0.998m;
        var stepMinutes = interval > 0 ? interval : 5;

        while (runningTime <= endTime)
        {
            var changePct = (decimal)(random.NextDouble() * 0.008 - 0.0038); // -0.38% to +0.42%
            var open = Math.Round(runningPrice, 2);
            var close = Math.Round(open * (1 + changePct), 2);
            var high = Math.Round(Math.Max(open, close) * (1 + (decimal)(random.NextDouble() * 0.003)), 2);
            var low = Math.Round(Math.Min(open, close) * (1 - (decimal)(random.NextDouble() * 0.003)), 2);
            var vol = (long)(baseVolume / 75 * (0.5 + random.NextDouble() * 1.2));

            var istOffset = TimeSpan.FromHours(5.5);
            var candleTime = new DateTimeOffset(today.ToDateTime(runningTime), istOffset);

            candles.Add(new CandleDto
            {
                Timestamp = candleTime,
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = vol,
                OpenInterest = 0
            });

            runningPrice = close;
            runningTime = runningTime.AddMinutes(stepMinutes);
        }

        candles.Reverse();
        return candles;
    }

    private static (string Symbol, decimal BasePrice, long BaseVolume) ResolveStock(string instrumentKey)
    {
        if (KnownStocks.TryGetValue(instrumentKey, out var known))
        {
            return known;
        }

        // Derive deterministic pseudo-data for any unknown stock key
        var cleanKey = instrumentKey.Replace("NSE_EQ|", "").Replace("NSE_INDEX|", "");
        var hash = Math.Abs(cleanKey.GetHashCode());
        var price = 100m + (hash % 450000) / 100m;
        var volume = 500000L + (hash % 5000000);
        return (cleanKey, Math.Round(price, 2), volume);
    }

    private static (decimal Open, decimal High, decimal Low, decimal Close, long Volume) GenerateDayOhlcv(decimal basePrice, long baseVolume)
    {
        var open = Math.Round(basePrice * 1.002m, 2);
        var high = Math.Round(basePrice * 1.018m, 2);
        var low = Math.Round(basePrice * 0.991m, 2);
        var close = Math.Round(basePrice * 1.008m, 2);
        return (open, high, low, close, baseVolume);
    }
}

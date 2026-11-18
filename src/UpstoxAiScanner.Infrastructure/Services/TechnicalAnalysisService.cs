using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UpstoxAiScanner.Application.DTOs.MarketData;
using UpstoxAiScanner.Application.DTOs.TechnicalAnalysis;
using UpstoxAiScanner.Application.Interfaces;
using UpstoxAiScanner.Domain.Calculators;
using UpstoxAiScanner.Domain.Entities;
using UpstoxAiScanner.Domain.Enums;
using UpstoxAiScanner.Domain.Indicators;
using UpstoxAiScanner.Domain.Models;
using UpstoxAiScanner.Infrastructure.Data;

namespace UpstoxAiScanner.Infrastructure.Services;

public class TechnicalAnalysisService : ITechnicalAnalysisService
{
    private readonly UpstoxScannerDbContext _dbContext;
    private readonly IUpstoxMarketDataService _marketDataService;
    private readonly ILogger<TechnicalAnalysisService> _logger;

    public TechnicalAnalysisService(
        UpstoxScannerDbContext dbContext,
        IUpstoxMarketDataService marketDataService,
        ILogger<TechnicalAnalysisService> logger)
    {
        _dbContext = dbContext;
        _marketDataService = marketDataService;
        _logger = logger;
    }

    public async Task<TechnicalAnalysisDto?> AnalyzeInstrumentAsync(
        string symbol,
        string interval = "1d",
        CancellationToken cancellationToken = default)
    {
        var normalizedSymbol = symbol.Trim().ToUpperInvariant();

        // 1. Resolve Instrument from DB
        Instrument? instrument;
        if (normalizedSymbol.Contains('|'))
        {
            instrument = await _dbContext.Instruments
                .FirstOrDefaultAsync(i => i.InstrumentKey == normalizedSymbol, cancellationToken);
        }
        else
        {
            instrument = await _dbContext.Instruments
                .Where(i => i.Symbol == normalizedSymbol)
                .OrderByDescending(i => i.Universe == "NIFTY500")
                .ThenByDescending(i => i.Exchange == "NSE")
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (instrument == null && !normalizedSymbol.Contains('|'))
        {
            _logger.LogWarning("Instrument with symbol '{Symbol}' was not found in the tracked database.", normalizedSymbol);
            return null;
        }

        string instrumentKey = instrument?.InstrumentKey ?? normalizedSymbol;
        string targetSymbol = instrument?.Symbol ?? normalizedSymbol;

        // 2. Fetch candles from Upstox Market Data service
        IReadOnlyList<CandleDto>? rawCandleDtos;
        var now = DateTime.UtcNow;

        if (interval.Equals("5m", StringComparison.OrdinalIgnoreCase) ||
            interval.Equals("minutes", StringComparison.OrdinalIgnoreCase))
        {
            var response = await _marketDataService.GetIntradayCandlesAsync(
                instrumentKey,
                "minutes",
                5,
                cancellationToken);

            rawCandleDtos = response.Data;
        }
        else
        {
            // Daily candles (default) - fetch 1 year of history for accurate 200 EMA
            var toDate = DateOnly.FromDateTime(now);
            var fromDate = DateOnly.FromDateTime(now.AddDays(-365));

            var response = await _marketDataService.GetHistoricalCandlesAsync(
                instrumentKey,
                "days",
                1,
                toDate,
                fromDate,
                cancellationToken);

            rawCandleDtos = response.Data;
        }

        if (rawCandleDtos == null || rawCandleDtos.Count == 0)
        {
            _logger.LogWarning("No candle data available for {Symbol} ({Key}) interval {Interval}", targetSymbol, instrumentKey, interval);
            return null;
        }

        // 3. Map CandleDto to domain Candle. Upstox returns newest candles first; indicators require chronological order (oldest first)
        var chronologicalCandles = rawCandleDtos
            .Select(c => new Candle
            {
                Timestamp = c.Timestamp,
                Open = c.Open,
                High = c.High,
                Low = c.Low,
                Close = c.Close,
                Volume = c.Volume,
                OpenInterest = c.OpenInterest
            })
            .OrderBy(c => c.Timestamp)
            .ToList();

        // 4. Compute indicator snapshot
        var snapshot = TechnicalIndicatorEngine.ComputeSnapshot(chronologicalCandles);
        if (snapshot == null)
            return null;

        // 5. Optionally cache/persist latest indicator values to SQL Server if instrument exists
        if (instrument != null)
        {
            await PersistIndicatorAsync(instrument.Id, interval, snapshot, cancellationToken);
        }

        return MapToDto(targetSymbol, instrumentKey, interval, snapshot);
    }

    public TechnicalAnalysisDto AnalyzeCandles(
        string symbol,
        string instrumentKey,
        string interval,
        IReadOnlyList<Candle> candles)
    {
        var chronologicalCandles = candles.OrderBy(c => c.Timestamp).ToList();
        var snapshot = TechnicalIndicatorEngine.ComputeSnapshot(chronologicalCandles)
            ?? new TechnicalAnalysisSnapshot
            {
                Timestamp = DateTimeOffset.UtcNow,
                ClosePrice = candles.Count > 0 ? candles[^1].Close : 0m
            };

        return MapToDto(symbol, instrumentKey, interval, snapshot);
    }

    private async Task PersistIndicatorAsync(
        int instrumentId,
        string interval,
        TechnicalAnalysisSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        try
        {
            var snapshotTimestamp = snapshot.Timestamp;
            var targetUtc = snapshotTimestamp.ToUniversalTime();
            var existing = await _dbContext.TechnicalIndicators
                .FirstOrDefaultAsync(t => t.InstrumentId == instrumentId &&
                                          t.Interval == interval &&
                                          (t.Timestamp == snapshotTimestamp || t.Timestamp == targetUtc), cancellationToken);

            if (existing != null)
            {
                existing.RSI = snapshot.Rsi?.Value;
                existing.MACD = snapshot.Macd?.Macd;
                existing.MACDSignal = snapshot.Macd?.Signal;
                existing.MACDHistogram = snapshot.Macd?.Histogram;
                existing.EMA20 = snapshot.Ema?.Ema20;
                existing.EMA50 = snapshot.Ema?.Ema50;
                existing.EMA200 = snapshot.Ema?.Ema200;
                existing.AverageVolume = snapshot.Volume?.AverageVolume20;
                existing.RelativeVolume = snapshot.Volume?.VolumeRatio;
            }
            else
            {
                var record = new TechnicalIndicator
                {
                    InstrumentId = instrumentId,
                    Interval = interval,
                    Timestamp = snapshot.Timestamp,
                    RSI = snapshot.Rsi?.Value,
                    MACD = snapshot.Macd?.Macd,
                    MACDSignal = snapshot.Macd?.Signal,
                    MACDHistogram = snapshot.Macd?.Histogram,
                    EMA20 = snapshot.Ema?.Ema20,
                    EMA50 = snapshot.Ema?.Ema50,
                    EMA200 = snapshot.Ema?.Ema200,
                    AverageVolume = snapshot.Volume?.AverageVolume20,
                    RelativeVolume = snapshot.Volume?.VolumeRatio
                };

                await _dbContext.TechnicalIndicators.AddAsync(record, cancellationToken);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist technical indicator cache for instrument {Id}: {Message}", instrumentId, ex.Message);

            // Detach any failed added entry so it does not poison subsequent SaveChangesAsync calls on the DbContext
            _dbContext.ChangeTracker.Entries<TechnicalIndicator>()
                .Where(e => e.State == EntityState.Added)
                .ToList()
                .ForEach(e => e.State = EntityState.Detached);
        }
    }

    private static TechnicalAnalysisDto MapToDto(
        string symbol,
        string instrumentKey,
        string interval,
        TechnicalAnalysisSnapshot snapshot)
    {
        return new TechnicalAnalysisDto
        {
            Symbol = symbol,
            InstrumentKey = instrumentKey,
            Interval = interval,
            Timestamp = snapshot.Timestamp,
            ClosePrice = snapshot.ClosePrice,
            OpenPrice = snapshot.OpenPrice,
            HighPrice = snapshot.HighPrice,
            LowPrice = snapshot.LowPrice,
            Atr = snapshot.Atr,
            Rsi = snapshot.Rsi == null ? null : new RsiAnalysisDto
            {
                Value = snapshot.Rsi.Value,
                IsOverbought = snapshot.Rsi.IsOverbought,
                IsOversold = snapshot.Rsi.IsOversold,
                CrossedAbove30 = snapshot.Rsi.CrossedAbove30,
                CrossedBelow70 = snapshot.Rsi.CrossedBelow70,
                CrossedAbove50 = snapshot.Rsi.CrossedAbove50,
                CrossedBelow50 = snapshot.Rsi.CrossedBelow50
            },
            Macd = snapshot.Macd == null ? null : new MacdAnalysisDto
            {
                Macd = snapshot.Macd.Macd,
                Signal = snapshot.Macd.Signal,
                Histogram = snapshot.Macd.Histogram,
                IsBullishCrossover = snapshot.Macd.IsBullishCrossover,
                IsBearishCrossover = snapshot.Macd.IsBearishCrossover,
                IsAboveZero = snapshot.Macd.IsAboveZero,
                IsBelowZero = snapshot.Macd.IsBelowZero,
                HistogramMomentum = snapshot.Macd.HistogramMomentum
            },
            Ema = snapshot.Ema == null ? null : new EmaAnalysisDto
            {
                Ema20 = snapshot.Ema.Ema20,
                Ema50 = snapshot.Ema.Ema50,
                Ema200 = snapshot.Ema.Ema200,
                IsBullishAlignment = snapshot.Ema.IsBullishAlignment,
                IsBearishAlignment = snapshot.Ema.IsBearishAlignment,
                IsPriceAboveEma20 = snapshot.Ema.IsPriceAboveEma20,
                IsPriceAboveEma50 = snapshot.Ema.IsPriceAboveEma50,
                IsPriceAboveEma200 = snapshot.Ema.IsPriceAboveEma200,
                CrossedAboveEma20 = snapshot.Ema.CrossedAboveEma20,
                CrossedBelowEma20 = snapshot.Ema.CrossedBelowEma20,
                GoldenCross = snapshot.Ema.GoldenCross,
                DeathCross = snapshot.Ema.DeathCross
            },
            Breakout = snapshot.Breakout == null ? null : new BreakoutAnalysisDto
            {
                Is20PeriodHighBreakout = snapshot.Breakout.Is20PeriodHighBreakout,
                Is50PeriodHighBreakout = snapshot.Breakout.Is50PeriodHighBreakout,
                Is20PeriodLowBreakdown = snapshot.Breakout.Is20PeriodLowBreakdown,
                Is50PeriodLowBreakdown = snapshot.Breakout.Is50PeriodLowBreakdown,
                Resistance20 = snapshot.Breakout.Resistance20,
                Resistance50 = snapshot.Breakout.Resistance50,
                Support20 = snapshot.Breakout.Support20,
                Support50 = snapshot.Breakout.Support50,
                IsVolumeConfirmed = snapshot.Breakout.IsVolumeConfirmed
            },
            Volume = snapshot.Volume == null ? null : new VolumeAnalysisDto
            {
                CurrentVolume = snapshot.Volume.CurrentVolume,
                AverageVolume20 = snapshot.Volume.AverageVolume20,
                VolumeRatio = snapshot.Volume.VolumeRatio,
                RelativeVolume = snapshot.Volume.RelativeVolume,
                IsVolumeSurge = snapshot.Volume.IsVolumeSurge,
                SurgeThreshold = snapshot.Volume.SurgeThreshold
            }
        };
    }
}

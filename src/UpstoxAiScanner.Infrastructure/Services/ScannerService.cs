using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UpstoxAiScanner.Application.DTOs.Scanner;
using UpstoxAiScanner.Application.DTOs.TechnicalAnalysis;
using UpstoxAiScanner.Application.Interfaces;
using UpstoxAiScanner.Domain.Entities;
using UpstoxAiScanner.Domain.Indicators;
using UpstoxAiScanner.Domain.Scoring;
using UpstoxAiScanner.Infrastructure.Data;

namespace UpstoxAiScanner.Infrastructure.Services;

public class ScannerService : IScannerService
{
    private readonly UpstoxScannerDbContext _dbContext;
    private readonly ITechnicalAnalysisService _technicalAnalysisService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScannerService> _logger;

    public ScannerService(
        UpstoxScannerDbContext dbContext,
        ITechnicalAnalysisService technicalAnalysisService,
        IServiceScopeFactory scopeFactory,
        ILogger<ScannerService> logger)
    {
        _dbContext = dbContext;
        _technicalAnalysisService = technicalAnalysisService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ScanResultDto>> RunScanAsync(
        string scanType = "Swing",
        string universe = "NIFTY500",
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        bool isIntraday = scanType.Equals("Intraday", StringComparison.OrdinalIgnoreCase);
        string normalizedScanType = isIntraday ? "Intraday" : "Swing";
        string interval = isIntraday ? "5m" : "1d";

        _logger.LogInformation("Starting {ScanType} scan for universe {Universe}", normalizedScanType, universe);

        // Fetch active universe instruments
        var query = _dbContext.Instruments.Where(i => i.IsActive);

        if (!universe.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            if (universe.Equals("NSE", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(i => i.Universe == "NSE" || i.Exchange == "NSE");
            }
            else if (universe.Equals("BSE", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(i => i.Universe == "BSE" || i.Exchange == "BSE");
            }
            else
            {
                query = query.Where(i => i.Universe == universe);
            }
        }

        int scanBatchSize = Math.Clamp(limit > 0 ? limit * 2 : 50, 20, 100);

        var instruments = await query
            .OrderByDescending(i => i.Universe == "NIFTY500")
            .ThenByDescending(i => i.Exchange == "NSE")
            .ThenBy(i => i.Symbol)
            .Take(scanBatchSize)
            .ToListAsync(cancellationToken);

        if (instruments.Count == 0)
        {
            _logger.LogWarning("No active instruments found for universe {Universe} in database", universe);
            return Array.Empty<ScanResultDto>();
        }

        var scannedResults = new List<ScanResultDto>();
        var entitiesToPersist = new List<ScanResult>();

        // Controlled concurrency to stay well within Upstox rate limits
        var semaphore = new SemaphoreSlim(5, 5);
        var tasks = instruments.Select(async instrument =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var scopedTaService = scope.ServiceProvider.GetRequiredService<ITechnicalAnalysisService>();

                var analysis = await scopedTaService.AnalyzeInstrumentAsync(
                    instrument.Symbol,
                    interval,
                    cancellationToken);

                if (analysis == null)
                    return;

                var snapshot = MapToSnapshot(analysis);
                var scoreResult = isIntraday
                    ? TechnicalScoringEngine.ScoreIntraday(snapshot)
                    : TechnicalScoringEngine.ScoreSwing(snapshot);

                var dto = new ScanResultDto
                {
                    Symbol = instrument.Symbol,
                    CompanyName = instrument.CompanyName,
                    InstrumentKey = instrument.InstrumentKey,
                    ScanType = normalizedScanType,
                    Timestamp = analysis.Timestamp,
                    CurrentPrice = analysis.ClosePrice,
                    TechnicalScore = scoreResult.Score,
                    Signal = scoreResult.Signal,
                    BreakoutStatus = scoreResult.BreakoutStatus,
                    VolumeStatus = scoreResult.VolumeStatus,
                    Explanation = scoreResult.Explanation,
                    Rsi = analysis.Rsi?.Value,
                    Macd = analysis.Macd?.Macd,
                    Ema20 = analysis.Ema?.Ema20,
                    Ema50 = analysis.Ema?.Ema50,
                    Ema200 = analysis.Ema?.Ema200,
                    VolumeRatio = analysis.Volume?.VolumeRatio
                };

                var entity = new ScanResult
                {
                    InstrumentId = instrument.Id,
                    ScanType = normalizedScanType,
                    Timestamp = analysis.Timestamp,
                    CurrentPrice = analysis.ClosePrice,
                    TechnicalScore = scoreResult.Score,
                    Signal = scoreResult.Signal,
                    BreakoutStatus = scoreResult.BreakoutStatus,
                    VolumeStatus = scoreResult.VolumeStatus,
                    Explanation = scoreResult.Explanation
                };

                lock (scannedResults)
                {
                    scannedResults.Add(dto);
                    entitiesToPersist.Add(entity);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to scan instrument {Symbol}: {Message}", instrument.Symbol, ex.Message);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        // Bulk persist scan results to SQL Server
        if (entitiesToPersist.Count > 0)
        {
            try
            {
                await _dbContext.ScanResults.AddRangeAsync(entitiesToPersist, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Persisted {Count} scan results for {ScanType}", entitiesToPersist.Count, normalizedScanType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error persisting scan results to database: {Message}", ex.Message);
            }
        }

        // Rank by TechnicalScore descending
        var ranked = scannedResults
            .OrderByDescending(r => r.TechnicalScore)
            .Take(limit)
            .ToList();

        return ranked;
    }

    public async Task<IReadOnlyList<ScanResultDto>> GetLatestScanResultsAsync(
        string scanType = "Swing",
        ScannerFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedScanType = scanType.Equals("Intraday", StringComparison.OrdinalIgnoreCase) ? "Intraday" : "Swing";
        var query = _dbContext.ScanResults
            .AsNoTracking()
            .Include(s => s.Instrument)
            .Where(s => s.ScanType == normalizedScanType);

        if (filter != null)
        {
            if (!string.IsNullOrWhiteSpace(filter.Universe) && !filter.Universe.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                if (filter.Universe.Equals("NSE", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(s => s.Instrument != null && (s.Instrument.Universe == "NSE" || s.Instrument.Exchange == "NSE"));
                }
                else if (filter.Universe.Equals("BSE", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(s => s.Instrument != null && (s.Instrument.Universe == "BSE" || s.Instrument.Exchange == "BSE"));
                }
                else
                {
                    query = query.Where(s => s.Instrument != null && s.Instrument.Universe == filter.Universe);
                }
            }

            if (!string.IsNullOrWhiteSpace(filter.Signal))
            {
                query = query.Where(s => s.Signal == filter.Signal);
            }

            if (filter.MinScore.HasValue)
            {
                query = query.Where(s => s.TechnicalScore >= filter.MinScore.Value);
            }

            if (filter.MaxScore.HasValue)
            {
                query = query.Where(s => s.TechnicalScore <= filter.MaxScore.Value);
            }
        }

        int maxLimit = filter?.Limit > 0 ? Math.Min(filter.Limit, 200) : 20;

        var items = await query
            .OrderByDescending(s => s.Timestamp)
            .ThenByDescending(s => s.TechnicalScore)
            .Take(maxLimit)
            .Select(s => new ScanResultDto
            {
                Id = s.Id,
                Symbol = s.Instrument != null ? s.Instrument.Symbol : "UNKNOWN",
                CompanyName = s.Instrument != null ? s.Instrument.CompanyName : string.Empty,
                InstrumentKey = s.Instrument != null ? s.Instrument.InstrumentKey : string.Empty,
                ScanType = s.ScanType,
                Timestamp = s.Timestamp,
                CurrentPrice = s.CurrentPrice,
                TechnicalScore = s.TechnicalScore,
                Signal = s.Signal,
                BreakoutStatus = s.BreakoutStatus,
                VolumeStatus = s.VolumeStatus,
                Explanation = s.Explanation
            })
            .ToListAsync(cancellationToken);

        return items;
    }

    public async Task<ScanResultDto?> ScanSingleInstrumentAsync(
        string symbol,
        string scanType = "Swing",
        CancellationToken cancellationToken = default)
    {
        bool isIntraday = scanType.Equals("Intraday", StringComparison.OrdinalIgnoreCase);
        string normalizedScanType = isIntraday ? "Intraday" : "Swing";
        string interval = isIntraday ? "5m" : "1d";

        var analysis = await _technicalAnalysisService.AnalyzeInstrumentAsync(symbol, interval, cancellationToken);
        if (analysis == null)
            return null;

        var snapshot = MapToSnapshot(analysis);
        var scoreResult = isIntraday
            ? TechnicalScoringEngine.ScoreIntraday(snapshot)
            : TechnicalScoringEngine.ScoreSwing(snapshot);

        var instrument = await _dbContext.Instruments
            .AsNoTracking()
            .Where(i => i.Symbol == analysis.Symbol || i.InstrumentKey == analysis.InstrumentKey)
            .OrderByDescending(i => i.Universe == "NIFTY500")
            .ThenByDescending(i => i.Exchange == "NSE")
            .FirstOrDefaultAsync(cancellationToken);

        var dto = new ScanResultDto
        {
            Symbol = analysis.Symbol,
            CompanyName = instrument?.CompanyName ?? analysis.Symbol,
            InstrumentKey = analysis.InstrumentKey,
            ScanType = normalizedScanType,
            Timestamp = analysis.Timestamp,
            CurrentPrice = analysis.ClosePrice,
            TechnicalScore = scoreResult.Score,
            Signal = scoreResult.Signal,
            BreakoutStatus = scoreResult.BreakoutStatus,
            VolumeStatus = scoreResult.VolumeStatus,
            Explanation = scoreResult.Explanation,
            Rsi = analysis.Rsi?.Value,
            Macd = analysis.Macd?.Macd,
            Ema20 = analysis.Ema?.Ema20,
            Ema50 = analysis.Ema?.Ema50,
            Ema200 = analysis.Ema?.Ema200,
            VolumeRatio = analysis.Volume?.VolumeRatio
        };

        if (instrument != null)
        {
            var entity = new ScanResult
            {
                InstrumentId = instrument.Id,
                ScanType = normalizedScanType,
                Timestamp = analysis.Timestamp,
                CurrentPrice = analysis.ClosePrice,
                TechnicalScore = scoreResult.Score,
                Signal = scoreResult.Signal,
                BreakoutStatus = scoreResult.BreakoutStatus,
                VolumeStatus = scoreResult.VolumeStatus,
                Explanation = scoreResult.Explanation
            };

            await _dbContext.ScanResults.AddAsync(entity, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            dto.Id = entity.Id;
        }

        return dto;
    }

    private static TechnicalAnalysisSnapshot MapToSnapshot(TechnicalAnalysisDto dto)
    {
        return new TechnicalAnalysisSnapshot
        {
            Timestamp = dto.Timestamp,
            ClosePrice = dto.ClosePrice,
            Rsi = dto.Rsi == null ? null : new RsiAnalysis
            {
                Value = dto.Rsi.Value,
                CrossedAbove30 = dto.Rsi.CrossedAbove30,
                CrossedBelow70 = dto.Rsi.CrossedBelow70,
                CrossedAbove50 = dto.Rsi.CrossedAbove50,
                CrossedBelow50 = dto.Rsi.CrossedBelow50
            },
            Macd = dto.Macd == null ? null : new MacdAnalysis
            {
                Macd = dto.Macd.Macd,
                Signal = dto.Macd.Signal,
                Histogram = dto.Macd.Histogram,
                IsBullishCrossover = dto.Macd.IsBullishCrossover,
                IsBearishCrossover = dto.Macd.IsBearishCrossover,
                HistogramMomentum = dto.Macd.HistogramMomentum
            },
            Ema = dto.Ema == null ? null : new EmaAnalysis
            {
                Ema20 = dto.Ema.Ema20,
                Ema50 = dto.Ema.Ema50,
                Ema200 = dto.Ema.Ema200,
                IsPriceAboveEma20 = dto.Ema.IsPriceAboveEma20,
                IsPriceAboveEma50 = dto.Ema.IsPriceAboveEma50,
                IsPriceAboveEma200 = dto.Ema.IsPriceAboveEma200,
                CrossedAboveEma20 = dto.Ema.CrossedAboveEma20,
                CrossedBelowEma20 = dto.Ema.CrossedBelowEma20,
                GoldenCross = dto.Ema.GoldenCross,
                DeathCross = dto.Ema.DeathCross
            },
            Breakout = dto.Breakout == null ? null : new BreakoutAnalysis
            {
                Is20PeriodHighBreakout = dto.Breakout.Is20PeriodHighBreakout,
                Is50PeriodHighBreakout = dto.Breakout.Is50PeriodHighBreakout,
                Is20PeriodLowBreakdown = dto.Breakout.Is20PeriodLowBreakdown,
                Is50PeriodLowBreakdown = dto.Breakout.Is50PeriodLowBreakdown,
                Resistance20 = dto.Breakout.Resistance20,
                Resistance50 = dto.Breakout.Resistance50,
                Support20 = dto.Breakout.Support20,
                Support50 = dto.Breakout.Support50,
                IsVolumeConfirmed = dto.Breakout.IsVolumeConfirmed
            },
            Volume = dto.Volume == null ? null : new VolumeAnalysis
            {
                CurrentVolume = dto.Volume.CurrentVolume,
                AverageVolume20 = dto.Volume.AverageVolume20,
                VolumeRatio = dto.Volume.VolumeRatio,
                IsVolumeSurge = dto.Volume.IsVolumeSurge,
                SurgeThreshold = dto.Volume.SurgeThreshold
            }
        };
    }
}

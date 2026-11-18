namespace UpstoxAiScanner.Application.DTOs.TechnicalAnalysis;

public class RsiAnalysisDto
{
    public decimal Value { get; set; }
    public bool IsOverbought { get; set; }
    public bool IsOversold { get; set; }
    public bool CrossedAbove30 { get; set; }
    public bool CrossedBelow70 { get; set; }
    public bool CrossedAbove50 { get; set; }
    public bool CrossedBelow50 { get; set; }
}

public class MacdAnalysisDto
{
    public decimal Macd { get; set; }
    public decimal Signal { get; set; }
    public decimal Histogram { get; set; }
    public bool IsBullishCrossover { get; set; }
    public bool IsBearishCrossover { get; set; }
    public bool IsAboveZero { get; set; }
    public bool IsBelowZero { get; set; }
    public string HistogramMomentum { get; set; } = string.Empty;
}

public class EmaAnalysisDto
{
    public decimal? Ema20 { get; set; }
    public decimal? Ema50 { get; set; }
    public decimal? Ema200 { get; set; }
    public bool IsBullishAlignment { get; set; }
    public bool IsBearishAlignment { get; set; }
    public bool IsPriceAboveEma20 { get; set; }
    public bool IsPriceAboveEma50 { get; set; }
    public bool IsPriceAboveEma200 { get; set; }
    public bool CrossedAboveEma20 { get; set; }
    public bool CrossedBelowEma20 { get; set; }
    public bool GoldenCross { get; set; }
    public bool DeathCross { get; set; }
}

public class BreakoutAnalysisDto
{
    public bool Is20PeriodHighBreakout { get; set; }
    public bool Is50PeriodHighBreakout { get; set; }
    public bool Is20PeriodLowBreakdown { get; set; }
    public bool Is50PeriodLowBreakdown { get; set; }
    public decimal Resistance20 { get; set; }
    public decimal Resistance50 { get; set; }
    public decimal Support20 { get; set; }
    public decimal Support50 { get; set; }
    public bool IsVolumeConfirmed { get; set; }
}

public class VolumeAnalysisDto
{
    public long CurrentVolume { get; set; }
    public decimal AverageVolume20 { get; set; }
    public decimal VolumeRatio { get; set; }
    public decimal RelativeVolume { get; set; }
    public bool IsVolumeSurge { get; set; }
    public decimal SurgeThreshold { get; set; }
}

public class TechnicalAnalysisDto
{
    public string Symbol { get; set; } = string.Empty;
    public string InstrumentKey { get; set; } = string.Empty;
    public string Interval { get; set; } = "1d";
    public DateTimeOffset Timestamp { get; set; }
    public decimal ClosePrice { get; set; }
    public decimal? OpenPrice { get; set; }
    public decimal? HighPrice { get; set; }
    public decimal? LowPrice { get; set; }
    public decimal? Atr { get; set; }
    public RsiAnalysisDto? Rsi { get; set; }
    public MacdAnalysisDto? Macd { get; set; }
    public EmaAnalysisDto? Ema { get; set; }
    public BreakoutAnalysisDto? Breakout { get; set; }
    public VolumeAnalysisDto? Volume { get; set; }
}

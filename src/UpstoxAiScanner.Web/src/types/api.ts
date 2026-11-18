export interface ApiResponse<T> {
  success: boolean;
  data: T;
  message?: string;
  isMockData: boolean;
  serverTimeUtc: string;
  errorCode?: string;
}

export interface ScanResultDto {
  id: number;
  symbol: string;
  companyName: string;
  instrumentKey: string;
  scanType: 'Swing' | 'Intraday';
  timestamp: string;
  currentPrice: number;
  technicalScore: number;
  signal: 'Strong Bullish' | 'Bullish' | 'Neutral' | 'Bearish' | 'Strong Bearish';
  breakoutStatus?: string;
  volumeStatus?: string;
  explanation?: string;
  rsi?: number;
  macd?: number;
  ema20?: number;
  ema50?: number;
  ema200?: number;
  volumeRatio?: number;
}

export interface MarketSessionStatus {
  isMarketOpen: boolean;
  sessionPhase?: 'PreOpen' | 'RegularOpen' | 'PostClose' | 'Closed' | string;
  phase?: 'PreOpen' | 'RegularOpen' | 'PostClose' | 'Closed' | string;
  currentTimeUtc?: string;
  currentTimeIst?: string;
  currentUtcTime?: string;
  currentIstTime?: string;
  timeUntilOpen?: string;
  timeUntilClose?: string;
  isWeekend?: boolean;
  message: string;
}

export interface BackgroundScannerStatusDto {
  isEnabled: boolean;
  isMarketOpen: boolean;
  ignoreMarketHours: boolean;
  marketSessionPhase?: string;
  intradayScanIntervalSeconds?: number;
  scanIntervalMinutes?: number;
  totalScansExecuted: number;
  lastScanTimeUtc?: string;
  nextScheduledScanTimeUtc?: string;
  lastScanUtc?: string;
  nextScheduledScanUtc?: string;
  isWebSocketConnected?: boolean;
  webSocketActiveSubscriptionsCount?: number;
  trackedSubscribedSymbols?: string[];
  latestTopSignals?: ScanResultDto[];
}

export interface AiExplanationDto {
  symbol: string;
  companyName: string;
  scanType: string;
  technicalScore: number;
  signal: string;
  currentPrice: number;
  executiveSummary: string;
  keyDrivers: string[];
  supportResistanceLevels: string;
  riskFactors: string;
  fullCommentary: string;
  provider: string;
  generatedAt: string;
}

export interface TechnicalAnalysisDto {
  symbol: string;
  instrumentKey: string;
  closePrice: number;
  timestamp: string;
  interval: string;
  rsi?: {
    value: number;
    crossedAbove50: boolean;
    crossedAbove70: boolean;
    crossedBelow30: boolean;
    crossedBelow50: boolean;
  };
  macd?: {
    macd: number;
    signal: number;
    histogram: number;
    isBullishCrossover: boolean;
    isBearishCrossover: boolean;
    histogramMomentum: string;
  };
  ema?: {
    ema20: number;
    ema50: number;
    ema200: number;
    isPriceAboveEma20: boolean;
    isPriceAboveEma50: boolean;
    isPriceAboveEma200: boolean;
    hasBullishAlignment: boolean;
    hasBearishAlignment: boolean;
    crossedAboveEma20: boolean;
  };
  breakout?: {
    is20PeriodHighBreakout: boolean;
    is50PeriodHighBreakout: boolean;
    is20PeriodLowBreakdown: boolean;
    is50PeriodLowBreakdown: boolean;
    resistance20: number;
    support20: number;
    resistance50: number;
    support50: number;
    isVolumeConfirmed: boolean;
  };
  volume?: {
    currentVolume: number;
    averageVolume20: number;
    volumeRatio: number;
    isSurge: boolean;
  };
}

export interface MarketQuoteDto {
  instrumentKey: string;
  symbol: string;
  lastPrice: number;
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
  averagePrice: number;
  netChange: number;
  percentageChange: number;
  prevClosePrice: number;
  yearHigh: number;
  yearLow: number;
}

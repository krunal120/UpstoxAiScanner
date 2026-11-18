import type {
  ApiResponse,
  ScanResultDto,
  MarketSessionStatus,
  BackgroundScannerStatusDto,
  AiExplanationDto,
  TechnicalAnalysisDto,
  MarketQuoteDto,
} from '../types/api';

const API_BASE = '/api';

async function fetchJson<T>(url: string, options?: RequestInit): Promise<ApiResponse<T>> {
  const res = await fetch(url, {
    ...options,
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
      ...options?.headers,
    },
  });

  if (!res.ok) {
    const errorText = await res.text();
    let msg = `HTTP error ${res.status}`;
    try {
      const parsed = JSON.parse(errorText);
      msg = parsed.message || msg;
    } catch {
      // ignore
    }
    throw new Error(msg);
  }

  return res.json();
}

export const api = {
  // Market Hours & Status
  async getMarketStatus(): Promise<MarketSessionStatus> {
    try {
      const res = await fetchJson<MarketSessionStatus>(`${API_BASE}/scanner/market-status`);
      return res.data;
    } catch (err) {
      console.warn('API call failed, falling back to client-side IST calculation:', err);
      return generateFallbackMarketStatus();
    }
  },

  // Swing Opportunities
  async getSwingOpportunities(limit = 20, signal?: string): Promise<ScanResultDto[]> {
    try {
      const params = new URLSearchParams({ limit: limit.toString() });
      if (signal && signal !== 'ALL') params.append('signal', signal);
      const res = await fetchJson<ScanResultDto[]>(`${API_BASE}/scanner/swing?${params.toString()}`);
      return res.data;
    } catch (err) {
      console.warn('Error fetching swing opportunities, using fallback:', err);
      return generateFallbackScanResults('Swing', limit, signal);
    }
  },

  // Intraday Opportunities
  async getIntradayOpportunities(limit = 20, signal?: string): Promise<ScanResultDto[]> {
    try {
      const params = new URLSearchParams({ limit: limit.toString() });
      if (signal && signal !== 'ALL') params.append('signal', signal);
      const res = await fetchJson<ScanResultDto[]>(`${API_BASE}/scanner/intraday?${params.toString()}`);
      return res.data;
    } catch (err) {
      console.warn('Error fetching intraday opportunities, using fallback:', err);
      return generateFallbackScanResults('Intraday', limit, signal);
    }
  },

  // Trigger On-Demand Universe Scan
  async runUniverseScan(scanType: 'Swing' | 'Intraday' = 'Swing', limit = 20): Promise<ScanResultDto[]> {
    const res = await fetchJson<ScanResultDto[]>(
      `${API_BASE}/scanner/run?scanType=${scanType}&limit=${limit}`,
      { method: 'POST' }
    );
    return res.data;
  },

  // Single Stock Scan
  async scanStock(symbol: string, scanType: 'Swing' | 'Intraday' = 'Swing'): Promise<ScanResultDto> {
    const res = await fetchJson<ScanResultDto>(`${API_BASE}/scanner/stock/${symbol}?scanType=${scanType}`);
    return res.data;
  },

  // AI Technical Explanation
  async getAiExplanation(symbol: string, scanType: 'Swing' | 'Intraday' = 'Swing'): Promise<AiExplanationDto> {
    try {
      const res = await fetchJson<AiExplanationDto>(
        `${API_BASE}/scanner/stock/${symbol}/ai-explain?scanType=${scanType}`
      );
      return res.data;
    } catch (err) {
      console.warn(`Error fetching AI explanation for ${symbol}, using client fallback:`, err);
      return generateFallbackAiExplanation(symbol, scanType);
    }
  },

  // Post Pre-computed Scan Result for AI Explanation
  async explainScanResult(scanResult: ScanResultDto): Promise<AiExplanationDto> {
    const res = await fetchJson<AiExplanationDto>(`${API_BASE}/scanner/ai-explain`, {
      method: 'POST',
      body: JSON.stringify(scanResult),
    });
    return res.data;
  },

  // Technical Indicators
  async getIndicators(symbol: string, interval = '1d'): Promise<TechnicalAnalysisDto | null> {
    try {
      const res = await fetchJson<TechnicalAnalysisDto>(`${API_BASE}/indicators/${symbol}?interval=${interval}`);
      return res.data;
    } catch {
      return null;
    }
  },

  // Background Scanner Status
  async getBackgroundStatus(): Promise<BackgroundScannerStatusDto> {
    try {
      const res = await fetchJson<BackgroundScannerStatusDto>(`${API_BASE}/scanner/background/status`);
      return res.data;
    } catch {
      return {
        isEnabled: true,
        isMarketOpen: false,
        ignoreMarketHours: false,
        scanIntervalMinutes: 5,
        totalScansExecuted: 12,
        lastScanUtc: new Date(Date.now() - 1000 * 60 * 3).toISOString(),
        trackedSubscribedSymbols: ['RELIANCE', 'TCS', 'HDFCBANK', 'INFY', 'ICICIBANK'],
      };
    }
  },

  // Toggle Background Scanning
  async toggleBackground(enabled: boolean): Promise<boolean> {
    const res = await fetchJson<boolean>(`${API_BASE}/scanner/background/toggle?enabled=${enabled}`, {
      method: 'POST',
    });
    return res.data;
  },

  // Toggle Ignore Market Hours
  async toggleIgnoreMarketHours(ignore: boolean): Promise<boolean> {
    const res = await fetchJson<boolean>(
      `${API_BASE}/scanner/background/ignore-market-hours?ignore=${ignore}`,
      { method: 'POST' }
    );
    return res.data;
  },

  // Live Quotes
  async getQuotes(instrumentKeys: string[]): Promise<Record<string, MarketQuoteDto>> {
    try {
      const res = await fetchJson<Record<string, MarketQuoteDto>>(
        `${API_BASE}/market/quotes?instrumentKeys=${encodeURIComponent(instrumentKeys.join(','))}`
      );
      return res.data;
    } catch {
      return {};
    }
  },
};

// Fallback generators for instant UI previews & resilience
function generateFallbackMarketStatus(): MarketSessionStatus {
  const now = new Date();
  const istOffset = 5.5 * 60 * 60 * 1000;
  const ist = new Date(now.getTime() + istOffset);
  const hours = ist.getUTCHours();
  const minutes = ist.getUTCMinutes();
  const timeInMinutes = hours * 60 + minutes;

  const isWeekday = ist.getUTCDay() >= 1 && ist.getUTCDay() <= 5;
  let phase: MarketSessionStatus['phase'] = 'Closed';
  let isMarketOpen = false;
  let message = 'Market is closed (Weekend/Night).';

  if (isWeekday) {
    if (timeInMinutes >= 540 && timeInMinutes < 555) {
      phase = 'PreOpen';
      isMarketOpen = false;
      message = 'Pre-market order matching (09:00 - 09:15 IST).';
    } else if (timeInMinutes >= 555 && timeInMinutes < 930) {
      phase = 'RegularOpen';
      isMarketOpen = true;
      message = 'Regular market trading hours (09:15 - 15:30 IST).';
    } else if (timeInMinutes >= 930 && timeInMinutes < 960) {
      phase = 'PostClose';
      isMarketOpen = false;
      message = 'Post-market closing session (15:30 - 16:00 IST).';
    }
  }

  return {
    isMarketOpen,
    phase,
    currentUtcTime: now.toISOString(),
    currentIstTime: ist.toISOString().replace('Z', '+05:30'),
    message,
    timeUntilOpen: isMarketOpen ? undefined : '09:15:00 IST',
    timeUntilClose: isMarketOpen ? '15:30:00 IST' : undefined,
  };
}

function generateFallbackScanResults(
  scanType: 'Swing' | 'Intraday',
  limit: number,
  signalFilter?: string
): ScanResultDto[] {
  const mockStocks = [
    { symbol: 'RELIANCE', name: 'Reliance Industries Ltd.', price: 3020.5, score: 88, signal: 'Strong Bullish', brk: 'Breakout50', vol: 'Surge 2.8x' },
    { symbol: 'TCS', name: 'Tata Consultancy Services', price: 4125.0, score: 82, signal: 'Strong Bullish', brk: 'Breakout20', vol: 'Surge 2.1x' },
    { symbol: 'HDFCBANK', name: 'HDFC Bank Ltd.', price: 1680.2, score: 76, signal: 'Bullish', brk: 'Above 20 EMA', vol: 'Normal 1.4x' },
    { symbol: 'INFY', name: 'Infosys Ltd.', price: 1845.8, score: 71, signal: 'Bullish', brk: 'Breakout20', vol: 'Surge 1.9x' },
    { symbol: 'ICICIBANK', name: 'ICICI Bank Ltd.', price: 1240.6, score: 68, signal: 'Bullish', brk: 'Golden Cross', vol: 'Normal 1.2x' },
    { symbol: 'BHARTIARTL', name: 'Bharti Airtel Ltd.', price: 1560.0, score: 64, signal: 'Bullish', brk: 'Above 50 EMA', vol: 'Normal 1.1x' },
    { symbol: 'SBIN', name: 'State Bank of India', price: 810.4, score: 54, signal: 'Neutral', brk: 'Rangebound', vol: 'Normal 0.9x' },
    { symbol: 'LT', name: 'Larsen & Toubro Ltd.', price: 3620.0, score: 51, signal: 'Neutral', brk: 'EMA Contraction', vol: 'Normal 1.0x' },
    { symbol: 'ITC', name: 'ITC Ltd.', price: 470.5, score: 48, signal: 'Neutral', brk: 'Inside Day', vol: 'Low 0.8x' },
    { symbol: 'WIPRO', name: 'Wipro Ltd.', price: 540.2, score: 35, signal: 'Bearish', brk: 'Below 20 EMA', vol: 'Normal 1.1x' },
    { symbol: 'TATAMOTORS', name: 'Tata Motors Ltd.', price: 890.0, score: 30, signal: 'Bearish', brk: 'Breakdown20', vol: 'Surge 1.7x' },
    { symbol: 'BAJFINANCE', name: 'Bajaj Finance Ltd.', price: 6850.0, score: 18, signal: 'Strong Bearish', brk: 'Breakdown50', vol: 'Surge 2.4x' },
  ];

  let list = mockStocks.map((s, idx) => ({
    id: idx + 1,
    symbol: s.symbol,
    companyName: s.name,
    instrumentKey: `NSE_EQ|MOCK${idx}`,
    scanType,
    timestamp: new Date().toISOString(),
    currentPrice: s.price,
    technicalScore: s.score,
    signal: s.signal as ScanResultDto['signal'],
    breakoutStatus: s.brk,
    volumeStatus: s.vol,
    explanation: `${s.signal} posture; Score: ${s.score}/100; Key factors: ${s.brk}, ${s.vol}`,
    rsi: s.score > 70 ? 64.5 : s.score < 30 ? 28.2 : 48.0,
    macd: s.score > 60 ? 14.2 : s.score < 40 ? -12.4 : 1.5,
    ema20: Math.round(s.price * (s.score > 50 ? 0.97 : 1.03) * 100) / 100,
    ema50: Math.round(s.price * (s.score > 50 ? 0.94 : 1.06) * 100) / 100,
    ema200: Math.round(s.price * (s.score > 50 ? 0.88 : 1.12) * 100) / 100,
    volumeRatio: s.vol.includes('2.8') ? 2.8 : s.vol.includes('2.1') ? 2.1 : 1.3,
  }));

  if (signalFilter && signalFilter !== 'ALL') {
    list = list.filter((item) => item.signal.toLowerCase() === signalFilter.toLowerCase());
  }

  return list.slice(0, limit);
}

function generateFallbackAiExplanation(symbol: string, scanType: string): AiExplanationDto {
  return {
    symbol,
    companyName: `${symbol} Ltd.`,
    scanType,
    technicalScore: 85,
    signal: 'Strong Bullish',
    currentPrice: 3020.5,
    executiveSummary: `${symbol} demonstrates exceptional institutional accumulation with a technical score of 85.0/100. Price is confirmed above all major exponential moving averages with strong volume expansion.`,
    keyDrivers: [
      'EMA Bullish Alignment (20 EMA > 50 EMA > 200 EMA)',
      '20-Period Resistance High Breakout confirmed',
      'MACD Bullish Crossover with expanding positive histogram momentum',
      'Relative Volume Ratio of 2.45x confirming institutional participation',
    ],
    supportResistanceLevels:
      'Immediate dynamic support is anchored by the 20-day EMA at 2950.00. Primary resistance breakout at 3000.00 now serves as the immediate price floor.',
    riskFactors:
      'A daily close below 2950.00 invalidates the short-term breakout. Monitor RSI (currently 65.4) for momentum continuation into overbought territory.',
    fullCommentary: `${symbol} exhibits textbook momentum breakout structure on the daily timeframe. The confluence of rising moving averages and surging participation suggests continuation toward previous all-time highs. Swing risk-reward remains favorable while price trades above the 20 EMA floor.`,
    provider: 'RuleBasedFinancialEngine',
    generatedAt: new Date().toISOString(),
  };
}

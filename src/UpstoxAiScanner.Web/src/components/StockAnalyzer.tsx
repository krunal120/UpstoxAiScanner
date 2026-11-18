import React, { useState } from 'react';
import {
  Search,
  Sparkles,
  TrendingUp,
  Cpu,
  Layers,
  Compass,
  Volume2,
  CheckCircle2,
  ShieldAlert,
} from 'lucide-react';
import { ScoreGauge } from './ScoreGauge';
import { api } from '../services/api';
import type { ScanResultDto, AiExplanationDto, TechnicalAnalysisDto } from '../types/api';

export const StockAnalyzer: React.FC = () => {
  const [symbolInput, setSymbolInput] = useState('RELIANCE');
  const [scanType, setScanType] = useState<'Swing' | 'Intraday'>('Swing');
  const [scanResult, setScanResult] = useState<ScanResultDto | null>(null);
  const [aiExplanation, setAiExplanation] = useState<AiExplanationDto | null>(null);
  const [indicators, setIndicators] = useState<TechnicalAnalysisDto | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const popularSymbols = ['RELIANCE', 'TCS', 'HDFCBANK', 'INFY', 'ICICIBANK', 'BHARTIARTL', 'SBIN', 'LT', 'BAJFINANCE'];

  const handleAnalyze = async (symToAnalyze?: string) => {
    const targetSymbol = (symToAnalyze || symbolInput).trim().toUpperCase();
    if (!targetSymbol) return;

    setIsLoading(true);
    setError(null);

    try {
      // 1. Scan Stock
      const scanRes = await api.scanStock(targetSymbol, scanType);
      setScanResult(scanRes);

      // 2. Fetch AI Explanation & Detailed Indicators in parallel
      const [aiRes, indRes] = await Promise.all([
        api.getAiExplanation(targetSymbol, scanType),
        api.getIndicators(targetSymbol, scanType === 'Intraday' ? '5m' : '1d'),
      ]);

      setAiExplanation(aiRes);
      setIndicators(indRes);
    } catch (err: any) {
      console.error('Error analyzing stock:', err);
      setError(err?.message || `Failed to analyze ${targetSymbol}`);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="space-y-6">
      {/* Title */}
      <div>
        <h2 className="text-xl font-bold tracking-tight text-white">Stock Deep-Dive & Grounded AI Intelligence</h2>
        <p className="text-xs text-slate-400">
          On-demand quantitative analysis and zero-hallucination institutional market narrative.
        </p>
      </div>

      {/* Search and Timeframe Selector */}
      <div className="rounded-xl border border-slate-800 bg-slate-900/60 p-4 space-y-4">
        <div className="flex flex-col sm:flex-row gap-3">
          <div className="relative flex-1">
            <Search className="absolute left-3.5 top-3 h-4 w-4 text-slate-400" />
            <input
              type="text"
              value={symbolInput}
              onChange={(e) => setSymbolInput(e.target.value.toUpperCase())}
              placeholder="Enter any NSE or BSE symbol (e.g. RELIANCE, TCS, ZOMATO)..."
              className="w-full rounded-lg border border-slate-800 bg-slate-950 pl-10 pr-4 py-2.5 text-xs font-mono uppercase text-white placeholder-slate-500 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
              onKeyDown={(e) => e.key === 'Enter' && handleAnalyze()}
            />
          </div>

          <div className="flex rounded-lg border border-slate-800 bg-slate-950 p-1">
            <button
              onClick={() => setScanType('Swing')}
              className={`px-3 py-1.5 rounded-md text-xs font-medium transition-all ${
                scanType === 'Swing' ? 'bg-indigo-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200'
              }`}
            >
              Daily Swing (1d)
            </button>
            <button
              onClick={() => setScanType('Intraday')}
              className={`px-3 py-1.5 rounded-md text-xs font-medium transition-all ${
                scanType === 'Intraday' ? 'bg-indigo-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200'
              }`}
            >
              Intraday (5m)
            </button>
          </div>

          <button
            onClick={() => handleAnalyze()}
            disabled={isLoading || !symbolInput}
            className="flex items-center justify-center gap-2 rounded-lg bg-indigo-600 px-5 py-2.5 text-xs font-medium text-white shadow hover:bg-indigo-500 disabled:opacity-50 transition-colors"
          >
            {isLoading ? (
              <div className="h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
            ) : (
              <Sparkles className="h-4 w-4" />
            )}
            <span>Analyze Stock</span>
          </button>
        </div>

        {/* Popular Stock Badges */}
        <div className="flex flex-wrap items-center gap-1.5 pt-1">
          <span className="text-[11px] text-slate-500">Popular:</span>
          {popularSymbols.map((sym) => (
            <button
              key={sym}
              onClick={() => {
                setSymbolInput(sym);
                handleAnalyze(sym);
              }}
              className="rounded-md bg-slate-800/80 px-2 py-0.5 font-mono text-[11px] text-slate-300 hover:bg-indigo-600/30 hover:text-indigo-300 border border-slate-700/60 transition-colors"
            >
              {sym}
            </button>
          ))}
        </div>
      </div>

      {error && (
        <div className="rounded-lg border border-rose-500/30 bg-rose-500/10 p-3 text-xs text-rose-300">
          {error}
        </div>
      )}

      {/* Analysis Results View */}
      {scanResult && (
        <div className="space-y-6">
          {/* Header Banner */}
          <div className="rounded-xl border border-slate-800 bg-slate-900/60 p-6 flex flex-col md:flex-row items-center justify-between gap-6">
            <div className="space-y-1 text-center md:text-left">
              <div className="flex items-center justify-center md:justify-start gap-2">
                <h3 className="text-2xl font-extrabold text-white">{scanResult.symbol}</h3>
                <span
                  className={`inline-flex items-center rounded px-2 py-0.5 text-xs font-mono font-medium ${
                    scanResult.instrumentKey.startsWith('BSE')
                      ? 'bg-amber-500/10 text-amber-400 border border-amber-500/30'
                      : 'bg-cyan-500/10 text-cyan-400 border border-cyan-500/30'
                  }`}
                >
                  {scanResult.instrumentKey.startsWith('BSE') ? 'BSE' : 'NSE'}
                </span>
                <span className="rounded-full bg-slate-800 px-2.5 py-0.5 text-xs font-mono text-slate-300">
                  {scanResult.scanType}
                </span>
              </div>
              <p className="text-xs text-slate-400">{scanResult.companyName}</p>
              <div className="text-2xl font-bold font-mono text-white pt-2">
                ₹{scanResult.currentPrice.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
              </div>
            </div>

            <div className="w-full md:w-64">
              <ScoreGauge score={scanResult.technicalScore} signal={scanResult.signal} size="lg" showBar={true} />
            </div>
          </div>

          {/* Indicator Cards Matrix */}
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            {/* Card 1: EMAs */}
            <div className="rounded-xl border border-slate-800 bg-slate-900/60 p-4 space-y-2">
              <div className="flex items-center justify-between text-xs text-slate-400">
                <span className="flex items-center gap-1.5 font-medium">
                  <Layers className="h-4 w-4 text-cyan-400" />
                  EMAs (20/50/200)
                </span>
                <span className="font-mono text-cyan-300 text-[11px]">
                  {scanResult.ema20 && scanResult.ema50 && scanResult.ema20 > scanResult.ema50 ? 'Bullish' : 'Neutral'}
                </span>
              </div>
              <div className="space-y-1 font-mono text-xs">
                <div className="flex justify-between text-slate-300">
                  <span>EMA 20:</span>
                  <span className="font-bold">₹{scanResult.ema20?.toFixed(2) || '—'}</span>
                </div>
                <div className="flex justify-between text-slate-300">
                  <span>EMA 50:</span>
                  <span className="font-bold">₹{scanResult.ema50?.toFixed(2) || '—'}</span>
                </div>
                <div className="flex justify-between text-slate-300">
                  <span>EMA 200:</span>
                  <span className="font-bold">₹{scanResult.ema200?.toFixed(2) || '—'}</span>
                </div>
              </div>
            </div>

            {/* Card 2: MACD */}
            <div className="rounded-xl border border-slate-800 bg-slate-900/60 p-4 space-y-2">
              <div className="flex items-center justify-between text-xs text-slate-400">
                <span className="flex items-center gap-1.5 font-medium">
                  <TrendingUp className="h-4 w-4 text-indigo-400" />
                  MACD (12, 26, 9)
                </span>
                <span className="font-mono text-indigo-300 text-[11px]">
                  {indicators?.macd?.histogramMomentum || 'Expanding'}
                </span>
              </div>
              <div className="space-y-1 font-mono text-xs">
                <div className="flex justify-between text-slate-300">
                  <span>MACD Line:</span>
                  <span className="font-bold text-indigo-300">{scanResult.macd?.toFixed(2) || '—'}</span>
                </div>
                <div className="flex justify-between text-slate-300">
                  <span>Signal Line:</span>
                  <span className="font-bold">{indicators?.macd?.signal.toFixed(2) || '—'}</span>
                </div>
                <div className="flex justify-between text-slate-300">
                  <span>Histogram:</span>
                  <span className="font-bold text-emerald-400">{indicators?.macd?.histogram.toFixed(2) || '—'}</span>
                </div>
              </div>
            </div>

            {/* Card 3: RSI */}
            <div className="rounded-xl border border-slate-800 bg-slate-900/60 p-4 space-y-2">
              <div className="flex items-center justify-between text-xs text-slate-400">
                <span className="flex items-center gap-1.5 font-medium">
                  <Compass className="h-4 w-4 text-violet-400" />
                  Wilder RSI (14)
                </span>
                <span className="font-mono text-violet-300 text-[11px]">
                  {scanResult.rsi && scanResult.rsi >= 70 ? 'Overbought' : scanResult.rsi && scanResult.rsi <= 30 ? 'Oversold' : 'Neutral'}
                </span>
              </div>
              <div className="space-y-2">
                <div className="flex justify-between items-baseline font-mono">
                  <span className="text-2xl font-bold text-violet-300">{scanResult.rsi?.toFixed(1) || '—'}</span>
                  <span className="text-xs text-slate-500">Zone 30-70</span>
                </div>
                <div className="w-full bg-slate-800 rounded-full h-1.5 overflow-hidden">
                  <div
                    className="bg-violet-400 h-1.5 rounded-full"
                    style={{ width: `${Math.min(100, Math.max(0, scanResult.rsi || 50))}%` }}
                  />
                </div>
              </div>
            </div>

            {/* Card 4: Volume & Breakouts */}
            <div className="rounded-xl border border-slate-800 bg-slate-900/60 p-4 space-y-2">
              <div className="flex items-center justify-between text-xs text-slate-400">
                <span className="flex items-center gap-1.5 font-medium">
                  <Volume2 className="h-4 w-4 text-amber-400" />
                  Volume & Breakouts
                </span>
                <span className="font-mono text-amber-300 text-[11px]">
                  {scanResult.volumeRatio && scanResult.volumeRatio >= 1.5 ? 'Surge' : 'Standard'}
                </span>
              </div>
              <div className="space-y-1 font-mono text-xs">
                <div className="flex justify-between text-slate-300">
                  <span>Relative Volume:</span>
                  <span className="font-bold text-amber-300">{scanResult.volumeRatio?.toFixed(2) || '1.00'}x</span>
                </div>
                <div className="flex justify-between text-slate-300">
                  <span>Breakout Status:</span>
                  <span className="font-bold text-emerald-400">{scanResult.breakoutStatus || 'Confirmed'}</span>
                </div>
                <div className="flex justify-between text-slate-300">
                  <span>Volume Status:</span>
                  <span className="font-bold">{scanResult.volumeStatus || 'Normal'}</span>
                </div>
              </div>
            </div>
          </div>

          {/* AI Narrative Section */}
          {aiExplanation && (
            <div className="rounded-xl border border-slate-800 bg-slate-900/60 p-6 space-y-4">
              <div className="flex items-center justify-between border-b border-slate-800 pb-3">
                <div className="flex items-center gap-2">
                  <Sparkles className="h-5 w-5 text-indigo-400" />
                  <h3 className="text-sm font-bold text-white">Grounded Institutional Market Commentary</h3>
                </div>
                <div className="flex items-center gap-2 text-xs">
                  <Cpu className="h-3.5 w-3.5 text-cyan-400" />
                  <span className="px-2 py-0.5 rounded-full bg-cyan-500/10 text-cyan-300 border border-cyan-500/20 font-mono text-[11px]">
                    {aiExplanation.provider}
                  </span>
                </div>
              </div>

              {/* Executive Summary */}
              <div className="rounded-xl border border-indigo-500/30 bg-indigo-500/10 p-4">
                <div className="text-xs font-bold text-indigo-300 uppercase tracking-wider mb-1">
                  Executive Summary
                </div>
                <p className="text-xs text-slate-200 leading-relaxed">
                  {aiExplanation.executiveSummary}
                </p>
              </div>

              {/* Key Drivers */}
              <div className="rounded-xl border border-slate-800 bg-slate-950/40 p-4 space-y-2">
                <div className="text-xs font-bold text-slate-300 uppercase tracking-wider">
                  Quantitative Drivers & Catalysts
                </div>
                <ul className="space-y-1.5 text-xs text-slate-300">
                  {aiExplanation.keyDrivers.map((driver, idx) => (
                    <li key={idx} className="flex items-start gap-2">
                      <CheckCircle2 className="h-4 w-4 text-emerald-400 shrink-0 mt-0.5" />
                      <span>{driver}</span>
                    </li>
                  ))}
                </ul>
              </div>

              {/* Support & Resistance */}
              <div className="rounded-xl border border-slate-800 bg-slate-950/40 p-4 space-y-1">
                <div className="text-xs font-bold text-slate-300 uppercase tracking-wider">
                  Key Support & Resistance Levels
                </div>
                <p className="text-xs text-slate-300 leading-relaxed font-mono">
                  {aiExplanation.supportResistanceLevels}
                </p>
              </div>

              {/* Risk Factors */}
              <div className="rounded-xl border border-amber-500/20 bg-amber-500/5 p-4 space-y-1">
                <div className="flex items-center gap-1.5 text-xs font-bold text-amber-300 uppercase tracking-wider">
                  <ShieldAlert className="h-4 w-4" />
                  Risk Factors & Invalidation Criteria
                </div>
                <p className="text-xs text-amber-200/90 leading-relaxed">
                  {aiExplanation.riskFactors}
                </p>
              </div>

              {/* Full Narrative */}
              <div className="rounded-xl border border-slate-800 bg-slate-950/40 p-4 space-y-2">
                <div className="text-xs font-bold text-slate-300 uppercase tracking-wider">
                  Comprehensive Strategy Report
                </div>
                <p className="text-xs text-slate-300 leading-relaxed whitespace-pre-line">
                  {aiExplanation.fullCommentary}
                </p>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
};

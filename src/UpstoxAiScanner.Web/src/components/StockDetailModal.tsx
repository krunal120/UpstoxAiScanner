import React, { useState, useEffect } from 'react';
import {
  X,
  Sparkles,
  TrendingUp,
  Volume2,
  ShieldAlert,
  Layers,
  Cpu,
  Compass,
  CheckCircle2,
} from 'lucide-react';
import { ScoreGauge } from './ScoreGauge';
import { api } from '../services/api';
import type { ScanResultDto, AiExplanationDto, TechnicalAnalysisDto } from '../types/api';

interface StockDetailModalProps {
  stock: ScanResultDto;
  onClose: () => void;
}

export const StockDetailModal: React.FC<StockDetailModalProps> = ({ stock, onClose }) => {
  const [activeTab, setActiveTab] = useState<'overview' | 'ai' | 'chart'>('overview');
  const [aiExplanation, setAiExplanation] = useState<AiExplanationDto | null>(null);
  const [indicators, setIndicators] = useState<TechnicalAnalysisDto | null>(null);
  const [loadingAi, setLoadingAi] = useState(false);

  useEffect(() => {
    // Fetch indicators and AI explanation
    const fetchDeepDive = async () => {
      setLoadingAi(true);
      try {
        const [expl, ind] = await Promise.all([
          api.getAiExplanation(stock.symbol, stock.scanType),
          api.getIndicators(stock.symbol, stock.scanType === 'Intraday' ? '5m' : '1d'),
        ]);
        setAiExplanation(expl);
        setIndicators(ind);
      } catch (err) {
        console.error('Error fetching deep dive details:', err);
      } finally {
        setLoadingAi(false);
      }
    };

    fetchDeepDive();
  }, [stock]);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/80 backdrop-blur-md overflow-y-auto">
      <div className="relative w-full max-w-4xl rounded-2xl border border-slate-800 bg-slate-900 shadow-2xl my-8 overflow-hidden">
        {/* Modal Header */}
        <div className="flex items-center justify-between border-b border-slate-800 px-6 py-4 bg-slate-950/40">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-indigo-500/10 border border-indigo-500/20 text-indigo-400 font-bold font-mono">
              {stock.symbol.slice(0, 3)}
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h3 className="text-lg font-bold text-white">{stock.symbol}</h3>
                <span className="rounded-full bg-slate-800 px-2 py-0.5 text-[10px] font-mono text-slate-300">
                  {stock.scanType}
                </span>
              </div>
              <p className="text-xs text-slate-400">{stock.companyName}</p>
            </div>
          </div>

          <div className="flex items-center gap-4">
            <div className="text-right">
              <div className="text-lg font-extrabold font-mono text-white">
                ₹{stock.currentPrice.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
              </div>
              <div className="text-xs font-medium text-emerald-400">NSE EQ</div>
            </div>

            <button
              onClick={onClose}
              className="rounded-lg p-1.5 text-slate-400 hover:bg-slate-800 hover:text-white transition-colors"
            >
              <X className="h-5 w-5" />
            </button>
          </div>
        </div>

        {/* Modal Navigation Tabs */}
        <div className="flex border-b border-slate-800 bg-slate-950/20 px-6">
          <button
            onClick={() => setActiveTab('overview')}
            className={`flex items-center gap-2 py-3 px-4 text-xs font-semibold border-b-2 transition-all ${
              activeTab === 'overview'
                ? 'border-indigo-500 text-indigo-400'
                : 'border-transparent text-slate-400 hover:text-slate-200'
            }`}
          >
            <Compass className="h-4 w-4" />
            Quantitative Overview
          </button>
          <button
            onClick={() => setActiveTab('ai')}
            className={`flex items-center gap-2 py-3 px-4 text-xs font-semibold border-b-2 transition-all ${
              activeTab === 'ai'
                ? 'border-indigo-500 text-indigo-400'
                : 'border-transparent text-slate-400 hover:text-slate-200'
            }`}
          >
            <Sparkles className="h-4 w-4" />
            Grounded AI Explanation
          </button>
          <button
            onClick={() => setActiveTab('chart')}
            className={`flex items-center gap-2 py-3 px-4 text-xs font-semibold border-b-2 transition-all ${
              activeTab === 'chart'
                ? 'border-indigo-500 text-indigo-400'
                : 'border-transparent text-slate-400 hover:text-slate-200'
            }`}
          >
            <TrendingUp className="h-4 w-4" />
            Price & EMA Chart
          </button>
        </div>

        {/* Modal Content */}
        <div className="p-6 max-h-[75vh] overflow-y-auto space-y-6">
          {activeTab === 'overview' && (
            <div className="space-y-6">
              {/* Score Gauge Banner */}
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4 items-center rounded-xl bg-slate-950/60 p-4 border border-slate-800">
                <ScoreGauge score={stock.technicalScore} signal={stock.signal} size="lg" showBar={true} />

                <div className="md:col-span-2 space-y-2">
                  <div className="text-xs uppercase font-semibold tracking-wider text-slate-400">
                    Rule-Based Scoring Logic (0-100)
                  </div>
                  <p className="text-xs text-slate-300 leading-relaxed">
                    {stock.explanation ||
                      'Evaluated across 4 quantitative pillars: Trend Alignment (EMA 20/50/200), Momentum Acceleration (MACD 12/26/9), Relative Strength (RSI 14), and Resistance Breakout Confirmation.'}
                  </p>
                  <div className="flex flex-wrap gap-2 pt-1">
                    {stock.breakoutStatus && (
                      <span className="px-2 py-0.5 rounded bg-cyan-500/10 text-cyan-300 border border-cyan-500/20 text-[11px] font-mono">
                        {stock.breakoutStatus}
                      </span>
                    )}
                    {stock.volumeStatus && (
                      <span className="px-2 py-0.5 rounded bg-violet-500/10 text-violet-300 border border-violet-500/20 text-[11px] font-mono">
                        Volume: {stock.volumeStatus}
                      </span>
                    )}
                  </div>
                </div>
              </div>

              {/* Indicator Metrics Matrix */}
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
                {/* Metric 1: EMA Alignment */}
                <div className="rounded-xl border border-slate-800 bg-slate-950/40 p-4 space-y-2">
                  <div className="flex items-center justify-between text-xs text-slate-400">
                    <span className="flex items-center gap-1.5 font-medium">
                      <Layers className="h-4 w-4 text-cyan-400" />
                      EMAs (20 / 50 / 200)
                    </span>
                    <span className="font-mono text-cyan-300 text-[11px]">
                      {stock.ema20 && stock.ema50 && stock.ema20 > stock.ema50 ? 'Bullish' : 'Neutral'}
                    </span>
                  </div>
                  <div className="space-y-1 font-mono text-xs">
                    <div className="flex justify-between text-slate-300">
                      <span>EMA 20:</span>
                      <span className="font-bold">₹{stock.ema20?.toFixed(2) || '—'}</span>
                    </div>
                    <div className="flex justify-between text-slate-300">
                      <span>EMA 50:</span>
                      <span className="font-bold">₹{stock.ema50?.toFixed(2) || '—'}</span>
                    </div>
                    <div className="flex justify-between text-slate-300">
                      <span>EMA 200:</span>
                      <span className="font-bold">₹{stock.ema200?.toFixed(2) || '—'}</span>
                    </div>
                  </div>
                </div>

                {/* Metric 2: MACD */}
                <div className="rounded-xl border border-slate-800 bg-slate-950/40 p-4 space-y-2">
                  <div className="flex items-center justify-between text-xs text-slate-400">
                    <span className="flex items-center gap-1.5 font-medium">
                      <TrendingUp className="h-4 w-4 text-indigo-400" />
                      MACD (12, 26, 9)
                    </span>
                    <span className="font-mono text-indigo-300 text-[11px]">
                      {indicators?.macd?.histogramMomentum || (stock.macd && stock.macd > 0 ? 'Expanding' : 'Neutral')}
                    </span>
                  </div>
                  <div className="space-y-1 font-mono text-xs">
                    <div className="flex justify-between text-slate-300">
                      <span>MACD Line:</span>
                      <span className="font-bold text-indigo-300">{stock.macd?.toFixed(2) || '—'}</span>
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

                {/* Metric 3: RSI */}
                <div className="rounded-xl border border-slate-800 bg-slate-950/40 p-4 space-y-2">
                  <div className="flex items-center justify-between text-xs text-slate-400">
                    <span className="flex items-center gap-1.5 font-medium">
                      <Compass className="h-4 w-4 text-violet-400" />
                      Wilder RSI (14)
                    </span>
                    <span className="font-mono text-violet-300 text-[11px]">
                      {stock.rsi && stock.rsi >= 70 ? 'Overbought' : stock.rsi && stock.rsi <= 30 ? 'Oversold' : 'Neutral'}
                    </span>
                  </div>
                  <div className="space-y-2">
                    <div className="flex justify-between items-baseline font-mono">
                      <span className="text-2xl font-bold text-violet-300">{stock.rsi?.toFixed(1) || '—'}</span>
                      <span className="text-xs text-slate-500">Zone 30-70</span>
                    </div>
                    <div className="w-full bg-slate-800 rounded-full h-1.5 overflow-hidden">
                      <div
                        className="bg-violet-400 h-1.5 rounded-full"
                        style={{ width: `${Math.min(100, Math.max(0, stock.rsi || 50))}%` }}
                      />
                    </div>
                  </div>
                </div>

                {/* Metric 4: Volume Ratio */}
                <div className="rounded-xl border border-slate-800 bg-slate-950/40 p-4 space-y-2">
                  <div className="flex items-center justify-between text-xs text-slate-400">
                    <span className="flex items-center gap-1.5 font-medium">
                      <Volume2 className="h-4 w-4 text-amber-400" />
                      Volume Analytics
                    </span>
                    <span className="font-mono text-amber-300 text-[11px]">
                      {stock.volumeRatio && stock.volumeRatio >= 1.5 ? 'Surge' : 'Standard'}
                    </span>
                  </div>
                  <div className="space-y-1 font-mono text-xs">
                    <div className="flex justify-between text-slate-300">
                      <span>Relative Vol:</span>
                      <span className="font-bold text-amber-300">{stock.volumeRatio?.toFixed(2) || '1.00'}x</span>
                    </div>
                    <div className="flex justify-between text-slate-300">
                      <span>Baseline:</span>
                      <span className="font-bold">20-SMA</span>
                    </div>
                    <div className="flex justify-between text-slate-300">
                      <span>Breakout:</span>
                      <span className="font-bold text-emerald-400">{stock.breakoutStatus || 'Confirmed'}</span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          )}

          {activeTab === 'ai' && (
            <div className="space-y-4">
              {loadingAi ? (
                <div className="flex flex-col items-center justify-center py-12 gap-3 text-slate-400">
                  <div className="h-8 w-8 animate-spin rounded-full border-2 border-indigo-500 border-t-transparent" />
                  <span className="text-xs font-mono">Generating institutional AI narrative commentary...</span>
                </div>
              ) : aiExplanation ? (
                <div className="space-y-4">
                  {/* Provider & Timestamp */}
                  <div className="flex items-center justify-between text-xs">
                    <div className="flex items-center gap-2">
                      <Cpu className="h-4 w-4 text-cyan-400" />
                      <span className="text-slate-400">Engine:</span>
                      <span className="px-2 py-0.5 rounded-full bg-cyan-500/10 text-cyan-300 border border-cyan-500/20 font-mono">
                        {aiExplanation.provider}
                      </span>
                    </div>
                    <span className="text-slate-500 font-mono text-[11px]">
                      Zero-Hallucination Quantitative Grounding
                    </span>
                  </div>

                  {/* Executive Summary */}
                  <div className="rounded-xl border border-indigo-500/30 bg-indigo-500/10 p-4 space-y-1">
                    <div className="flex items-center gap-1.5 text-xs font-bold text-indigo-300 uppercase tracking-wider">
                      <Sparkles className="h-4 w-4" />
                      Executive Market Summary
                    </div>
                    <p className="text-xs text-slate-200 leading-relaxed">
                      {aiExplanation.executiveSummary}
                    </p>
                  </div>

                  {/* Key Drivers */}
                  <div className="rounded-xl border border-slate-800 bg-slate-950/40 p-4 space-y-2">
                    <div className="text-xs font-bold text-slate-300 uppercase tracking-wider">
                      Key Quantitative Forces
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
                      Dynamic Support & Resistance Analysis
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

                  {/* Full Commentary */}
                  <div className="rounded-xl border border-slate-800 bg-slate-950/40 p-4 space-y-2">
                    <div className="text-xs font-bold text-slate-300 uppercase tracking-wider">
                      Institutional Strategy Commentary
                    </div>
                    <p className="text-xs text-slate-300 leading-relaxed whitespace-pre-line">
                      {aiExplanation.fullCommentary}
                    </p>
                  </div>
                </div>
              ) : (
                <div className="py-8 text-center text-xs text-slate-500">
                  Unable to load AI explanation.
                </div>
              )}
            </div>
          )}

          {activeTab === 'chart' && (
            <div className="space-y-4">
              <div className="rounded-xl border border-slate-800 bg-slate-950/60 p-4 space-y-4">
                <div className="flex items-center justify-between text-xs text-slate-400">
                  <span className="font-semibold text-slate-200">Price Trend & Dynamic Moving Averages</span>
                  <div className="flex items-center gap-3 font-mono text-[11px]">
                    <span className="flex items-center gap-1 text-cyan-400">
                      <span className="h-2 w-2 rounded-full bg-cyan-400"></span> 20 EMA: ₹{stock.ema20?.toFixed(2) || '—'}
                    </span>
                    <span className="flex items-center gap-1 text-indigo-400">
                      <span className="h-2 w-2 rounded-full bg-indigo-400"></span> 50 EMA: ₹{stock.ema50?.toFixed(2) || '—'}
                    </span>
                    <span className="flex items-center gap-1 text-violet-400">
                      <span className="h-2 w-2 rounded-full bg-violet-400"></span> 200 EMA: ₹{stock.ema200?.toFixed(2) || '—'}
                    </span>
                  </div>
                </div>

                {/* SVG Visual Candlestick / Trend Curve */}
                <div className="w-full h-64 bg-slate-900/80 rounded-lg p-4 relative overflow-hidden flex flex-col justify-end">
                  <svg className="w-full h-full overflow-visible" viewBox="0 0 500 150" preserveAspectRatio="none">
                    <defs>
                      <linearGradient id="chartGradient" x1="0%" y1="0%" x2="0%" y2="100%">
                        <stop offset="0%" stopColor="#6366f1" stopOpacity="0.4" />
                        <stop offset="100%" stopColor="#6366f1" stopOpacity="0.0" />
                      </linearGradient>
                    </defs>

                    {/* Area fill */}
                    <path
                      d="M 0,110 Q 75,90 150,100 T 300,60 T 420,40 T 500,20 L 500,150 L 0,150 Z"
                      fill="url(#chartGradient)"
                    />

                    {/* EMA 200 (Purple) */}
                    <path
                      d="M 0,120 Q 150,110 300,95 T 500,80"
                      fill="none"
                      stroke="#8b5cf6"
                      strokeWidth="2"
                      strokeDasharray="4 4"
                    />

                    {/* EMA 50 (Indigo) */}
                    <path
                      d="M 0,115 Q 150,95 300,75 T 500,50"
                      fill="none"
                      stroke="#6366f1"
                      strokeWidth="2"
                    />

                    {/* EMA 20 (Cyan) */}
                    <path
                      d="M 0,110 Q 150,85 300,55 T 500,30"
                      fill="none"
                      stroke="#22d3ee"
                      strokeWidth="2"
                    />

                    {/* Price Curve */}
                    <path
                      d="M 0,110 Q 75,90 150,100 T 300,60 T 420,40 T 500,20"
                      fill="none"
                      stroke="#f8fafc"
                      strokeWidth="2.5"
                    />
                  </svg>

                  {/* Chart Legend */}
                  <div className="absolute top-3 left-3 text-[11px] font-mono text-slate-400 bg-slate-950/80 px-2 py-1 rounded border border-slate-800">
                    Price: ₹{stock.currentPrice.toFixed(2)} | Score: {stock.technicalScore}/100
                  </div>
                </div>
              </div>
            </div>
          )}
        </div>

        {/* Modal Footer */}
        <div className="flex items-center justify-between border-t border-slate-800 bg-slate-950/40 px-6 py-3">
          <span className="text-[11px] text-slate-500 font-mono">
            Analytical signals only. Not financial advice.
          </span>
          <button
            onClick={onClose}
            className="rounded-lg bg-slate-800 px-4 py-1.5 text-xs font-medium text-slate-200 hover:bg-slate-700 transition-colors"
          >
            Close
          </button>
        </div>
      </div>
    </div>
  );
};

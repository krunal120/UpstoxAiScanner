import React from 'react';
import {
  Activity,
  Clock,
  RefreshCw,
  Zap,
  TrendingUp,
  Cpu,
  ShieldCheck,
} from 'lucide-react';
import type { MarketSessionStatus, BackgroundScannerStatusDto } from '../types/api';

interface NavbarProps {
  marketStatus: MarketSessionStatus | null;
  backgroundStatus: BackgroundScannerStatusDto | null;
  activeTab: 'swing' | 'intraday' | 'analyzer' | 'background';
  setActiveTab: (tab: 'swing' | 'intraday' | 'analyzer' | 'background') => void;
  onRunScan: () => void;
  isScanning: boolean;
}

export const Navbar: React.FC<NavbarProps> = ({
  marketStatus,
  backgroundStatus,
  activeTab,
  setActiveTab,
  onRunScan,
  isScanning,
}) => {
  const getPhaseColor = (phase?: string) => {
    switch (phase) {
      case 'RegularOpen':
        return 'bg-emerald-500/20 text-emerald-400 border-emerald-500/40';
      case 'PreOpen':
        return 'bg-amber-500/20 text-amber-400 border-amber-500/40';
      case 'PostClose':
        return 'bg-blue-500/20 text-blue-400 border-blue-500/40';
      default:
        return 'bg-slate-700/40 text-slate-400 border-slate-600/40';
    }
  };

  return (
    <header className="sticky top-0 z-40 border-b border-slate-800 bg-slate-950/80 backdrop-blur-md">
      <div className="mx-auto flex max-w-7xl items-center justify-between px-4 py-3 sm:px-6 lg:px-8">
        {/* Brand / Logo */}
        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-tr from-cyan-600 via-indigo-600 to-violet-500 shadow-lg shadow-indigo-500/20">
            <TrendingUp className="h-5 w-5 text-white" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <span className="font-bold tracking-tight text-white text-lg">
                Upstox <span className="bg-gradient-to-r from-cyan-400 to-indigo-400 bg-clip-text text-transparent">AI Scanner</span>
              </span>
              <span className="rounded-full bg-indigo-500/10 px-2 py-0.5 text-[10px] font-semibold text-indigo-400 border border-indigo-500/20">
                NSE & BSE ALL STOCKS
              </span>
            </div>
            <p className="text-xs text-slate-400 hidden sm:block">Quantitative Market Intelligence & Grounded AI Analysis</p>
          </div>
        </div>

        {/* Live Market Hours & Session Pill */}
        <div className="hidden md:flex items-center gap-3">
          {marketStatus && (
            <div className={`flex items-center gap-2 px-3 py-1.5 rounded-full border text-xs font-medium ${getPhaseColor(marketStatus.sessionPhase || marketStatus.phase)}`}>
              <span className="relative flex h-2 w-2">
                {marketStatus.isMarketOpen && (
                  <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
                )}
                <span className={`relative inline-flex rounded-full h-2 w-2 ${marketStatus.isMarketOpen ? 'bg-emerald-500' : 'bg-slate-500'}`}></span>
              </span>
              <span className="font-semibold uppercase tracking-wider">{marketStatus.sessionPhase || marketStatus.phase || 'Closed'}</span>
              <span className="text-slate-500">|</span>
              <span className="flex items-center gap-1 font-mono text-slate-300">
                <Clock className="h-3.5 w-3.5" />
                {(marketStatus.currentTimeIst || marketStatus.currentIstTime)
                  ? new Date(marketStatus.currentTimeIst || marketStatus.currentIstTime!).toLocaleTimeString('en-IN', { hour12: false })
                  : '--:--:--'} IST
              </span>
            </div>
          )}

          {backgroundStatus && (
            <div className="flex items-center gap-1.5 px-2.5 py-1 rounded-md bg-slate-900 border border-slate-800 text-[11px] text-slate-400 font-mono">
              <Cpu className="h-3.5 w-3.5 text-cyan-400" />
              <span>BG: {backgroundStatus.isEnabled ? 'Active' : 'Paused'}</span>
              <span className="text-slate-600">({backgroundStatus.totalScansExecuted} runs)</span>
            </div>
          )}
        </div>

        {/* Navigation & Actions */}
        <div className="flex items-center gap-2 sm:gap-3">
          <button
            onClick={onRunScan}
            disabled={isScanning}
            className="flex items-center gap-2 rounded-lg bg-indigo-600 px-3 py-1.5 text-xs font-medium text-white shadow-sm hover:bg-indigo-500 disabled:opacity-50 transition-colors"
          >
            <RefreshCw className={`h-3.5 w-3.5 ${isScanning ? 'animate-spin' : ''}`} />
            <span className="hidden sm:inline">Run Scan</span>
          </button>
        </div>
      </div>

      {/* Tabs */}
      <div className="mx-auto flex max-w-7xl border-t border-slate-800/60 px-4 sm:px-6 lg:px-8">
        <nav className="flex space-x-2 overflow-x-auto py-2">
          <button
            onClick={() => setActiveTab('swing')}
            className={`flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-medium transition-all ${
              activeTab === 'swing'
                ? 'bg-slate-800 text-cyan-400 shadow-sm border border-slate-700'
                : 'text-slate-400 hover:text-slate-200 hover:bg-slate-900/50'
            }`}
          >
            <TrendingUp className="h-3.5 w-3.5" />
            Daily Swing Scanner
          </button>

          <button
            onClick={() => setActiveTab('intraday')}
            className={`flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-medium transition-all ${
              activeTab === 'intraday'
                ? 'bg-slate-800 text-cyan-400 shadow-sm border border-slate-700'
                : 'text-slate-400 hover:text-slate-200 hover:bg-slate-900/50'
            }`}
          >
            <Zap className="h-3.5 w-3.5" />
            5m Intraday Momentum
          </button>

          <button
            onClick={() => setActiveTab('analyzer')}
            className={`flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-medium transition-all ${
              activeTab === 'analyzer'
                ? 'bg-slate-800 text-cyan-400 shadow-sm border border-slate-700'
                : 'text-slate-400 hover:text-slate-200 hover:bg-slate-900/50'
            }`}
          >
            <Activity className="h-3.5 w-3.5" />
            Stock Deep-Dive & AI
          </button>

          <button
            onClick={() => setActiveTab('background')}
            className={`flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-medium transition-all ${
              activeTab === 'background'
                ? 'bg-slate-800 text-cyan-400 shadow-sm border border-slate-700'
                : 'text-slate-400 hover:text-slate-200 hover:bg-slate-900/50'
            }`}
          >
            <ShieldCheck className="h-3.5 w-3.5" />
            Market Hours & Telemetry
          </button>
        </nav>
      </div>
    </header>
  );
};

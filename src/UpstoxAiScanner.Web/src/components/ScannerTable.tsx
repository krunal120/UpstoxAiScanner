import React, { useState, useMemo } from 'react';
import {
  Search,
  Sparkles,
  SlidersHorizontal,
  ChevronRight,
  TrendingUp,
  Volume2,
  AlertTriangle,
} from 'lucide-react';
import { ScoreGauge } from './ScoreGauge';
import type { ScanResultDto } from '../types/api';

interface ScannerTableProps {
  title: string;
  subtitle: string;
  items: ScanResultDto[];
  isLoading: boolean;
  universe?: string;
  onUniverseChange?: (universe: string) => void;
  onSelectStock: (stock: ScanResultDto) => void;
  onExplainAi: (stock: ScanResultDto) => void;
}

export const ScannerTable: React.FC<ScannerTableProps> = ({
  title,
  subtitle,
  items,
  isLoading,
  universe,
  onUniverseChange,
  onSelectStock,
  onExplainAi,
}) => {
  const [search, setSearch] = useState('');
  const [signalFilter, setSignalFilter] = useState('ALL');
  const [minScore, setMinScore] = useState<number>(0);

  const filteredItems = useMemo(() => {
    return items.filter((item) => {
      const matchesSearch =
        item.symbol.toLowerCase().includes(search.toLowerCase()) ||
        item.companyName.toLowerCase().includes(search.toLowerCase());

      const matchesSignal =
        signalFilter === 'ALL' ||
        item.signal.toLowerCase() === signalFilter.toLowerCase();

      const matchesScore = item.technicalScore >= minScore;

      return matchesSearch && matchesSignal && matchesScore;
    });
  }, [items, search, signalFilter, minScore]);

  return (
    <div className="space-y-4">
      {/* Universe Selection Bar */}
      {onUniverseChange && (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-800 bg-slate-900/60 p-2.5">
          <div className="flex items-center gap-2">
            <span className="text-xs font-semibold text-slate-300">Market Universe:</span>
            <div className="flex flex-wrap items-center gap-1.5">
              {[
                { id: 'ALL', label: 'All Stocks (NSE & BSE)' },
                { id: 'NIFTY500', label: 'Nifty 500' },
                { id: 'NSE', label: 'NSE Equities' },
                { id: 'BSE', label: 'BSE Equities' },
              ].map((u) => (
                <button
                  key={u.id}
                  type="button"
                  onClick={() => onUniverseChange(u.id)}
                  className={`rounded-lg px-3 py-1 text-xs font-medium transition-all ${
                    (universe || 'ALL').toUpperCase() === u.id
                      ? 'bg-gradient-to-r from-cyan-600 to-indigo-600 text-white shadow-md shadow-indigo-500/20'
                      : 'bg-slate-800/80 text-slate-400 hover:bg-slate-800 hover:text-slate-200 border border-slate-700/50'
                  }`}
                >
                  {u.label}
                </button>
              ))}
            </div>
          </div>
          <span className="text-[11px] font-mono text-slate-500 hidden sm:inline">
            Cross-Exchange Institutional Coverage
          </span>
        </div>
      )}

      {/* Header and Controls */}
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <h2 className="text-xl font-bold tracking-tight text-white">{title}</h2>
          <p className="text-xs text-slate-400">{subtitle}</p>
        </div>

        {/* Filters */}
        <div className="flex flex-wrap items-center gap-2">
          {/* Search Input */}
          <div className="relative min-w-[200px]">
            <Search className="absolute left-3 top-2.5 h-3.5 w-3.5 text-slate-400" />
            <input
              type="text"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search symbol or name..."
              className="w-full rounded-lg border border-slate-800 bg-slate-900/80 pl-9 pr-3 py-1.5 text-xs text-slate-200 placeholder-slate-500 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
            />
          </div>

          {/* Min Score Range */}
          <div className="flex items-center gap-2 rounded-lg border border-slate-800 bg-slate-900/80 px-3 py-1.5 text-xs text-slate-400">
            <SlidersHorizontal className="h-3.5 w-3.5 text-slate-400" />
            <span>Min Score:</span>
            <input
              type="range"
              min="0"
              max="90"
              step="5"
              value={minScore}
              onChange={(e) => setMinScore(Number(e.target.value))}
              className="w-16 accent-indigo-500 h-1"
            />
            <span className="font-mono text-slate-200 w-5">{minScore}</span>
          </div>
        </div>
      </div>

      {/* Signal Filter Chips */}
      <div className="flex flex-wrap items-center gap-1.5">
        {[
          { label: 'All Setups', value: 'ALL' },
          { label: 'Strong Bullish (≥80)', value: 'Strong Bullish' },
          { label: 'Bullish (60-79)', value: 'Bullish' },
          { label: 'Neutral (40-59)', value: 'Neutral' },
          { label: 'Bearish (20-39)', value: 'Bearish' },
          { label: 'Strong Bearish (<20)', value: 'Strong Bearish' },
        ].map((tab) => (
          <button
            key={tab.value}
            onClick={() => setSignalFilter(tab.value)}
            className={`rounded-full px-2.5 py-1 text-xs font-medium transition-all ${
              signalFilter === tab.value
                ? 'bg-indigo-600 text-white shadow-sm'
                : 'bg-slate-900 text-slate-400 hover:bg-slate-800 hover:text-slate-200 border border-slate-800'
            }`}
          >
            {tab.label}
          </button>
        ))}
        <span className="ml-auto text-xs text-slate-500">
          Showing {filteredItems.length} of {items.length} candidates
        </span>
      </div>

      {/* Results Table */}
      <div className="overflow-hidden rounded-xl border border-slate-800 bg-slate-900/40 backdrop-blur-sm shadow-xl">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead className="border-b border-slate-800 bg-slate-900/90 text-slate-400 uppercase tracking-wider font-semibold">
              <tr>
                <th scope="col" className="px-4 py-3">#</th>
                <th scope="col" className="px-4 py-3">Stock Symbol</th>
                <th scope="col" className="px-4 py-3">Current Price</th>
                <th scope="col" className="px-4 py-3">Technical Score</th>
                <th scope="col" className="px-4 py-3">Signal</th>
                <th scope="col" className="px-4 py-3">Breakout / Setup</th>
                <th scope="col" className="px-4 py-3">Volume Ratio</th>
                <th scope="col" className="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/60 font-medium">
              {isLoading ? (
                <tr>
                  <td colSpan={8} className="px-4 py-12 text-center text-slate-500">
                    <div className="flex flex-col items-center justify-center gap-2">
                      <div className="h-6 w-6 animate-spin rounded-full border-2 border-indigo-500 border-t-transparent" />
                      <span>Scanning Nifty 500 universe...</span>
                    </div>
                  </td>
                </tr>
              ) : filteredItems.length === 0 ? (
                <tr>
                  <td colSpan={8} className="px-4 py-12 text-center text-slate-500">
                    <div className="flex flex-col items-center justify-center gap-2">
                      <AlertTriangle className="h-6 w-6 text-slate-600" />
                      <span>No trading candidates match your active filters.</span>
                    </div>
                  </td>
                </tr>
              ) : (
                filteredItems.map((item, idx) => (
                  <tr
                    key={item.symbol}
                    className="hover:bg-slate-800/40 transition-colors group cursor-pointer"
                    onClick={() => onSelectStock(item)}
                  >
                    {/* Rank */}
                    <td className="px-4 py-3 font-mono text-slate-500">{idx + 1}</td>

                    {/* Stock Symbol & Name */}
                    <td className="px-4 py-3">
                      <div>
                        <div className="flex items-center gap-2">
                          <span className="font-bold text-white group-hover:text-cyan-400 transition-colors">
                            {item.symbol}
                          </span>
                          <span
                            className={`inline-flex items-center rounded px-1.5 py-0.5 text-[10px] font-mono font-medium ${
                              item.instrumentKey.startsWith('BSE')
                                ? 'bg-amber-500/10 text-amber-400 border border-amber-500/30'
                                : 'bg-cyan-500/10 text-cyan-400 border border-cyan-500/30'
                            }`}
                          >
                            {item.instrumentKey.startsWith('BSE') ? 'BSE' : 'NSE'}
                          </span>
                        </div>
                        <div className="text-[11px] text-slate-400 truncate max-w-[180px]">
                          {item.companyName}
                        </div>
                      </div>
                    </td>

                    {/* Current Price */}
                    <td className="px-4 py-3 font-mono text-slate-200">
                      ₹{item.currentPrice.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                    </td>

                    {/* Technical Score */}
                    <td className="px-4 py-3">
                      <ScoreGauge score={item.technicalScore} showBar={true} />
                    </td>

                    {/* Signal */}
                    <td className="px-4 py-3">
                      <span className={`px-2 py-0.5 rounded-full text-[11px] font-semibold border ${
                        item.signal === 'Strong Bullish' ? 'bg-emerald-500/20 text-emerald-400 border-emerald-500/30' :
                        item.signal === 'Bullish' ? 'bg-green-500/20 text-green-400 border-green-500/30' :
                        item.signal === 'Neutral' ? 'bg-amber-500/20 text-amber-400 border-amber-500/30' :
                        item.signal === 'Bearish' ? 'bg-orange-500/20 text-orange-400 border-orange-500/30' :
                        'bg-rose-500/20 text-rose-400 border-rose-500/30'
                      }`}>
                        {item.signal}
                      </span>
                    </td>

                    {/* Breakout Status */}
                    <td className="px-4 py-3">
                      {item.breakoutStatus ? (
                        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded bg-indigo-500/10 text-indigo-300 border border-indigo-500/20 font-mono text-[11px]">
                          <TrendingUp className="h-3 w-3" />
                          {item.breakoutStatus}
                        </span>
                      ) : (
                        <span className="text-slate-600">—</span>
                      )}
                    </td>

                    {/* Volume Ratio */}
                    <td className="px-4 py-3">
                      {item.volumeRatio ? (
                        <div className="flex items-center gap-1 font-mono">
                          <Volume2 className={`h-3.5 w-3.5 ${item.volumeRatio >= 1.5 ? 'text-cyan-400' : 'text-slate-500'}`} />
                          <span className={item.volumeRatio >= 2.0 ? 'text-cyan-300 font-bold' : 'text-slate-300'}>
                            {item.volumeRatio.toFixed(2)}x
                          </span>
                        </div>
                      ) : (
                        <span className="text-slate-600">—</span>
                      )}
                    </td>

                    {/* Actions */}
                    <td className="px-4 py-3 text-right" onClick={(e) => e.stopPropagation()}>
                      <div className="flex items-center justify-end gap-1.5">
                        <button
                          onClick={() => onExplainAi(item)}
                          title="Generate Grounded AI Explanation"
                          className="flex items-center gap-1 rounded-md bg-gradient-to-r from-violet-600 to-indigo-600 px-2.5 py-1 text-[11px] font-medium text-white shadow hover:from-violet-500 hover:to-indigo-500 transition-all"
                        >
                          <Sparkles className="h-3 w-3" />
                          <span>AI Explain</span>
                        </button>
                        <button
                          onClick={() => onSelectStock(item)}
                          title="View Technical Breakdown"
                          className="p-1 rounded-md text-slate-400 hover:text-white hover:bg-slate-800 transition-colors"
                        >
                          <ChevronRight className="h-4 w-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};

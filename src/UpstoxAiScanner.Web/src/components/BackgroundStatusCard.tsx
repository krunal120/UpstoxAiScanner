import React, { useState } from 'react';
import {
  Clock,
  Cpu,
  Power,
  Zap,
  Radio,
  CheckCircle2,
} from 'lucide-react';
import { api } from '../services/api';
import type { MarketSessionStatus, BackgroundScannerStatusDto } from '../types/api';

interface BackgroundStatusCardProps {
  marketStatus: MarketSessionStatus | null;
  backgroundStatus: BackgroundScannerStatusDto | null;
  onRefresh: () => void;
}

export const BackgroundStatusCard: React.FC<BackgroundStatusCardProps> = ({
  marketStatus,
  backgroundStatus,
  onRefresh,
}) => {
  const [isTogglingBg, setIsTogglingBg] = useState(false);
  const [isTogglingOverride, setIsTogglingOverride] = useState(false);

  const handleToggleBackground = async () => {
    if (!backgroundStatus) return;
    setIsTogglingBg(true);
    try {
      await api.toggleBackground(!backgroundStatus.isEnabled);
      onRefresh();
    } catch (err) {
      console.error('Failed to toggle background scanning:', err);
    } finally {
      setIsTogglingBg(false);
    }
  };

  const handleToggleOverride = async () => {
    if (!backgroundStatus) return;
    setIsTogglingOverride(true);
    try {
      await api.toggleIgnoreMarketHours(!backgroundStatus.ignoreMarketHours);
      onRefresh();
    } catch (err) {
      console.error('Failed to toggle market hours override:', err);
    } finally {
      setIsTogglingOverride(false);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-bold tracking-tight text-white">
          Market Hours & Telemetry Control Center
        </h2>
        <p className="text-xs text-slate-400">
          Real-time session tracker for Indian exchanges (NSE/BSE) and background scanner lifecycle controller.
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        {/* Card 1: Indian Market Hours */}
        <div className="rounded-xl border border-slate-800 bg-slate-900/60 p-5 space-y-4">
          <div className="flex items-center justify-between border-b border-slate-800 pb-3">
            <div className="flex items-center gap-2">
              <Clock className="h-5 w-5 text-indigo-400" />
              <h3 className="text-sm font-bold text-white">Indian Market Trading Hours</h3>
            </div>
            <span
              className={`px-2.5 py-0.5 rounded-full text-xs font-semibold border ${
                marketStatus?.isMarketOpen
                  ? 'bg-emerald-500/20 text-emerald-400 border-emerald-500/30'
                  : 'bg-slate-800 text-slate-400 border-slate-700'
              }`}
            >
              {marketStatus?.sessionPhase || marketStatus?.phase || 'Closed'}
            </span>
          </div>

          <div className="space-y-3 text-xs">
            <div className="flex items-center justify-between py-1 border-b border-slate-800/40">
              <span className="text-slate-400">Current IST Time:</span>
              <span className="font-mono font-bold text-slate-200">
                {(marketStatus?.currentTimeIst || marketStatus?.currentIstTime)
                  ? new Date(marketStatus.currentTimeIst || marketStatus.currentIstTime!).toLocaleTimeString('en-IN', { hour12: false })
                  : '--:--:--'}{' '}
                IST (UTC+05:30)
              </span>
            </div>

            <div className="flex items-center justify-between py-1 border-b border-slate-800/40">
              <span className="text-slate-400">Session Status:</span>
              <span className="text-slate-300 font-medium">
                {marketStatus?.message || 'Exchange closed.'}
              </span>
            </div>

            <div className="flex items-center justify-between py-1 border-b border-slate-800/40">
              <span className="text-slate-400">Trading Schedule:</span>
              <span className="font-mono text-slate-300">
                Mon - Fri (09:15 - 15:30 IST)
              </span>
            </div>

            <div className="flex items-center justify-between py-1">
              <span className="text-slate-400">Pre-Open Session:</span>
              <span className="font-mono text-slate-300">09:00 - 09:15 IST</span>
            </div>
          </div>
        </div>

        {/* Card 2: Hosted Background Scanner Controller */}
        <div className="rounded-xl border border-slate-800 bg-slate-900/60 p-5 space-y-4">
          <div className="flex items-center justify-between border-b border-slate-800 pb-3">
            <div className="flex items-center gap-2">
              <Cpu className="h-5 w-5 text-cyan-400" />
              <h3 className="text-sm font-bold text-white">Automated Background Scanner</h3>
            </div>
            <div className="flex items-center gap-1.5 font-mono text-xs">
              <span
                className={`h-2 w-2 rounded-full ${
                  backgroundStatus?.isEnabled ? 'bg-emerald-400 animate-pulse' : 'bg-slate-600'
                }`}
              />
              <span className="text-slate-300">
                {backgroundStatus?.isEnabled ? 'Running' : 'Paused'}
              </span>
            </div>
          </div>

          <div className="space-y-3 text-xs">
            <div className="flex items-center justify-between py-1 border-b border-slate-800/40">
              <span className="text-slate-400">Total Scans Executed:</span>
              <span className="font-mono font-bold text-cyan-300">
                {backgroundStatus?.totalScansExecuted || 0} cycles
              </span>
            </div>

            <div className="flex items-center justify-between py-1 border-b border-slate-800/40">
              <span className="text-slate-400">Scheduled Scan Interval:</span>
              <span className="font-mono text-slate-200">
                Every {backgroundStatus?.scanIntervalMinutes || 5} Minutes
              </span>
            </div>

            <div className="flex items-center justify-between py-1 border-b border-slate-800/40">
              <span className="text-slate-400">Market Hours Constraint:</span>
              <span className="font-mono text-slate-300">
                {backgroundStatus?.ignoreMarketHours
                  ? 'Overridden (24/7 Scanning)'
                  : 'Active (Regular Hours Only)'}
              </span>
            </div>
          </div>

          {/* Controller Actions */}
          <div className="pt-2 flex flex-wrap gap-2">
            <button
              onClick={handleToggleBackground}
              disabled={isTogglingBg}
              className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium transition-colors ${
                backgroundStatus?.isEnabled
                  ? 'bg-rose-500/10 text-rose-400 border border-rose-500/20 hover:bg-rose-500/20'
                  : 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 hover:bg-emerald-500/20'
              }`}
            >
              <Power className="h-3.5 w-3.5" />
              <span>{backgroundStatus?.isEnabled ? 'Pause Scanner' : 'Resume Scanner'}</span>
            </button>

            <button
              onClick={handleToggleOverride}
              disabled={isTogglingOverride}
              className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium transition-colors ${
                backgroundStatus?.ignoreMarketHours
                  ? 'bg-indigo-500 text-white'
                  : 'bg-slate-800 text-slate-300 hover:bg-slate-700'
              }`}
            >
              <Zap className="h-3.5 w-3.5" />
              <span>
                {backgroundStatus?.ignoreMarketHours
                  ? 'Disable 24/7 Override'
                  : 'Enable 24/7 Testing Mode'}
              </span>
            </button>
          </div>
        </div>
      </div>

      {/* WebSocket Active Subscriptions */}
      <div className="rounded-xl border border-slate-800 bg-slate-900/60 p-5 space-y-3">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Radio className="h-4 w-4 text-violet-400" />
            <h3 className="text-sm font-bold text-white">Live WebSocket Subscribed Instruments</h3>
          </div>
          <span className="text-xs font-mono text-slate-400">
            {backgroundStatus?.trackedSubscribedSymbols?.length || 0} Active Channels
          </span>
        </div>

        <p className="text-xs text-slate-400">
          The background scanning engine dynamically streams top scored candidates into the Upstox Market Data WebSocket V3 feed for real-time tick analysis.
        </p>

        <div className="flex flex-wrap gap-2 pt-2">
          {backgroundStatus?.trackedSubscribedSymbols?.map((sym) => (
            <span
              key={sym}
              className="flex items-center gap-1 px-2.5 py-1 rounded-md bg-slate-800 border border-slate-700 font-mono text-xs text-slate-200"
            >
              <CheckCircle2 className="h-3 w-3 text-emerald-400" />
              {sym}
            </span>
          )) || <span className="text-xs text-slate-500">No active subscriptions.</span>}
        </div>
      </div>
    </div>
  );
};

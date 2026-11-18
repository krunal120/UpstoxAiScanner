import React from 'react';

interface ScoreGaugeProps {
  score: number;
  signal?: string;
  size?: 'sm' | 'md' | 'lg';
  showBar?: boolean;
}

export const ScoreGauge: React.FC<ScoreGaugeProps> = ({
  score,
  signal,
  size = 'md',
  showBar = false,
}) => {
  const getTheme = (val: number) => {
    if (val >= 80) {
      return {
        text: 'text-emerald-400',
        bg: 'bg-emerald-500/10',
        border: 'border-emerald-500/30',
        bar: 'bg-emerald-500',
        pill: 'bg-emerald-500/20 text-emerald-300 border-emerald-500/30',
        defaultSignal: 'Strong Bullish',
      };
    }
    if (val >= 60) {
      return {
        text: 'text-green-400',
        bg: 'bg-green-500/10',
        border: 'border-green-500/30',
        bar: 'bg-green-500',
        pill: 'bg-green-500/20 text-green-300 border-green-500/30',
        defaultSignal: 'Bullish',
      };
    }
    if (val >= 40) {
      return {
        text: 'text-amber-400',
        bg: 'bg-amber-500/10',
        border: 'border-amber-500/30',
        bar: 'bg-amber-500',
        pill: 'bg-amber-500/20 text-amber-300 border-amber-500/30',
        defaultSignal: 'Neutral',
      };
    }
    if (val >= 20) {
      return {
        text: 'text-orange-400',
        bg: 'bg-orange-500/10',
        border: 'border-orange-500/30',
        bar: 'bg-orange-500',
        pill: 'bg-orange-500/20 text-orange-300 border-orange-500/30',
        defaultSignal: 'Bearish',
      };
    }
    return {
      text: 'text-rose-400',
      bg: 'bg-rose-500/10',
      border: 'border-rose-500/30',
      bar: 'bg-rose-500',
      pill: 'bg-rose-500/20 text-rose-300 border-rose-500/30',
      defaultSignal: 'Strong Bearish',
    };
  };

  const theme = getTheme(score);
  const displaySignal = signal || theme.defaultSignal;

  if (size === 'sm') {
    return (
      <div className="flex items-center gap-1.5 font-mono">
        <span className={`font-bold ${theme.text}`}>{score.toFixed(0)}</span>
        <span className="text-[10px] text-slate-500">/100</span>
      </div>
    );
  }

  if (size === 'lg') {
    return (
      <div className={`p-4 rounded-xl border ${theme.border} ${theme.bg} flex flex-col items-center justify-center`}>
        <div className="text-xs font-semibold uppercase tracking-wider text-slate-400 mb-1">
          Technical Score
        </div>
        <div className="flex items-baseline gap-1 font-mono">
          <span className={`text-4xl font-extrabold ${theme.text}`}>{score.toFixed(1)}</span>
          <span className="text-sm text-slate-500 font-semibold">/100</span>
        </div>
        <div className={`mt-2 px-2.5 py-0.5 rounded-full text-xs font-medium border ${theme.pill}`}>
          {displaySignal}
        </div>
        {showBar && (
          <div className="w-full bg-slate-800 rounded-full h-2 mt-3 overflow-hidden">
            <div
              className={`h-2 rounded-full transition-all duration-500 ${theme.bar}`}
              style={{ width: `${Math.min(100, Math.max(0, score))}%` }}
            />
          </div>
        )}
      </div>
    );
  }

  // Medium (Default)
  return (
    <div className="flex flex-col gap-1">
      <div className="flex items-center gap-2">
        <div className={`px-2 py-0.5 rounded-md border font-mono font-bold text-xs ${theme.bg} ${theme.border} ${theme.text}`}>
          {score.toFixed(0)}
        </div>
        {signal && (
          <span className={`px-2 py-0.5 rounded-full text-[11px] font-medium border ${theme.pill}`}>
            {signal}
          </span>
        )}
      </div>
      {showBar && (
        <div className="w-24 bg-slate-800 rounded-full h-1.5 overflow-hidden">
          <div
            className={`h-1.5 rounded-full ${theme.bar}`}
            style={{ width: `${Math.min(100, Math.max(0, score))}%` }}
          />
        </div>
      )}
    </div>
  );
};

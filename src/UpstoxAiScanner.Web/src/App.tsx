import React, { useState, useEffect } from 'react';
import { Navbar } from './components/Navbar';
import { ScannerTable } from './components/ScannerTable';
import { StockDetailModal } from './components/StockDetailModal';
import { BackgroundStatusCard } from './components/BackgroundStatusCard';
import { StockAnalyzer } from './components/StockAnalyzer';
import { api } from './services/api';
import type {
  MarketSessionStatus,
  BackgroundScannerStatusDto,
  ScanResultDto,
} from './types/api';

export const App: React.FC = () => {
  const [activeTab, setActiveTab] = useState<'swing' | 'intraday' | 'analyzer' | 'background'>('swing');
  const [marketStatus, setMarketStatus] = useState<MarketSessionStatus | null>(null);
  const [backgroundStatus, setBackgroundStatus] = useState<BackgroundScannerStatusDto | null>(null);
  const [swingItems, setSwingItems] = useState<ScanResultDto[]>([]);
  const [intradayItems, setIntradayItems] = useState<ScanResultDto[]>([]);
  const [selectedStock, setSelectedStock] = useState<ScanResultDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isScanning, setIsScanning] = useState(false);

  // Initial data load
  const loadInitialData = async () => {
    setIsLoading(true);
    try {
      const [mStatus, bgStatus, swings, intradays] = await Promise.all([
        api.getMarketStatus(),
        api.getBackgroundStatus(),
        api.getSwingOpportunities(30),
        api.getIntradayOpportunities(30),
      ]);

      setMarketStatus(mStatus);
      setBackgroundStatus(bgStatus);
      setSwingItems(swings);
      setIntradayItems(intradays);
    } catch (err) {
      console.error('Failed to load initial scanner data:', err);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadInitialData();

    // Heartbeat for market clock & background status every 15 seconds
    const interval = setInterval(async () => {
      try {
        const [mStatus, bgStatus] = await Promise.all([
          api.getMarketStatus(),
          api.getBackgroundStatus(),
        ]);
        setMarketStatus(mStatus);
        setBackgroundStatus(bgStatus);
      } catch {
        // ignore background poll error
      }
    }, 15000);

    return () => clearInterval(interval);
  }, []);

  const handleRunScan = async () => {
    setIsScanning(true);
    try {
      const scanType = activeTab === 'intraday' ? 'Intraday' : 'Swing';
      const results = await api.runUniverseScan(scanType, 30);
      if (scanType === 'Swing') {
        setSwingItems(results);
      } else {
        setIntradayItems(results);
      }

      // Refresh background telemetry
      const bg = await api.getBackgroundStatus();
      setBackgroundStatus(bg);
    } catch (err) {
      console.error('Error running universe scan:', err);
    } finally {
      setIsScanning(false);
    }
  };

  return (
    <div className="min-h-screen bg-[#0b0f19] text-slate-100 flex flex-col font-sans selection:bg-indigo-500 selection:text-white">
      {/* Sticky Header */}
      <Navbar
        marketStatus={marketStatus}
        backgroundStatus={backgroundStatus}
        activeTab={activeTab}
        setActiveTab={setActiveTab}
        onRunScan={handleRunScan}
        isScanning={isScanning}
      />

      {/* Main Body */}
      <main className="flex-1 mx-auto w-full max-w-7xl px-4 py-6 sm:px-6 lg:px-8">
        {activeTab === 'swing' && (
          <ScannerTable
            title="Daily Swing Trading Opportunities"
            subtitle="Ranked 0-100 based on EMA 20/50/200 alignment, 20/50-day resistance breakouts, MACD momentum, and volume surge."
            items={swingItems}
            isLoading={isLoading}
            onSelectStock={(stk) => setSelectedStock(stk)}
            onExplainAi={(stk) => setSelectedStock(stk)}
          />
        )}

        {activeTab === 'intraday' && (
          <ScannerTable
            title="5-Minute Intraday Momentum Scanner"
            subtitle="Ranked 0-100 on 5-minute candles: Fast EMA slope, intraday resistance breaks, MACD crossovers, and volume expansions."
            items={intradayItems}
            isLoading={isLoading}
            onSelectStock={(stk) => setSelectedStock(stk)}
            onExplainAi={(stk) => setSelectedStock(stk)}
          />
        )}

        {activeTab === 'analyzer' && <StockAnalyzer />}

        {activeTab === 'background' && (
          <BackgroundStatusCard
            marketStatus={marketStatus}
            backgroundStatus={backgroundStatus}
            onRefresh={async () => {
              const [m, bg] = await Promise.all([
                api.getMarketStatus(),
                api.getBackgroundStatus(),
              ]);
              setMarketStatus(m);
              setBackgroundStatus(bg);
            }}
          />
        )}
      </main>

      {/* Stock Deep-Dive Modal */}
      {selectedStock && (
        <StockDetailModal
          stock={selectedStock}
          onClose={() => setSelectedStock(null)}
        />
      )}

      {/* Institutional Footer */}
      <footer className="border-t border-slate-800/80 bg-slate-950/60 py-6 mt-12">
        <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8 flex flex-col sm:flex-row items-center justify-between gap-4 text-xs text-slate-500">
          <div>
            <span className="font-semibold text-slate-400">Upstox AI Stock Scanner</span> | Nifty 500 Market Intelligence Platform
          </div>
          <div className="font-mono text-[11px] text-slate-500">
            DISCLAIMER: All scores and signals are quantitative and educational only. Not financial advice.
          </div>
        </div>
      </footer>
    </div>
  );
};

export default App;

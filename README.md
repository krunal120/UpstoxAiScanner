# Upstox AI Stock Scanner

Production-grade AI-powered Indian Stock Market Scanner built using **ASP.NET Core Web API (.NET 8)**, **Entity Framework Core**, **SQL Server**, and **Upstox V3 Market Data APIs**.

The system scans the Nifty 500 universe for intraday and swing trading opportunities using technical indicators, quantitative scoring, and rule-based breakout detection.

---

## 1. Solution Architecture

The solution adheres strictly to **Clean Architecture** principles:

```
UpstoxAiScanner/
│
├── src/
│   ├── UpstoxAiScanner.Domain/            # Pure domain entities (Instrument, HistoricalCandle, TechnicalIndicator, ScanResult)
│   ├── UpstoxAiScanner.Application/       # DTOs, interfaces, common envelopes (ApiResponse, PagedResult), config options
│   ├── UpstoxAiScanner.Infrastructure/    # Upstox V3 API client, EF Core DbContext, Migrations, InstrumentService, Polly retry, RateLimiter
│   └── UpstoxAiScanner.Api/               # ASP.NET Core Web API, Controllers (MarketData, Instruments), Swagger UI, Middleware
│
├── tests/
│   ├── UpstoxAiScanner.UnitTests/         # Unit tests (CSV parsing, JSON deserialization, options logic, mock generator)
│   └── UpstoxAiScanner.IntegrationTests/  # Integration tests (WebApplicationFactory endpoint tests with real DB migrations)
│
├── postman/
│   └── UpstoxAiScanner.postman_collection.json # Ready-to-import Postman Collection
│
├── UpstoxAiScanner.sln                    # Standard Solution file (.NET 8)
├── dotnet-tools.json                      # Local tool manifest (dotnet-ef 8.0.10)
└── README.md
```

---

## 2. Key Features

### Phase 1: Market Data & Upstox V3 API Integration
- **Full Quotes V3**: Fetch prices, OHLC, depth, volume, and circuits for up to 500 stocks in a single request.
- **LTP & OHLC Endpoints**: Lightweight queries for intraday streaming.
- **Historical & Intraday Candles**: Daily candles for swing trading and 5-minute candles for intraday momentum.
- **Resilience & Rate Limiting**: Token-bucket rate limiter (10 req/s) and Polly exponential backoff with retry on transient errors and HTTP 429.
- **Dual-Mode (Live + Mock Fallback)**: Automatically serves deterministic realistic mock data if unauthenticated, and switches to live Upstox data seamlessly once a token is provided.

### Phase 2: Stock Universe & SQL Server Database
- **Entity Framework Core 8**: Code-first entity modeling with Fluent API mapping and precision controls (`decimal(18, 4)` for prices, UTC `DateTimeOffset` for timestamps).
- **Domain Entities**:
  - `Instrument`: Stock symbols, company names, Upstox instrument keys (`NSE_EQ|...`), universe tag (`NIFTY500`), and active flags.
  - `HistoricalCandle`: Persisted daily and intraday OHLCV bars with composite unique index `(InstrumentId, Interval, Timestamp)` to guarantee idempotency.
  - `TechnicalIndicator`: Pre-calculated RSI, MACD, EMA20/50/200, and volume analytics.
  - `ScanResult`: Historical intraday/swing scanner signals with technical scores (0–100).
- **Upstox Instrument Master Synchronization**:
  - Automatically downloads and decompresses the official Upstox NSE master file (`https://assets.upstox.com/market-quote/instruments/exchange/NSE.json.gz`).
  - Matches 500+ Nifty constituents using ISIN (primary) and Symbol (secondary) for 100% precision.
  - Stores constituents in SQL Server with periodic sync support (`POST /api/instruments/sync`).
  - Bundled fallback seed CSV (`ind_nifty500list.csv`) ensures zero failure even in air-gapped or network-restricted environments.
- **Automatic Migration on Startup**: The API automatically applies EF Core migrations (`UpstoxScannerDbContext.Database.MigrateAsync()`) when started.

### Phase 3: Technical Indicator Engine
- **Wilder's RSI (14 periods)**: Smooth calculation using Wilder's method, detecting overbought (>70), oversold (<30), and crossing conditions (above/below 30, 50, 70).
- **MACD (12, 26, 9)**: Fast/Slow EMA difference, Signal line, Histogram, bullish/bearish crossover detection, zero-line transitions, and histogram momentum acceleration.
- **EMA Engine (20, 50, 200)**: Bullish alignment (EMA20 > EMA50 > EMA200), Bearish alignment (EMA20 < EMA50 < EMA200), price crossover events, and Golden/Death cross detection.
- **Breakout & Breakdown Detection**: 20-period and 50-period high breakouts and low breakdowns. Strictly avoids look-ahead bias by calculating historical resistance/support strictly on completed prior candles.
- **Volume Analytics & Surges**: 20-period baseline average volume, volume surge detection (ratio >= 1.5x), and volume confirmation for technical breakouts.
- **Indicator Persistence**: Automatic caching of calculated indicators to the `TechnicalIndicators` table in SQL Server.

### Phase 4: Trading Scanner Strategies & Scoring Engine
- **Technical Scoring Engine (0 to 100)**: Quantitative rule-based system scoring setups across Trend/EMA alignment (+/-30), Momentum/MACD (+/-30), RSI conditions (+/-20), and Breakouts/Volume surges (+/-25).
- **Signal Classifications**:
  - `Strong Bullish`: Score $\ge 80$
  - `Bullish`: Score $60 - 79$
  - `Neutral`: Score $40 - 59$
  - `Bearish`: Score $20 - 39$
  - `Strong Bearish`: Score $< 20$
- **Daily Swing Scanner (`/api/scanner/swing`)**: Scans Nifty 500 stocks for multi-day swing opportunities (EMA 20/50/200 bullish alignment, 20/50-day high breakouts, MACD bullish crossovers, RSI oversold recoveries).
- **Intraday Scanner (`/api/scanner/intraday`)**: Scans 5-minute candles for intraday momentum breakouts, fast EMA trends, volume surges ($\ge 2.0\times$), and MACD crossovers.
- **Persistence & History**: Scanned results and score explanations are automatically persisted into SQL Server `ScanResults` table.

---

## 3. API Endpoints

### A. Trading Scanners & Technical Scoring (`/api/scanner`)

| Method | Endpoint | Description | Query Parameters |
|---|---|---|---|
| `GET` | `/api/scanner/swing` | Ranked daily swing trading opportunities | `universe`, `signal`, `minScore`, `maxScore`, `limit` |
| `GET` | `/api/scanner/intraday` | Ranked 5-minute intraday momentum opportunities | `universe`, `signal`, `minScore`, `maxScore`, `limit` |
| `GET` | `/api/scanner/stock/{symbol}` | Evaluates single stock on-demand with score breakdown | `symbol`, `scanType` (`Swing`/`Intraday`) |
| `POST` | `/api/scanner/run` | Triggers universe scan and persists to SQL Server | `scanType`, `universe`, `limit` |
| `GET` | `/api/scanner/history` | Retrieves historical scan results from database | `scanType`, `universe`, `signal`, `limit` |

### B. Technical Indicators & Analytics (`/api/indicators`)

| Method | Endpoint | Description | Query / Body Parameters |
|---|---|---|---|
| `GET` | `/api/indicators/{symbol}` | Calculates RSI, MACD, EMAs, Breakouts, and Volume | `symbol`, `interval` (`1d` or `5m`) |
| `POST` | `/api/indicators/calculate` | Computes indicators on arbitrary OHLCV candle arrays | JSON request with candle list |

### B. Stock Universe & Instruments (`/api/instruments`)

| Method | Endpoint | Description | Query / Route Parameters |
|---|---|---|---|
| `POST` | `/api/instruments/sync` | Syncs universe with Upstox instrument master | `universe=NIFTY500` |
| `GET` | `/api/instruments` | Paginated and filtered instruments | `universe`, `isActive`, `query`, `page`, `pageSize` |
| `GET` | `/api/instruments/{symbol}` | Get instrument by Symbol or InstrumentKey | `symbol` (e.g. `RELIANCE` or `NSE_EQ\|INE002A01018`) |
| `PATCH` | `/api/instruments/{symbol}/status` | Activate or deactivate an instrument | `symbol`, `isActive` (`true`/`false`) |

### B. Market Data Gateway (`/api/market`)

| Method | Endpoint | Description | Query Parameters |
|---|---|---|---|
| `GET` | `/api/market/status` | Returns integration status, mode, and token availability | None |
| `GET` | `/api/market/quotes` | Full market quotes (OHLC, depth, volume, circuits, 52w range) | `instrumentKeys` (comma-separated) |
| `GET` | `/api/market/ltp` | Last Traded Price, volume, and percentage change | `instrumentKeys` (comma-separated) |
| `GET` | `/api/market/ohlc` | Current & previous session OHLC | `instrumentKeys`, `interval` (`1d`) |
| `GET` | `/api/market/historical` | Historical daily/intraday candles | `instrumentKey`, `unit`, `interval`, `toDate`, `fromDate` |
| `GET` | `/api/market/intraday` | Intraday candle bars for the current trading day | `instrumentKey`, `unit`, `interval` |

---

## 4. Database Setup & Migrations

The project uses **Microsoft SQL Server** (LocalDB for development, or any standard SQL Server instance).

### Connection String Configuration
Configured in `src/UpstoxAiScanner.Api/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=UpstoxAiScannerDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"
}
```

### Running EF Core Migrations Manually
A local tool manifest is configured with `dotnet-ef 8.0.10`:

```bash
# Restore local tools (dotnet-ef)
dotnet tool restore

# Apply migrations to database
dotnet dotnet-ef database update --project src/UpstoxAiScanner.Infrastructure --startup-project src/UpstoxAiScanner.Api

# Add a new migration (if schema changes)
dotnet dotnet-ef migrations add <MigrationName> --project src/UpstoxAiScanner.Infrastructure --startup-project src/UpstoxAiScanner.Api --output-dir Data/Migrations
```

---

## 5. How to Run the Application

### Option 1: Run with Visual Studio
1. Open `UpstoxAiScanner.sln` in Visual Studio 2022.
2. Set `UpstoxAiScanner.Api` as the **Startup Project**.
3. Press **F5** or click **Start Debugging**.
4. Swagger UI will open automatically at:
   `https://localhost:7109/swagger` or `http://localhost:5000/swagger`

### Option 2: Run via .NET CLI
```bash
dotnet build UpstoxAiScanner.sln
dotnet run --project src/UpstoxAiScanner.Api
```

---

## 6. Running Tests

Execute all 30 unit and integration tests:

```bash
dotnet test UpstoxAiScanner.sln
```

All integration tests run against LocalDB with real EF migrations and verify live/mock Upstox responses.

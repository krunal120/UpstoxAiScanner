using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UpstoxAiScanner.Application.Common;
using UpstoxAiScanner.Application.DTOs.Instruments;
using UpstoxAiScanner.Application.Interfaces;
using UpstoxAiScanner.Domain.Entities;
using UpstoxAiScanner.Infrastructure.Data;
using UpstoxAiScanner.Infrastructure.Services.Models;

namespace UpstoxAiScanner.Infrastructure.Services;

public class InstrumentService : IInstrumentService
{
    private readonly UpstoxScannerDbContext _dbContext;
    private readonly HttpClient _httpClient;
    private readonly ILogger<InstrumentService> _logger;

    private const string UpstoxNseMasterUrl = "https://assets.upstox.com/market-quote/instruments/exchange/NSE.json.gz";
    private const string UpstoxBseMasterUrl = "https://assets.upstox.com/market-quote/instruments/exchange/BSE.json.gz";
    private const string NseArchivesNifty500Url = "https://archives.nseindia.com/content/indices/ind_nifty500list.csv";

    // NSE equity series: EQ (normal), BE (trade-to-trade), SM (SME), ST (SME trade-to-trade), BZ (Z group)
    private static readonly HashSet<string> NseEquityTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "EQ", "BE", "SM", "ST", "BZ"
    };

    // Exclude debt / government / mutual fund types from BSE EQ segment: F (debt/bonds), G (Govt securities), E (ETFs/MFs)
    private static readonly HashSet<string> BseExcludedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "F", "G", "E"
    };

    public InstrumentService(
        UpstoxScannerDbContext dbContext,
        HttpClient httpClient,
        ILogger<InstrumentService> logger)
    {
        _dbContext = dbContext;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<InstrumentSyncSummaryDto> SyncUniverseInstrumentsAsync(string universe = "ALL", CancellationToken cancellationToken = default)
    {
        var summary = new InstrumentSyncSummaryDto
        {
            Universe = universe,
            TimestampUtc = DateTimeOffset.UtcNow
        };

        try
        {
            _logger.LogInformation("Starting instrument universe synchronization for {Universe}", universe);

            string normalizedUniverse = (universe ?? "ALL").ToUpperInvariant();
            bool syncNse = normalizedUniverse == "ALL" || normalizedUniverse == "NSE" || normalizedUniverse == "NIFTY500";
            bool syncBse = normalizedUniverse == "ALL" || normalizedUniverse == "BSE";

            // Step 1: Download Nifty 500 constituents if syncing NSE or NIFTY500
            var nifty500Isins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var nifty500Symbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var nifty500BySymbol = new Dictionary<string, Nifty500ConstituentRecord>(StringComparer.OrdinalIgnoreCase);

            if (syncNse)
            {
                try
                {
                    var constituents = await GetNifty500ConstituentsAsync(cancellationToken);
                    summary.TotalConstituentsProcessed = constituents.Count;
                    foreach (var c in constituents)
                    {
                        if (!string.IsNullOrWhiteSpace(c.IsinCode)) nifty500Isins.Add(c.IsinCode);
                        if (!string.IsNullOrWhiteSpace(c.Symbol))
                        {
                            nifty500Symbols.Add(c.Symbol);
                            nifty500BySymbol[c.Symbol] = c;
                        }
                    }
                    _logger.LogInformation("Loaded {Count} Nifty 500 constituents for index tagging", constituents.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not load Nifty 500 constituents, proceeding with Upstox master records only: {Message}", ex.Message);
                }
            }

            // Step 2: Download Upstox Master files for requested exchanges
            var recordsToProcess = new List<(UpstoxMasterRecord Record, string DefaultExchange, string DefaultSegment, string TargetUniverse)>();
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (syncNse)
            {
                _logger.LogInformation("Downloading Upstox NSE instrument master from {Url}...", UpstoxNseMasterUrl);
                var nseRecords = await DownloadUpstoxNseMasterAsync(cancellationToken);
                summary.TotalDownloadedFromMaster += nseRecords.Count;
                _logger.LogInformation("Downloaded and decompressed {Count} NSE equity instruments", nseRecords.Count);

                foreach (var record in nseRecords)
                {
                    if (string.IsNullOrWhiteSpace(record.InstrumentKey) || !seenKeys.Add(record.InstrumentKey))
                        continue;

                    // Tag as NIFTY500 constituent if matching ISIN or trading symbol
                    bool isNifty500 = (!string.IsNullOrWhiteSpace(record.Isin) && nifty500Isins.Contains(record.Isin)) ||
                                      (!string.IsNullOrWhiteSpace(record.TradingSymbol) && nifty500Symbols.Contains(record.TradingSymbol));

                    string targetUniverse = isNifty500 ? "NIFTY500" : "NSE";

                    // If user requested strictly NIFTY500, filter out non-Nifty stocks
                    if (normalizedUniverse == "NIFTY500" && !isNifty500)
                        continue;

                    recordsToProcess.Add((record, "NSE", "NSE_EQ", targetUniverse));
                }
            }

            if (syncBse)
            {
                _logger.LogInformation("Downloading Upstox BSE instrument master from {Url}...", UpstoxBseMasterUrl);
                var bseRecords = await DownloadUpstoxBseMasterAsync(cancellationToken);
                summary.TotalDownloadedFromMaster += bseRecords.Count;
                _logger.LogInformation("Downloaded and decompressed {Count} BSE equity instruments", bseRecords.Count);

                foreach (var record in bseRecords)
                {
                    if (string.IsNullOrWhiteSpace(record.InstrumentKey) || !seenKeys.Add(record.InstrumentKey))
                        continue;

                    recordsToProcess.Add((record, "BSE", "BSE_EQ", "BSE"));
                }
            }

            _logger.LogInformation("Total active equities queued for upsert: {Count}", recordsToProcess.Count);

            // Step 3: Load existing instruments by InstrumentKey
            var existingInstruments = await _dbContext.Instruments
                .ToDictionaryAsync(i => i.InstrumentKey, StringComparer.OrdinalIgnoreCase, cancellationToken);

            var now = DateTimeOffset.UtcNow;
            int batchCount = 0;

            foreach (var (record, defExchange, defSegment, targetUniv) in recordsToProcess)
            {
                string symbol = (record.TradingSymbol ?? string.Empty).Trim().ToUpperInvariant();
                if (symbol.Length > 50) symbol = symbol[..50];

                string companyName = (record.Name ?? record.TradingSymbol ?? symbol).Trim();
                if (nifty500BySymbol.TryGetValue(symbol, out var n500) && !string.IsNullOrWhiteSpace(n500.CompanyName))
                {
                    companyName = n500.CompanyName.Trim();
                }
                if (companyName.Length > 250) companyName = companyName[..250];

                string instrumentKey = record.InstrumentKey!.Trim();
                if (instrumentKey.Length > 100) instrumentKey = instrumentKey[..100];

                string exchange = (record.Exchange ?? defExchange).Trim();
                if (exchange.Length > 20) exchange = exchange[..20];

                string segment = (record.Segment ?? defSegment).Trim();
                if (segment.Length > 20) segment = segment[..20];

                string instrumentType = (record.InstrumentType ?? "EQ").Trim();
                if (instrumentType.Length > 20) instrumentType = instrumentType[..20];

                string? isin = record.Isin?.Trim();
                if (isin != null && isin.Length > 50) isin = isin[..50];

                summary.MatchedCount++;

                if (existingInstruments.TryGetValue(instrumentKey, out var existing))
                {
                    bool modified = false;
                    if (existing.Symbol != symbol) { existing.Symbol = symbol; modified = true; }
                    if (existing.CompanyName != companyName) { existing.CompanyName = companyName; modified = true; }
                    if (existing.Exchange != exchange) { existing.Exchange = exchange; modified = true; }
                    if (existing.Segment != segment) { existing.Segment = segment; modified = true; }
                    if (existing.InstrumentType != instrumentType) { existing.InstrumentType = instrumentType; modified = true; }
                    if (existing.Isin != isin) { existing.Isin = isin; modified = true; }
                    if (existing.Universe != targetUniv) { existing.Universe = targetUniv; modified = true; }

                    if (modified)
                    {
                        existing.UpdatedAt = now;
                        summary.UpdatedCount++;
                    }
                    else
                    {
                        summary.UnchangedCount++;
                    }
                }
                else
                {
                    var newInst = new Instrument
                    {
                        Symbol = symbol,
                        CompanyName = companyName,
                        InstrumentKey = instrumentKey,
                        Exchange = exchange,
                        Segment = segment,
                        InstrumentType = instrumentType,
                        Isin = isin,
                        Universe = targetUniv,
                        IsActive = true,
                        CreatedAt = now,
                        UpdatedAt = now
                    };

                    await _dbContext.Instruments.AddAsync(newInst, cancellationToken);
                    existingInstruments[instrumentKey] = newInst;
                    summary.AddedCount++;
                }

                batchCount++;
                if (batchCount % 1000 == 0)
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Saved batch of 1,000 instruments ({Processed}/{Total})", batchCount, recordsToProcess.Count);
                }
            }

            // Save remaining batch
            await _dbContext.SaveChangesAsync(cancellationToken);

            summary.Message = $"Sync successful for {universe}. Processed: {recordsToProcess.Count}, Added: {summary.AddedCount}, Updated: {summary.UpdatedCount}, Unchanged: {summary.UnchangedCount}.";
            _logger.LogInformation(summary.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing instrument universe {Universe}: {Message}", universe, ex.Message);
            summary.Message = $"Sync failed: {ex.Message}";
            throw;
        }

        return summary;
    }

    public async Task<PagedResult<InstrumentDto>> GetInstrumentsAsync(InstrumentFilterDto filter, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Instruments.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Universe) && !filter.Universe.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            if (filter.Universe.Equals("NSE", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(i => i.Universe == "NSE" || i.Exchange == "NSE");
            }
            else if (filter.Universe.Equals("BSE", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(i => i.Universe == "BSE" || i.Exchange == "BSE");
            }
            else
            {
                query = query.Where(i => i.Universe == filter.Universe);
            }
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(i => i.IsActive == filter.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var q = filter.Query.Trim();
            query = query.Where(i => i.Symbol.Contains(q) || i.CompanyName.Contains(q));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 500);

        var items = await query
            .OrderByDescending(i => i.Universe == "NIFTY500")
            .ThenByDescending(i => i.Exchange == "NSE")
            .ThenBy(i => i.Symbol)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InstrumentDto
            {
                Id = i.Id,
                Symbol = i.Symbol,
                CompanyName = i.CompanyName,
                InstrumentKey = i.InstrumentKey,
                Exchange = i.Exchange,
                Segment = i.Segment,
                InstrumentType = i.InstrumentType,
                Isin = i.Isin,
                Universe = i.Universe,
                IsActive = i.IsActive,
                CreatedAt = i.CreatedAt,
                UpdatedAt = i.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return PagedResult<InstrumentDto>.Create(items, totalCount, page, pageSize);
    }

    public async Task<InstrumentDto?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var normalized = symbol.Trim().ToUpperInvariant();
        var entity = await _dbContext.Instruments.AsNoTracking()
            .Where(i => i.Symbol == normalized)
            .OrderByDescending(i => i.Universe == "NIFTY500")
            .ThenByDescending(i => i.Exchange == "NSE")
            .FirstOrDefaultAsync(cancellationToken);

        return entity == null ? null : MapToDto(entity);
    }

    public async Task<InstrumentDto?> GetByInstrumentKeyAsync(string instrumentKey, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Instruments.AsNoTracking()
            .FirstOrDefaultAsync(i => i.InstrumentKey == instrumentKey.Trim(), cancellationToken);

        return entity == null ? null : MapToDto(entity);
    }

    public async Task<bool> SetActiveStatusAsync(string symbol, bool isActive, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Instruments
            .FirstOrDefaultAsync(i => i.Symbol == symbol.ToUpperInvariant(), cancellationToken);

        if (entity == null)
            return false;

        entity.IsActive = isActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<InstrumentDto>> GetActiveUniverseInstrumentsAsync(string universe = "ALL", CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Instruments.AsNoTracking().Where(i => i.IsActive);

        if (!universe.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            if (universe.Equals("NSE", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(i => i.Universe == "NSE" || i.Exchange == "NSE");
            }
            else if (universe.Equals("BSE", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(i => i.Universe == "BSE" || i.Exchange == "BSE");
            }
            else
            {
                query = query.Where(i => i.Universe == universe);
            }
        }

        return await query
            .OrderByDescending(i => i.Universe == "NIFTY500")
            .ThenByDescending(i => i.Exchange == "NSE")
            .ThenBy(i => i.Symbol)
            .Select(i => MapToDto(i))
            .ToListAsync(cancellationToken);
    }

    private static InstrumentDto MapToDto(Instrument entity)
    {
        return new InstrumentDto
        {
            Id = entity.Id,
            Symbol = entity.Symbol,
            CompanyName = entity.CompanyName,
            InstrumentKey = entity.InstrumentKey,
            Exchange = entity.Exchange,
            Segment = entity.Segment,
            InstrumentType = entity.InstrumentType,
            Isin = entity.Isin,
            Universe = entity.Universe,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private async Task<List<UpstoxMasterRecord>> DownloadUpstoxNseMasterAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UpstoxNseMasterUrl);
        request.Headers.Add("User-Agent", "UpstoxAiScanner/1.0");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var gzipStream = new GZipStream(stream, CompressionMode.Decompress);

        var records = await JsonSerializer.DeserializeAsync<List<UpstoxMasterRecord>>(gzipStream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }, cancellationToken);

        if (records == null)
            return new List<UpstoxMasterRecord>();

        // Filter for active NSE equities across EQ, BE, SM, ST, BZ
        return records
            .Where(r => string.Equals(r.Segment, "NSE_EQ", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(r.InstrumentType) &&
                        NseEquityTypes.Contains(r.InstrumentType) &&
                        !string.IsNullOrWhiteSpace(r.TradingSymbol) &&
                        !string.IsNullOrWhiteSpace(r.InstrumentKey))
            .ToList();
    }

    private async Task<List<UpstoxMasterRecord>> DownloadUpstoxBseMasterAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UpstoxBseMasterUrl);
        request.Headers.Add("User-Agent", "UpstoxAiScanner/1.0");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var gzipStream = new GZipStream(stream, CompressionMode.Decompress);

        var records = await JsonSerializer.DeserializeAsync<List<UpstoxMasterRecord>>(gzipStream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }, cancellationToken);

        if (records == null)
            return new List<UpstoxMasterRecord>();

        // Filter for active BSE equities, excluding debt and bond types
        return records
            .Where(r => string.Equals(r.Segment, "BSE_EQ", StringComparison.OrdinalIgnoreCase) &&
                        (string.IsNullOrWhiteSpace(r.InstrumentType) || !BseExcludedTypes.Contains(r.InstrumentType)) &&
                        !string.IsNullOrWhiteSpace(r.TradingSymbol) &&
                        !string.IsNullOrWhiteSpace(r.InstrumentKey))
            .ToList();
    }

    private async Task<List<Nifty500ConstituentRecord>> GetNifty500ConstituentsAsync(CancellationToken cancellationToken)
    {
        string? csvContent = null;

        // Try downloading live Nifty 500 CSV
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, NseArchivesNifty500Url);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            request.Headers.Add("Accept", "text/csv,application/csv,text/plain");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                csvContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogInformation("Downloaded live Nifty 500 constituents from NSE archives");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Live download of Nifty 500 CSV failed, falling back to embedded seed data: {Message}", ex.Message);
        }

        // Fallback to embedded seed CSV
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            var seedPath = Path.Combine(AppContext.BaseDirectory, "Data", "Seed", "ind_nifty500list.csv");
            if (!File.Exists(seedPath))
            {
                var altPath = Path.Combine(Directory.GetCurrentDirectory(), "src", "UpstoxAiScanner.Infrastructure", "Data", "Seed", "ind_nifty500list.csv");
                if (File.Exists(altPath))
                    seedPath = altPath;
            }

            if (File.Exists(seedPath))
            {
                _logger.LogInformation("Reading Nifty 500 constituents from seed file: {Path}", seedPath);
                csvContent = await File.ReadAllTextAsync(seedPath, cancellationToken);
            }
            else
            {
                throw new FileNotFoundException("Nifty 500 constituent seed file not found at " + seedPath);
            }
        }

        return ParseNifty500Csv(csvContent);
    }

    public static List<Nifty500ConstituentRecord> ParseNifty500Csv(string csvContent)
    {
        var records = new List<Nifty500ConstituentRecord>();
        using var reader = new StringReader(csvContent);

        string? line;
        bool isHeader = true;

        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (isHeader)
            {
                isHeader = false;
                continue;
            }

            // CSV Columns: Company Name,Industry,Symbol,Series,ISIN Code
            var parts = ParseCsvLine(line);
            if (parts.Count >= 5)
            {
                records.Add(new Nifty500ConstituentRecord
                {
                    CompanyName = parts[0].Trim(),
                    Industry = parts[1].Trim(),
                    Symbol = parts[2].Trim().ToUpperInvariant(),
                    Series = parts[3].Trim(),
                    IsinCode = parts[4].Trim().ToUpperInvariant()
                });
            }
        }

        return records;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '\"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString());
        return result;
    }
}

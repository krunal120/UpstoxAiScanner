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
    private const string NseArchivesNifty500Url = "https://archives.nseindia.com/content/indices/ind_nifty500list.csv";

    public InstrumentService(
        UpstoxScannerDbContext dbContext,
        HttpClient httpClient,
        ILogger<InstrumentService> logger)
    {
        _dbContext = dbContext;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<InstrumentSyncSummaryDto> SyncUniverseInstrumentsAsync(string universe = "NIFTY500", CancellationToken cancellationToken = default)
    {
        var summary = new InstrumentSyncSummaryDto
        {
            Universe = universe,
            TimestampUtc = DateTimeOffset.UtcNow
        };

        try
        {
            _logger.LogInformation("Starting instrument universe synchronization for {Universe}", universe);

            // Step 1: Download & decompress Upstox NSE Master
            _logger.LogInformation("Downloading Upstox NSE instrument master from {Url}...", UpstoxNseMasterUrl);
            var masterRecords = await DownloadUpstoxNseMasterAsync(cancellationToken);
            summary.TotalDownloadedFromMaster = masterRecords.Count;
            _logger.LogInformation("Downloaded and decompressed {Count} total equity instruments from Upstox master", masterRecords.Count);

            // Index Upstox master by symbol and ISIN for fast O(1) matching
            var bySymbol = new Dictionary<string, UpstoxMasterRecord>(StringComparer.OrdinalIgnoreCase);
            var byIsin = new Dictionary<string, UpstoxMasterRecord>(StringComparer.OrdinalIgnoreCase);

            foreach (var record in masterRecords)
            {
                if (!string.IsNullOrWhiteSpace(record.TradingSymbol) && !bySymbol.ContainsKey(record.TradingSymbol))
                {
                    bySymbol[record.TradingSymbol] = record;
                }

                if (!string.IsNullOrWhiteSpace(record.Isin) && !byIsin.ContainsKey(record.Isin))
                {
                    byIsin[record.Isin] = record;
                }
            }

            // Step 2: Fetch constituents (Nifty 500)
            var constituents = await GetNifty500ConstituentsAsync(cancellationToken);
            summary.TotalConstituentsProcessed = constituents.Count;
            _logger.LogInformation("Retrieved {Count} constituents for {Universe}", constituents.Count, universe);

            // Step 3: Match constituents to Upstox master
            var existingInstruments = await _dbContext.Instruments
                .Where(i => i.Universe == universe)
                .ToDictionaryAsync(i => i.Symbol, StringComparer.OrdinalIgnoreCase, cancellationToken);

            var unmatchedList = new List<string>();
            var now = DateTimeOffset.UtcNow;

            foreach (var constituent in constituents)
            {
                // Match primarily by ISIN, then by trading symbol
                UpstoxMasterRecord? matched = null;
                if (!string.IsNullOrWhiteSpace(constituent.IsinCode) && byIsin.TryGetValue(constituent.IsinCode, out var byIsinMatch))
                {
                    matched = byIsinMatch;
                }
                else if (bySymbol.TryGetValue(constituent.Symbol, out var bySymbolMatch))
                {
                    matched = bySymbolMatch;
                }

                if (matched == null || string.IsNullOrWhiteSpace(matched.InstrumentKey))
                {
                    unmatchedList.Add(constituent.Symbol);
                    _logger.LogWarning("Constituent {Symbol} ({Company}) could not be matched with Upstox master", constituent.Symbol, constituent.CompanyName);
                    continue;
                }

                summary.MatchedCount++;

                if (existingInstruments.TryGetValue(constituent.Symbol, out var existing))
                {
                    // Update existing
                    bool modified = false;
                    if (existing.InstrumentKey != matched.InstrumentKey) { existing.InstrumentKey = matched.InstrumentKey; modified = true; }
                    if (existing.CompanyName != constituent.CompanyName) { existing.CompanyName = constituent.CompanyName; modified = true; }
                    if (existing.Isin != constituent.IsinCode) { existing.Isin = constituent.IsinCode; modified = true; }
                    if (existing.Exchange != (matched.Exchange ?? "NSE")) { existing.Exchange = matched.Exchange ?? "NSE"; modified = true; }
                    if (existing.Segment != (matched.Segment ?? "NSE_EQ")) { existing.Segment = matched.Segment ?? "NSE_EQ"; modified = true; }
                    if (existing.InstrumentType != (matched.InstrumentType ?? "EQ")) { existing.InstrumentType = matched.InstrumentType ?? "EQ"; modified = true; }

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
                    // Insert new
                    var newInstrument = new Instrument
                    {
                        Symbol = constituent.Symbol,
                        CompanyName = constituent.CompanyName,
                        InstrumentKey = matched.InstrumentKey,
                        Exchange = matched.Exchange ?? "NSE",
                        Segment = matched.Segment ?? "NSE_EQ",
                        InstrumentType = matched.InstrumentType ?? "EQ",
                        Isin = constituent.IsinCode,
                        Universe = universe,
                        IsActive = true,
                        CreatedAt = now,
                        UpdatedAt = now
                    };

                    await _dbContext.Instruments.AddAsync(newInstrument, cancellationToken);
                    summary.AddedCount++;
                }
            }

            summary.UnmatchedSymbols = unmatchedList;

            // Save to database
            await _dbContext.SaveChangesAsync(cancellationToken);

            summary.Message = $"Sync successful for {universe}. Processed: {summary.TotalConstituentsProcessed}, Matched: {summary.MatchedCount}, Added: {summary.AddedCount}, Updated: {summary.UpdatedCount}, Unchanged: {summary.UnchangedCount}, Unmatched: {unmatchedList.Count}.";
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
            query = query.Where(i => i.Universe == filter.Universe);
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
            .OrderBy(i => i.Symbol)
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
        var entity = await _dbContext.Instruments.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Symbol == symbol.ToUpperInvariant(), cancellationToken);

        return entity == null ? null : MapToDto(entity);
    }

    public async Task<InstrumentDto?> GetByInstrumentKeyAsync(string instrumentKey, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Instruments.AsNoTracking()
            .FirstOrDefaultAsync(i => i.InstrumentKey == instrumentKey, cancellationToken);

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

    public async Task<IReadOnlyList<InstrumentDto>> GetActiveUniverseInstrumentsAsync(string universe = "NIFTY500", CancellationToken cancellationToken = default)
    {
        return await _dbContext.Instruments.AsNoTracking()
            .Where(i => i.Universe == universe && i.IsActive)
            .OrderBy(i => i.Symbol)
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

        // We only care about NSE equities (NSE_EQ / EQ)
        return records
            .Where(r => string.Equals(r.Segment, "NSE_EQ", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(r.InstrumentType, "EQ", StringComparison.OrdinalIgnoreCase))
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
                // Try relative to project root
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

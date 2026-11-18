using UpstoxAiScanner.Application.Common;
using UpstoxAiScanner.Application.DTOs.Instruments;

namespace UpstoxAiScanner.Application.Interfaces;

/// <summary>
/// Service contract for managing stock universe instruments, downloading Upstox instrument master,
/// and synchronizing constituent lists.
/// </summary>
public interface IInstrumentService
{
    /// <summary>
    /// Synchronizes the instrument universe (e.g. ALL, NSE, BSE, NIFTY500) with Upstox's instrument master.
    /// Downloads latest Upstox master files, matches symbols, and upserts into database.
    /// </summary>
    Task<InstrumentSyncSummaryDto> SyncUniverseInstrumentsAsync(string universe = "ALL", CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves paginated and filtered instruments from the database.
    /// </summary>
    Task<PagedResult<InstrumentDto>> GetInstrumentsAsync(InstrumentFilterDto filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single instrument by trading symbol.
    /// </summary>
    Task<InstrumentDto?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single instrument by Upstox instrument key (e.g. "NSE_EQ|INE002A01018" or "BSE_EQ|INE002A01018").
    /// </summary>
    Task<InstrumentDto?> GetByInstrumentKeyAsync(string instrumentKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates or deactivates an instrument by symbol.
    /// </summary>
    Task<bool> SetActiveStatusAsync(string symbol, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active instruments for a given universe (e.g. "ALL", "NSE", "BSE", "NIFTY500").
    /// </summary>
    Task<IReadOnlyList<InstrumentDto>> GetActiveUniverseInstrumentsAsync(string universe = "ALL", CancellationToken cancellationToken = default);
}

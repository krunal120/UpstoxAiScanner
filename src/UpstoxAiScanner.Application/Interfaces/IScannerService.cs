using UpstoxAiScanner.Application.DTOs.Scanner;

namespace UpstoxAiScanner.Application.Interfaces;

/// <summary>
/// Service contract for running intraday and swing trading scans across the stock universe.
/// </summary>
public interface IScannerService
{
    /// <summary>
    /// Executes a live scan across active instruments in the specified universe, computes technical indicators,
    /// scores each instrument (0-100), persists results to the database, and returns the top ranked setups.
    /// </summary>
    /// <param name="scanType">"Swing" (daily) or "Intraday" (5-minute).</param>
    /// <param name="universe">Universe name (e.g. "NIFTY500").</param>
    /// <param name="limit">Max number of top stocks to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<ScanResultDto>> RunScanAsync(
        string scanType = "Swing",
        string universe = "NIFTY500",
        int limit = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the most recent scan results from the database matching the provided filters.
    /// </summary>
    Task<IReadOnlyList<ScanResultDto>> GetLatestScanResultsAsync(
        string scanType = "Swing",
        ScannerFilterDto? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs an on-demand scan and scoring analysis for a single stock symbol.
    /// </summary>
    Task<ScanResultDto?> ScanSingleInstrumentAsync(
        string symbol,
        string scanType = "Swing",
        CancellationToken cancellationToken = default);
}

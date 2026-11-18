using UpstoxAiScanner.Application.DTOs.Ai;
using UpstoxAiScanner.Application.DTOs.Scanner;

namespace UpstoxAiScanner.Application.Interfaces;

/// <summary>
/// Service contract for generating grounded AI technical explanations of stock scanning signals.
/// strictly consumes pre-computed quantitative data and never invents prices or signals.
/// </summary>
public interface IAiExplanationService
{
    /// <summary>
    /// Generates structured institutional commentary for a pre-calculated scan result.
    /// </summary>
    Task<AiExplanationDto> ExplainScanResultAsync(
        ScanResultDto scanResult,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans a stock on-demand and generates an AI technical explanation for the result.
    /// </summary>
    Task<AiExplanationDto?> ExplainStockAsync(
        string symbol,
        string scanType = "Swing",
        CancellationToken cancellationToken = default);
}

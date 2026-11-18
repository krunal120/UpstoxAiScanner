using UpstoxAiScanner.Application.DTOs.Scanner;

namespace UpstoxAiScanner.Application.Interfaces;

/// <summary>
/// Control and telemetry contract for the hosted background market scanner service.
/// </summary>
public interface IBackgroundScannerControllerService
{
    /// <summary>
    /// Gets current execution telemetry, scheduling, and top signals from the background scanner.
    /// </summary>
    BackgroundScannerStatusDto GetStatus();

    /// <summary>
    /// Dynamically enables or disables the background scanning loop.
    /// </summary>
    void SetEnabled(bool enabled);

    /// <summary>
    /// Overrides market hours checking (enables continuous scanning for testing and demo purposes).
    /// </summary>
    void SetIgnoreMarketHours(bool ignore);
}

namespace UpstoxAiScanner.Application.DTOs.Instruments;

/// <summary>
/// Result summary of an instrument master and constituent synchronization execution.
/// </summary>
public class InstrumentSyncSummaryDto
{
    public string Universe { get; set; } = "NIFTY500";
    public int TotalDownloadedFromMaster { get; set; }
    public int TotalConstituentsProcessed { get; set; }
    public int MatchedCount { get; set; }
    public int AddedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int UnchangedCount { get; set; }
    public IReadOnlyList<string> UnmatchedSymbols { get; set; } = Array.Empty<string>();
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Message { get; set; } = string.Empty;
}

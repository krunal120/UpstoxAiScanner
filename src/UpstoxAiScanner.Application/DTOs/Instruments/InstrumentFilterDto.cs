namespace UpstoxAiScanner.Application.DTOs.Instruments;

/// <summary>
/// Query parameters for listing and searching instruments.
/// </summary>
public class InstrumentFilterDto
{
    /// <summary>
    /// Filter by stock universe (e.g. NIFTY500, NIFTY50, ALL). Defaults to NIFTY500 if not specified.
    /// </summary>
    public string? Universe { get; set; } = "NIFTY500";

    /// <summary>
    /// Filter by active status (true/false, or null for all).
    /// </summary>
    public bool? IsActive { get; set; }

    /// <summary>
    /// Case-insensitive search query matching symbol or company name.
    /// </summary>
    public string? Query { get; set; }

    /// <summary>
    /// 1-based page index.
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Page size (1 to 500). Defaults to 50.
    /// </summary>
    public int PageSize { get; set; } = 50;
}

namespace UpstoxAiScanner.Application.DTOs.Instruments;

/// <summary>
/// Data transfer object for stock instrument.
/// </summary>
public class InstrumentDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string InstrumentKey { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public string Segment { get; set; } = string.Empty;
    public string InstrumentType { get; set; } = string.Empty;
    public string? Isin { get; set; }
    public string Universe { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

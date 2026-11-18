using FluentAssertions;
using UpstoxAiScanner.Infrastructure.Services;
using UpstoxAiScanner.Infrastructure.Services.Models;
using Xunit;

namespace UpstoxAiScanner.UnitTests.Services;

public class InstrumentServiceTests
{
    [Fact]
    public void ParseNifty500Csv_ValidCsvContent_ShouldParseAllRecordsCorrectly()
    {
        // Arrange
        var csv = @"Company Name,Industry,Symbol,Series,ISIN Code
Reliance Industries Ltd.,Oil Gas & Consumable Fuels,RELIANCE,EQ,INE002A01018
Tata Consultancy Services Ltd.,Information Technology,TCS,EQ,INE467B01029
HDFC Bank Ltd.,Financial Services,HDFCBANK,EQ,INE040A01034
""Larsen & Toubro Ltd."",Construction,LT,EQ,INE018A01030";

        // Act
        var result = InstrumentService.ParseNifty500Csv(csv);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(4);

        result[0].Symbol.Should().Be("RELIANCE");
        result[0].CompanyName.Should().Be("Reliance Industries Ltd.");
        result[0].IsinCode.Should().Be("INE002A01018");

        result[1].Symbol.Should().Be("TCS");
        result[1].CompanyName.Should().Be("Tata Consultancy Services Ltd.");
        result[1].IsinCode.Should().Be("INE467B01029");

        result[3].Symbol.Should().Be("LT");
        result[3].CompanyName.Should().Be("Larsen & Toubro Ltd.");
        result[3].IsinCode.Should().Be("INE018A01030");
    }

    [Fact]
    public void ParseNifty500Csv_WithHeaderOnly_ShouldReturnEmptyList()
    {
        // Arrange
        var csv = "Company Name,Industry,Symbol,Series,ISIN Code\n";

        // Act
        var result = InstrumentService.ParseNifty500Csv(csv);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseNifty500Csv_WithQuotesAndCommasInCompanyName_ShouldParseCleanly()
    {
        // Arrange
        var csv = "Company Name,Industry,Symbol,Series,ISIN Code\n\"ABC, XYZ Ltd.\",Financial Services,ABCXYZ,EQ,INE123456789";

        // Act
        var result = InstrumentService.ParseNifty500Csv(csv);

        // Assert
        result.Should().HaveCount(1);
        result[0].CompanyName.Should().Be("ABC, XYZ Ltd.");
        result[0].Symbol.Should().Be("ABCXYZ");
    }
}

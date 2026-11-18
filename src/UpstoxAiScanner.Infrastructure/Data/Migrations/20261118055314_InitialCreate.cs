using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UpstoxAiScanner.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Instruments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    InstrumentKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Exchange = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "NSE"),
                    Segment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "NSE_EQ"),
                    InstrumentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "EQ"),
                    Isin = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Universe = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "NIFTY500"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Instruments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HistoricalCandles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstrumentId = table.Column<int>(type: "int", nullable: false),
                    Interval = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Open = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    High = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Low = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Close = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Volume = table.Column<long>(type: "bigint", nullable: false),
                    OpenInterest = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalCandles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoricalCandles_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScanResults",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstrumentId = table.Column<int>(type: "int", nullable: false),
                    ScanType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CurrentPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TechnicalScore = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Signal = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BreakoutStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    VolumeStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Explanation = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScanResults_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TechnicalIndicators",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstrumentId = table.Column<int>(type: "int", nullable: false),
                    Interval = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RSI = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    MACD = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    MACDSignal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    MACDHistogram = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    EMA20 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    EMA50 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    EMA200 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    AverageVolume = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    RelativeVolume = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicalIndicators", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnicalIndicators_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalCandles_InstrumentId_Interval",
                table: "HistoricalCandles",
                columns: new[] { "InstrumentId", "Interval" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalCandles_InstrumentId_Interval_Timestamp",
                table: "HistoricalCandles",
                columns: new[] { "InstrumentId", "Interval", "Timestamp" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalCandles_Timestamp",
                table: "HistoricalCandles",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Instruments_InstrumentKey",
                table: "Instruments",
                column: "InstrumentKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Instruments_IsActive",
                table: "Instruments",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Instruments_Symbol",
                table: "Instruments",
                column: "Symbol");

            migrationBuilder.CreateIndex(
                name: "IX_Instruments_Universe",
                table: "Instruments",
                column: "Universe");

            migrationBuilder.CreateIndex(
                name: "IX_Instruments_Universe_IsActive",
                table: "Instruments",
                columns: new[] { "Universe", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ScanResults_InstrumentId",
                table: "ScanResults",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanResults_ScanType_Timestamp",
                table: "ScanResults",
                columns: new[] { "ScanType", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_ScanResults_Signal",
                table: "ScanResults",
                column: "Signal");

            migrationBuilder.CreateIndex(
                name: "IX_ScanResults_TechnicalScore",
                table: "ScanResults",
                column: "TechnicalScore");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalIndicators_InstrumentId_Interval_Timestamp",
                table: "TechnicalIndicators",
                columns: new[] { "InstrumentId", "Interval", "Timestamp" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalIndicators_Timestamp",
                table: "TechnicalIndicators",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoricalCandles");

            migrationBuilder.DropTable(
                name: "ScanResults");

            migrationBuilder.DropTable(
                name: "TechnicalIndicators");

            migrationBuilder.DropTable(
                name: "Instruments");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeIngestion.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedOctober2026MarketData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "FxRates",
                keyColumn: "Id",
                keyValue: 1,
                column: "AsOfDate",
                value: new DateOnly(2026, 10, 1));

            migrationBuilder.UpdateData(
                table: "FxRates",
                keyColumn: "Id",
                keyValue: 2,
                column: "AsOfDate",
                value: new DateOnly(2026, 10, 2));

            migrationBuilder.UpdateData(
                table: "MarketPrices",
                keyColumn: "Id",
                keyValue: 1,
                column: "AsOfDate",
                value: new DateOnly(2026, 10, 1));

            migrationBuilder.UpdateData(
                table: "MarketPrices",
                keyColumn: "Id",
                keyValue: 2,
                column: "AsOfDate",
                value: new DateOnly(2026, 10, 2));

            migrationBuilder.UpdateData(
                table: "MarketPrices",
                keyColumn: "Id",
                keyValue: 3,
                column: "AsOfDate",
                value: new DateOnly(2026, 10, 1));

            migrationBuilder.UpdateData(
                table: "MarketPrices",
                keyColumn: "Id",
                keyValue: 4,
                column: "AsOfDate",
                value: new DateOnly(2026, 10, 2));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "FxRates",
                keyColumn: "Id",
                keyValue: 1,
                column: "AsOfDate",
                value: new DateOnly(2025, 3, 1));

            migrationBuilder.UpdateData(
                table: "FxRates",
                keyColumn: "Id",
                keyValue: 2,
                column: "AsOfDate",
                value: new DateOnly(2025, 3, 2));

            migrationBuilder.UpdateData(
                table: "MarketPrices",
                keyColumn: "Id",
                keyValue: 1,
                column: "AsOfDate",
                value: new DateOnly(2025, 3, 1));

            migrationBuilder.UpdateData(
                table: "MarketPrices",
                keyColumn: "Id",
                keyValue: 2,
                column: "AsOfDate",
                value: new DateOnly(2025, 3, 2));

            migrationBuilder.UpdateData(
                table: "MarketPrices",
                keyColumn: "Id",
                keyValue: 3,
                column: "AsOfDate",
                value: new DateOnly(2025, 3, 1));

            migrationBuilder.UpdateData(
                table: "MarketPrices",
                keyColumn: "Id",
                keyValue: 4,
                column: "AsOfDate",
                value: new DateOnly(2025, 3, 2));
        }
    }
}

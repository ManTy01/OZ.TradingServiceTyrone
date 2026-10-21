using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TradeIngestion.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FxRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BaseCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    QuoteCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FxRates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Instruments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Isin = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Instruments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CashEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalRef = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TradeDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AsOf = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsApplied = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashEvents_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketPrices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstrumentId = table.Column<int>(type: "int", nullable: false),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketPrices_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TradeEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalRef = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    InstrumentId = table.Column<int>(type: "int", nullable: false),
                    Side = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TradeDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AsOf = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsApplied = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradeEvents_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TradeEvents_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Accounts",
                columns: new[] { "Id", "AccountCode", "Name" },
                values: new object[,]
                {
                    { 1, "ACC-001", "Demo Equity Account" },
                    { 2, "ACC-002", "Demo Second Account" }
                });

            migrationBuilder.InsertData(
                table: "FxRates",
                columns: new[] { "Id", "AsOfDate", "BaseCurrency", "QuoteCurrency", "Rate" },
                values: new object[,]
                {
                    { 1, new DateOnly(2025, 3, 1), "USD", "ZAR", 18.72m },
                    { 2, new DateOnly(2025, 3, 2), "USD", "ZAR", 18.72m }
                });

            migrationBuilder.InsertData(
                table: "Instruments",
                columns: new[] { "Id", "Currency", "Isin", "Symbol" },
                values: new object[,]
                {
                    { 1, "USD", "US0378331005", "AAPL" },
                    { 2, "ZAR", "ZAE000015889", "NPN" }
                });

            migrationBuilder.InsertData(
                table: "MarketPrices",
                columns: new[] { "Id", "AsOfDate", "Currency", "InstrumentId", "Price" },
                values: new object[,]
                {
                    { 1, new DateOnly(2025, 3, 1), "USD", 1, 186.00m },
                    { 2, new DateOnly(2025, 3, 2), "USD", 1, 186.00m },
                    { 3, new DateOnly(2025, 3, 1), "ZAR", 2, 3744.00m },
                    { 4, new DateOnly(2025, 3, 2), "ZAR", 2, 3744.00m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_AccountCode",
                table: "Accounts",
                column: "AccountCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashEvents_Account_AsOf_Applied",
                table: "CashEvents",
                columns: new[] { "AccountId", "AsOf" },
                filter: "[IsApplied] = 1")
                .Annotation("SqlServer:Include", new[] { "ExternalRef", "Type", "Amount", "Currency", "TradeDate", "IsApplied", "Decision" });

            migrationBuilder.CreateIndex(
                name: "IX_CashEvents_ExternalRef_AsOf",
                table: "CashEvents",
                columns: new[] { "ExternalRef", "AsOf" });

            migrationBuilder.CreateIndex(
                name: "IX_FxRates_BaseCurrency_QuoteCurrency_AsOfDate",
                table: "FxRates",
                columns: new[] { "BaseCurrency", "QuoteCurrency", "AsOfDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Instruments_Isin",
                table: "Instruments",
                column: "Isin",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketPrices_InstrumentId_AsOfDate",
                table: "MarketPrices",
                columns: new[] { "InstrumentId", "AsOfDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TradeEvents_Account_AsOf_Applied",
                table: "TradeEvents",
                columns: new[] { "AccountId", "AsOf" },
                filter: "[IsApplied] = 1")
                .Annotation("SqlServer:Include", new[] { "ExternalRef", "InstrumentId", "Side", "Quantity", "Price", "Currency", "TradeDate", "IsApplied", "Decision" });

            migrationBuilder.CreateIndex(
                name: "IX_TradeEvents_ExternalRef_AsOf",
                table: "TradeEvents",
                columns: new[] { "ExternalRef", "AsOf" });

            migrationBuilder.CreateIndex(
                name: "IX_TradeEvents_InstrumentId",
                table: "TradeEvents",
                column: "InstrumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashEvents");

            migrationBuilder.DropTable(
                name: "FxRates");

            migrationBuilder.DropTable(
                name: "MarketPrices");

            migrationBuilder.DropTable(
                name: "TradeEvents");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "Instruments");
        }
    }
}

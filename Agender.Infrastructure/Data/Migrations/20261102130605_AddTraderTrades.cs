using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agender.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTraderTrades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TraderKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    KeyHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TraderKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TraderTrades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExternalId = table.Column<long>(type: "INTEGER", nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsBuy = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsClosed = table.Column<bool>(type: "INTEGER", nullable: false),
                    EntryTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EntryPrice = table.Column<double>(type: "REAL", nullable: false),
                    Quantity = table.Column<double>(type: "REAL", nullable: false),
                    StopLoss = table.Column<double>(type: "REAL", nullable: true),
                    TakeProfit = table.Column<double>(type: "REAL", nullable: true),
                    ExitTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExitDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ExitPrice = table.Column<double>(type: "REAL", nullable: true),
                    Fees = table.Column<double>(type: "REAL", nullable: false),
                    Invested = table.Column<double>(type: "REAL", nullable: false),
                    RiskAmount = table.Column<double>(type: "REAL", nullable: true),
                    NetPnl = table.Column<double>(type: "REAL", nullable: true),
                    RMultiple = table.Column<double>(type: "REAL", nullable: true),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Setup = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Tags = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Emotion = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TimeEntryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TraderTrades", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TraderKeys_KeyHash",
                table: "TraderKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TraderKeys_UserId",
                table: "TraderKeys",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TraderTrades_UserId_ExitDate",
                table: "TraderTrades",
                columns: new[] { "UserId", "ExitDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TraderTrades_UserId_ExternalId",
                table: "TraderTrades",
                columns: new[] { "UserId", "ExternalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TraderKeys");

            migrationBuilder.DropTable(
                name: "TraderTrades");
        }
    }
}

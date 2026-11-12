using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agender.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyReminders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Section = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Destination = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    SendTime = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    LastSentOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    LastAttemptAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyReminders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyReminders_HouseholdId_Section",
                table: "DailyReminders",
                columns: new[] { "HouseholdId", "Section" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyReminders");
        }
    }
}

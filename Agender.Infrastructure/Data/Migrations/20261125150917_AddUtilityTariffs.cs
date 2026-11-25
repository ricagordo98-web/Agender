using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agender.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUtilityTariffs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UtilityTariffs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Service = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    EnergyPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    EnergyTax = table.Column<decimal>(type: "TEXT", nullable: false),
                    FixedDaily = table.Column<decimal>(type: "TEXT", nullable: false),
                    ConversionFactor = table.Column<decimal>(type: "TEXT", nullable: false),
                    MonthlyFees = table.Column<decimal>(type: "TEXT", nullable: false),
                    VatEnergy = table.Column<decimal>(type: "TEXT", nullable: false),
                    VatFixed = table.Column<decimal>(type: "TEXT", nullable: false),
                    StartReading = table.Column<decimal>(type: "TEXT", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    PeriodDays = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UtilityTariffs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UtilityTariffs_HouseholdId_Service",
                table: "UtilityTariffs",
                columns: new[] { "HouseholdId", "Service" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UtilityTariffs");
        }
    }
}

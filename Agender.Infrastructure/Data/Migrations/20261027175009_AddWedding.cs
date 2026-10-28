using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agender.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWedding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WeddingCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Planned = table.Column<decimal>(type: "TEXT", nullable: false),
                    Cost = table.Column<decimal>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WeddingDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Venue = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingDetails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WeddingGuests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Group = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    PlusOnes = table.Column<int>(type: "INTEGER", nullable: false),
                    Contact = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Table = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Diet = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingGuests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WeddingTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    AssigneeUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    Done = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WeddingVendors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Contact = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Quote = table.Column<decimal>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CategoryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingVendors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WeddingPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CategoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    PaidBy = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeddingPayments_WeddingCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "WeddingCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeddingCategories_HouseholdId",
                table: "WeddingCategories",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingDetails_HouseholdId",
                table: "WeddingDetails",
                column: "HouseholdId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingGuests_HouseholdId",
                table: "WeddingGuests",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPayments_CategoryId",
                table: "WeddingPayments",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPayments_HouseholdId",
                table: "WeddingPayments",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingTasks_HouseholdId",
                table: "WeddingTasks",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingVendors_HouseholdId",
                table: "WeddingVendors",
                column: "HouseholdId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WeddingDetails");

            migrationBuilder.DropTable(
                name: "WeddingGuests");

            migrationBuilder.DropTable(
                name: "WeddingPayments");

            migrationBuilder.DropTable(
                name: "WeddingTasks");

            migrationBuilder.DropTable(
                name: "WeddingVendors");

            migrationBuilder.DropTable(
                name: "WeddingCategories");
        }
    }
}

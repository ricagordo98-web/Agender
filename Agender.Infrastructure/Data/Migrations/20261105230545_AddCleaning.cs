using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agender.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCleaning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CleaningRooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Emoji = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 9, nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CleaningRooms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CleaningChores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoomId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Every = table.Column<int>(type: "INTEGER", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CleaningChores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CleaningChores_CleaningRooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "CleaningRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CleaningCompletions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChoreId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CleaningCompletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CleaningCompletions_CleaningChores_ChoreId",
                        column: x => x.ChoreId,
                        principalTable: "CleaningChores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CleaningChores_HouseholdId",
                table: "CleaningChores",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_CleaningChores_RoomId",
                table: "CleaningChores",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_CleaningCompletions_ChoreId_Date",
                table: "CleaningCompletions",
                columns: new[] { "ChoreId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_CleaningCompletions_HouseholdId",
                table: "CleaningCompletions",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_CleaningRooms_HouseholdId",
                table: "CleaningRooms",
                column: "HouseholdId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CleaningCompletions");

            migrationBuilder.DropTable(
                name: "CleaningChores");

            migrationBuilder.DropTable(
                name: "CleaningRooms");
        }
    }
}

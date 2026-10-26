using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agender.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseholdInvites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HouseholdInvites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    InvitedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdInvites", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdInvites_Email",
                table: "HouseholdInvites",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdInvites_HouseholdId_Email",
                table: "HouseholdInvites",
                columns: new[] { "HouseholdId", "Email" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HouseholdInvites");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agender.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIncomeSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            const string TradeEntries = "SELECT \"TimeEntryId\" FROM \"TraderTrades\" WHERE \"TimeEntryId\" IS NOT NULL";
            const string TraderAgendas = "SELECT \"Id\" FROM \"Agendas\" WHERE lower(\"Name\") = 'trader' AND \"IsDefault\" = 0";
            const string TraderProjects = "SELECT \"Id\" FROM \"Projects\" WHERE \"AgendaId\" IN (" + TraderAgendas + ") AND \"Name\" IN ('Ganho', 'Perda', 'Aberta')";
            const string DefaultAgenda = "(SELECT d.\"Id\" FROM \"Agendas\" d WHERE d.\"UserId\" = t.\"UserId\" AND d.\"IsDefault\" = 1)";

            migrationBuilder.Sql($"DELETE FROM \"EntryTags\" WHERE \"TimeEntryId\" IN ({TradeEntries});");
            migrationBuilder.Sql($"DELETE FROM \"TimeEntries\" WHERE \"Id\" IN ({TradeEntries});");

            foreach (string table in new[] { "TimeEntries", "Tasks", "ActiveTimers" })
            {
                migrationBuilder.Sql($"UPDATE \"{table}\" SET \"ProjectId\" = NULL WHERE \"ProjectId\" IN ({TraderProjects});");
            }

            migrationBuilder.Sql($"DELETE FROM \"Projects\" WHERE \"Id\" IN ({TraderProjects});");

            foreach (string table in new[] { "Projects", "Tasks", "TimeEntries", "ActiveTimers" })
            {
                migrationBuilder.Sql($"UPDATE \"{table}\" AS t SET \"AgendaId\" = {DefaultAgenda} WHERE t.\"AgendaId\" IN ({TraderAgendas}) AND {DefaultAgenda} IS NOT NULL;");
            }

            migrationBuilder.Sql($"UPDATE \"Agendas\" SET \"IsActive\" = 1, \"IsVisible\" = 1 WHERE \"IsDefault\" = 1 AND \"UserId\" IN (SELECT \"UserId\" FROM \"Agendas\" WHERE \"Id\" IN ({TraderAgendas}) AND \"IsActive\" = 1);");
            migrationBuilder.Sql($"DELETE FROM \"Agendas\" AS t WHERE t.\"Id\" IN ({TraderAgendas}) AND {DefaultAgenda} IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "TimeEntryId",
                table: "TraderTrades");

            migrationBuilder.CreateTable(
                name: "IncomeSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Emoji = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomeSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IncomeEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomeEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncomeEntries_IncomeSources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "IncomeSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IncomeEntries_SourceId",
                table: "IncomeEntries",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeEntries_UserId_SourceId_Date",
                table: "IncomeEntries",
                columns: new[] { "UserId", "SourceId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IncomeEntries");

            migrationBuilder.DropTable(
                name: "IncomeSources");

            migrationBuilder.AddColumn<Guid>(
                name: "TimeEntryId",
                table: "TraderTrades",
                type: "TEXT",
                nullable: true);
        }
    }
}

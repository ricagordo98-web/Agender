using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agender.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgendas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AgendaId",
                table: "TimeEntries",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "AgendaId",
                table: "Tasks",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "AgendaId",
                table: "Projects",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "AgendaId",
                table: "ActiveTimers",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Agendas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CustomColor = table.Column<string>(type: "TEXT", maxLength: 7, nullable: true),
                    IsVisible = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agendas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Agendas_UserId",
                table: "Agendas",
                column: "UserId",
                unique: true,
                filter: "\"IsDefault\" = 1");

            migrationBuilder.Sql(@"
INSERT INTO ""Agendas"" (""Id"", ""Name"", ""Color"", ""CustomColor"", ""IsVisible"", ""IsActive"", ""IsDefault"", ""CreatedAt"", ""UserId"")
SELECT upper(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-4' || substr(hex(randomblob(2)), 2) || '-' || substr('89AB', abs(random()) % 4 + 1, 1) || substr(hex(randomblob(2)), 2) || '-' || hex(randomblob(6))),
       'Geral', 'Indigo', NULL, 1, 1, 1, strftime('%Y-%m-%d %H:%M:%f', 'now'), u.""UserId""
FROM (SELECT ""UserId"" FROM ""Projects"" UNION SELECT ""UserId"" FROM ""Tasks"" UNION SELECT ""UserId"" FROM ""TimeEntries"" UNION SELECT ""UserId"" FROM ""ActiveTimers"") u;");

            foreach (string table in new[] { "Projects", "Tasks", "TimeEntries", "ActiveTimers" })
            {
                migrationBuilder.Sql($@"
UPDATE ""{table}"" SET ""AgendaId"" = (SELECT a.""Id"" FROM ""Agendas"" a WHERE a.""UserId"" = ""{table}"".""UserId"" AND a.""IsDefault"" = 1)
WHERE ""AgendaId"" = '00000000-0000-0000-0000-000000000000';");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Agendas");

            migrationBuilder.DropColumn(
                name: "AgendaId",
                table: "TimeEntries");

            migrationBuilder.DropColumn(
                name: "AgendaId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "AgendaId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "AgendaId",
                table: "ActiveTimers");
        }
    }
}

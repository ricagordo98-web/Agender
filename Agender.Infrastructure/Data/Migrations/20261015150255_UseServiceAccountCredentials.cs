using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agender.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UseServiceAccountCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM \"GoogleConnections\";");
            migrationBuilder.Sql("UPDATE \"TimeEntries\" SET \"SyncToGoogle\" = 0, \"GoogleSyncFailed\" = 0, \"GoogleEventId\" = NULL;");

            migrationBuilder.DropColumn(
                name: "ProtectedRefreshToken",
                table: "GoogleConnections");

            migrationBuilder.AddColumn<string>(
                name: "ProtectedCredentials",
                table: "GoogleConnections",
                type: "TEXT",
                maxLength: 10000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ServiceAccountEmail",
                table: "GoogleConnections",
                type: "TEXT",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WriteCalendarId",
                table: "GoogleConnections",
                type: "TEXT",
                maxLength: 300,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProtectedCredentials",
                table: "GoogleConnections");

            migrationBuilder.DropColumn(
                name: "ServiceAccountEmail",
                table: "GoogleConnections");

            migrationBuilder.DropColumn(
                name: "WriteCalendarId",
                table: "GoogleConnections");

            migrationBuilder.AddColumn<string>(
                name: "ProtectedRefreshToken",
                table: "GoogleConnections",
                type: "TEXT",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");
        }
    }
}

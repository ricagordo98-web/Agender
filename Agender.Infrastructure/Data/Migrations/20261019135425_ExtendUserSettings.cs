using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agender.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExtendUserSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HideDayNotes",
                table: "UserSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HideEntryDetails",
                table: "UserSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HideWeekends",
                table: "UserSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "StartScrollMinute",
                table: "UserSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TimerRoundingMinutes",
                table: "UserSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "WeekStartsOnSunday",
                table: "UserSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HideDayNotes",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "HideEntryDetails",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "HideWeekends",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "StartScrollMinute",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "TimerRoundingMinutes",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "WeekStartsOnSunday",
                table: "UserSettings");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agender.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RedesignFinance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsImported",
                table: "FinanceTransactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTransfer",
                table: "FinanceTransactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsWithdrawal",
                table: "FinanceTransactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonId",
                table: "FinanceTransactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FinancePeople",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    MemberUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancePeople", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavingsAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    PersonId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Bank = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    Value = table.Column<decimal>(type: "TEXT", nullable: false),
                    InitialValue = table.Column<decimal>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavingsAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavingsSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Value = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavingsSnapshots", x => x.Id);
                });

            migrationBuilder.Sql("""
                INSERT INTO "FinancePeople" ("Id", "HouseholdId", "Name", "Color", "Order", "MemberUserId", "CreatedAt")
                SELECT upper(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-4' || substr(hex(randomblob(2)), 2) || '-' || substr('89AB', abs(random()) % 4 + 1, 1) || substr(hex(randomblob(2)), 2) || '-' || hex(randomblob(6))),
                       t."HouseholdId",
                       COALESCE(NULLIF(u."DisplayName", ''), NULLIF(t."MemberName", ''), 'Pessoa'),
                       COALESCE(m."Color", '#D4B483'),
                       0,
                       t."MemberId",
                       datetime('now')
                FROM (SELECT "HouseholdId", "MemberId", MAX("MemberName") AS "MemberName" FROM "FinanceTransactions" WHERE "MemberId" IS NOT NULL GROUP BY "HouseholdId", "MemberId") t
                LEFT JOIN "AspNetUsers" u ON u."Id" = t."MemberId"
                LEFT JOIN "HouseholdMembers" m ON m."UserId" = t."MemberId" AND m."HouseholdId" = t."HouseholdId";
                """);

            migrationBuilder.Sql("""
                UPDATE "FinanceTransactions"
                SET "PersonId" = (SELECT p."Id" FROM "FinancePeople" p WHERE p."HouseholdId" = "FinanceTransactions"."HouseholdId" AND p."MemberUserId" = "FinanceTransactions"."MemberId")
                WHERE "MemberId" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "MemberId",
                table: "FinanceTransactions");

            migrationBuilder.DropColumn(
                name: "MemberName",
                table: "FinanceTransactions");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePeople_HouseholdId",
                table: "FinancePeople",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsAccounts_HouseholdId",
                table: "SavingsAccounts",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsSnapshots_HouseholdId_Year_PersonId",
                table: "SavingsSnapshots",
                columns: new[] { "HouseholdId", "Year", "PersonId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinancePeople");

            migrationBuilder.DropTable(
                name: "SavingsAccounts");

            migrationBuilder.DropTable(
                name: "SavingsSnapshots");

            migrationBuilder.DropColumn(
                name: "IsImported",
                table: "FinanceTransactions");

            migrationBuilder.DropColumn(
                name: "IsTransfer",
                table: "FinanceTransactions");

            migrationBuilder.DropColumn(
                name: "IsWithdrawal",
                table: "FinanceTransactions");

            migrationBuilder.DropColumn(
                name: "PersonId",
                table: "FinanceTransactions");

            migrationBuilder.AddColumn<string>(
                name: "MemberId",
                table: "FinanceTransactions",
                type: "TEXT",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MemberName",
                table: "FinanceTransactions",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }
    }
}

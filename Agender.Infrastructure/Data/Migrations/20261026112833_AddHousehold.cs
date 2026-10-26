using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agender.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHousehold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FamilyEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Start = table.Column<DateTime>(type: "TEXT", nullable: false),
                    End = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AllDay = table.Column<bool>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Recurrence = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    MemberId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    MemberName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinanceTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    MemberId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    MemberName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    GoalId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HomeProjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Priority = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Room = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "TEXT", nullable: true),
                    ActualCost = table.Column<decimal>(type: "TEXT", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Link = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeProjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HouseholdMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdMembers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Households",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    InviteCode = table.Column<string>(type: "TEXT", maxLength: 6, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Households", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavingsGoals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Emoji = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Target = table.Column<decimal>(type: "TEXT", nullable: false),
                    Current = table.Column<decimal>(type: "TEXT", nullable: false),
                    Deadline = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Color = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavingsGoals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShoppingLists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Emoji = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShoppingLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HomeTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Done = table.Column<bool>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomeTasks_HomeProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "HomeProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ListShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ListId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CanEdit = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListShares_ShoppingLists_ListId",
                        column: x => x.ListId,
                        principalTable: "ShoppingLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShoppingItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ListId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    IsChecked = table.Column<bool>(type: "INTEGER", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    AddedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShoppingItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShoppingItems_ShoppingLists_ListId",
                        column: x => x.ListId,
                        principalTable: "ShoppingLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyEvents_HouseholdId",
                table: "FamilyEvents",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceTransactions_HouseholdId_Date",
                table: "FinanceTransactions",
                columns: new[] { "HouseholdId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_HomeProjects_HouseholdId",
                table: "HomeProjects",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeTasks_ProjectId",
                table: "HomeTasks",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdMembers_HouseholdId",
                table: "HouseholdMembers",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdMembers_UserId",
                table: "HouseholdMembers",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Households_InviteCode",
                table: "Households",
                column: "InviteCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListShares_Email",
                table: "ListShares",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_ListShares_ListId_Email",
                table: "ListShares",
                columns: new[] { "ListId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavingsGoals_HouseholdId",
                table: "SavingsGoals",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingItems_ListId",
                table: "ShoppingItems",
                column: "ListId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingLists_HouseholdId",
                table: "ShoppingLists",
                column: "HouseholdId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FamilyEvents");

            migrationBuilder.DropTable(
                name: "FinanceTransactions");

            migrationBuilder.DropTable(
                name: "HomeTasks");

            migrationBuilder.DropTable(
                name: "HouseholdMembers");

            migrationBuilder.DropTable(
                name: "Households");

            migrationBuilder.DropTable(
                name: "ListShares");

            migrationBuilder.DropTable(
                name: "SavingsGoals");

            migrationBuilder.DropTable(
                name: "ShoppingItems");

            migrationBuilder.DropTable(
                name: "HomeProjects");

            migrationBuilder.DropTable(
                name: "ShoppingLists");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agender.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMeals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MealPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    People = table.Column<int>(type: "INTEGER", nullable: false),
                    Pantry = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    WhatsAppEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    WhatsAppApiUrl = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    WhatsAppInstance = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    WhatsAppApiKey = table.Column<string>(type: "TEXT", nullable: true),
                    WhatsAppDestination = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    SendDay = table.Column<int>(type: "INTEGER", nullable: false),
                    SendTime = table.Column<TimeOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MealWeeks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WeekStart = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ShoppingListId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastAttemptAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealWeeks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Recipes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    ImageUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    SourceName = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    SourceUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    Servings = table.Column<int>(type: "INTEGER", nullable: false),
                    Minutes = table.Column<int>(type: "INTEGER", nullable: true),
                    Ingredients = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    Instructions = table.Column<string>(type: "TEXT", maxLength: 20000, nullable: true),
                    IsFavorite = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecipeSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    SitemapUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    UrlFilter = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MealPlanEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Slot = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    RecipeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Servings = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealPlanEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealPlanEntries_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CatalogRecipes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    ImageUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    Minutes = table.Column<int>(type: "INTEGER", nullable: true),
                    Servings = table.Column<int>(type: "INTEGER", nullable: true),
                    Ingredients = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    Instructions = table.Column<string>(type: "TEXT", maxLength: 20000, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 12, nullable: false),
                    DiscoveredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogRecipes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatalogRecipes_RecipeSources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "RecipeSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogRecipes_HouseholdId_Status",
                table: "CatalogRecipes",
                columns: new[] { "HouseholdId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogRecipes_SourceId_Url",
                table: "CatalogRecipes",
                columns: new[] { "SourceId", "Url" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MealPlanEntries_HouseholdId_Date_Slot",
                table: "MealPlanEntries",
                columns: new[] { "HouseholdId", "Date", "Slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MealPlanEntries_RecipeId",
                table: "MealPlanEntries",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_MealPreferences_HouseholdId",
                table: "MealPreferences",
                column: "HouseholdId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MealWeeks_HouseholdId_WeekStart",
                table: "MealWeeks",
                columns: new[] { "HouseholdId", "WeekStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_HouseholdId",
                table: "Recipes",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSources_HouseholdId",
                table: "RecipeSources",
                column: "HouseholdId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogRecipes");

            migrationBuilder.DropTable(
                name: "MealPlanEntries");

            migrationBuilder.DropTable(
                name: "MealPreferences");

            migrationBuilder.DropTable(
                name: "MealWeeks");

            migrationBuilder.DropTable(
                name: "RecipeSources");

            migrationBuilder.DropTable(
                name: "Recipes");
        }
    }
}

using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;

namespace Agender.Web.Api;

public static class CasaEndpoints
{
    private const long FinanceImportLimit = 26 * 1024 * 1024;

    public static void MapCasa(this RouteGroupBuilder group)
    {
        RouteGroupBuilder household = group.MapGroup("/household");

        household.MapGet("/", async (IHouseholdService service, CancellationToken ct) => new HouseholdEnvelope(await service.GetAsync(ct)));

        household.MapPost("/", async (CreateHouseholdRequest request, IHouseholdService service, CancellationToken ct) =>
            await service.CreateAsync(request.Name, ct));

        household.MapPost("/join", async (JoinHouseholdRequest request, IHouseholdService service, CancellationToken ct) =>
            await service.JoinAsync(request.Code, ct));

        household.MapPut("/", async (RenameHouseholdRequest request, IHouseholdService service, CancellationToken ct) =>
            await service.RenameAsync(request.Name, ct));

        household.MapPost("/code", async (IHouseholdService service, CancellationToken ct) => await service.NewInviteCodeAsync(ct));

        household.MapPut("/color", async (MemberColorRequest request, IHouseholdService service, CancellationToken ct) =>
            await service.SetMyColorAsync(request.Color, ct));

        household.MapPost("/invites", async (InviteRequest request, IHouseholdService service, CancellationToken ct) =>
            await service.InviteAsync(request.Email, ct));

        household.MapDelete("/invites/{id:guid}", async (Guid id, IHouseholdService service, CancellationToken ct) =>
            await service.CancelInviteAsync(id, ct));

        household.MapPost("/leave", async (IHouseholdService service, CancellationToken ct) =>
        {
            await service.LeaveAsync(ct);
            return Results.NoContent();
        });

        group.MapGet("/dashboard", async (IHouseholdService service, CancellationToken ct) => await service.GetDashboardAsync(ct));

        MapShopping(group.MapGroup("/shopping"));
        MapHomeProjects(group.MapGroup("/projects"));
        MapFinance(group.MapGroup("/finance"));
        MapFamilyEvents(group.MapGroup("/events"));
        MapWedding(group.MapGroup("/wedding"));
        MapCleaning(group.MapGroup("/cleaning"));
        MapMeals(group.MapGroup("/meals"));
        MapInventory(group.MapGroup("/inventory"));
        MapMealSettings(group.MapGroup("/meal-settings"));
    }

    private static void MapMeals(RouteGroupBuilder group)
    {
        group.MapGet("/week", async (DateOnly start, IMealService service, CancellationToken ct) => await service.GetWeekAsync(start, ct));

        group.MapPost("/entries", async (MealPlanEntry entry, IMealService service, CancellationToken ct) => await service.SaveEntryAsync(entry, ct));

        group.MapDelete("/entries/{id:guid}", async (Guid id, IMealService service, CancellationToken ct) =>
        {
            await service.DeleteEntryAsync(id, ct);
            return Results.NoContent();
        });

        group.MapGet("/recipes", async (IMealService service, CancellationToken ct) => await service.GetRecipesAsync(ct));

        group.MapPost("/recipes", async (Recipe recipe, IMealService service, CancellationToken ct) => await service.SaveRecipeAsync(recipe, ct));

        group.MapDelete("/recipes/{id:guid}", async (Guid id, IMealService service, CancellationToken ct) =>
        {
            await service.DeleteRecipeAsync(id, ct);
            return Results.NoContent();
        });

        group.MapPost("/recipes/import", async (ImportRecipeRequest request, IMealService service, CancellationToken ct) =>
            await service.ImportRecipeAsync(request.Url, ct));

        group.MapGet("/catalog", async (string? q, string? category, int? skip, IMealService service, CancellationToken ct) =>
            await service.SearchCatalogAsync(q, category, skip ?? 0, ct));

        group.MapPost("/catalog/details", async (List<Guid> ids, IMealService service, CancellationToken ct) =>
            await service.LoadCatalogDetailsAsync(ids, ct));

        group.MapGet("/catalog/{id:guid}", async (Guid id, IMealService service, CancellationToken ct) =>
            await service.GetCatalogRecipeAsync(id, ct));

        group.MapPost("/catalog/{id:guid}/adopt", async (Guid id, IMealService service, CancellationToken ct) =>
            await service.AdoptCatalogRecipeAsync(id, ct));

        group.MapPost("/shopping", async (DateOnly start, IMealService service, CancellationToken ct) =>
            await service.CreateShoppingListAsync(start, ct));

        group.MapGet("/share", async (DateOnly start, IMealService service, CancellationToken ct) =>
            new MealShareText(await service.GetShareTextAsync(start, ct)));

        group.MapPost("/send", async (DateOnly start, IMealService service, CancellationToken ct) =>
        {
            await service.SendWhatsAppAsync(start, ct);
            return Results.NoContent();
        });
    }

    private static void MapInventory(RouteGroupBuilder group)
    {
        group.MapGet("/", async (IInventoryService service, CancellationToken ct) => await service.GetAsync(ct));

        group.MapPost("/", async (InventoryItem item, IInventoryService service, CancellationToken ct) => await service.AddAsync(item, ct));

        group.MapPut("/{id:guid}", async (Guid id, InventoryItem item, IInventoryService service, CancellationToken ct) =>
        {
            EnsureSameId(id, item.Id);
            return await service.UpdateAsync(item, ct);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IInventoryService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return Results.NoContent();
        });
    }

    private static void MapMealSettings(RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMealSettingsService service, CancellationToken ct) => await service.GetAsync(ct));

        group.MapPut("/", async (MealSettingsDto settings, IMealSettingsService service, CancellationToken ct) => await service.SaveAsync(settings, ct));

        group.MapPost("/sources", async (RecipeSource source, IMealSettingsService service, CancellationToken ct) => await service.SaveSourceAsync(source, ct));

        group.MapDelete("/sources/{id:guid}", async (Guid id, IMealSettingsService service, CancellationToken ct) =>
        {
            await service.DeleteSourceAsync(id, ct);
            return Results.NoContent();
        });

        group.MapPost("/sources/suggested", async (IMealSettingsService service, CancellationToken ct) => await service.AddSuggestedSourcesAsync(ct));

        group.MapPost("/sources/refresh", async (IMealSettingsService service, CancellationToken ct) =>
        {
            await service.RefreshSourcesAsync(ct);
            return Results.NoContent();
        });

        group.MapPost("/test", async (IMealSettingsService service, CancellationToken ct) =>
        {
            await service.SendTestAsync(ct);
            return Results.NoContent();
        });
    }

    private static void MapShopping(RouteGroupBuilder group)
    {
        group.MapGet("/lists", async (IShoppingService service, CancellationToken ct) => await service.GetListsAsync(ct));

        group.MapGet("/lists/{id:guid}", async (Guid id, IShoppingService service, CancellationToken ct) => await service.GetListAsync(id, ct));

        group.MapPost("/lists", async (ShoppingList list, IShoppingService service, CancellationToken ct) =>
        {
            ShoppingList created = await service.CreateListAsync(list, ct);
            return Results.Created($"/api/casa/shopping/lists/{created.Id}", created);
        });

        group.MapPut("/lists/{id:guid}", async (Guid id, ShoppingList list, IShoppingService service, CancellationToken ct) =>
        {
            EnsureSameId(id, list.Id);
            await service.UpdateListAsync(list, ct);
            return Results.NoContent();
        });

        group.MapDelete("/lists/{id:guid}", async (Guid id, IShoppingService service, CancellationToken ct) =>
        {
            await service.DeleteListAsync(id, ct);
            return Results.NoContent();
        });

        group.MapPost("/lists/{id:guid}/items", async (Guid id, ShoppingItem item, IShoppingService service, CancellationToken ct) =>
            await service.AddItemAsync(id, item, ct));

        group.MapPut("/items/{id:guid}", async (Guid id, ShoppingItem item, IShoppingService service, CancellationToken ct) =>
        {
            EnsureSameId(id, item.Id);
            await service.UpdateItemAsync(item, ct);
            return Results.NoContent();
        });

        group.MapDelete("/items/{id:guid}", async (Guid id, IShoppingService service, CancellationToken ct) =>
        {
            await service.DeleteItemAsync(id, ct);
            return Results.NoContent();
        });

        group.MapPost("/lists/{id:guid}/clear", async (Guid id, IShoppingService service, CancellationToken ct) =>
        {
            await service.ClearCheckedAsync(id, ct);
            return Results.NoContent();
        });

        group.MapPost("/lists/{id:guid}/shares", async (Guid id, ShareListRequest request, IShoppingService service, CancellationToken ct) =>
            await service.ShareAsync(id, request.Email, request.CanEdit, ct));

        group.MapDelete("/lists/{id:guid}/shares/{shareId:guid}", async (Guid id, Guid shareId, IShoppingService service, CancellationToken ct) =>
        {
            await service.UnshareAsync(id, shareId, ct);
            return Results.NoContent();
        });
    }

    private static void MapHomeProjects(RouteGroupBuilder group)
    {
        group.MapGet("/", async (IHomeProjectService service, CancellationToken ct) => await service.GetAllAsync(ct));

        group.MapPost("/", async (HomeProject project, IHomeProjectService service, CancellationToken ct) =>
        {
            HomeProject created = await service.CreateAsync(project, ct);
            return Results.Created($"/api/casa/projects/{created.Id}", created);
        });

        group.MapPut("/{id:guid}", async (Guid id, HomeProject project, IHomeProjectService service, CancellationToken ct) =>
        {
            EnsureSameId(id, project.Id);
            await service.UpdateAsync(project, ct);
            return Results.NoContent();
        });

        group.MapDelete("/{id:guid}", async (Guid id, IHomeProjectService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return Results.NoContent();
        });
    }

    private static void MapCleaning(RouteGroupBuilder group)
    {
        group.MapGet("/", async (ICleaningService service, CancellationToken ct) => await service.GetAsync(ct));

        group.MapGet("/history", async (DateOnly from, DateOnly to, ICleaningService service, CancellationToken ct) => await service.GetHistoryAsync(from, to, ct));

        group.MapPost("/rooms", async (CleaningRoom room, ICleaningService service, CancellationToken ct) => await service.SaveRoomAsync(room, ct));

        group.MapDelete("/rooms/{id:guid}", async (Guid id, ICleaningService service, CancellationToken ct) =>
        {
            await service.DeleteRoomAsync(id, ct);
            return Results.NoContent();
        });

        group.MapPost("/chores", async (CleaningChore chore, ICleaningService service, CancellationToken ct) => await service.SaveChoreAsync(chore, ct));

        group.MapDelete("/chores/{id:guid}", async (Guid id, ICleaningService service, CancellationToken ct) =>
        {
            await service.DeleteChoreAsync(id, ct);
            return Results.NoContent();
        });

        group.MapPost("/chores/{id:guid}/done", async (Guid id, ICleaningService service, CancellationToken ct) =>
        {
            await service.CompleteAsync(id, ct);
            return Results.NoContent();
        });

        group.MapDelete("/chores/{id:guid}/done", async (Guid id, ICleaningService service, CancellationToken ct) =>
        {
            await service.UndoAsync(id, ct);
            return Results.NoContent();
        });
    }

    private static void MapWedding(RouteGroupBuilder group)
    {
        group.MapGet("/", async (IWeddingService service, CancellationToken ct) => await service.GetAsync(ct));

        group.MapPut("/settings", async (WeddingSettings settings, IWeddingService service, CancellationToken ct) => await service.SaveSettingsAsync(settings, ct));

        group.MapPost("/guests", async (WeddingGuest guest, IWeddingService service, CancellationToken ct) => await service.SaveGuestAsync(guest, ct));

        group.MapDelete("/guests/{id:guid}", async (Guid id, IWeddingService service, CancellationToken ct) =>
        {
            await service.DeleteGuestAsync(id, ct);
            return Results.NoContent();
        });

        group.MapPost("/categories", async (WeddingCategory category, IWeddingService service, CancellationToken ct) => await service.SaveCategoryAsync(category, ct));

        group.MapDelete("/categories/{id:guid}", async (Guid id, IWeddingService service, CancellationToken ct) =>
        {
            await service.DeleteCategoryAsync(id, ct);
            return Results.NoContent();
        });

        group.MapPost("/categories/suggested", async (IWeddingService service, CancellationToken ct) => await service.AddSuggestedCategoriesAsync(ct));

        group.MapPost("/payments", async (WeddingPayment payment, IWeddingService service, CancellationToken ct) => await service.SavePaymentAsync(payment, ct));

        group.MapDelete("/payments/{id:guid}", async (Guid id, IWeddingService service, CancellationToken ct) =>
        {
            await service.DeletePaymentAsync(id, ct);
            return Results.NoContent();
        });

        group.MapPost("/vendors", async (WeddingVendor vendor, IWeddingService service, CancellationToken ct) => await service.SaveVendorAsync(vendor, ct));

        group.MapDelete("/vendors/{id:guid}", async (Guid id, IWeddingService service, CancellationToken ct) =>
        {
            await service.DeleteVendorAsync(id, ct);
            return Results.NoContent();
        });

        group.MapPost("/tasks", async (WeddingTask task, IWeddingService service, CancellationToken ct) => await service.SaveTaskAsync(task, ct));

        group.MapDelete("/tasks/{id:guid}", async (Guid id, IWeddingService service, CancellationToken ct) =>
        {
            await service.DeleteTaskAsync(id, ct);
            return Results.NoContent();
        });

        group.MapPost("/tasks/suggested", async (IWeddingService service, CancellationToken ct) => await service.AddSuggestedTasksAsync(ct));
    }

    private static void MapFinance(RouteGroupBuilder group)
    {
        group.MapGet("/summary", async (int year, int month, string? owner, IFinanceService service, CancellationToken ct) =>
            await service.GetSummaryAsync(year, month, owner, ct));

        group.MapGet("/transactions", async (int year, int month, string? owner, IFinanceService service, CancellationToken ct) =>
            await service.GetTransactionsAsync(year, month, owner, ct));

        group.MapGet("/balance", async (IFinanceService service, CancellationToken ct) => await service.GetBalanceAsync(ct));

        group.MapGet("/people", async (IFinanceService service, CancellationToken ct) => await service.GetPeopleAsync(ct));

        group.MapPost("/people", async (FinancePerson person, IFinanceService service, CancellationToken ct) =>
        {
            FinancePerson created = await service.CreatePersonAsync(person, ct);
            return Results.Created($"/api/casa/finance/people/{created.Id}", created);
        });

        group.MapPut("/people/{id:guid}", async (Guid id, FinancePerson person, IFinanceService service, CancellationToken ct) =>
        {
            EnsureSameId(id, person.Id);
            await service.UpdatePersonAsync(person, ct);
            return Results.NoContent();
        });

        group.MapDelete("/people/{id:guid}", async (Guid id, IFinanceService service, CancellationToken ct) =>
        {
            await service.DeletePersonAsync(id, ct);
            return Results.NoContent();
        });

        group.MapGet("/savings/accounts", async (IFinanceService service, CancellationToken ct) => await service.GetSavingsAccountsAsync(ct));

        group.MapPost("/savings/accounts", async (SavingsAccount account, IFinanceService service, CancellationToken ct) =>
        {
            SavingsAccount created = await service.CreateSavingsAccountAsync(account, ct);
            return Results.Created($"/api/casa/finance/savings/accounts/{created.Id}", created);
        });

        group.MapPut("/savings/accounts/{id:guid}", async (Guid id, SavingsAccount account, IFinanceService service, CancellationToken ct) =>
        {
            EnsureSameId(id, account.Id);
            await service.UpdateSavingsAccountAsync(account, ct);
            return Results.NoContent();
        });

        group.MapDelete("/savings/accounts/{id:guid}", async (Guid id, IFinanceService service, CancellationToken ct) =>
        {
            await service.DeleteSavingsAccountAsync(id, ct);
            return Results.NoContent();
        });

        group.MapGet("/savings/snapshots", async (IFinanceService service, CancellationToken ct) => await service.GetSavingsSnapshotsAsync(ct));

        group.MapPut("/savings/snapshots", async (SnapshotRequest request, IFinanceService service, CancellationToken ct) =>
        {
            await service.SetSavingsSnapshotAsync(request.Year, request.PersonId, request.Value, ct);
            return Results.NoContent();
        });

        group.MapDelete("/savings/snapshots/{id:guid}", async (Guid id, IFinanceService service, CancellationToken ct) =>
        {
            await service.DeleteSavingsSnapshotAsync(id, ct);
            return Results.NoContent();
        });

        group.MapPost("/import", async (HttpRequest request, bool? apply, IFinanceImportService importer, CancellationToken ct) =>
        {
            if (request.ContentLength is > FinanceImportLimit)
            {
                throw new ArgumentException("The file is too large.");
            }

            using MemoryStream buffer = new();
            await request.Body.CopyToAsync(buffer, ct);

            return await importer.ImportAsync(buffer.ToArray(), apply ?? false, ct);
        }).DisableAntiforgery();

        group.MapPost("/transactions", async (FinanceTransaction transaction, IFinanceService service, CancellationToken ct) =>
        {
            FinanceTransaction created = await service.CreateTransactionAsync(transaction, ct);
            return Results.Created($"/api/casa/finance/transactions/{created.Id}", created);
        });

        group.MapPut("/transactions/{id:guid}", async (Guid id, FinanceTransaction transaction, IFinanceService service, CancellationToken ct) =>
        {
            EnsureSameId(id, transaction.Id);
            await service.UpdateTransactionAsync(transaction, ct);
            return Results.NoContent();
        });

        group.MapDelete("/transactions/{id:guid}", async (Guid id, IFinanceService service, CancellationToken ct) =>
        {
            await service.DeleteTransactionAsync(id, ct);
            return Results.NoContent();
        });

        group.MapGet("/goals", async (IFinanceService service, CancellationToken ct) => await service.GetGoalsAsync(ct));

        group.MapPost("/goals", async (SavingsGoal goal, IFinanceService service, CancellationToken ct) =>
        {
            SavingsGoal created = await service.CreateGoalAsync(goal, ct);
            return Results.Created($"/api/casa/finance/goals/{created.Id}", created);
        });

        group.MapPut("/goals/{id:guid}", async (Guid id, SavingsGoal goal, IFinanceService service, CancellationToken ct) =>
        {
            EnsureSameId(id, goal.Id);
            await service.UpdateGoalAsync(goal, ct);
            return Results.NoContent();
        });

        group.MapDelete("/goals/{id:guid}", async (Guid id, IFinanceService service, CancellationToken ct) =>
        {
            await service.DeleteGoalAsync(id, ct);
            return Results.NoContent();
        });
    }

    private static void MapFamilyEvents(RouteGroupBuilder group)
    {
        group.MapGet("/", async (DateTime from, DateTime to, IFamilyEventService service, CancellationToken ct) =>
            await service.GetAsync(from, to, ct));

        group.MapPost("/", async (FamilyEvent familyEvent, IFamilyEventService service, CancellationToken ct) =>
        {
            FamilyEvent created = await service.CreateAsync(familyEvent, ct);
            return Results.Created($"/api/casa/events/{created.Id}", created);
        });

        group.MapPut("/{id:guid}", async (Guid id, FamilyEvent familyEvent, IFamilyEventService service, CancellationToken ct) =>
        {
            EnsureSameId(id, familyEvent.Id);
            await service.UpdateAsync(familyEvent, ct);
            return Results.NoContent();
        });

        group.MapDelete("/{id:guid}", async (Guid id, IFamilyEventService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return Results.NoContent();
        });
    }

    private static void EnsureSameId(Guid routeId, Guid bodyId)
    {
        if (routeId != bodyId)
        {
            throw new ArgumentException("The identifier in the route does not match the body.");
        }
    }
}

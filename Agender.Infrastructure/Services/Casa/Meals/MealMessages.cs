using System.Text;
using Agender.Infrastructure.Data;
using Agender.Shared.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

internal static class MealMessages
{
    public const string ProtectorPurpose = "Agender.Meals.WhatsApp";

    public static bool IsReady(MealSettings? settings) =>
        settings is not null
        && !string.IsNullOrWhiteSpace(settings.WhatsAppApiUrl)
        && !string.IsNullOrWhiteSpace(settings.WhatsAppInstance)
        && !string.IsNullOrWhiteSpace(settings.WhatsAppApiKey)
        && !string.IsNullOrWhiteSpace(settings.WhatsAppDestination);

    public static Task SendAsync(WhatsAppSender sender, IDataProtectionProvider dataProtection, MealSettings settings, string text, CancellationToken cancellationToken)
    {
        string apiKey;

        try
        {
            apiKey = dataProtection.CreateProtector(ProtectorPurpose).Unprotect(settings.WhatsAppApiKey!);
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            throw new ArgumentException("A chave da Evolution API já não é válida. Volta a colá-la nas definições.", ex);
        }

        return sender.SendAsync(settings.WhatsAppApiUrl!, settings.WhatsAppInstance!, apiKey, settings.WhatsAppDestination!, text, cancellationToken);
    }

    public static async Task<(List<MealPlanEntry> Entries, Dictionary<Guid, Recipe> Recipes)> LoadWeekAsync(AgenderDbContext context, Guid householdId, DateOnly weekStart, CancellationToken cancellationToken)
    {
        DateOnly end = weekStart.AddDays(6);

        List<MealPlanEntry> entries = await context.MealPlanEntries.AsNoTracking()
            .Where(e => e.HouseholdId == householdId && e.Date >= weekStart && e.Date <= end)
            .OrderBy(e => e.Date).ThenBy(e => e.Slot)
            .ToListAsync(cancellationToken);

        List<Guid> recipeIds = [.. entries.Where(e => e.RecipeId is not null).Select(e => e.RecipeId!.Value).Distinct()];

        Dictionary<Guid, Recipe> recipes = await context.Recipes.AsNoTracking()
            .Where(r => r.HouseholdId == householdId && recipeIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        return (entries, recipes);
    }

    public static async Task<List<MealIngredientLine>> PreviewAsync(AgenderDbContext context, Guid householdId, DateOnly weekStart, CancellationToken cancellationToken)
    {
        (List<MealPlanEntry> entries, Dictionary<Guid, Recipe> recipes) = await LoadWeekAsync(context, householdId, weekStart, cancellationToken);

        string pantry = await context.MealPreferences.AsNoTracking().Where(s => s.HouseholdId == householdId)
            .Select(s => s.Pantry).FirstOrDefaultAsync(cancellationToken) ?? MealCatalog.DefaultPantry;

        List<(string, decimal, string)> items = [];

        foreach (MealPlanEntry entry in entries)
        {
            if (entry.RecipeId is not Guid recipeId || !recipes.TryGetValue(recipeId, out Recipe? recipe))
            {
                continue;
            }

            decimal factor = recipe.Servings > 0 ? (decimal)entry.Servings / recipe.Servings : 1;
            items.AddRange(recipe.IngredientLines.Select(line => (line, factor, recipe.Title)));
        }

        return IngredientParser.Aggregate(items, Recipe.SplitLines(pantry));
    }

    public static async Task<string> BuildAsync(AgenderDbContext context, Guid householdId, DateOnly weekStart, CancellationToken cancellationToken)
    {
        (List<MealPlanEntry> entries, Dictionary<Guid, Recipe> recipes) = await LoadWeekAsync(context, householdId, weekStart, cancellationToken);
        StringBuilder text = new();

        text.Append("*🍽️ Refeições · ").Append(MealCatalog.WeekLabel(weekStart)).Append('*').Append('\n');

        foreach (IGrouping<DateOnly, MealPlanEntry> day in entries.GroupBy(e => e.Date))
        {
            text.Append('\n').Append('*').Append(CasaCatalog.Capitalize(day.Key.ToString("dddd, d", CasaCatalog.Culture))).Append('*').Append('\n');

            foreach (MealPlanEntry entry in day.OrderBy(e => e.Slot))
            {
                string title = entry.RecipeId is Guid id && recipes.TryGetValue(id, out Recipe? recipe) ? recipe.Title : entry.Title ?? string.Empty;
                text.Append(MealCatalog.SlotEmoji(entry.Slot)).Append(' ').Append(MealCatalog.SlotLabel(entry.Slot)).Append(": ").Append(title).Append('\n');
            }
        }

        List<(string Category, string Line)> shopping = await ShoppingLinesAsync(context, householdId, weekStart, cancellationToken);

        if (shopping.Count > 0)
        {
            text.Append('\n').Append("*🛒 Lista de compras*").Append('\n');

            foreach (IGrouping<string, (string Category, string Line)> group in shopping.GroupBy(s => s.Category))
            {
                CasaCatalog.Category category = CasaCatalog.ShoppingCategory(group.Key);
                text.Append('\n').Append(category.Emoji).Append(" _").Append(category.Name).Append('_').Append('\n');

                foreach ((_, string line) in group)
                {
                    text.Append("• ").Append(line).Append('\n');
                }
            }
        }

        return text.ToString().TrimEnd();
    }

    public static string Describe(decimal? quantity, string? unit, string name) => quantity switch
    {
        null => name,
        1 when string.IsNullOrEmpty(unit) => name,
        _ => string.IsNullOrEmpty(unit) ? $"{CasaCatalog.Quantity(quantity.Value)} {name}" : $"{CasaCatalog.Quantity(quantity.Value)} {unit} {name}"
    };

    private static async Task<List<(string Category, string Line)>> ShoppingLinesAsync(AgenderDbContext context, Guid householdId, DateOnly weekStart, CancellationToken cancellationToken)
    {
        Guid? listId = await context.MealWeeks.AsNoTracking().Where(w => w.HouseholdId == householdId && w.WeekStart == weekStart)
            .Select(w => w.ShoppingListId).FirstOrDefaultAsync(cancellationToken);

        List<string> order = [.. CasaCatalog.ShoppingCategories.Select(c => c.Name)];

        if (listId is Guid id && await context.ShoppingLists.AnyAsync(l => l.Id == id, cancellationToken))
        {
            List<ShoppingItem> items = await context.ShoppingItems.AsNoTracking().Where(i => i.ListId == id && !i.IsChecked).ToListAsync(cancellationToken);

            return [.. items.OrderBy(i => order.IndexOf(i.Category)).ThenBy(i => i.Name)
                .Select(i => (i.Category, Describe(i.Quantity, i.Unit, i.Name)))];
        }

        List<MealIngredientLine> preview = await PreviewAsync(context, householdId, weekStart, cancellationToken);

        return [.. preview.Where(l => l.Include).Select(l => (l.Category, Describe(l.Quantity, l.Unit, l.Name)))];
    }
}

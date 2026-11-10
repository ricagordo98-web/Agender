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

        List<InventoryItem> inventory = await context.InventoryItems.AsNoTracking().Where(i => i.HouseholdId == householdId).ToListAsync(cancellationToken);

        return IngredientParser.Aggregate(items, Recipe.SplitLines(pantry), inventory);
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
                Recipe? recipe = entry.RecipeId is Guid id ? recipes.GetValueOrDefault(id) : null;
                text.Append(MealCatalog.SlotEmoji(entry.Slot)).Append(' ').Append(MealCatalog.SlotLabel(entry.Slot)).Append(": ");

                if (entry.IsLeftover)
                {
                    text.Append("🍱 Restos · ").Append(recipe?.Title ?? entry.Title ?? string.Empty).Append('\n');
                    continue;
                }

                text.Append(recipe?.Title ?? entry.Title ?? string.Empty);

                if (!string.IsNullOrEmpty(recipe?.SourceName))
                {
                    text.Append(" · ").Append(recipe.SourceName);
                }

                text.Append('\n');

                if (!string.IsNullOrEmpty(recipe?.SourceUrl))
                {
                    text.Append("   ").Append(recipe.SourceUrl).Append('\n');
                }
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

    public static async Task<List<string>> BuildRecipesAsync(AgenderDbContext context, Guid householdId, DateOnly weekStart, CancellationToken cancellationToken)
    {
        (List<MealPlanEntry> entries, Dictionary<Guid, Recipe> recipes) = await LoadWeekAsync(context, householdId, weekStart, cancellationToken);
        List<string> messages = [];

        foreach (IGrouping<Guid, MealPlanEntry> group in entries.Where(e => e.RecipeId is Guid id && recipes.ContainsKey(id)).GroupBy(e => e.RecipeId!.Value))
        {
            Recipe recipe = recipes[group.Key];
            StringBuilder text = new();

            text.Append("*📖 ").Append(recipe.Title).Append('*').Append('\n');
            text.Append("🗓️ ").Append(string.Join(", ", group.OrderBy(e => e.Date).ThenBy(e => e.Slot)
                .Select(e => $"{CasaCatalog.Capitalize(e.Date.ToString("dddd", CasaCatalog.Culture).Split('-')[0])} ({MealCatalog.SlotLabel(e.Slot).ToLowerInvariant()}{(e.IsLeftover ? ", restos" : string.Empty)})"))).Append('\n');

            if (group.Any(e => e.IsLeftover))
            {
                text.Append("🍱 Fazer a dobrar: os restos dão para a refeição seguinte").Append('\n');
            }

            if (!string.IsNullOrEmpty(recipe.SourceName))
            {
                text.Append("🏪 Receita ").Append(recipe.SourceName).Append(" — atenção a ingredientes da marca da loja").Append('\n');
            }

            if (!string.IsNullOrEmpty(recipe.SourceUrl))
            {
                text.Append("🔗 ").Append(recipe.SourceUrl).Append('\n');
            }

            int people = group.Where(e => !e.IsLeftover).Select(e => e.Servings + LeftoverServings(group, e)).DefaultIfEmpty(group.Max(e => e.Servings)).Max();
            text.Append("👥 Receita para ").Append(recipe.Servings).Append(recipe.Servings == 1 ? " pessoa" : " pessoas");

            if (people != recipe.Servings)
            {
                text.Append(" (vão cozinhar para ").Append(people).Append(')');
            }

            text.Append('\n');

            List<string> ingredients = [.. recipe.IngredientLines];

            if (ingredients.Count > 0)
            {
                text.Append('\n').Append("*Ingredientes*").Append('\n');
                ingredients.ForEach(i => text.Append("• ").Append(i).Append('\n'));
            }

            List<string> steps = [.. Recipe.SplitLines(recipe.Instructions)];

            if (steps.Count > 0)
            {
                text.Append('\n').Append("*Preparação*").Append('\n');

                for (int index = 0; index < steps.Count; index++)
                {
                    text.Append(index + 1).Append(". ").Append(steps[index]).Append('\n');
                }
            }

            messages.Add(text.ToString().TrimEnd());
        }

        return messages;
    }

    public static async Task<string?> SendWeekAsync(WhatsAppSender sender, IDataProtectionProvider dataProtection, MealSettings settings, AgenderDbContext context, Guid householdId, DateOnly weekStart, CancellationToken cancellationToken)
    {
        string main = await BuildAsync(context, householdId, weekStart, cancellationToken);
        List<string> recipes = await BuildRecipesAsync(context, householdId, weekStart, cancellationToken);

        await SendAsync(sender, dataProtection, settings, main, cancellationToken);

        try
        {
            foreach (string recipe in recipes)
            {
                await SendAsync(sender, dataProtection, settings, recipe, cancellationToken);
            }
        }
        catch (ArgumentException ex)
        {
            return $"A ementa foi enviada, mas falhou o envio de uma receita: {ex.Message}";
        }

        return null;
    }

    private static int LeftoverServings(IEnumerable<MealPlanEntry> group, MealPlanEntry cooked) =>
        MealCatalog.NextMeal(cooked.Date, cooked.Slot) is var (date, slot)
            ? group.Where(e => e.IsLeftover && e.Date == date && e.Slot == slot).Sum(e => e.Servings)
            : 0;

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

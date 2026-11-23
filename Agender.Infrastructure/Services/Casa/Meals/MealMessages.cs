using System.Text;
using Agender.Infrastructure.Data;
using Agender.Shared.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

internal static class MealMessages
{
    public const string ProtectorPurpose = "Agender.Meals.WhatsApp";

    public static bool IsConnected(MealSettings? settings) =>
        settings is not null
        && !string.IsNullOrWhiteSpace(settings.WhatsAppApiUrl)
        && !string.IsNullOrWhiteSpace(settings.WhatsAppInstance)
        && !string.IsNullOrWhiteSpace(settings.WhatsAppApiKey);

    public static bool IsReady(MealSettings? settings) =>
        IsConnected(settings) && !string.IsNullOrWhiteSpace(settings!.WhatsAppDestination);

    public static Task SendAsync(WhatsAppSender sender, IDataProtectionProvider dataProtection, MealSettings settings, string text, CancellationToken cancellationToken) =>
        SendToAsync(sender, dataProtection, settings, settings.WhatsAppDestination!, text, cancellationToken);

    public static Task SendToAsync(WhatsAppSender sender, IDataProtectionProvider dataProtection, MealSettings settings, string destination, string text, CancellationToken cancellationToken) =>
        sender.SendAsync(settings.WhatsAppApiUrl!, settings.WhatsAppInstance!, ApiKey(dataProtection, settings), destination, text, cancellationToken);

    public static string ApiKey(IDataProtectionProvider dataProtection, MealSettings settings)
    {
        try
        {
            return dataProtection.CreateProtector(ProtectorPurpose).Unprotect(settings.WhatsAppApiKey!);
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            throw new ArgumentException("A chave da Evolution API já não é válida. Volta a colá-la nas definições.", ex);
        }
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
        List<MealIngredientLine> lines = [.. (await PreviewAsync(context, householdId, weekStart, cancellationToken)).Where(l => l.Include)];
        StringBuilder text = new();

        text.Append("*🛒 Lista de compras · ").Append(MealCatalog.WeekLabel(weekStart)).Append('*').Append('\n');

        if (lines.Count == 0)
        {
            text.Append('\n').Append("_Não há ingredientes para comprar nesta semana._");
            return text.ToString();
        }

        AppendByCategory(text, lines.Select(l => (l.Category, l.Name, Describe(l.Quantity, l.Unit, l.Name))));

        return text.ToString().TrimEnd();
    }

    public static string BuildShoppingList(ShoppingList list, IEnumerable<ShoppingItem> items)
    {
        StringBuilder text = new();
        text.Append('*').Append(list.Emoji).Append(' ').Append(list.Name).Append('*').Append('\n');
        AppendByCategory(text, items.Select(i => (i.Category, i.Name, Describe(i.Quantity, i.Unit, i.Name))));

        return text.ToString().TrimEnd();
    }

    private static void AppendByCategory(StringBuilder text, IEnumerable<(string Category, string Name, string Line)> lines)
    {
        List<string> order = [.. CasaCatalog.ShoppingCategories.Select(c => c.Name)];
        StringComparer byName = StringComparer.Create(CasaCatalog.Culture, true);

        foreach (IGrouping<string, (string Category, string Name, string Line)> group in lines.OrderBy(l => order.IndexOf(l.Category)).ThenBy(l => l.Name, byName).GroupBy(l => l.Category))
        {
            CasaCatalog.Category category = CasaCatalog.ShoppingCategory(group.Key);
            text.Append('\n').Append(category.Emoji).Append(" *").Append(category.Name).Append('*').Append('\n');

            foreach ((_, _, string line) in group)
            {
                text.Append("• ").Append(line).Append('\n');
            }
        }
    }

    public static async Task SendWeekAsync(WhatsAppSender sender, IDataProtectionProvider dataProtection, MealSettings settings, AgenderDbContext context, Guid householdId, DateOnly weekStart, CancellationToken cancellationToken) =>
        await SendAsync(sender, dataProtection, settings, await BuildAsync(context, householdId, weekStart, cancellationToken), cancellationToken);

    private static readonly Dictionary<string, string> PluralUnits = new()
    {
        ["dente"] = "dentes", ["lata"] = "latas", ["pacote"] = "pacotes", ["frasco"] = "frascos", ["fatia"] = "fatias", ["folha"] = "folhas",
        ["ramo"] = "ramos", ["pitada"] = "pitadas", ["colher"] = "colheres", ["chávena"] = "chávenas", ["copo"] = "copos"
    };

    public static string Describe(decimal? quantity, string? unit, string name)
    {
        if (quantity is not decimal amount || name.Length == 0 || (amount == 1 && string.IsNullOrEmpty(unit)))
        {
            return name;
        }

        string item = char.ToLower(name[0], CasaCatalog.Culture) + name[1..];

        if (string.IsNullOrEmpty(unit))
        {
            return $"{CasaCatalog.Quantity(amount)} {item}";
        }

        string label = amount > 1 && PluralUnits.TryGetValue(unit, out string? plural) ? plural : unit;

        return $"{CasaCatalog.Quantity(amount)} {label} de {item}";
    }
}

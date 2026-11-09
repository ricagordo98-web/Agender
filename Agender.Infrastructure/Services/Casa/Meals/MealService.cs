using Agender.Infrastructure.Data;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

public class MealService(
    IUserDbContextFactory contextFactory,
    RecipeScraper scraper,
    WhatsAppSender whatsApp,
    IDataProtectionProvider dataProtection,
    TimeProvider time) : IMealService
{
    private const int CatalogPageSize = 24;
    private const int MaxCatalogCategories = 30;
    private const int MaxShoppingLines = 300;

    public async Task<MealWeekOverview> GetWeekAsync(DateOnly weekStart, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);
        weekStart = MealCatalog.WeekStartOf(weekStart);

        (List<MealPlanEntry> entries, Dictionary<Guid, Recipe> recipes) = await MealMessages.LoadWeekAsync(context, householdId, weekStart, cancellationToken);
        MealSettings? settings = await context.MealPreferences.AsNoTracking().FirstOrDefaultAsync(s => s.HouseholdId == householdId, cancellationToken);
        MealWeek? week = await context.MealWeeks.AsNoTracking().FirstOrDefaultAsync(w => w.HouseholdId == householdId && w.WeekStart == weekStart, cancellationToken);

        Guid? listId = week?.ShoppingListId is Guid id && await context.ShoppingLists.AnyAsync(l => l.Id == id, cancellationToken) ? id : null;

        return new MealWeekOverview
        {
            WeekStart = weekStart,
            Today = Today(),
            People = settings?.People ?? 2,
            Entries = entries,
            Recipes = [.. recipes.Values],
            ShoppingListId = listId,
            SentAt = week?.SentAt,
            SendError = week?.SentAt is null ? week?.LastError : null,
            WhatsAppReady = settings?.WhatsAppEnabled == true && MealMessages.IsReady(settings)
        };
    }

    public async Task<MealPlanEntry> SaveEntryAsync(MealPlanEntry entry, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        if (!Enum.IsDefined(entry.Slot) || entry.Servings < 1 || entry.Servings > Recipe.MaxServings)
        {
            throw new ArgumentException("The meal is invalid.");
        }

        string? title = null;

        if (entry.RecipeId is Guid recipeId)
        {
            if (!await context.Recipes.AnyAsync(r => r.Id == recipeId && r.HouseholdId == householdId, cancellationToken))
            {
                throw new NotFoundException($"Recipe {recipeId} was not found.");
            }
        }
        else
        {
            title = HouseholdAccess.RequiredText(entry.Title, "The title", MealPlanEntry.TitleMaxLength);
        }

        MealPlanEntry? target = await context.MealPlanEntries.FirstOrDefaultAsync(e => e.HouseholdId == householdId && e.Date == entry.Date && e.Slot == entry.Slot, cancellationToken);

        if (target is null)
        {
            target = new MealPlanEntry { HouseholdId = householdId, Date = entry.Date, Slot = entry.Slot };
            context.MealPlanEntries.Add(target);
        }

        target.RecipeId = entry.RecipeId;
        target.Title = title;
        target.Servings = entry.Servings;
        await context.SaveChangesAsync(cancellationToken);

        return target;
    }

    public async Task DeleteEntryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        if (await context.MealPlanEntries.Where(e => e.Id == id && e.HouseholdId == householdId).ExecuteDeleteAsync(cancellationToken) == 0)
        {
            throw new NotFoundException($"Meal {id} was not found.");
        }

        context.MarkWritten();
    }

    public async Task<List<Recipe>> GetRecipesAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        return [.. (await context.Recipes.AsNoTracking().Where(r => r.HouseholdId == householdId).ToListAsync(cancellationToken))
            .OrderByDescending(r => r.IsFavorite).ThenBy(r => r.Title, StringComparer.Create(CasaCatalog.Culture, true))];
    }

    public async Task<Recipe> SaveRecipeAsync(Recipe recipe, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        if (recipe.Servings < 1 || recipe.Servings > Recipe.MaxServings || recipe.Minutes is < 0 or > Recipe.MaxMinutes)
        {
            throw new ArgumentException("The servings or the time are out of range.");
        }

        Recipe? target = await context.Recipes.FirstOrDefaultAsync(r => r.Id == recipe.Id && r.HouseholdId == householdId, cancellationToken);

        if (target is null)
        {
            if (await context.Recipes.AnyAsync(r => r.Id == recipe.Id, cancellationToken))
            {
                throw new NotFoundException($"Recipe {recipe.Id} was not found.");
            }

            target = new Recipe { Id = recipe.Id, HouseholdId = householdId };
            context.Recipes.Add(target);
        }

        target.Title = HouseholdAccess.RequiredText(recipe.Title, "The title", Recipe.TitleMaxLength);
        target.ImageUrl = OptionalUrl(recipe.ImageUrl, "The image");
        target.SourceUrl = OptionalUrl(recipe.SourceUrl, "The link");
        target.SourceName = HouseholdAccess.OptionalText(recipe.SourceName, "The source", Recipe.SourceMaxLength);
        target.Category = HouseholdAccess.OptionalText(recipe.Category, "The category", Recipe.CategoryMaxLength);
        target.Servings = recipe.Servings;
        target.Minutes = recipe.Minutes is > 0 ? recipe.Minutes : null;
        target.Ingredients = Lines(recipe.Ingredients, "The ingredients", Recipe.IngredientsMaxLength);
        target.Instructions = HouseholdAccess.OptionalText(recipe.Instructions, "The instructions", Recipe.InstructionsMaxLength);
        target.IsFavorite = recipe.IsFavorite;
        await context.SaveChangesAsync(cancellationToken);

        return target;
    }

    public async Task DeleteRecipeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        Recipe recipe = await context.Recipes.FirstOrDefaultAsync(r => r.Id == id && r.HouseholdId == householdId, cancellationToken)
            ?? throw new NotFoundException($"Recipe {id} was not found.");

        context.Recipes.Remove(recipe);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Recipe> ImportRecipeAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!RecipeScraper.TryCreateSafeUri(url, out Uri uri))
        {
            throw new ArgumentException("O link não é válido. Cola o endereço completo da receita (https://…).");
        }

        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);
        string address = uri.ToString();

        Recipe? existing = await context.Recipes.AsNoTracking().FirstOrDefaultAsync(r => r.HouseholdId == householdId && r.SourceUrl == address, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        ScrapedRecipe scraped = await FetchAsync(address, cancellationToken)
            ?? throw new ArgumentException("Não encontrei uma receita nesta página. Experimenta outro link ou cria a receita à mão.");

        List<RecipeSource> sources = await context.RecipeSources.AsNoTracking().Where(s => s.HouseholdId == householdId).ToListAsync(cancellationToken);
        string sourceName = sources.FirstOrDefault(s => Uri.TryCreate(s.SitemapUrl, UriKind.Absolute, out Uri? sitemap) && SameSite(sitemap.Host, uri.Host))?.Name
            ?? (uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host);

        int people = await PeopleAsync(context, householdId, cancellationToken);
        Recipe recipe = FromScraped(scraped, householdId, address, sourceName, people);
        context.Recipes.Add(recipe);
        await context.SaveChangesAsync(cancellationToken);

        return recipe;
    }

    public async Task<CatalogPage> SearchCatalogAsync(string? query, string? category, int skip, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        Dictionary<Guid, string> sources = await context.RecipeSources.AsNoTracking()
            .Where(s => s.HouseholdId == householdId && s.IsEnabled)
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        List<Guid> sourceIds = [.. sources.Keys];

        IQueryable<CatalogRecipe> catalog = context.CatalogRecipes.AsNoTracking()
            .Where(c => c.HouseholdId == householdId && sourceIds.Contains(c.SourceId)
                && (c.Status == CatalogRecipeStatus.Ready || c.Status == CatalogRecipeStatus.Pending));

        IQueryable<CatalogRecipe> filtered = catalog;

        if (!string.IsNullOrWhiteSpace(query))
        {
            string pattern = $"%{query.Trim().Replace("%", string.Empty).Replace("_", string.Empty)}%";
            filtered = filtered.Where(c => EF.Functions.Like(c.Title, pattern));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            filtered = filtered.Where(c => c.Category == category);
        }

        int total = await filtered.CountAsync(cancellationToken);

        List<CatalogRecipe> items = await filtered
            .OrderBy(c => c.Status == CatalogRecipeStatus.Ready ? 0 : 1).ThenBy(c => c.Title)
            .Skip(Math.Max(0, skip)).Take(CatalogPageSize)
            .ToListAsync(cancellationToken);

        foreach (CatalogRecipe item in items)
        {
            item.SourceName = sources.GetValueOrDefault(item.SourceId);
            item.Instructions = null;
        }

        List<string> categories = await catalog
            .Where(c => c.Category != null && c.Category != string.Empty)
            .GroupBy(c => c.Category!)
            .OrderByDescending(g => g.Count())
            .Take(MaxCatalogCategories)
            .Select(g => g.Key)
            .ToListAsync(cancellationToken);

        return new CatalogPage
        {
            Items = items,
            Total = total,
            Categories = [.. categories.Order(StringComparer.Create(CasaCatalog.Culture, true))]
        };
    }

    public async Task<Recipe> AdoptCatalogRecipeAsync(Guid catalogId, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        CatalogRecipe item = await context.CatalogRecipes.FirstOrDefaultAsync(c => c.Id == catalogId && c.HouseholdId == householdId, cancellationToken)
            ?? throw new NotFoundException($"Catalog recipe {catalogId} was not found.");

        Recipe? existing = await context.Recipes.AsNoTracking().FirstOrDefaultAsync(r => r.HouseholdId == householdId && r.SourceUrl == item.Url, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        if (item.Status != CatalogRecipeStatus.Ready)
        {
            await RecipeCatalogSync.EnrichAsync(item, scraper, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            if (item.Status != CatalogRecipeStatus.Ready)
            {
                throw new ArgumentException(item.Status == CatalogRecipeStatus.NotRecipe
                    ? "Esta página não tem uma receita reconhecível."
                    : "Não foi possível abrir a receita no site. Tenta novamente daqui a pouco.");
            }
        }

        string sourceName = await context.RecipeSources.Where(s => s.Id == item.SourceId).Select(s => s.Name).FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
        int people = await PeopleAsync(context, householdId, cancellationToken);

        Recipe recipe = new()
        {
            HouseholdId = householdId,
            Title = item.Title,
            ImageUrl = item.ImageUrl,
            SourceName = Truncate(sourceName, Recipe.SourceMaxLength),
            SourceUrl = item.Url,
            Category = item.Category,
            Servings = item.Servings ?? people,
            Minutes = item.Minutes,
            Ingredients = item.Ingredients,
            Instructions = item.Instructions
        };

        context.Recipes.Add(recipe);
        await context.SaveChangesAsync(cancellationToken);

        return recipe;
    }

    public async Task<List<MealIngredientLine>> GetShoppingPreviewAsync(DateOnly weekStart, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        return await MealMessages.PreviewAsync(context, householdId, MealCatalog.WeekStartOf(weekStart), cancellationToken);
    }

    public async Task<ShoppingList> CreateShoppingListAsync(DateOnly weekStart, List<MealIngredientLine> lines, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);
        (string displayName, _) = await HouseholdAccess.GetCurrentUserAsync(context, cancellationToken);
        weekStart = MealCatalog.WeekStartOf(weekStart);

        List<MealIngredientLine> selected = [.. lines.Where(l => l.Include && !string.IsNullOrWhiteSpace(l.Name)).Take(MaxShoppingLines)];

        if (selected.Count == 0)
        {
            throw new ArgumentException("Escolhe pelo menos um ingrediente.");
        }

        ShoppingList list = new()
        {
            HouseholdId = householdId,
            Name = $"Refeições {MealCatalog.ShortWeekLabel(weekStart)}",
            Emoji = "🍽️",
            Color = CasaCatalog.ListColors[1]
        };

        context.ShoppingLists.Add(list);

        foreach (MealIngredientLine line in selected)
        {
            string name = Truncate(line.Name.Trim(), ShoppingItem.NameMaxLength);
            string? recipes = line.Recipes.Count == 0 ? null : Truncate("Para: " + string.Join(", ", line.Recipes), ShoppingItem.NoteMaxLength);

            context.ShoppingItems.Add(new ShoppingItem
            {
                ListId = list.Id,
                Name = name,
                Quantity = line.Quantity is > 0 and <= 100_000m ? Math.Round(line.Quantity.Value, 2) : 1,
                Unit = line.Quantity is null || line.Unit is not { Length: <= 10 } ? null : line.Unit,
                Category = CasaCatalog.ShoppingCategories.Any(c => c.Name == line.Category) ? line.Category : CasaCatalog.GuessCategory(name),
                Note = recipes,
                AddedBy = displayName
            });
        }

        MealWeek? week = await context.MealWeeks.FirstOrDefaultAsync(w => w.HouseholdId == householdId && w.WeekStart == weekStart, cancellationToken);

        if (week is null)
        {
            week = new MealWeek { HouseholdId = householdId, WeekStart = weekStart };
            context.MealWeeks.Add(week);
        }

        week.ShoppingListId = list.Id;
        await context.SaveChangesAsync(cancellationToken);

        list.TotalCount = selected.Count;
        list.PendingCount = selected.Count;

        return list;
    }

    public async Task<string> GetShareTextAsync(DateOnly weekStart, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        return await MealMessages.BuildAsync(context, householdId, MealCatalog.WeekStartOf(weekStart), cancellationToken);
    }

    public async Task SendWhatsAppAsync(DateOnly weekStart, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);
        weekStart = MealCatalog.WeekStartOf(weekStart);

        MealSettings? settings = await context.MealPreferences.AsNoTracking().FirstOrDefaultAsync(s => s.HouseholdId == householdId, cancellationToken);

        if (!MealMessages.IsReady(settings))
        {
            throw new ArgumentException("Configura primeiro o WhatsApp em Definições → Refeições.");
        }

        string text = await MealMessages.BuildAsync(context, householdId, weekStart, cancellationToken);

        MealWeek? week = await context.MealWeeks.FirstOrDefaultAsync(w => w.HouseholdId == householdId && w.WeekStart == weekStart, cancellationToken);

        if (week is null)
        {
            week = new MealWeek { HouseholdId = householdId, WeekStart = weekStart };
            context.MealWeeks.Add(week);
        }

        week.LastAttemptAt = DateTime.UtcNow;

        try
        {
            await MealMessages.SendAsync(whatsApp, dataProtection, settings!, text, cancellationToken);
            week.SentAt = DateTime.UtcNow;
            week.LastError = null;
        }
        catch (ArgumentException ex)
        {
            week.LastError = Truncate(ex.Message, MealWeek.ErrorMaxLength);
            await context.SaveChangesAsync(cancellationToken);
            throw;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    internal static Recipe FromScraped(ScrapedRecipe scraped, Guid householdId, string url, string sourceName, int people) => new()
    {
        HouseholdId = householdId,
        Title = Truncate(scraped.Title, Recipe.TitleMaxLength),
        ImageUrl = scraped.ImageUrl is { Length: <= Recipe.UrlMaxLength } ? scraped.ImageUrl : null,
        SourceName = Truncate(sourceName, Recipe.SourceMaxLength),
        SourceUrl = url,
        Category = scraped.Category is null ? null : Truncate(scraped.Category, Recipe.CategoryMaxLength),
        Servings = scraped.Servings ?? people,
        Minutes = scraped.Minutes is > 0 and <= Recipe.MaxMinutes ? scraped.Minutes : null,
        Ingredients = Truncate(string.Join('\n', scraped.Ingredients), Recipe.IngredientsMaxLength),
        Instructions = scraped.Instructions is null ? null : Truncate(scraped.Instructions, Recipe.InstructionsMaxLength)
    };

    internal static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];

    private async Task<ScrapedRecipe?> FetchAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            return await scraper.FetchAsync(url, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new ArgumentException("Não foi possível abrir esse link. Confirma o endereço e tenta novamente.", ex);
        }
    }

    private static async Task<int> PeopleAsync(AgenderDbContext context, Guid householdId, CancellationToken cancellationToken) =>
        await context.MealPreferences.Where(s => s.HouseholdId == householdId).Select(s => (int?)s.People).FirstOrDefaultAsync(cancellationToken) ?? 2;

    private static bool SameSite(string a, string b) =>
        string.Equals(a.Replace("www.", string.Empty), b.Replace("www.", string.Empty), StringComparison.OrdinalIgnoreCase)
        || a.EndsWith("." + b.Replace("www.", string.Empty), StringComparison.OrdinalIgnoreCase)
        || b.EndsWith("." + a.Replace("www.", string.Empty), StringComparison.OrdinalIgnoreCase);

    private static string? OptionalUrl(string? value, string label)
    {
        string? url = HouseholdAccess.OptionalText(value, label, Recipe.UrlMaxLength);

        if (url is not null && !RecipeScraper.IsHttpUrl(url))
        {
            throw new ArgumentException($"{label} must be an http(s) address.");
        }

        return url;
    }

    private static string Lines(string? value, string label, int maxLength)
    {
        string text = string.Join('\n', Recipe.SplitLines(value));

        if (text.Length > maxLength)
        {
            throw new ArgumentException($"{label} must be at most {maxLength} characters.");
        }

        return text;
    }

    private DateOnly Today() => DateOnly.FromDateTime(time.GetLocalNow().DateTime);
}

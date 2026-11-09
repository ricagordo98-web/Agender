using Agender.Shared.Domain;

namespace Agender.Shared.Contracts;

public class MealWeekOverview
{
    public DateOnly WeekStart { get; set; }

    public DateOnly Today { get; set; }

    public int People { get; set; } = 2;

    public List<MealPlanEntry> Entries { get; set; } = [];

    public List<Recipe> Recipes { get; set; } = [];

    public Guid? ShoppingListId { get; set; }

    public DateTime? SentAt { get; set; }

    public string? SendError { get; set; }

    public bool WhatsAppReady { get; set; }
}

public class CatalogPage
{
    public List<CatalogRecipe> Items { get; set; } = [];

    public int Total { get; set; }

    public List<string> Categories { get; set; } = [];
}

public class MealSettingsDto
{
    public int People { get; set; } = 2;

    public string Pantry { get; set; } = MealCatalog.DefaultPantry;

    public bool WhatsAppEnabled { get; set; }

    public string? WhatsAppApiUrl { get; set; }

    public string? WhatsAppInstance { get; set; }

    public string? WhatsAppApiKey { get; set; }

    public bool HasWhatsAppApiKey { get; set; }

    public string? WhatsAppDestination { get; set; }

    public DayOfWeek SendDay { get; set; } = DayOfWeek.Sunday;

    public TimeOnly SendTime { get; set; } = new(18, 0);

    public List<RecipeSource> Sources { get; set; } = [];
}

public record ImportRecipeRequest(string Url);

public record MealShoppingRequest(DateOnly WeekStart, List<MealIngredientLine> Lines);

public record MealShareText(string Text);

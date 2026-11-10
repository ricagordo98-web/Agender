using System.Text.Json.Serialization;

namespace Agender.Shared.Domain;

public enum MealSlot { Lunch = 0, Dinner = 1, Dessert = 2 }

public enum CatalogRecipeStatus { Pending = 0, Ready = 1, NotRecipe = 2, Failed = 3 }

public class Recipe
{
    public const int TitleMaxLength = 160;
    public const int UrlMaxLength = 500;
    public const int SourceMaxLength = 60;
    public const int CategoryMaxLength = 60;
    public const int IngredientsMaxLength = 8000;
    public const int InstructionsMaxLength = 20000;
    public const int MaxServings = 50;
    public const int MaxMinutes = 1440;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public string? SourceName { get; set; }

    public string? SourceUrl { get; set; }

    public string? Category { get; set; }

    public int Servings { get; set; } = 4;

    public int? Minutes { get; set; }

    public string Ingredients { get; set; } = string.Empty;

    public string? Instructions { get; set; }

    public bool IsFavorite { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public IEnumerable<string> IngredientLines => SplitLines(Ingredients);

    public static IEnumerable<string> SplitLines(string? text) =>
        (text ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public class MealPlanEntry
{
    public const int TitleMaxLength = 120;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public DateOnly Date { get; set; }

    public MealSlot Slot { get; set; }

    public Guid? RecipeId { get; set; }

    public string? Title { get; set; }

    public int Servings { get; set; } = 2;

    public bool IsLeftover { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class RecipeSource
{
    public const int NameMaxLength = 60;
    public const int UrlMaxLength = 500;
    public const int FilterMaxLength = 120;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string SitemapUrl { get; set; } = string.Empty;

    public string? UrlFilter { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTime? LastSyncAt { get; set; }

    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int RecipeCount { get; set; }

    public int PendingCount { get; set; }
}

public class CatalogRecipe
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public Guid SourceId { get; set; }

    public string Url { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public string? Category { get; set; }

    public int? Minutes { get; set; }

    public int? Servings { get; set; }

    public string Ingredients { get; set; } = string.Empty;

    public string? Instructions { get; set; }

    public CatalogRecipeStatus Status { get; set; }

    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;

    public DateTime? FetchedAt { get; set; }

    public string? SourceName { get; set; }

    public RecipeCourse Course { get; set; }

    public HomeMatch? AtHome { get; set; }
}

public class MealSettings
{
    public const int UrlMaxLength = 300;
    public const int TextMaxLength = 120;
    public const int PantryMaxLength = 2000;
    public const int MaxPeople = 20;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public int People { get; set; } = 2;

    public string? Pantry { get; set; }

    public bool WhatsAppEnabled { get; set; }

    public string? WhatsAppApiUrl { get; set; }

    public string? WhatsAppInstance { get; set; }

    public string? WhatsAppApiKey { get; set; }

    public string? WhatsAppDestination { get; set; }

    public DayOfWeek SendDay { get; set; } = DayOfWeek.Sunday;

    public TimeOnly SendTime { get; set; } = new(18, 0);
}

public class MealWeek
{
    public const int ErrorMaxLength = 300;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public DateOnly WeekStart { get; set; }

    public Guid? ShoppingListId { get; set; }

    public DateTime? SentAt { get; set; }

    public DateTime? LastAttemptAt { get; set; }

    public string? LastError { get; set; }
}

public static class MealCatalog
{
    public const string DefaultPantry = "Sal\nPimenta\nAzeite\nÓleo\nÁgua\nVinagre\nAçúcar\nFarinha\nLouro\nColorau";

    public static readonly MealSlot[] Slots = [MealSlot.Lunch, MealSlot.Dinner, MealSlot.Dessert];

    public static readonly string[] QuickTitles = ["Sobras", "Jantar fora", "Takeaway"];

    public record SuggestedSource(string Name, string SitemapUrl, string UrlFilter);

    public static readonly SuggestedSource[] SuggestedSources =
    [
        new("Continente", "https://feed.continente.pt/sitemap", "/receitas/"),
        new("Pingo Doce", "https://www.pingodoce.pt/sitemap_index.xml", "/receitas/"),
        new("Lidl", "https://receitas.lidl.pt/sitemap.xml", "/receitas/")
    ];

    public static string SlotLabel(MealSlot slot) => slot switch
    {
        MealSlot.Lunch => "Almoço",
        MealSlot.Dinner => "Jantar",
        _ => "Sobremesa"
    };

    public static string SlotEmoji(MealSlot slot) => slot switch
    {
        MealSlot.Lunch => "☀️",
        MealSlot.Dinner => "🌙",
        _ => "🍰"
    };

    public static string SlotWithArticle(MealSlot slot) => slot == MealSlot.Dessert ? "a sobremesa" : $"o {SlotLabel(slot).ToLowerInvariant()}";

    public static (DateOnly Date, MealSlot Slot)? NextMeal(DateOnly date, MealSlot slot) => slot switch
    {
        MealSlot.Lunch => (date, MealSlot.Dinner),
        MealSlot.Dinner => (date.AddDays(1), MealSlot.Lunch),
        _ => null
    };

    public static (DateOnly Date, MealSlot Slot)? PreviousMeal(DateOnly date, MealSlot slot) => slot switch
    {
        MealSlot.Dinner => (date, MealSlot.Lunch),
        MealSlot.Lunch => (date.AddDays(-1), MealSlot.Dinner),
        _ => null
    };

    public static DateOnly WeekStartOf(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    public static DateTime LastScheduled(DateTime now, DayOfWeek day, TimeOnly time)
    {
        DateOnly today = DateOnly.FromDateTime(now);
        DateTime candidate = today.AddDays(-(((int)today.DayOfWeek - (int)day + 7) % 7)).ToDateTime(time);

        return candidate > now ? candidate.AddDays(-7) : candidate;
    }

    public static DateTime NextScheduled(DateTime now, DayOfWeek day, TimeOnly time) => LastScheduled(now, day, time).AddDays(7);

    public static DateOnly TargetWeek(DateOnly sendDate)
    {
        int days = ((int)DayOfWeek.Monday - (int)sendDate.DayOfWeek + 7) % 7;
        return sendDate.AddDays(days == 0 ? 7 : days);
    }

    public static string WeekLabel(DateOnly weekStart)
    {
        DateOnly end = weekStart.AddDays(6);
        string endText = end.ToString("d 'de' MMMM", CasaCatalog.Culture);

        return weekStart.Month == end.Month
            ? $"{weekStart.Day} a {endText}"
            : $"{weekStart.ToString("d 'de' MMMM", CasaCatalog.Culture)} a {endText}";
    }

    public static string ShortWeekLabel(DateOnly weekStart)
    {
        DateOnly end = weekStart.AddDays(6);

        return weekStart.Month == end.Month
            ? $"{weekStart.Day}–{end.ToString("d MMM", CasaCatalog.Culture)}"
            : $"{weekStart.ToString("d MMM", CasaCatalog.Culture)} – {end.ToString("d MMM", CasaCatalog.Culture)}";
    }

    public static string? Duration(int? minutes) => minutes switch
    {
        null or <= 0 => null,
        < 60 => $"{minutes} min",
        _ when minutes % 60 == 0 => $"{minutes / 60} h",
        _ => $"{minutes / 60} h {minutes % 60:00}"
    };
}

using System.Text.RegularExpressions;

namespace Agender.Shared.Domain;

public enum RecipeCourse { Main = 0, Dessert = 1, Drink = 2, Other = 3 }

public enum MealProtein { Meat = 0, Fish = 1, Veggie = 2 }

public sealed record HomeMatch(int Have, int Total, List<string> Expiring)
{
    public double Ratio => Total == 0 ? 0 : (double)Have / Total;
}

public sealed record VarietySummary(int Meat, int Fish, int Veggie, List<string> Notes);

public static partial class RecipeClassifier
{
    private static readonly string[] DessertCategories = ["doce e compota", "sobremesa", "gelado", "bolo", "doce", "doce e sobremesa"];

    private static readonly string[] DrinkCategories = ["bebida", "cocktail"];

    private static readonly string[] OtherCategories =
        ["molho", "entrada", "sande e snack", "snack", "pequeno almoco", "acompanhamento", "truque de cozinha", "compota e geleia", "pao", "petisco"];

    private static readonly string[] FruitCategories = ["fruta"];

    private static readonly string[] DessertTerms =
    [
        "bolo", "bolinho", "pudim", "pudin", "mousse", "gelado", "gelatina", "sorvete", "semifrio", "cheesecake", "brownie", "cookie", "bolacha",
        "biscoito", "tiramisu", "pannacotta", "panna cotta", "crumble", "leite creme", "arroz doce", "pao de lo", "salame de chocolate", "fudge",
        "trufa", "bombom", "bombon", "profiterole", "profiterol", "eclair", "merengue", "pavlova", "marquise", "coulant", "fondant", "petit gateau", "donut",
        "churro", "filho", "sonho", "rabanada", "queijada", "travesseiro", "pastel de nata", "pastei de nata", "macaron", "cake pop", "goma",
        "doce", "sobremesa", "baba de camelo", "toucinhodoceu", "farofia", "aletria", "sericaia", "tarte merengada", "folar", "rocambole",
        "torta", "cupcake", "madalena", "bola de berlim", "pao de deu", "ovo mole", "mil folha", "creme brulee", "granita"
    ];

    private static readonly string[] SweetBakeTerms = ["tarte", "tartelete", "folhado", "queque", "muffin", "crepe", "panqueca", "waffle", "scone", "strudel", "empada", "fatia", "barra"];

    private static readonly string[] SweetTerms =
    [
        "maca", "pera", "limao", "laranja", "amendoa", "chocolate", "morango", "framboesa", "mirtilo", "fruto vermelho", "fruto do bosque", "banana",
        "anana", "coco", "figo", "pessego", "manga", "caramelo", "mel", "canela", "nata", "iogurte", "baunilha", "noz", "avela", "cereja", "maracuja",
        "kiwi", "ameixa", "alfarroba", "nutella", "oreo", "creme", "fruta", "doce", "acucar", "frutos secos", "pistacio", "roma", "uva", "melancia", "meloa"
    ];

    private static readonly string[] SavoryTerms =
    [
        "carne", "frango", "peru", "porco", "vaca", "novilho", "borrego", "pato", "bacalhau", "salmao", "atum", "peixe", "camarao", "legume",
        "espinafre", "espinafr", "cogumelo", "brocolo", "tomate", "cebola", "alho", "curgete", "courgette", "pimento", "espargo", "alho frances", "couve",
        "batata", "fiambre", "presunto", "chourico", "bacon", "feta", "mozzarella", "alheira", "salsicha", "queijo da serra", "sardinha", "polvo", "pescada"
    ];

    private static readonly string[] DrinkTerms =
    [
        "batido", "smoothie", "sumo", "limonada", "sangria", "cocktail", "mojito", "caipirinha", "gin", "licor", "cha", "infusao", "cafe", "latte",
        "cappuccino", "frappe", "granizado", "kombucha", "ponche", "margarita", "spritz", "bebida", "agua aromatizada", "chocolate quente", "vinho quente",
        "milkshake", "frape", "mocktail", "shot"
    ];

    private static readonly string[] OtherTerms =
    [
        "compota", "geleia", "pate", "dip", "hummu", "humu", "guacamole", "granola", "papa de aveia", "papas de aveia", "overnight", "tosta",
        "sande", "sanduiche", "snack", "canape", "bruschetta", "pickle", "pickl", "tempero", "sal aromatizado", "marinada", "cracker", "crack", "focaccia",
        "croissant", "pipoca", "massa quebrada", "salame"
    ];

    private static readonly string[] OtherStarts = ["pao", "broa", "tosta", "sande", "molho"];

    private static readonly string[] MeatTerms =
    [
        "frango", "galinha", "peru", "porco", "leitao", "vaca", "novilho", "vitela", "borrego", "cabrito", "cordeiro", "pato", "coelho", "carne", "bife",
        "febra", "entrecosto", "costeleta", "lombo", "salsicha", "chourico", "chourica", "alheira", "farinheira", "morcela", "bacon", "presunto", "fiambre",
        "toucinho", "entremeada", "secreto", "pluma", "picanha", "rojao", "cachaco", "pernil", "moela", "figado", "chispe",
        "cozido", "feijoada", "francesinha", "bitoque", "rosbife", "strogonoff", "estrogonofe", "bolonhesa", "carbonara", "pancetta",
        "chorizo", "linguica", "perdiz", "codorniz", "javali", "veado", "tripa", "dobrada", "chili com carne"
    ];

    private static readonly string[] MeatDishTerms = ["hamburguer", "almondega", "lasanha", "cachorro", "nugget", "panado", "prego", "kebab"];

    private static readonly string[] FishTerms =
    [
        "peixe", "bacalhau", "salmao", "atum", "pescada", "sardinha", "dourada", "robalo", "carapau", "cavala", "truta", "linguado", "polvo", "lula",
        "choco", "camarao", "gamba", "ameijoa", "mexilhao", "berbigao", "marisco", "sapateira", "caranguejo", "lagosta", "lagostim", "lagostin", "peixe espada",
        "espadarte", "tamboril", "raia", "safio", "enguia", "cherne", "garoupa", "corvina", "perca", "solha", "hadoque", "pota", "delicia do mar", "ova",
        "choquinho", "vieira", "ostra", "lingueirao", "percebe", "anchova", "biqueirao", "arenque", "tilapia", "pangasius", "abrotea", "maruca",
        "sushi", "ceviche", "cataplana", "caldeirada", "sarrabulho de peixe", "cacao", "pargo", "sargo", "besugo", "chicharro", "goraz", "peixe galo"
    ];

    private static readonly string[] StrongVeggieTerms = ["vegan", "vegano", "vegetariano", "vegetariana", "tofu", "seitan", "tempeh", "soja"];

    private static readonly string[] VeggieTerms =
        ["beterraba", "lentilha", "grao", "feijao", "legume", "vegetai", "vegetal", "cogumelo", "espinafre", "espinafr", "quinoa", "curgete", "beringela"];

    private static readonly string[] MeatCategories = ["carne", "aves", "porco", "vaca"];

    private static readonly string[] FishCategories = ["peixe", "marisco", "peixe e marisco"];

    private static readonly string[] StrongVeggieCategories = ["vegetariano", "vegan", "vegano"];

    private static readonly string[] VeggieCategories = ["vegetai", "vegetal"];

    private static readonly string[] MainCategories = ["carne", "aves", "porco", "vaca", "peixe", "marisco", "peixe e marisco", "massa", "arroz", "sopa", "salada", "pizza"];

    public static RecipeCourse Course(string? title, string? category)
    {
        string name = Normalize(title);
        string group = Normalize(category).Trim();

        if (DrinkCategories.Contains(group))
        {
            return RecipeCourse.Drink;
        }

        if (DessertCategories.Contains(group))
        {
            return Has(name, MeatTerms) || Has(name, MeatDishTerms) || Has(name, FishTerms) ? RecipeCourse.Main : RecipeCourse.Dessert;
        }

        if (OtherCategories.Contains(group))
        {
            return RecipeCourse.Other;
        }

        if (FruitCategories.Contains(group))
        {
            return Has(name, OtherTerms) ? RecipeCourse.Other : RecipeCourse.Dessert;
        }

        if (OtherStarts.Any(s => name.StartsWith($" {s} ", StringComparison.Ordinal)) && !Has(name, DessertTerms))
        {
            return RecipeCourse.Other;
        }

        if (DrinkTerms.Any(t => name.StartsWith($" {t} ", StringComparison.Ordinal)))
        {
            return RecipeCourse.Drink;
        }

        bool savory = Has(name, SavoryTerms) || Has(name, MeatTerms) || Has(name, MeatDishTerms) || Has(name, FishTerms);

        if (!savory && (Has(name, DessertTerms) || Has(name, SweetBakeTerms) && Has(name, SweetTerms)))
        {
            return RecipeCourse.Dessert;
        }

        if (Has(name, DrinkTerms) && !savory)
        {
            return RecipeCourse.Drink;
        }

        return !MainCategories.Contains(group) && Has(name, OtherTerms) && !Has(name, MeatTerms) && !Has(name, FishTerms) ? RecipeCourse.Other : RecipeCourse.Main;
    }

    public static MealProtein? Protein(string? title, string? category, string? ingredients)
    {
        string name = Normalize(title);
        string group = Normalize(category).Trim();

        if (FishCategories.Contains(group))
        {
            return MealProtein.Fish;
        }

        if (MeatCategories.Contains(group))
        {
            return MealProtein.Meat;
        }

        if (StrongVeggieCategories.Contains(group) || Has(name, StrongVeggieTerms))
        {
            return MealProtein.Veggie;
        }

        if (Has(name, FishTerms))
        {
            return MealProtein.Fish;
        }

        if (Has(name, VeggieTerms) && !Has(name, MeatTerms))
        {
            return MealProtein.Veggie;
        }

        if (Has(name, MeatTerms))
        {
            return MealProtein.Meat;
        }

        if (VeggieCategories.Contains(group))
        {
            return MealProtein.Veggie;
        }

        if (Has(name, MeatDishTerms))
        {
            return MealProtein.Meat;
        }

        List<string> lines = [.. Recipe.SplitLines(ingredients).Select(Normalize)];

        if (lines.Count == 0)
        {
            return null;
        }

        if (lines.Any(l => Has(l, FishTerms)))
        {
            return MealProtein.Fish;
        }

        return lines.Any(l => Has(l, MeatTerms)) ? MealProtein.Meat : MealProtein.Veggie;
    }

    public static string Emoji(MealProtein protein) => protein switch
    {
        MealProtein.Meat => "🥩",
        MealProtein.Fish => "🐟",
        _ => "🥦"
    };

    public static string Label(MealProtein protein, int count) => protein switch
    {
        MealProtein.Meat => "carne",
        MealProtein.Fish => "peixe",
        _ => count == 1 ? "vegetariana" : "vegetarianas"
    };

    internal static string Normalize(string? text)
    {
        string plain = IngredientParser.RemoveDiacritics((text ?? string.Empty).ToLowerInvariant());
        string spaced = NonLetters().Replace(plain, " ").Replace("batata doce", "batatadoce").Replace("bolo do caco", "bolodocaco").Replace("toucinho do ceu", "toucinhodoceu");

        return " " + string.Join(' ', spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(IngredientParser.Singular)) + " ";
    }

    private static bool Has(string normalized, string[] terms) => terms.Any(t => normalized.Contains($" {t} ", StringComparison.Ordinal));

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonLetters();
}

public static class HomeMatcher
{
    public const int ExpiringDays = 3;

    public static List<string> PantryKeys(IEnumerable<string> pantry) => [.. pantry.Select(IngredientParser.KeyOf).Where(k => k.Length > 0).Distinct()];

    public static List<(string Key, InventoryItem Item)> Stock(IEnumerable<InventoryItem> inventory) =>
        [.. inventory.Where(i => i.Quantity > 0).Select(i => (IngredientParser.KeyOf(i.Name), i))];

    public static List<string> KeysOf(IEnumerable<string> ingredientLines) =>
        [.. ingredientLines.Select(IngredientParser.Parse).Where(p => !p.ToTaste && p.Key.Length > 0).Select(p => p.Key).Distinct()];

    public static HomeMatch? Match(IEnumerable<string> keys, IReadOnlyList<string> pantryKeys, IReadOnlyList<(string Key, InventoryItem Item)> stock, DateOnly today)
    {
        List<string> needed = [.. keys.Where(k => !pantryKeys.Any(p => IngredientParser.SameIngredient(k, p)))];

        if (needed.Count == 0)
        {
            return null;
        }

        int have = 0;
        List<string> expiring = [];

        foreach (string key in needed)
        {
            List<InventoryItem> matches = [.. stock.Where(s => IngredientParser.SameIngredient(key, s.Key)).Select(s => s.Item)];

            if (matches.Count == 0)
            {
                continue;
            }

            have++;
            expiring.AddRange(matches.Where(i => IsExpiring(i, today)).Select(i => i.Name).Where(n => !expiring.Contains(n)));
        }

        return new HomeMatch(have, needed.Count, expiring);
    }

    public static IEnumerable<T> Rank<T>(IEnumerable<(T Item, HomeMatch? Match)> candidates) =>
        candidates.Where(c => c.Match is { Have: > 0 })
            .OrderByDescending(c => c.Match!.Expiring.Count > 0)
            .ThenByDescending(c => c.Match!.Ratio)
            .ThenByDescending(c => c.Match!.Have)
            .Select(c => c.Item);

    public static bool IsExpiring(InventoryItem item, DateOnly today) =>
        item.ExpiresOn is DateOnly expires && expires.DayNumber - today.DayNumber <= ExpiringDays;

    public static string? ExpiryLabel(DateOnly? expires, DateOnly today) => expires is not DateOnly date ? null : (date.DayNumber - today.DayNumber) switch
    {
        < 0 => "Expirou",
        0 => "Expira hoje",
        1 => "Expira amanhã",
        int days when days <= 30 => $"Expira em {days} dias",
        _ => $"Validade {date.ToString("d MMM yyyy", CasaCatalog.Culture)}"
    };
}

public static class MealVariety
{
    public const int StreakWarning = 4;
    public const int MinMealsForFish = 6;

    public sealed record PlannedMeal(DateOnly Date, MealSlot Slot, Guid? RecipeId, string Title, MealProtein? Protein, bool IsLeftover);

    public static VarietySummary Analyze(IReadOnlyList<PlannedMeal> meals, IReadOnlySet<Guid> previousWeekRecipes)
    {
        List<PlannedMeal> mains = [.. meals.Where(m => m.Slot != MealSlot.Dessert).OrderBy(m => m.Date).ThenBy(m => m.Slot)];
        List<string> notes = [];

        int meat = mains.Count(m => m.Protein == MealProtein.Meat);
        int fish = mains.Count(m => m.Protein == MealProtein.Fish);
        int veggie = mains.Count(m => m.Protein == MealProtein.Veggie);

        foreach ((MealProtein protein, int length, DateOnly from, DateOnly to) in Streaks(mains).Where(s => s.Length >= StreakWarning))
        {
            notes.Add($"{RecipeClassifier.Emoji(protein)} {length} refeições seguidas de {RecipeClassifier.Label(protein, length)} ({DayRange(from, to)})");
        }

        if (mains.Count >= MinMealsForFish && fish == 0)
        {
            notes.Add("🐟 Ainda não há peixe esta semana");
        }

        foreach (IGrouping<Guid, PlannedMeal> repeated in mains.Where(m => !m.IsLeftover && m.RecipeId is not null).GroupBy(m => m.RecipeId!.Value).Where(g => g.Count() > 1))
        {
            notes.Add($"🔁 {repeated.First().Title} aparece {repeated.Count()} vezes");
        }

        foreach (PlannedMeal again in mains.Where(m => !m.IsLeftover && m.RecipeId is Guid id && previousWeekRecipes.Contains(id)).DistinctBy(m => m.RecipeId))
        {
            notes.Add($"📅 {again.Title} já foi na semana passada");
        }

        return new VarietySummary(meat, fish, veggie, notes);
    }

    private static IEnumerable<(MealProtein Protein, int Length, DateOnly From, DateOnly To)> Streaks(List<PlannedMeal> mains)
    {
        int index = 0;

        while (index < mains.Count)
        {
            if (mains[index].Protein is not MealProtein protein || protein == MealProtein.Veggie)
            {
                index++;
                continue;
            }

            int end = index;

            while (end + 1 < mains.Count && mains[end + 1].Protein == protein)
            {
                end++;
            }

            yield return (protein, end - index + 1, mains[index].Date, mains[end].Date);
            index = end + 1;
        }
    }

    private static string DayRange(DateOnly from, DateOnly to)
    {
        static string Short(DateOnly day) => day.ToString("ddd", CasaCatalog.Culture).TrimEnd('.');

        return from == to ? Short(from) : $"{Short(from)} a {Short(to)}";
    }
}

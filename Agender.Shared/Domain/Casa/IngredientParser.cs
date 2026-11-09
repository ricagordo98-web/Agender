using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Agender.Shared.Domain;

public sealed record ParsedIngredient(decimal? Quantity, string? Unit, string Name, string Key, bool ToTaste);

public class MealIngredientLine
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal? Quantity { get; set; }

    public string? Unit { get; set; }

    public string Category { get; set; } = CasaCatalog.DefaultShoppingCategory;

    public List<string> Recipes { get; set; } = [];

    public bool IsPantry { get; set; }

    public bool IsCovered { get; set; }

    public string? AtHome { get; set; }

    public bool Include { get; set; } = true;
}

public static partial class IngredientParser
{
    private const string Grams = "g";
    private const string Millilitres = "ml";
    private const string ToTasteKey = "~";
    private const string CountKey = "#";

    private sealed record UnitRule(Regex Pattern, string? Unit, decimal Factor);

    private static readonly UnitRule[] Units =
    [
        Rule(@"kgs?|quilos?|quilogramas?", Grams, 1000),
        Rule(@"mg", Grams, 0.001m),
        Rule(@"g|gr|grs|gramas?", Grams, 1),
        Rule(@"dl|decilitros?", Millilitres, 100),
        Rule(@"cl", Millilitres, 10),
        Rule(@"ml|mililitros?", Millilitres, 1),
        Rule(@"l|lt|lts|litros?", Millilitres, 1000),
        Rule(@"c\.?\s*(?:de\s+)?sopa|colher(?:es)?\s+(?:de\s+)?sopa|c\.\s*s\.|cs", "c. sopa", 1),
        Rule(@"c\.?\s*(?:de\s+)?sobremesa|colher(?:es)?\s+(?:de\s+)?sobremesa", "c. sobr.", 1),
        Rule(@"c\.?\s*(?:de\s+)?ch[áa]|colher(?:es)?\s+(?:de\s+)?ch[áa]|c\.\s*c\.|cc", "c. chá", 1),
        Rule(@"c\.?\s*(?:de\s+)?caf[ée]|colher(?:es)?\s+(?:de\s+)?caf[ée]", "c. café", 1),
        Rule(@"colher(?:es)?", "colher", 1),
        Rule(@"ch[áa]venas?", "chávena", 1),
        Rule(@"copos?", "copo", 1),
        Rule(@"latas?", "lata", 1),
        Rule(@"emb\.?|embalage(?:m|ns)", "emb", 1),
        Rule(@"pacotes?", "pacote", 1),
        Rule(@"frascos?", "frasco", 1),
        Rule(@"dentes?", "dente", 1),
        Rule(@"fatias?", "fatia", 1),
        Rule(@"folhas?", "folha", 1),
        Rule(@"ram(?:o|os|inho|inhos)", "ramo", 1),
        Rule(@"pitadas?", "pitada", 1),
        Rule(@"unid\.?|unidades?|un\.?", null, 1)
    ];

    private static readonly HashSet<string> WholeUnits = ["dente", "lata", "emb", "pacote", "frasco", "fatia", "folha", "ramo", "pitada"];

    private static readonly Dictionary<char, decimal> Fractions = new()
    {
        ['½'] = 0.5m, ['¼'] = 0.25m, ['¾'] = 0.75m, ['⅓'] = 0.33m, ['⅔'] = 0.67m
    };

    public static ParsedIngredient Parse(string line)
    {
        string text = Whitespace().Replace(line ?? string.Empty, " ").Trim().TrimStart('-', '•', '*', '·').Trim();
        bool toTaste = false;

        if (ToTastePattern().IsMatch(text))
        {
            toTaste = true;
            text = Whitespace().Replace(ToTastePattern().Replace(text, " "), " ").Trim();
        }

        decimal? quantity = null;
        Match amount = QuantityPattern().Match(text);

        if (amount.Success)
        {
            quantity = ParseQuantity(amount.Groups["q"].Value);
            text = text[amount.Length..];
        }

        string? unit = null;
        decimal factor = 1;

        foreach (UnitRule rule in Units)
        {
            Match match = rule.Pattern.Match(text);

            if (match.Success)
            {
                unit = rule.Unit;
                factor = rule.Factor;
                text = text[match.Length..];
                break;
            }
        }

        string name = LeadingOf().Replace(text, string.Empty).Trim().TrimEnd('.', ',', ';', ':').Trim();

        if (name.Length == 0)
        {
            name = line?.Trim() ?? string.Empty;
        }

        if (toTaste && unit is null)
        {
            quantity = null;
            unit = null;
        }
        else if (quantity is not null)
        {
            quantity *= factor;
        }

        return new ParsedIngredient(quantity, unit, Capitalize(name), KeyOf(name), toTaste);
    }

    public static string KeyOf(string name)
    {
        string text = RemoveDiacritics(name.ToLowerInvariant());
        text = Parentheses().Replace(text, " ");
        text = text.Split(',', ';')[0];
        text = " " + NonAlphanumeric().Replace(text, " ") + " ";
        text = Whitespace().Replace(text, " ");

        int cut = PreparationPhrase().Match(text) is { Success: true } phrase ? phrase.Index : -1;

        if (cut > 0)
        {
            text = text[..cut];
        }

        List<string> words = [.. text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Singular)];
        List<string> core = [.. words.Where(w => !Descriptors.Contains(w))];

        return string.Join(' ', core.Count > 0 ? core : words);
    }

    private static readonly HashSet<string> Descriptors =
    [
        "picado", "picada", "picadinho", "picadinha", "ralado", "ralada", "cortado", "cortada", "laminado", "laminada",
        "esmagado", "esmagada", "descascado", "descascada", "pequeno", "pequena", "grande", "medio", "media", "inteiro", "inteira"
    ];

    public static List<MealIngredientLine> Aggregate(IEnumerable<(string Line, decimal Factor, string Recipe)> items, IEnumerable<string> pantry, IEnumerable<InventoryItem>? inventory = null)
    {
        List<string> pantryKeys = [.. pantry.Select(KeyOf).Where(k => k.Length > 0).Distinct()];
        Dictionary<(string Key, string Unit), MealIngredientLine> lines = [];

        foreach ((string line, decimal factor, string recipe) in items)
        {
            ParsedIngredient parsed = Parse(line);

            if (parsed.Key.Length == 0)
            {
                continue;
            }

            string unitKey = parsed.Quantity is null ? ToTasteKey : parsed.Unit ?? CountKey;

            if (!lines.TryGetValue((parsed.Key, unitKey), out MealIngredientLine? target))
            {
                target = new MealIngredientLine
                {
                    Key = parsed.Key,
                    Name = parsed.Name,
                    Unit = parsed.Quantity is null ? null : parsed.Unit,
                    Quantity = parsed.Quantity is null ? null : 0
                };
                lines[(parsed.Key, unitKey)] = target;
            }

            if (parsed.Quantity is not null)
            {
                target.Quantity += parsed.Quantity * factor;
            }

            if (!target.Recipes.Contains(recipe))
            {
                target.Recipes.Add(recipe);
            }
        }

        foreach (((string key, string unit), MealIngredientLine toTaste) in lines.Where(l => l.Key.Unit == ToTasteKey).ToList())
        {
            MealIngredientLine? measured = lines.Where(l => l.Key.Key == key && l.Key.Unit != ToTasteKey).Select(l => l.Value).FirstOrDefault();

            if (measured is not null)
            {
                measured.Recipes.AddRange(toTaste.Recipes.Where(r => !measured.Recipes.Contains(r)));
                lines.Remove((key, unit));
            }
        }

        List<string> categoryOrder = [.. CasaCatalog.ShoppingCategories.Select(c => c.Name)];
        List<(string Key, InventoryItem Item)> stock = [.. (inventory ?? []).Where(i => i.Quantity > 0).Select(i => (KeyOf(i.Name), i))];

        foreach (MealIngredientLine line in lines.Values)
        {
            SubtractStock(line, stock);
            Normalize(line);
            line.Category = CasaCatalog.GuessCategory(line.Name);
            line.IsPantry = pantryKeys.Any(p => SameIngredient(line.Key, p));
            line.Include = !line.IsPantry && !line.IsCovered;
        }

        return [.. lines.Values.OrderBy(l => categoryOrder.IndexOf(l.Category)).ThenBy(l => l.Name, StringComparer.Create(CasaCatalog.Culture, true))];
    }

    private static bool SameIngredient(string lineKey, string stockKey) =>
        lineKey == stockKey
        || lineKey.StartsWith(stockKey + " ", StringComparison.Ordinal)
        || stockKey.StartsWith(lineKey + " ", StringComparison.Ordinal);

    private static void SubtractStock(MealIngredientLine line, List<(string Key, InventoryItem Item)> stock)
    {
        List<InventoryItem> matches = [.. stock.Where(s => SameIngredient(line.Key, s.Key)).Select(s => s.Item)];

        if (matches.Count == 0)
        {
            return;
        }

        line.AtHome = string.Join(", ", matches.Select(i => InventoryCatalog.Format(i.Quantity, i.Unit)));

        if (line.Quantity is not decimal needed)
        {
            line.IsCovered = true;
            return;
        }

        decimal have = matches.Where(i => i.Unit == line.Unit).Sum(i => i.Quantity);

        if (have >= needed)
        {
            line.IsCovered = true;
        }
        else if (have > 0)
        {
            line.Quantity = needed - have;
        }
    }

    private static void Normalize(MealIngredientLine line)
    {
        if (line.Quantity is not decimal quantity)
        {
            return;
        }

        if (line.Unit == Grams && quantity >= 1000)
        {
            line.Unit = "kg";
            line.Quantity = Math.Round(quantity / 1000, 2);
        }
        else if (line.Unit == Millilitres && quantity >= 1000)
        {
            line.Unit = "L";
            line.Quantity = Math.Round(quantity / 1000, 2);
        }
        else if (line.Unit is Grams or Millilitres)
        {
            line.Quantity = Math.Ceiling(quantity);
        }
        else if (line.Unit is null || WholeUnits.Contains(line.Unit))
        {
            line.Quantity = Math.Max(1, Math.Ceiling(quantity));
        }
        else
        {
            line.Quantity = Math.Ceiling(quantity * 2) / 2;
        }
    }

    private static decimal? ParseQuantity(string value)
    {
        value = value.Trim();

        if (value.Length == 1 && Fractions.TryGetValue(value[0], out decimal fraction))
        {
            return fraction;
        }

        if (value is "meia" or "meio" or "Meia" or "Meio")
        {
            return 0.5m;
        }

        string[] parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        decimal total = 0;

        foreach (string part in parts)
        {
            if (part.Contains('/'))
            {
                string[] pieces = part.Split('/');

                if (decimal.TryParse(pieces[0], NumberStyles.Number, CultureInfo.InvariantCulture, out decimal top)
                    && decimal.TryParse(pieces[1], NumberStyles.Number, CultureInfo.InvariantCulture, out decimal bottom) && bottom != 0)
                {
                    total += top / bottom;
                }
            }
            else if (decimal.TryParse(part.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal number))
            {
                total += number;
            }
        }

        return total > 0 ? total : null;
    }

    private static string Singular(string word)
    {
        if (word.Length <= 3 || !word.EndsWith('s'))
        {
            return word;
        }

        if (word.EndsWith("oes", StringComparison.Ordinal) || word.EndsWith("aes", StringComparison.Ordinal))
        {
            return word[..^3] + "ao";
        }

        if (word.EndsWith("es", StringComparison.Ordinal) && word[^3] is 'r' or 'z' or 'l')
        {
            return word[..^2];
        }

        return word[..^1];
    }

    private static string RemoveDiacritics(string text)
    {
        StringBuilder builder = new(text.Length);

        foreach (char c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpper(text[0], CasaCatalog.Culture) + text[1..];

    private static UnitRule Rule(string pattern, string? unit, decimal factor) =>
        new(new Regex($@"^(?:{pattern})(?=[\s.,]|$)\.?\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), unit, factor);

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"(?<!\p{L})(?:q\.?\s?b\.?|a\s+gosto)(?!\p{L})", RegexOptions.IgnoreCase)]
    private static partial Regex ToTastePattern();

    [GeneratedRegex(@"^(?<q>\d+\s+\d+/\d+|\d+/\d+|\d+(?:[.,]\d+)?|[½¼¾⅓⅔]|meia|meio)(?:\s*(?:a|-|–|ou)\s*\d+(?:[.,]\d+)?)?(?=[\s\p{L}]|$)\s*", RegexOptions.IgnoreCase)]
    private static partial Regex QuantityPattern();

    [GeneratedRegex(@"^(?:de|d')\s*", RegexOptions.IgnoreCase)]
    private static partial Regex LeadingOf();

    [GeneratedRegex(@" (?:em|as|aos|para|sem|com|q b) ")]
    private static partial Regex PreparationPhrase();

    [GeneratedRegex(@"\(.*?\)")]
    private static partial Regex Parentheses();

    [GeneratedRegex(@"[^a-z0-9 ]")]
    private static partial Regex NonAlphanumeric();
}

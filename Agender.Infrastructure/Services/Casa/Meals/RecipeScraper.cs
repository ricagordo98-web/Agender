using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Agender.Infrastructure.Services;

public sealed record ScrapedRecipe(string Title, string? ImageUrl, string? Category, int? Minutes, int? Servings, List<string> Ingredients, string? Instructions);

public partial class RecipeScraper(HttpClient http)
{
    private const int MaxSitemaps = 25;
    private const int MaxSitemapDepth = 2;
    private const int MaxUrls = 20000;

    private static readonly string[] RecipeSitemapHints = ["recipe", "receit"];

    private static readonly XmlReaderSettings XmlSettings = new() { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };

    public async Task<ScrapedRecipe?> FetchAsync(string url, CancellationToken cancellationToken)
    {
        string html = await http.GetStringAsync(url, cancellationToken);
        return Parse(html, new Uri(url));
    }

    public async Task<List<string>> ReadSitemapAsync(string sitemapUrl, string? filter, CancellationToken cancellationToken)
    {
        List<string> pages = [];
        Queue<(string Url, int Depth)> queue = new([(sitemapUrl, 0)]);
        int visited = 0;

        while (queue.Count > 0 && visited < MaxSitemaps && pages.Count < MaxUrls)
        {
            (string url, int depth) = queue.Dequeue();
            visited++;

            string xml = (await http.GetStringAsync(url, cancellationToken)).Trim();
            using StringReader text = new(xml);
            using XmlReader reader = XmlReader.Create(text, XmlSettings);
            XElement root = XDocument.Load(reader).Root ?? throw new XmlException("The sitemap is empty.");
            XNamespace ns = root.Name.Namespace;

            List<string> locations = [.. root.Elements()
                .Select(e => e.Element(ns + "loc")?.Value.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .Select(l => l!)];

            if (root.Name.LocalName == "sitemapindex")
            {
                if (depth >= MaxSitemapDepth)
                {
                    continue;
                }

                List<string> preferred = [.. locations.Where(l => RecipeSitemapHints.Any(h => l.Contains(h, StringComparison.OrdinalIgnoreCase)))];

                foreach (string child in preferred.Count > 0 ? preferred : locations)
                {
                    queue.Enqueue((child, depth + 1));
                }
            }
            else
            {
                pages.AddRange(locations.Where(l => string.IsNullOrWhiteSpace(filter) || l.Contains(filter, StringComparison.OrdinalIgnoreCase)));
            }
        }

        return [.. pages.Distinct().Take(MaxUrls)];
    }

    public static ScrapedRecipe? Parse(string html, Uri baseUri)
    {
        foreach (Match match in JsonLdPattern().Matches(html))
        {
            JsonDocument document;

            try
            {
                document = JsonDocument.Parse(match.Groups[1].Value.Trim(), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            }
            catch (JsonException)
            {
                continue;
            }

            using (document)
            {
                if (FindRecipe(document.RootElement) is JsonElement recipe && Map(recipe, baseUri) is ScrapedRecipe scraped)
                {
                    return scraped;
                }
            }
        }

        return null;
    }

    public static string TitleFromUrl(string url)
    {
        string slug = Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            ? uri.AbsolutePath.TrimEnd('/').Split('/')[^1]
            : url;

        string title = Uri.UnescapeDataString(slug).Replace('-', ' ').Replace('_', ' ').Trim();

        return title.Length == 0 ? url : char.ToUpper(title[0], CultureInfo.InvariantCulture) + title[1..];
    }

    public static bool TryCreateSafeUri(string? value, out Uri uri)
    {
        uri = null!;

        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out Uri? parsed) || parsed.Scheme is not ("http" or "https"))
        {
            return false;
        }

        if (parsed.IsLoopback || parsed.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IPAddress.TryParse(parsed.Host.Trim('[', ']'), out IPAddress? address) && IsPrivate(address))
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    public static bool IsHttpUrl(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out Uri? parsed) && parsed.Scheme is "http" or "https";

    private static bool IsPrivate(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
        {
            return true;
        }

        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        byte[] bytes = address.GetAddressBytes();

        return bytes[0] is 10 or 127 or 0
            || bytes[0] == 172 && bytes[1] is >= 16 and <= 31
            || bytes[0] == 192 && bytes[1] == 168
            || bytes[0] == 169 && bytes[1] == 254
            || bytes[0] == 100 && bytes[1] is >= 64 and <= 127;
    }

    private static JsonElement? FindRecipe(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                if (FindRecipe(item) is JsonElement found)
                {
                    return found;
                }
            }

            return null;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (IsRecipe(element))
        {
            return element;
        }

        foreach (string nested in new[] { "@graph", "mainEntity", "mainEntityOfPage" })
        {
            if (element.TryGetProperty(nested, out JsonElement child) && FindRecipe(child) is JsonElement found)
            {
                return found;
            }
        }

        return null;
    }

    private static bool IsRecipe(JsonElement element)
    {
        if (!element.TryGetProperty("@type", out JsonElement type))
        {
            return false;
        }

        return type.ValueKind switch
        {
            JsonValueKind.String => type.GetString() == "Recipe",
            JsonValueKind.Array => type.EnumerateArray().Any(t => t.ValueKind == JsonValueKind.String && t.GetString() == "Recipe"),
            _ => false
        };
    }

    private static ScrapedRecipe? Map(JsonElement recipe, Uri baseUri)
    {
        string? title = Clean(Text(recipe, "name"));

        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        string? image = recipe.TryGetProperty("image", out JsonElement imageElement) ? Image(imageElement) : null;

        if (image is not null && Uri.TryCreate(baseUri, image, out Uri? absolute) && absolute.Scheme is "http" or "https")
        {
            image = absolute.ToString();
        }
        else
        {
            image = null;
        }

        int? total = Minutes(Text(recipe, "totalTime"));
        int? prepAndCook = (Minutes(Text(recipe, "prepTime")) ?? 0) + (Minutes(Text(recipe, "cookTime")) ?? 0);

        List<string> ingredients = [];

        foreach (string property in new[] { "recipeIngredient", "ingredients" })
        {
            if (recipe.TryGetProperty(property, out JsonElement list) && list.ValueKind == JsonValueKind.Array)
            {
                ingredients = [.. list.EnumerateArray()
                    .Where(i => i.ValueKind == JsonValueKind.String)
                    .Select(i => Clean(i.GetString()))
                    .Where(i => !string.IsNullOrWhiteSpace(i))
                    .Select(i => i!)];
                break;
            }
        }

        List<string> steps = [];

        if (recipe.TryGetProperty("recipeInstructions", out JsonElement instructions))
        {
            CollectSteps(instructions, steps);
        }

        return new ScrapedRecipe(
            title,
            image,
            Clean(Text(recipe, "recipeCategory")),
            total is > 0 ? total : prepAndCook > 0 ? prepAndCook : null,
            Servings(recipe),
            ingredients,
            steps.Count == 0 ? null : string.Join('\n', steps));
    }

    private static string? Text(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out JsonElement value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.Array => value.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)),
            _ => null
        };
    }

    private static string? Image(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Array => element.EnumerateArray().Select(Image).FirstOrDefault(i => !string.IsNullOrWhiteSpace(i)),
        JsonValueKind.Object => element.TryGetProperty("url", out JsonElement url) ? Image(url)
            : element.TryGetProperty("contentUrl", out JsonElement content) ? Image(content) : null,
        _ => null
    };

    private static int? Servings(JsonElement recipe)
    {
        if (!recipe.TryGetProperty("recipeYield", out JsonElement value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            value = value.EnumerateArray().FirstOrDefault();
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number))
        {
            return number is > 0 and <= 50 ? number : null;
        }

        if (value.ValueKind == JsonValueKind.String && FirstNumber().Match(value.GetString() ?? string.Empty) is { Success: true } match
            && int.TryParse(match.Value, CultureInfo.InvariantCulture, out int parsed))
        {
            return parsed is > 0 and <= 50 ? parsed : null;
        }

        return null;
    }

    private static int? Minutes(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
        {
            return null;
        }

        try
        {
            int minutes = (int)XmlConvert.ToTimeSpan(duration.Trim()).TotalMinutes;
            return minutes > 0 ? minutes : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static void CollectSteps(JsonElement element, List<string> steps)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                steps.AddRange((Clean(element.GetString(), keepLines: true) ?? string.Empty)
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    CollectSteps(item, steps);
                }

                break;
            case JsonValueKind.Object:
                if (element.TryGetProperty("itemListElement", out JsonElement items))
                {
                    if (Clean(Text(element, "name")) is { Length: > 0 } section)
                    {
                        steps.Add(section + ":");
                    }

                    CollectSteps(items, steps);
                }
                else if (Clean(Text(element, "text")) is { Length: > 0 } text)
                {
                    steps.Add(text);
                }

                break;
        }
    }

    private static string? Clean(string? value, bool keepLines = false)
    {
        if (value is null)
        {
            return null;
        }

        string text = WebUtility.HtmlDecode(value);
        text = keepLines ? BreakTags().Replace(text, "\n") : text;
        text = WebUtility.HtmlDecode(Tags().Replace(text, " "));
        text = keepLines
            ? string.Join('\n', text.Split('\n').Select(l => Spaces().Replace(l, " ").Trim()).Where(l => l.Length > 0))
            : Whitespace().Replace(text, " ").Trim();

        return text.Length == 0 ? null : text;
    }

    [GeneratedRegex(@"<script[^>]*type\s*=\s*[""']?application/ld\+json[""']?[^>]*>(.*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex JsonLdPattern();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"<\s*(?:br|/p|/li)\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BreakTags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"[ \t ]+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\d+")]
    private static partial Regex FirstNumber();
}

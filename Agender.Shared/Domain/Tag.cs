namespace Agender.Shared.Domain;

public class Tag
{
    public const int NameMaxLength = 30;
    public const int MaxPerEntry = 10;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public static string Normalize(string name) => name.Trim().ToLowerInvariant();

    public static IReadOnlyList<string> CleanNames(IEnumerable<string> names)
    {
        List<string> cleaned = [];

        foreach (string raw in names)
        {
            string name = raw.Trim().TrimStart('#').Trim();

            if (name.Length == 0)
            {
                continue;
            }

            if (name.Length > NameMaxLength || name.Contains(','))
            {
                throw new ArgumentException($"A tag must have at most {NameMaxLength} characters and no commas.");
            }

            if (!cleaned.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                cleaned.Add(name);
            }
        }

        if (cleaned.Count > MaxPerEntry)
        {
            throw new ArgumentException($"An entry can have at most {MaxPerEntry} tags.");
        }

        return cleaned;
    }
}

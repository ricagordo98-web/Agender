using System.Globalization;

namespace Agender.Shared.Domain;

public static class QuickAddParser
{
    public record Parsed(decimal Quantity, string? Unit, string Name);

    public static Parsed Parse(string text)
    {
        List<string> parts = [.. text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)];
        decimal quantity = 1;
        string? unit = null;

        if (parts.Count > 1 && decimal.TryParse(parts[0].Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed) && parsed > 0)
        {
            quantity = parsed;
            parts.RemoveAt(0);

            string? known = CasaCatalog.Units.FirstOrDefault(u => string.Equals(u, parts[0], StringComparison.OrdinalIgnoreCase));

            if (parts.Count > 1 && known is not null)
            {
                unit = known;
                parts.RemoveAt(0);
            }
        }

        string name = string.Join(' ', parts);

        return new Parsed(quantity, unit, CasaCatalog.Capitalize(name));
    }
}

using System.Text.RegularExpressions;

namespace Agender.Shared.Domain;

public static partial class ColorHex
{
    public const int Length = 7;

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex Pattern();

    public static bool IsValid(string? value) => value is null || Pattern().IsMatch(value);

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();

        if (!Pattern().IsMatch(trimmed))
        {
            throw new ArgumentException("The color must be in the #RRGGBB format.");
        }

        return trimmed.ToUpperInvariant();
    }
}

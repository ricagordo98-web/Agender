using Agender.Shared.Domain;

namespace Agender.Shared.Components;

public static class EntryColorStyle
{
    private static readonly EntryColor[] Colors = Enum.GetValues<EntryColor>();

    public static IReadOnlyList<EntryColor> Palette => Colors;

    public static string CssClass(EntryColor color) => $"c-{color.ToString().ToLowerInvariant()}";

    public static string Label(EntryColor color) => color switch
    {
        EntryColor.Indigo => "Índigo",
        EntryColor.Violet => "Violeta",
        EntryColor.Pink => "Rosa",
        EntryColor.Rose => "Carmim",
        EntryColor.Amber => "Âmbar",
        EntryColor.Emerald => "Esmeralda",
        EntryColor.Teal => "Azul-petróleo",
        EntryColor.Sky => "Azul-céu",
        _ => color.ToString()
    };

    public static string? Style(string? customColor) => customColor is null ? null : $"--ec:{customColor}";

    public static string Hex(EntryColor color) => color switch
    {
        EntryColor.Indigo => "#6366f1",
        EntryColor.Violet => "#8b5cf6",
        EntryColor.Pink => "#ec4899",
        EntryColor.Rose => "#f43f5e",
        EntryColor.Amber => "#f59e0b",
        EntryColor.Emerald => "#10b981",
        EntryColor.Teal => "#14b8a6",
        _ => "#0ea5e9"
    };

    public static EntryColor Next(EntryColor color) => Colors[(Array.IndexOf(Colors, color) + 1) % Colors.Length];
}

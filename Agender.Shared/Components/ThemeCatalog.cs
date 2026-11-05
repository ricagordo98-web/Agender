namespace Agender.Shared.Components;

public record ThemeOption(string Id, string Label, string Surface, string Accent);

public static class ThemeCatalog
{
    public const string SystemId = "system";

    public static IReadOnlyList<ThemeOption> All { get; } =
    [
        new(SystemId, "Sistema", "linear-gradient(135deg, #f2f3f9 50%, #080a10 50%)", "#6366f1"),
        new("light", "Claro", "#ffffff", "#6366f1"),
        new("dark", "Escuro", "#11141d", "#8b5cf6"),
        new("ocean", "Oceano", "#0d1d29", "#0ea5e9"),
        new("forest", "Floresta", "#fbfdf9", "#16a34a"),
        new("sunset", "Pôr do sol", "#1d1118", "#f97316"),
        new("rose", "Rosa", "#fffafc", "#ec4899"),
        new("lavender", "Lavanda", "#fdfbff", "#a78bfa"),
        new("peach", "Pêssego", "#fffbf8", "#fb7185"),
        new("cottoncandy", "Algodão-doce", "linear-gradient(135deg, #fde8f4 0%, #e9e3fd 50%, #e0edff 100%)", "#f472b6"),
        new("mauve", "Rosa velho", "#fdf9fa", "#b5728a"),
        new("rosegold", "Rosa dourado", "#fffaf7", "#c28173"),
        new("berry", "Amora", "#1c0f1e", "#e879f9")
    ];
}

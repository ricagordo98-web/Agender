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
        new("rose", "Rosa", "#fffafc", "#ec4899")
    ];
}

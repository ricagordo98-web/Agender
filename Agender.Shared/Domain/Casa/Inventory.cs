namespace Agender.Shared.Domain;

public class InventoryItem
{
    public const int NameMaxLength = 120;
    public const int UnitMaxLength = 10;
    public const decimal MaxQuantity = 1_000_000m;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1;

    public string? Unit { get; set; }

    public string Category { get; set; } = CasaCatalog.DefaultShoppingCategory;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public static class InventoryCatalog
{
    public record UnitOption(string Label, string? BaseUnit, decimal Factor);

    public static readonly UnitOption[] Units =
    [
        new("un", null, 1),
        new("g", "g", 1),
        new("kg", "g", 1000),
        new("ml", "ml", 1),
        new("L", "ml", 1000),
        new("dz", null, 12),
        new("lata", "lata", 1),
        new("emb", "emb", 1),
        new("pacote", "pacote", 1),
        new("frasco", "frasco", 1)
    ];

    public static UnitOption UnitOf(string? label) =>
        Units.FirstOrDefault(u => string.Equals(u.Label, label, StringComparison.OrdinalIgnoreCase)) ?? Units[0];

    public static string DisplayUnit(string? baseUnit, decimal quantity) => baseUnit switch
    {
        "g" when quantity >= 1000 => "kg",
        "ml" when quantity >= 1000 => "L",
        null => "un",
        _ => baseUnit
    };

    public static decimal DisplayQuantity(string? baseUnit, decimal quantity) =>
        baseUnit is "g" or "ml" && quantity >= 1000 ? quantity / 1000 : quantity;

    public static string Format(decimal quantity, string? baseUnit) =>
        $"{CasaCatalog.Quantity(DisplayQuantity(baseUnit, quantity))} {DisplayUnit(baseUnit, quantity)}";

    public static decimal Step(string? baseUnit) => baseUnit is "g" or "ml" ? 100 : 1;
}

namespace Agender.Shared.Domain;

public class ShoppingList
{
    public const int NameMaxLength = 80;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Emoji { get; set; } = "🛒";

    public string Color { get; set; } = CasaCatalog.ListColors[0];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<ShoppingItem> Items { get; set; } = [];

    public List<ListShare> Shares { get; set; } = [];

    public bool IsSharedWithMe { get; set; }

    public bool CanEdit { get; set; } = true;

    public string? OwnerHouseholdName { get; set; }

    public int TotalCount { get; set; }

    public int PendingCount { get; set; }
}

public class ShoppingItem
{
    public const int NameMaxLength = 120;
    public const int NoteMaxLength = 200;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ListId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1;

    public string? Unit { get; set; }

    public string Category { get; set; } = CasaCatalog.DefaultShoppingCategory;

    public bool IsChecked { get; set; }

    public decimal? Price { get; set; }

    public string? Note { get; set; }

    public string? AddedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ListShare
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ListId { get; set; }

    public string Email { get; set; } = string.Empty;

    public bool CanEdit { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

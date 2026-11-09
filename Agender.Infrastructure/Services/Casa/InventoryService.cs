using Agender.Infrastructure.Data;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

public class InventoryService(IUserDbContextFactory contextFactory) : IInventoryService
{
    public async Task<List<InventoryItem>> GetAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        return [.. (await context.InventoryItems.AsNoTracking().Where(i => i.HouseholdId == householdId).ToListAsync(cancellationToken))
            .OrderBy(i => i.Name, StringComparer.Create(CasaCatalog.Culture, true))];
    }

    public async Task<InventoryItem> AddAsync(InventoryItem item, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        (string name, decimal quantity, string? unit) = Validate(item);
        string key = IngredientParser.KeyOf(name);

        InventoryItem? existing = (await context.InventoryItems.Where(i => i.HouseholdId == householdId && i.Unit == unit).ToListAsync(cancellationToken))
            .FirstOrDefault(i => IngredientParser.KeyOf(i.Name) == key);

        if (existing is not null)
        {
            existing.Quantity = Math.Min(existing.Quantity + quantity, InventoryItem.MaxQuantity);
            existing.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);

            return existing;
        }

        InventoryItem created = new()
        {
            HouseholdId = householdId,
            Name = name,
            Quantity = quantity,
            Unit = unit,
            Category = Category(item.Category, name)
        };

        context.InventoryItems.Add(created);
        await context.SaveChangesAsync(cancellationToken);

        return created;
    }

    public async Task<InventoryItem> UpdateAsync(InventoryItem item, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        InventoryItem stored = await context.InventoryItems.FirstOrDefaultAsync(i => i.Id == item.Id && i.HouseholdId == householdId, cancellationToken)
            ?? throw new NotFoundException($"Inventory item {item.Id} was not found.");

        (string name, decimal quantity, string? unit) = Validate(item);

        stored.Name = name;
        stored.Quantity = quantity;
        stored.Unit = unit;
        stored.Category = Category(item.Category, name);
        stored.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return stored;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        InventoryItem item = await context.InventoryItems.FirstOrDefaultAsync(i => i.Id == id && i.HouseholdId == householdId, cancellationToken)
            ?? throw new NotFoundException($"Inventory item {id} was not found.");

        context.InventoryItems.Remove(item);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static (string Name, decimal Quantity, string? Unit) Validate(InventoryItem item)
    {
        if (item.Quantity <= 0 || item.Quantity > InventoryItem.MaxQuantity)
        {
            throw new ArgumentException("The quantity is out of range.");
        }

        string? unit = HouseholdAccess.OptionalText(item.Unit, "The unit", InventoryItem.UnitMaxLength);

        if (InventoryCatalog.Units.All(u => u.BaseUnit != unit))
        {
            throw new ArgumentException("The unit is not supported.");
        }

        return (HouseholdAccess.RequiredText(item.Name, "The name", InventoryItem.NameMaxLength), Math.Round(item.Quantity, 2), unit);
    }

    private static string Category(string? category, string name) =>
        CasaCatalog.ShoppingCategories.Any(c => c.Name == category) && category != CasaCatalog.DefaultShoppingCategory
            ? category!
            : CasaCatalog.GuessCategory(name);
}

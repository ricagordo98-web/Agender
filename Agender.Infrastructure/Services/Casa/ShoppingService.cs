using Agender.Infrastructure.Data;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

public class ShoppingService(IUserDbContextFactory contextFactory) : IShoppingService
{
    private const int PreviewItems = 4;

    public async Task<IReadOnlyList<ShoppingList>> GetListsAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);
        (_, string email) = await HouseholdAccess.GetCurrentUserAsync(context, cancellationToken);

        return await AccessibleListsAsync(context, householdId, email, cancellationToken);
    }

    public async Task<ShoppingList> GetListAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);
        (_, string email) = await HouseholdAccess.GetCurrentUserAsync(context, cancellationToken);

        ShoppingList list = await context.ShoppingLists.AsNoTracking().Include(l => l.Items).Include(l => l.Shares).FirstOrDefaultAsync(l => l.Id == id, cancellationToken)
            ?? throw new NotFoundException($"List {id} was not found.");

        bool canEdit = Access(list, householdId, email) ?? throw new NotFoundException($"List {id} was not found.");

        list.CanEdit = canEdit;
        list.IsSharedWithMe = list.HouseholdId != householdId;

        if (list.IsSharedWithMe)
        {
            list.OwnerHouseholdName = await context.Households.Where(h => h.Id == list.HouseholdId).Select(h => h.Name).FirstOrDefaultAsync(cancellationToken);
        }

        list.Items = [.. list.Items.OrderBy(i => i.IsChecked).ThenBy(i => i.Category).ThenBy(i => i.Name)];
        list.TotalCount = list.Items.Count;
        list.PendingCount = list.Items.Count(i => !i.IsChecked);

        return list;
    }

    public async Task<ShoppingList> CreateListAsync(ShoppingList list, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        ShoppingList created = new()
        {
            HouseholdId = householdId,
            Name = HouseholdAccess.RequiredText(list.Name, "The name", ShoppingList.NameMaxLength),
            Emoji = string.IsNullOrWhiteSpace(list.Emoji) ? "🛒" : list.Emoji,
            Color = HouseholdAccess.NormalizeColor(list.Color, CasaCatalog.ListColors[0])
        };

        context.ShoppingLists.Add(created);
        await context.SaveChangesAsync(cancellationToken);

        return created;
    }

    public async Task UpdateListAsync(ShoppingList list, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        (ShoppingList stored, _) = await FindEditableAsync(context, list.Id, cancellationToken);

        stored.Name = HouseholdAccess.RequiredText(list.Name, "The name", ShoppingList.NameMaxLength);
        stored.Emoji = string.IsNullOrWhiteSpace(list.Emoji) ? stored.Emoji : list.Emoji;
        stored.Color = HouseholdAccess.NormalizeColor(list.Color, stored.Color);
        stored.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteListAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        int deleted = await context.ShoppingLists.Where(l => l.Id == id && l.HouseholdId == householdId).ExecuteDeleteAsync(cancellationToken);

        if (deleted == 0)
        {
            throw new NotFoundException($"List {id} was not found.");
        }
    }

    public async Task<ShoppingItem> AddItemAsync(Guid listId, ShoppingItem item, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        (ShoppingList list, _) = await FindEditableAsync(context, listId, cancellationToken);
        (string displayName, _) = await HouseholdAccess.GetCurrentUserAsync(context, cancellationToken);

        string name = HouseholdAccess.RequiredText(item.Name, "The name", ShoppingItem.NameMaxLength);

        ShoppingItem created = new()
        {
            ListId = list.Id,
            Name = name,
            Quantity = NormalizeQuantity(item.Quantity),
            Unit = HouseholdAccess.OptionalText(item.Unit, "The unit", 10),
            Category = string.IsNullOrWhiteSpace(item.Category) || item.Category == CasaCatalog.DefaultShoppingCategory ? CasaCatalog.GuessCategory(name) : item.Category,
            Price = HouseholdAccess.OptionalMoney(item.Price, "The price"),
            Note = HouseholdAccess.OptionalText(item.Note, "The note", ShoppingItem.NoteMaxLength),
            AddedBy = displayName
        };

        context.ShoppingItems.Add(created);
        list.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return created;
    }

    public async Task UpdateItemAsync(ShoppingItem item, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        ShoppingItem stored = await context.ShoppingItems.FirstOrDefaultAsync(i => i.Id == item.Id, cancellationToken)
            ?? throw new NotFoundException($"Item {item.Id} was not found.");

        (ShoppingList list, _) = await FindEditableAsync(context, stored.ListId, cancellationToken);

        stored.Name = HouseholdAccess.RequiredText(item.Name, "The name", ShoppingItem.NameMaxLength);
        stored.Quantity = NormalizeQuantity(item.Quantity);
        stored.Unit = HouseholdAccess.OptionalText(item.Unit, "The unit", 10);
        stored.Category = string.IsNullOrWhiteSpace(item.Category) ? CasaCatalog.DefaultShoppingCategory : item.Category;
        stored.IsChecked = item.IsChecked;
        stored.Price = HouseholdAccess.OptionalMoney(item.Price, "The price");
        stored.Note = HouseholdAccess.OptionalText(item.Note, "The note", ShoppingItem.NoteMaxLength);
        list.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        ShoppingItem stored = await context.ShoppingItems.FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Item {id} was not found.");

        (ShoppingList list, _) = await FindEditableAsync(context, stored.ListId, cancellationToken);

        context.ShoppingItems.Remove(stored);
        list.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearCheckedAsync(Guid listId, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        (ShoppingList list, _) = await FindEditableAsync(context, listId, cancellationToken);

        await context.ShoppingItems.Where(i => i.ListId == list.Id && i.IsChecked).ExecuteDeleteAsync(cancellationToken);
        list.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ListShare> ShareAsync(Guid listId, string email, bool canEdit, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        ShoppingList list = await context.ShoppingLists.Include(l => l.Shares).FirstOrDefaultAsync(l => l.Id == listId && l.HouseholdId == householdId, cancellationToken)
            ?? throw new NotFoundException($"List {listId} was not found.");

        string normalized = (email ?? string.Empty).Trim().ToLowerInvariant();

        if (!normalized.Contains('@') || normalized.Length > 256)
        {
            throw new ArgumentException("The email is not valid.");
        }

        ListShare? existing = list.Shares.FirstOrDefault(s => s.Email == normalized);

        if (existing is null)
        {
            existing = new ListShare { ListId = list.Id, Email = normalized, CanEdit = canEdit };
            context.ListShares.Add(existing);
        }
        else
        {
            existing.CanEdit = canEdit;
        }

        await context.SaveChangesAsync(cancellationToken);

        return existing;
    }

    public async Task UnshareAsync(Guid listId, Guid shareId, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);
        (_, string email) = await HouseholdAccess.GetCurrentUserAsync(context, cancellationToken);

        ShoppingList list = await context.ShoppingLists.Include(l => l.Shares).FirstOrDefaultAsync(l => l.Id == listId, cancellationToken)
            ?? throw new NotFoundException($"List {listId} was not found.");

        ListShare share = list.Shares.FirstOrDefault(s => s.Id == shareId)
            ?? throw new NotFoundException($"Share {shareId} was not found.");

        bool isOwner = list.HouseholdId == householdId;

        if (!isOwner && share.Email != email)
        {
            throw new NotFoundException($"Share {shareId} was not found.");
        }

        context.ListShares.Remove(share);
        await context.SaveChangesAsync(cancellationToken);
    }

    internal static async Task<List<ShoppingList>> AccessibleListsAsync(AgenderDbContext context, Guid householdId, string email, CancellationToken cancellationToken)
    {
        List<ShoppingList> lists = await context.ShoppingLists.AsNoTracking()
            .Include(l => l.Items)
            .Include(l => l.Shares)
            .Where(l => l.HouseholdId == householdId || l.Shares.Any(s => s.Email == email))
            .ToListAsync(cancellationToken);

        List<Guid> otherHouseholds = [.. lists.Where(l => l.HouseholdId != householdId).Select(l => l.HouseholdId).Distinct()];
        Dictionary<Guid, string> names = await context.Households.AsNoTracking()
            .Where(h => otherHouseholds.Contains(h.Id))
            .ToDictionaryAsync(h => h.Id, h => h.Name, cancellationToken);

        foreach (ShoppingList list in lists)
        {
            list.TotalCount = list.Items.Count;
            list.PendingCount = list.Items.Count(i => !i.IsChecked);
            list.IsSharedWithMe = list.HouseholdId != householdId;
            list.CanEdit = Access(list, householdId, email) ?? false;
            list.OwnerHouseholdName = names.GetValueOrDefault(list.HouseholdId);
            list.Items = [.. list.Items.Where(i => !i.IsChecked).OrderBy(i => i.CreatedAt).Take(PreviewItems)];
        }

        return [.. lists.OrderByDescending(l => l.UpdatedAt)];
    }

    private static bool? Access(ShoppingList list, Guid householdId, string email)
    {
        if (list.HouseholdId == householdId)
        {
            return true;
        }

        ListShare? share = list.Shares.FirstOrDefault(s => s.Email == email);

        return share?.CanEdit;
    }

    private static async Task<(ShoppingList List, bool IsOwner)> FindEditableAsync(AgenderDbContext context, Guid listId, CancellationToken cancellationToken)
    {
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);
        (_, string email) = await HouseholdAccess.GetCurrentUserAsync(context, cancellationToken);

        ShoppingList list = await context.ShoppingLists.Include(l => l.Shares).FirstOrDefaultAsync(l => l.Id == listId, cancellationToken)
            ?? throw new NotFoundException($"List {listId} was not found.");

        bool canEdit = Access(list, householdId, email) ?? false;

        if (!canEdit)
        {
            throw new NotFoundException($"List {listId} was not found.");
        }

        return (list, list.HouseholdId == householdId);
    }

    private static decimal NormalizeQuantity(decimal quantity) => quantity <= 0 ? 1 : Math.Min(Math.Round(quantity, 2), 100_000m);
}

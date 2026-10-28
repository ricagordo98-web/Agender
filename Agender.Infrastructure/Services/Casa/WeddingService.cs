using Agender.Infrastructure.Data;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

public class WeddingService(IUserDbContextFactory contextFactory) : IWeddingService
{
    public async Task<WeddingOverview> GetAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        WeddingSettings settings = await context.WeddingDetails.AsNoTracking().FirstOrDefaultAsync(w => w.HouseholdId == householdId, cancellationToken)
            ?? new WeddingSettings { HouseholdId = householdId };

        List<WeddingCategory> categories = await context.WeddingCategories.AsNoTracking().Include(c => c.Payments)
            .Where(c => c.HouseholdId == householdId).OrderBy(c => c.Order).ThenBy(c => c.Name).ToListAsync(cancellationToken);

        foreach (WeddingCategory category in categories)
        {
            category.Payments = [.. category.Payments.OrderByDescending(p => p.Date)];
        }

        return new WeddingOverview
        {
            Settings = settings,
            Categories = categories,
            Guests = await context.WeddingGuests.AsNoTracking().Where(g => g.HouseholdId == householdId).OrderBy(g => g.Name).ToListAsync(cancellationToken),
            Vendors = await context.WeddingVendors.AsNoTracking().Where(v => v.HouseholdId == householdId).OrderBy(v => v.Name).ToListAsync(cancellationToken),
            Tasks = [.. (await context.WeddingTasks.AsNoTracking().Where(t => t.HouseholdId == householdId).ToListAsync(cancellationToken))
                .OrderBy(t => t.Done).ThenBy(t => t.DueDate ?? DateOnly.MaxValue).ThenBy(t => t.CreatedAt)]
        };
    }

    public async Task<WeddingSettings> SaveSettingsAsync(WeddingSettings settings, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        WeddingSettings? stored = await context.WeddingDetails.FirstOrDefaultAsync(w => w.HouseholdId == householdId, cancellationToken);

        if (stored is null)
        {
            stored = new WeddingSettings { HouseholdId = householdId };
            context.WeddingDetails.Add(stored);
        }

        stored.Date = settings.Date;
        stored.Venue = HouseholdAccess.OptionalText(settings.Venue, "The venue", WeddingSettings.VenueMaxLength);
        await context.SaveChangesAsync(cancellationToken);

        return stored;
    }

    public async Task<WeddingGuest> SaveGuestAsync(WeddingGuest guest, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        if (guest.PlusOnes < 0 || guest.PlusOnes > WeddingGuest.MaxPlusOnes || !Enum.IsDefined(guest.Status))
        {
            throw new ArgumentException("The guest is out of range.");
        }

        WeddingGuest target = await FindOrAddAsync(context.WeddingGuests, guest.Id, householdId, () => new WeddingGuest { Id = guest.Id, HouseholdId = householdId }, cancellationToken);

        target.Name = HouseholdAccess.RequiredText(guest.Name, "The name", WeddingGuest.NameMaxLength);
        target.Group = HouseholdAccess.OptionalText(guest.Group, "The group", WeddingGuest.TextMaxLength);
        target.PlusOnes = guest.PlusOnes;
        target.Contact = HouseholdAccess.OptionalText(guest.Contact, "The contact", WeddingGuest.TextMaxLength);
        target.Table = HouseholdAccess.OptionalText(guest.Table, "The table", WeddingGuest.TextMaxLength);
        target.Diet = HouseholdAccess.OptionalText(guest.Diet, "The diet", WeddingGuest.TextMaxLength);
        target.Notes = HouseholdAccess.OptionalText(guest.Notes, "The notes", WeddingGuest.NotesMaxLength);
        target.Status = guest.Status;
        await context.SaveChangesAsync(cancellationToken);

        return target;
    }

    public async Task DeleteGuestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        RequireDeleted(await context.WeddingGuests.Where(g => g.Id == id && g.HouseholdId == householdId).ExecuteDeleteAsync(cancellationToken), "Guest", id);
    }

    public async Task<WeddingCategory> SaveCategoryAsync(WeddingCategory category, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        WeddingCategory? existing = await context.WeddingCategories.FirstOrDefaultAsync(c => c.Id == category.Id && c.HouseholdId == householdId, cancellationToken);

        if (existing is null && await context.WeddingCategories.AnyAsync(c => c.Id == category.Id, cancellationToken))
        {
            throw new NotFoundException($"Category {category.Id} was not found.");
        }

        WeddingCategory target = existing ?? new WeddingCategory
        {
            Id = category.Id,
            HouseholdId = householdId,
            Order = (await context.WeddingCategories.Where(c => c.HouseholdId == householdId).Select(c => (int?)c.Order).MaxAsync(cancellationToken) ?? -1) + 1
        };

        if (existing is null)
        {
            context.WeddingCategories.Add(target);
        }

        target.Name = HouseholdAccess.RequiredText(category.Name, "The name", WeddingCategory.NameMaxLength);
        target.Planned = HouseholdAccess.OptionalMoney(category.Planned, "The planned amount") ?? 0;
        target.Cost = HouseholdAccess.OptionalMoney(category.Cost, "The cost") ?? 0;
        await context.SaveChangesAsync(cancellationToken);

        return target;
    }

    public async Task DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        await context.WeddingVendors.Where(v => v.CategoryId == id && v.HouseholdId == householdId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(v => v.CategoryId, (Guid?)null), cancellationToken);
        await context.WeddingPayments.Where(p => p.CategoryId == id && p.HouseholdId == householdId).ExecuteDeleteAsync(cancellationToken);
        RequireDeleted(await context.WeddingCategories.Where(c => c.Id == id && c.HouseholdId == householdId).ExecuteDeleteAsync(cancellationToken), "Category", id);
    }

    public async Task<WeddingPayment> SavePaymentAsync(WeddingPayment payment, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        if (!await context.WeddingCategories.AnyAsync(c => c.Id == payment.CategoryId && c.HouseholdId == householdId, cancellationToken))
        {
            throw new ArgumentException("The category does not exist.");
        }

        decimal amount = Math.Round(payment.Amount, 2);

        if (amount <= 0 || amount > 100_000_000m)
        {
            throw new ArgumentException("The amount is out of range.");
        }

        WeddingPayment target = await FindOrAddAsync(context.WeddingPayments, payment.Id, householdId, () => new WeddingPayment { Id = payment.Id, HouseholdId = householdId }, cancellationToken);

        target.CategoryId = payment.CategoryId;
        target.Amount = amount;
        target.Date = payment.Date;
        target.PaidBy = HouseholdAccess.OptionalText(payment.PaidBy, "The payer", WeddingPayment.PaidByMaxLength);
        target.Notes = HouseholdAccess.OptionalText(payment.Notes, "The notes", WeddingPayment.NotesMaxLength);
        await context.SaveChangesAsync(cancellationToken);

        return target;
    }

    public async Task DeletePaymentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        RequireDeleted(await context.WeddingPayments.Where(p => p.Id == id && p.HouseholdId == householdId).ExecuteDeleteAsync(cancellationToken), "Payment", id);
    }

    public async Task<WeddingVendor> SaveVendorAsync(WeddingVendor vendor, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        if (!Enum.IsDefined(vendor.Status))
        {
            throw new ArgumentException("The status is out of range.");
        }

        if (vendor.CategoryId is Guid categoryId && !await context.WeddingCategories.AnyAsync(c => c.Id == categoryId && c.HouseholdId == householdId, cancellationToken))
        {
            throw new ArgumentException("The category does not exist.");
        }

        WeddingVendor target = await FindOrAddAsync(context.WeddingVendors, vendor.Id, householdId, () => new WeddingVendor { Id = vendor.Id, HouseholdId = householdId }, cancellationToken);

        target.Name = HouseholdAccess.RequiredText(vendor.Name, "The name", WeddingVendor.NameMaxLength);
        target.Kind = HouseholdAccess.OptionalText(vendor.Kind, "The kind", WeddingVendor.TextMaxLength);
        target.Contact = HouseholdAccess.OptionalText(vendor.Contact, "The contact", WeddingVendor.TextMaxLength);
        target.Quote = HouseholdAccess.OptionalMoney(vendor.Quote, "The quote");
        target.Status = vendor.Status;
        target.CategoryId = vendor.CategoryId;
        target.Notes = HouseholdAccess.OptionalText(vendor.Notes, "The notes", WeddingVendor.NotesMaxLength);
        await context.SaveChangesAsync(cancellationToken);

        return target;
    }

    public async Task DeleteVendorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        RequireDeleted(await context.WeddingVendors.Where(v => v.Id == id && v.HouseholdId == householdId).ExecuteDeleteAsync(cancellationToken), "Vendor", id);
    }

    public async Task<WeddingTask> SaveTaskAsync(WeddingTask task, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        (string? assignee, _) = await HouseholdAccess.ResolveMemberAsync(context, householdId, task.AssigneeUserId, false, cancellationToken);
        WeddingTask target = await FindOrAddAsync(context.WeddingTasks, task.Id, householdId, () => new WeddingTask { Id = task.Id, HouseholdId = householdId }, cancellationToken);

        target.Title = HouseholdAccess.RequiredText(task.Title, "The title", WeddingTask.TitleMaxLength);
        target.DueDate = task.DueDate;
        target.AssigneeUserId = assignee;
        target.Done = task.Done;
        await context.SaveChangesAsync(cancellationToken);

        return target;
    }

    public async Task DeleteTaskAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        RequireDeleted(await context.WeddingTasks.Where(t => t.Id == id && t.HouseholdId == householdId).ExecuteDeleteAsync(cancellationToken), "Task", id);
    }

    public async Task<int> AddSuggestedCategoriesAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        List<string> existing = await context.WeddingCategories.Where(c => c.HouseholdId == householdId).Select(c => c.Name).ToListAsync(cancellationToken);
        int order = (await context.WeddingCategories.Where(c => c.HouseholdId == householdId).Select(c => (int?)c.Order).MaxAsync(cancellationToken) ?? -1) + 1;
        List<string> missing = [.. WeddingCatalog.SuggestedCategories.Where(name => !existing.Contains(name, StringComparer.OrdinalIgnoreCase))];

        foreach (string name in missing)
        {
            context.WeddingCategories.Add(new WeddingCategory { HouseholdId = householdId, Name = name, Order = order++ });
        }

        await context.SaveChangesAsync(cancellationToken);

        return missing.Count;
    }

    public async Task<int> AddSuggestedTasksAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        DateOnly? date = await context.WeddingDetails.Where(w => w.HouseholdId == householdId).Select(w => w.Date).FirstOrDefaultAsync(cancellationToken);
        List<string> existing = await context.WeddingTasks.Where(t => t.HouseholdId == householdId).Select(t => t.Title).ToListAsync(cancellationToken);
        List<(int MonthsBefore, string Title)> missing = [.. WeddingCatalog.SuggestedTasks.Where(t => !existing.Contains(t.Title, StringComparer.OrdinalIgnoreCase))];

        foreach ((int monthsBefore, string title) in missing)
        {
            context.WeddingTasks.Add(new WeddingTask { HouseholdId = householdId, Title = title, DueDate = date?.AddMonths(-monthsBefore) });
        }

        await context.SaveChangesAsync(cancellationToken);

        return missing.Count;
    }

    private static async Task<T> FindOrAddAsync<T>(DbSet<T> set, Guid id, Guid householdId, Func<T> create, CancellationToken cancellationToken)
        where T : class
    {
        T? existing = await set.FirstOrDefaultAsync(e => EF.Property<Guid>(e, "Id") == id && EF.Property<Guid>(e, "HouseholdId") == householdId, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        if (await set.AnyAsync(e => EF.Property<Guid>(e, "Id") == id, cancellationToken))
        {
            throw new NotFoundException($"{typeof(T).Name} {id} was not found.");
        }

        T created = create();
        set.Add(created);

        return created;
    }

    private static void RequireDeleted(int deleted, string what, Guid id)
    {
        if (deleted == 0)
        {
            throw new NotFoundException($"{what} {id} was not found.");
        }
    }
}

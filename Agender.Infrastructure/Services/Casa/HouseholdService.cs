using Agender.Infrastructure.Data;
using Agender.Shared.Abstractions;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

public class HouseholdService(IUserDbContextFactory contextFactory, TimeProvider time) : IHouseholdService
{
    private const int DashboardUpcomingDays = 14;
    private const int DashboardBirthdayDays = 90;

    public async Task<HouseholdDto?> GetAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);

        return await FindAsync(context, cancellationToken) ?? await AcceptInviteAsync(context, cancellationToken);
    }

    public async Task<HouseholdDto> InviteAsync(string email, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Household household = await RequireAsync(context, cancellationToken);
        string normalized = (email ?? string.Empty).Trim().ToLowerInvariant();

        if (!normalized.Contains('@') || normalized.Length > HouseholdInvite.EmailMaxLength)
        {
            throw new ArgumentException("The email is not valid.");
        }

        bool alreadyMember = await (
            from member in context.HouseholdMembers
            join user in context.Users on member.UserId equals user.Id
            where member.HouseholdId == household.Id && user.NormalizedEmail == normalized.ToUpperInvariant()
            select member.Id).AnyAsync(cancellationToken);

        if (alreadyMember)
        {
            throw new ArgumentException("That person is already in the household.");
        }

        DateTime now = time.GetUtcNow().UtcDateTime;
        HouseholdInvite? existing = await context.HouseholdInvites.FirstOrDefaultAsync(i => i.HouseholdId == household.Id && i.Email == normalized, cancellationToken);

        if (existing is null)
        {
            context.HouseholdInvites.Add(new HouseholdInvite { HouseholdId = household.Id, Email = normalized, InvitedBy = context.UserId, CreatedAt = now, ExpiresAt = now + HouseholdInvite.Lifetime });
        }
        else
        {
            existing.CreatedAt = now;
            existing.ExpiresAt = now + HouseholdInvite.Lifetime;
        }

        await context.SaveChangesAsync(cancellationToken);

        return (await FindAsync(context, cancellationToken))!;
    }

    public async Task<HouseholdDto> CancelInviteAsync(Guid inviteId, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        await context.HouseholdInvites.Where(i => i.Id == inviteId && i.HouseholdId == householdId).ExecuteDeleteAsync(cancellationToken);

        return (await FindAsync(context, cancellationToken))!;
    }

    public async Task<HouseholdDto> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);

        if (await context.HouseholdMembers.AnyAsync(m => m.UserId == context.UserId, cancellationToken))
        {
            throw new ArgumentException("You already belong to a household.");
        }

        Household household = new()
        {
            Name = string.IsNullOrWhiteSpace(name) ? Household.DefaultName : HouseholdAccess.RequiredText(name, "The name", Household.NameMaxLength),
            InviteCode = await UniqueCodeAsync(context, cancellationToken)
        };

        context.Households.Add(household);
        context.HouseholdMembers.Add(new HouseholdMember { HouseholdId = household.Id, UserId = context.UserId, Color = CasaCatalog.MemberColors[0] });
        await context.SaveChangesAsync(cancellationToken);

        return (await FindAsync(context, cancellationToken))!;
    }

    public async Task<HouseholdDto> JoinAsync(string code, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        string normalized = (code ?? string.Empty).Trim().ToUpperInvariant();

        Household household = await context.Households.FirstOrDefaultAsync(h => h.InviteCode == normalized, cancellationToken)
            ?? throw new NotFoundException("Código inválido.");

        HouseholdMember? existing = await context.HouseholdMembers.FirstOrDefaultAsync(m => m.UserId == context.UserId, cancellationToken);

        if (existing is not null && existing.HouseholdId != household.Id)
        {
            throw new ArgumentException("You already belong to another household. Leave it first.");
        }

        if (existing is null)
        {
            await JoinHouseholdAsync(context, household.Id, cancellationToken);
        }

        return (await FindAsync(context, cancellationToken))!;
    }

    public async Task<HouseholdDto> RenameAsync(string name, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Household household = await RequireAsync(context, cancellationToken);

        household.Name = HouseholdAccess.RequiredText(name, "The name", Household.NameMaxLength);
        await context.SaveChangesAsync(cancellationToken);

        return (await FindAsync(context, cancellationToken))!;
    }

    public async Task<HouseholdDto> NewInviteCodeAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Household household = await RequireAsync(context, cancellationToken);

        household.InviteCode = await UniqueCodeAsync(context, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return (await FindAsync(context, cancellationToken))!;
    }

    public async Task<HouseholdDto> SetMyColorAsync(string color, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        string normalized = ColorHex.Normalize(color) ?? throw new ArgumentException("The color is required.");

        HouseholdMember member = await context.HouseholdMembers.FirstOrDefaultAsync(m => m.UserId == context.UserId, cancellationToken)
            ?? throw new ArgumentException(HouseholdAccess.RequiredMessage);

        member.Color = normalized;
        await context.SaveChangesAsync(cancellationToken);

        return (await FindAsync(context, cancellationToken))!;
    }

    public async Task LeaveAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        string userId = context.UserId;

        HouseholdMember? member = await context.HouseholdMembers.FirstOrDefaultAsync(m => m.UserId == userId, cancellationToken);

        if (member is null)
        {
            return;
        }

        Guid householdId = member.HouseholdId;
        context.HouseholdMembers.Remove(member);
        await context.SaveChangesAsync(cancellationToken);

        if (await context.HouseholdMembers.AnyAsync(m => m.HouseholdId == householdId, cancellationToken))
        {
            return;
        }

        await context.ShoppingLists.Where(l => l.HouseholdId == householdId).ExecuteDeleteAsync(cancellationToken);
        await context.HomeProjects.Where(p => p.HouseholdId == householdId).ExecuteDeleteAsync(cancellationToken);
        await context.FinanceTransactions.Where(t => t.HouseholdId == householdId).ExecuteDeleteAsync(cancellationToken);
        await context.SavingsGoals.Where(g => g.HouseholdId == householdId).ExecuteDeleteAsync(cancellationToken);
        await context.FamilyEvents.Where(e => e.HouseholdId == householdId).ExecuteDeleteAsync(cancellationToken);
        await context.WeddingDetails.Where(w => w.HouseholdId == householdId).ExecuteDeleteAsync(cancellationToken);
        await context.WeddingGuests.Where(g => g.HouseholdId == householdId).ExecuteDeleteAsync(cancellationToken);
        await context.WeddingPayments.Where(p => p.HouseholdId == householdId).ExecuteDeleteAsync(cancellationToken);
        await context.WeddingCategories.Where(c => c.HouseholdId == householdId).ExecuteDeleteAsync(cancellationToken);
        await context.WeddingVendors.Where(v => v.HouseholdId == householdId).ExecuteDeleteAsync(cancellationToken);
        await context.WeddingTasks.Where(t => t.HouseholdId == householdId).ExecuteDeleteAsync(cancellationToken);
        await context.HouseholdInvites.Where(i => i.HouseholdId == householdId).ExecuteDeleteAsync(cancellationToken);
        await context.Households.Where(h => h.Id == householdId).ExecuteDeleteAsync(cancellationToken);
        await PurgeHouseholdAgendaAsync(context, householdId, cancellationToken);
    }

    public async Task<CasaDashboard> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);
        (_, string email) = await HouseholdAccess.GetCurrentUserAsync(context, cancellationToken);
        DateTime today = time.GetLocalNow().DateTime.Date;

        CasaDashboard dashboard = new()
        {
            Finance = await FinanceService.BuildSummaryAsync(context, householdId, today.Year, today.Month, cancellationToken),
            Lists = await ShoppingService.AccessibleListsAsync(context, householdId, email, cancellationToken)
        };

        dashboard.PendingShoppingItems = dashboard.Lists.Sum(l => l.PendingCount);

        List<HomeProject> open = await context.HomeProjects.AsNoTracking().Include(p => p.Tasks)
            .Where(p => p.HouseholdId == householdId && p.Status != HomeStatus.Done)
            .ToListAsync(cancellationToken);

        dashboard.OpenProjects = open.Count;
        dashboard.TopProjects = [.. open.OrderByDescending(p => p.Priority).ThenBy(p => p.DueDate ?? DateOnly.MaxValue).Take(4)];

        List<FamilyEvent> events = await context.FamilyEvents.AsNoTracking().Where(e => e.HouseholdId == householdId).ToListAsync(cancellationToken);

        dashboard.Upcoming = [.. EventExpander.Expand(events.Where(e => e.Kind != FamilyEventKind.Birthday), today, today.AddDays(DashboardUpcomingDays)).OrderBy(o => o.Start).Take(6)];
        dashboard.Birthdays = [.. EventExpander.Expand(events.Where(e => e.Kind == FamilyEventKind.Birthday), today, today.AddDays(DashboardBirthdayDays)).OrderBy(o => o.Start).Take(4)];
        dashboard.Goals = await context.SavingsGoals.AsNoTracking().Where(g => g.HouseholdId == householdId).OrderBy(g => g.CreatedAt).ToListAsync(cancellationToken);
        dashboard.WeddingDate = await context.WeddingDetails.AsNoTracking().Where(w => w.HouseholdId == householdId).Select(w => w.Date).FirstOrDefaultAsync(cancellationToken);

        return dashboard;
    }

    private static async Task PurgeHouseholdAgendaAsync(AgenderDbContext context, Guid householdId, CancellationToken cancellationToken)
    {
        context.UserId = IAgendaScope.HouseholdPrefix + householdId;

        await context.TimeEntries.ExecuteDeleteAsync(cancellationToken);
        await context.Tasks.ExecuteDeleteAsync(cancellationToken);
        await context.Projects.ExecuteDeleteAsync(cancellationToken);
        await context.Tags.ExecuteDeleteAsync(cancellationToken);
        await context.DayNotes.ExecuteDeleteAsync(cancellationToken);
        await context.Agendas.ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<HouseholdDto?> AcceptInviteAsync(AgenderDbContext context, CancellationToken cancellationToken)
    {
        (_, string email) = await HouseholdAccess.GetCurrentUserAsync(context, cancellationToken);

        if (email.Length == 0)
        {
            return null;
        }

        DateTime now = time.GetUtcNow().UtcDateTime;
        HouseholdInvite? invite = await context.HouseholdInvites
            .Where(i => i.Email == email && i.ExpiresAt > now)
            .OrderBy(i => i.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (invite is null)
        {
            return null;
        }

        await JoinHouseholdAsync(context, invite.HouseholdId, cancellationToken);
        await context.HouseholdInvites.Where(i => i.Email == email).ExecuteDeleteAsync(cancellationToken);

        return await FindAsync(context, cancellationToken);
    }

    private static async Task JoinHouseholdAsync(AgenderDbContext context, Guid householdId, CancellationToken cancellationToken)
    {
        List<string> used = await context.HouseholdMembers.Where(m => m.HouseholdId == householdId).Select(m => m.Color).ToListAsync(cancellationToken);
        string color = CasaCatalog.MemberColors.FirstOrDefault(c => !used.Contains(c)) ?? CasaCatalog.MemberColors[0];

        context.HouseholdMembers.Add(new HouseholdMember { HouseholdId = householdId, UserId = context.UserId, Color = color });
        await context.SaveChangesAsync(cancellationToken);

        (string displayName, string email) = await HouseholdAccess.GetCurrentUserAsync(context, cancellationToken);
        await context.HouseholdInvites.Where(i => i.HouseholdId == householdId && i.Email == email).ExecuteDeleteAsync(cancellationToken);
        await FinancePeople.OnMemberJoinedAsync(context, householdId, context.UserId, displayName, color, cancellationToken);
    }

    private static async Task<Household> RequireAsync(AgenderDbContext context, CancellationToken cancellationToken)
    {
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        return await context.Households.FirstAsync(h => h.Id == householdId, cancellationToken);
    }

    private async Task<HouseholdDto?> FindAsync(AgenderDbContext context, CancellationToken cancellationToken)
    {
        string userId = context.UserId;

        Guid? householdId = await context.HouseholdMembers.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => (Guid?)m.HouseholdId)
            .FirstOrDefaultAsync(cancellationToken);

        if (householdId is null)
        {
            return null;
        }

        Household household = await context.Households.AsNoTracking().FirstAsync(h => h.Id == householdId, cancellationToken);

        List<MemberDto> members = await (
            from member in context.HouseholdMembers.AsNoTracking()
            join user in context.Users.AsNoTracking() on member.UserId equals user.Id
            where member.HouseholdId == householdId
            orderby member.JoinedAt
            select new MemberDto(user.Id, user.DisplayName, user.Email ?? string.Empty, member.Color)).ToListAsync(cancellationToken);

        DateTime now = time.GetUtcNow().UtcDateTime;
        List<InviteDto> invites = (await context.HouseholdInvites.AsNoTracking()
                .Where(i => i.HouseholdId == householdId && i.ExpiresAt > now)
                .ToListAsync(cancellationToken))
            .OrderBy(i => i.CreatedAt)
            .Select(i => new InviteDto(i.Id, i.Email, i.ExpiresAt))
            .ToList();

        return new HouseholdDto(household.Id, household.Name, household.InviteCode, userId, members, invites);
    }

    private static async Task<string> UniqueCodeAsync(AgenderDbContext context, CancellationToken cancellationToken)
    {
        string code;

        do
        {
            code = CasaCatalog.NewInviteCode();
        }
        while (await context.Households.AnyAsync(h => h.InviteCode == code, cancellationToken));

        return code;
    }
}

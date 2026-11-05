using Agender.Infrastructure.Data;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

public class CleaningService(IUserDbContextFactory contextFactory, TimeProvider time) : ICleaningService
{
    private const int MaxHistoryDays = 400;

    public async Task<CleaningOverview> GetAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);
        DateOnly today = Today();

        return new CleaningOverview
        {
            Today = today,
            Rooms = await context.CleaningRooms.AsNoTracking().Where(r => r.HouseholdId == householdId)
                .OrderBy(r => r.Order).ThenBy(r => r.Name).ToListAsync(cancellationToken),
            Chores = await BuildStatusesAsync(context, householdId, today, cancellationToken)
        };
    }

    public async Task<CleaningRoom> SaveRoomAsync(CleaningRoom room, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        CleaningRoom? target = await context.CleaningRooms.FirstOrDefaultAsync(r => r.Id == room.Id && r.HouseholdId == householdId, cancellationToken);

        if (target is null)
        {
            if (await context.CleaningRooms.AnyAsync(r => r.Id == room.Id, cancellationToken))
            {
                throw new NotFoundException($"Room {room.Id} was not found.");
            }

            target = new CleaningRoom
            {
                Id = room.Id,
                HouseholdId = householdId,
                Order = (await context.CleaningRooms.Where(r => r.HouseholdId == householdId).Select(r => (int?)r.Order).MaxAsync(cancellationToken) ?? -1) + 1
            };
            context.CleaningRooms.Add(target);
        }

        target.Name = HouseholdAccess.RequiredText(room.Name, "The name", CleaningRoom.NameMaxLength);
        target.Emoji = CleaningCatalog.RoomEmojis.Contains(room.Emoji) ? room.Emoji : CleaningCatalog.RoomEmojis[0];
        target.Color = HouseholdAccess.NormalizeColor(room.Color, CasaCatalog.ListColors[0]);
        await context.SaveChangesAsync(cancellationToken);

        return target;
    }

    public async Task DeleteRoomAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        if (await context.CleaningRooms.Where(r => r.Id == id && r.HouseholdId == householdId).ExecuteDeleteAsync(cancellationToken) == 0)
        {
            throw new NotFoundException($"Room {id} was not found.");
        }
    }

    public async Task<CleaningChore> SaveChoreAsync(CleaningChore chore, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        if (chore.Every < 1 || chore.Every > CleaningChore.MaxEvery || !Enum.IsDefined(chore.Unit))
        {
            throw new ArgumentException("The frequency is out of range.");
        }

        if (!await context.CleaningRooms.AnyAsync(r => r.Id == chore.RoomId && r.HouseholdId == householdId, cancellationToken))
        {
            throw new ArgumentException("The room does not belong to the household.");
        }

        CleaningChore? target = await context.CleaningChores.FirstOrDefaultAsync(c => c.Id == chore.Id && c.HouseholdId == householdId, cancellationToken);

        if (target is null)
        {
            if (await context.CleaningChores.AnyAsync(c => c.Id == chore.Id, cancellationToken))
            {
                throw new NotFoundException($"Chore {chore.Id} was not found.");
            }

            target = new CleaningChore { Id = chore.Id, HouseholdId = householdId };
            context.CleaningChores.Add(target);
        }

        target.RoomId = chore.RoomId;
        target.Title = HouseholdAccess.RequiredText(chore.Title, "The title", CleaningChore.TitleMaxLength);
        target.Every = chore.Every;
        target.Unit = chore.Unit;
        await context.SaveChangesAsync(cancellationToken);

        return target;
    }

    public async Task DeleteChoreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        if (await context.CleaningChores.Where(c => c.Id == id && c.HouseholdId == householdId).ExecuteDeleteAsync(cancellationToken) == 0)
        {
            throw new NotFoundException($"Chore {id} was not found.");
        }
    }

    public async Task CompleteAsync(Guid choreId, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);
        DateOnly today = Today();

        await RequireChoreAsync(context, choreId, householdId, cancellationToken);

        if (await context.CleaningCompletions.AnyAsync(c => c.ChoreId == choreId && c.Date == today, cancellationToken))
        {
            return;
        }

        context.CleaningCompletions.Add(new CleaningCompletion { HouseholdId = householdId, ChoreId = choreId, Date = today, UserId = context.UserId });
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UndoAsync(Guid choreId, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);
        DateOnly today = Today();

        await RequireChoreAsync(context, choreId, householdId, cancellationToken);
        await context.CleaningCompletions.Where(c => c.ChoreId == choreId && c.HouseholdId == householdId && c.Date == today).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<List<CleaningHistoryEntry>> GetHistoryAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        if (to < from || to.DayNumber - from.DayNumber > MaxHistoryDays)
        {
            throw new ArgumentException("The history range is invalid.");
        }

        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        List<CleaningCompletion> completions = await context.CleaningCompletions.AsNoTracking()
            .Where(c => c.HouseholdId == householdId && c.Date >= from && c.Date <= to)
            .OrderBy(c => c.Date).ToListAsync(cancellationToken);

        List<string> userIds = [.. completions.Select(c => c.UserId).Distinct()];
        Dictionary<string, string> names = await context.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

        return [.. completions.Select(c => new CleaningHistoryEntry { ChoreId = c.ChoreId, Date = c.Date, DoneBy = names.GetValueOrDefault(c.UserId) })];
    }

    internal static async Task<List<CleaningChoreStatus>> BuildStatusesAsync(AgenderDbContext context, Guid householdId, DateOnly today, CancellationToken cancellationToken)
    {
        List<CleaningChore> chores = await context.CleaningChores.AsNoTracking().Where(c => c.HouseholdId == householdId)
            .OrderBy(c => c.CreatedAt).ToListAsync(cancellationToken);

        List<CleaningCompletion> latest = [.. (await context.CleaningCompletions.AsNoTracking().Where(c => c.HouseholdId == householdId).ToListAsync(cancellationToken))
            .GroupBy(c => c.ChoreId)
            .Select(g => g.OrderByDescending(c => c.Date).ThenByDescending(c => c.CompletedAt).First())];

        List<string> userIds = [.. latest.Select(c => c.UserId).Distinct()];
        Dictionary<string, string> names = await context.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

        return [.. chores.Select(chore =>
        {
            CleaningCompletion? last = latest.FirstOrDefault(c => c.ChoreId == chore.Id);
            return CleaningSchedule.Evaluate(chore, last, last is null ? null : names.GetValueOrDefault(last.UserId), today);
        })];
    }

    private static async Task RequireChoreAsync(AgenderDbContext context, Guid choreId, Guid householdId, CancellationToken cancellationToken)
    {
        if (!await context.CleaningChores.AnyAsync(c => c.Id == choreId && c.HouseholdId == householdId, cancellationToken))
        {
            throw new NotFoundException($"Chore {choreId} was not found.");
        }
    }

    private DateOnly Today() => DateOnly.FromDateTime(time.GetLocalNow().DateTime);
}

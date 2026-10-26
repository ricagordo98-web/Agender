using Agender.Infrastructure.Data;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

public class FamilyEventService(IUserDbContextFactory contextFactory) : IFamilyEventService
{
    private const int MaxRangeDays = 800;

    public async Task<IReadOnlyList<EventOccurrence>> GetAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        if (to <= from || (to - from).TotalDays > MaxRangeDays)
        {
            throw new ArgumentException("The date range is not valid.");
        }

        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        List<FamilyEvent> events = await context.FamilyEvents.AsNoTracking()
            .Where(e => e.HouseholdId == householdId && (e.Recurrence != EventRecurrence.None || e.Start < to))
            .ToListAsync(cancellationToken);

        return [.. EventExpander.Expand(events, from, to).OrderBy(o => o.Start)];
    }

    public async Task<FamilyEvent> CreateAsync(FamilyEvent familyEvent, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        FamilyEvent created = new() { HouseholdId = householdId };
        await ApplyAsync(context, householdId, created, familyEvent, cancellationToken);

        context.FamilyEvents.Add(created);
        await context.SaveChangesAsync(cancellationToken);

        return created;
    }

    public async Task UpdateAsync(FamilyEvent familyEvent, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        FamilyEvent stored = await context.FamilyEvents.FirstOrDefaultAsync(e => e.Id == familyEvent.Id && e.HouseholdId == householdId, cancellationToken)
            ?? throw new NotFoundException($"Event {familyEvent.Id} was not found.");

        await ApplyAsync(context, householdId, stored, familyEvent, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        int deleted = await context.FamilyEvents.Where(e => e.Id == id && e.HouseholdId == householdId).ExecuteDeleteAsync(cancellationToken);

        if (deleted == 0)
        {
            throw new NotFoundException($"Event {id} was not found.");
        }
    }

    private static async Task ApplyAsync(AgenderDbContext context, Guid householdId, FamilyEvent target, FamilyEvent source, CancellationToken cancellationToken)
    {
        bool isBirthday = source.Kind == FamilyEventKind.Birthday;
        bool allDay = isBirthday || source.AllDay;
        DateTime? end = allDay ? null : source.End;

        if (end is DateTime finish && finish < source.Start)
        {
            throw new ArgumentException("The end must not be before the start.");
        }

        (string? memberId, string? memberName) = await HouseholdAccess.ResolveMemberAsync(context, householdId, source.MemberId, fallbackToCurrent: false, cancellationToken);

        target.Title = HouseholdAccess.RequiredText(source.Title, "The title", FamilyEvent.TitleMaxLength);
        target.Start = allDay ? source.Start.Date : source.Start;
        target.End = end;
        target.AllDay = allDay;
        target.Kind = source.Kind;
        target.Recurrence = isBirthday ? EventRecurrence.Yearly : source.Recurrence;
        target.Location = HouseholdAccess.OptionalText(source.Location, "The location", FamilyEvent.LocationMaxLength);
        target.Notes = HouseholdAccess.OptionalText(source.Notes, "The notes", FamilyEvent.NotesMaxLength);
        target.MemberId = memberId;
        target.MemberName = memberName;
    }
}

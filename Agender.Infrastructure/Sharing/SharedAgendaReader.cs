using Agender.Infrastructure.Data;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Sharing;

internal static class SharedAgendaReader
{
    private const int MaxRangeDays = 400;

    public static async Task<SharedAgendaDto> ReadAsync(
        AgenderDbContext context,
        string ownerId,
        string ownerName,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        if (to < from || to.DayNumber - from.DayNumber > MaxRangeDays)
        {
            throw new ArgumentException($"The range must be ascending and span at most {MaxRangeDays} days.");
        }

        context.UserId = ownerId;

        List<TimeEntry> entries = await context.TimeEntries
            .AsNoTracking()
            .Include(e => e.Tags)
            .Where(e => e.Date >= from && e.Date <= to)
            .OrderBy(e => e.Date)
            .ThenBy(e => e.StartMinute)
            .ToListAsync(cancellationToken);

        List<Guid> projectIds = [.. entries.Where(e => e.ProjectId is not null).Select(e => e.ProjectId!.Value).Distinct()];
        List<Project> projects = await context.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id)).ToListAsync(cancellationToken);
        List<DayNote> notes = await context.DayNotes.AsNoTracking().Where(n => n.Date >= from && n.Date <= to).ToListAsync(cancellationToken);

        return new SharedAgendaDto(
            ownerName,
            [.. entries.Select(e => e.ToDto())],
            [.. projects.Select(p => p.ToDto())],
            [.. notes.Select(n => n.ToDto())]);
    }
}

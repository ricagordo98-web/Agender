using Agender.Infrastructure.Data;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

public class TimeEntryService(IUserDbContextFactory contextFactory) : ITimeEntryService
{
    private const int MaxSearchPageSize = 50;

    public async Task<IReadOnlyList<TimeEntry>> GetRangeAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);

        return await context.TimeEntries
            .AsNoTracking()
            .Include(e => e.Tags)
            .Where(e => e.Date >= from && e.Date <= to)
            .OrderBy(e => e.Date)
            .ThenBy(e => e.StartMinute)
            .ToListAsync(cancellationToken);
    }

    public async Task<TimeEntry> CreateAsync(TimeEntry entry, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> tagNames = Normalize(entry);

        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        await ValidateReferencesAsync(context, entry, cancellationToken);
        entry.AgendaId = await ResolveAgendaAsync(context, entry, cancellationToken);

        entry.Tags = await OwnershipGuard.ResolveTagsAsync(context, tagNames, cancellationToken);
        context.TimeEntries.Add(entry);
        await context.SaveChangesAsync(cancellationToken);

        return entry;
    }

    public async Task UpdateAsync(TimeEntry entry, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> tagNames = Normalize(entry);

        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        TimeEntry existing = await context.TimeEntries.Include(e => e.Tags).FirstOrDefaultAsync(e => e.Id == entry.Id, cancellationToken)
            ?? throw new NotFoundException($"Time entry {entry.Id} was not found.");

        if (existing.IsCompleted && entry.IsCompleted)
        {
            throw new ArgumentException("A completed entry is locked. Reopen it to edit.");
        }

        await ValidateReferencesAsync(context, entry, cancellationToken);
        entry.AgendaId = await ResolveAgendaAsync(context, entry, cancellationToken);

        string? googleEventId = existing.GoogleEventId;

        context.Entry(existing).CurrentValues.SetValues(entry);
        existing.GoogleEventId = googleEventId;
        existing.Tags.Clear();
        existing.Tags.AddRange(await OwnershipGuard.ResolveTagsAsync(context, tagNames, cancellationToken));
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        TimeEntry? existing = await context.TimeEntries.Include(e => e.Tags).FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (existing is null)
        {
            return;
        }

        context.TimeEntries.Remove(existing);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TimeEntry>> SearchAsync(string query, int skip, int take, CancellationToken cancellationToken = default)
    {
        string text = query.Trim();
        if (text.Length == 0)
        {
            return [];
        }

        string pattern = $"%{text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";

        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);

        return await context.TimeEntries
            .AsNoTracking()
            .Include(e => e.Tags)
            .Where(e => EF.Functions.Like(e.Description, pattern, "\\")
                || e.Tags.Any(t => EF.Functions.Like(t.Name, pattern, "\\"))
                || (e.ProjectId != null && context.Projects.Any(p => p.Id == e.ProjectId && EF.Functions.Like(p.Name, pattern, "\\"))))
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.StartMinute)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, MaxSearchPageSize))
            .ToListAsync(cancellationToken);
    }

    private static IReadOnlyList<string> Normalize(TimeEntry entry)
    {
        _ = entry.Slot;
        entry.Description = entry.Description.Trim();
        entry.CustomColor = ColorHex.Normalize(entry.CustomColor);

        if (entry.Description.Length > TimeEntry.DescriptionMaxLength)
        {
            throw new ArgumentException($"Description exceeds {TimeEntry.DescriptionMaxLength} characters.");
        }

        IReadOnlyList<string> names = Tag.CleanNames(entry.Tags.Select(t => t.Name));
        entry.Tags = [];

        return names;
    }

    private static async Task<Guid> ResolveAgendaAsync(AgenderDbContext context, TimeEntry entry, CancellationToken cancellationToken)
    {
        Guid? fromProject = entry.ProjectId is Guid projectId
            ? await context.Projects.Where(p => p.Id == projectId).Select(p => (Guid?)p.AgendaId).FirstOrDefaultAsync(cancellationToken)
            : null;

        if (fromProject is Guid projectAgenda && projectAgenda != Guid.Empty)
        {
            return projectAgenda;
        }

        Guid? fromTask = entry.TaskItemId is Guid taskId
            ? await context.Tasks.Where(t => t.Id == taskId).Select(t => (Guid?)t.AgendaId).FirstOrDefaultAsync(cancellationToken)
            : null;

        if (fromTask is Guid taskAgenda && taskAgenda != Guid.Empty)
        {
            return taskAgenda;
        }

        return await OwnershipGuard.ResolveAgendaAsync(context, entry.AgendaId, cancellationToken);
    }

    private static async Task ValidateReferencesAsync(AgenderDbContext context, TimeEntry entry, CancellationToken cancellationToken)
    {
        await OwnershipGuard.EnsureProjectAsync(context, entry.ProjectId, cancellationToken);
        await OwnershipGuard.EnsureTaskAsync(context, entry.TaskItemId, cancellationToken);
    }
}

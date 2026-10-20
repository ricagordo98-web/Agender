using Agender.Infrastructure.Data;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

public class TimerService(IUserDbContextFactory contextFactory) : ITimerService
{
    public async Task<ActiveTimer?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);

        return await context.ActiveTimers.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ActiveTimer> StartAsync(ActiveTimer timer, CancellationToken cancellationToken = default)
    {
        timer.Description = timer.Description.Trim();

        if (timer.Description.Length > TimeEntry.DescriptionMaxLength)
        {
            throw new ArgumentException($"Description exceeds {TimeEntry.DescriptionMaxLength} characters.");
        }

        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);

        if (await context.ActiveTimers.AnyAsync(cancellationToken))
        {
            throw new ArgumentException("A timer is already running.");
        }

        await OwnershipGuard.EnsureProjectAsync(context, timer.ProjectId, cancellationToken);
        await OwnershipGuard.EnsureTaskAsync(context, timer.TaskItemId, cancellationToken);
        timer.AgendaId = await ResolveAgendaAsync(context, timer, cancellationToken);

        context.ActiveTimers.Add(timer);
        await context.SaveChangesAsync(cancellationToken);

        return timer;
    }

    public async Task<IReadOnlyList<TimeEntry>> StopAsync(DateTime stoppedAt, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);

        ActiveTimer timer = await context.ActiveTimers.FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("There is no running timer.");

        (EntryColor color, string? customColor) = await ResolveColorAsync(context, timer, cancellationToken);
        int rounding = await context.UserSettings.AsNoTracking().Select(s => s.TimerRoundingMinutes).FirstOrDefaultAsync(cancellationToken);

        if (rounding > 0)
        {
            int minutes = (int)Math.Ceiling(Math.Max(0, (stoppedAt - timer.StartedAt).TotalMinutes) / rounding) * rounding;
            stoppedAt = timer.StartedAt.AddMinutes(Math.Max(minutes, rounding));
        }

        List<TimeEntry> entries =
        [
            .. TimerSplitter.Split(timer.StartedAt, stoppedAt).Select(segment => new TimeEntry
            {
                Date = segment.Date,
                StartMinute = segment.StartMinute,
                EndMinute = segment.EndMinute,
                Description = timer.Description,
                Color = color,
                CustomColor = customColor,
                TaskItemId = timer.TaskItemId,
                ProjectId = timer.ProjectId,
                AgendaId = timer.AgendaId
            })
        ];

        context.TimeEntries.AddRange(entries);
        context.ActiveTimers.Remove(timer);
        await context.SaveChangesAsync(cancellationToken);

        return entries;
    }

    private static async Task<Guid> ResolveAgendaAsync(AgenderDbContext context, ActiveTimer timer, CancellationToken cancellationToken)
    {
        if (timer.ProjectId is Guid projectId)
        {
            Guid agenda = await context.Projects.Where(p => p.Id == projectId).Select(p => p.AgendaId).FirstAsync(cancellationToken);

            if (agenda != Guid.Empty)
            {
                return agenda;
            }
        }

        if (timer.TaskItemId is Guid taskId)
        {
            Guid agenda = await context.Tasks.Where(t => t.Id == taskId).Select(t => t.AgendaId).FirstAsync(cancellationToken);

            if (agenda != Guid.Empty)
            {
                return agenda;
            }
        }

        return await OwnershipGuard.ResolveAgendaAsync(context, timer.AgendaId, cancellationToken);
    }

    private static async Task<(EntryColor Color, string? CustomColor)> ResolveColorAsync(AgenderDbContext context, ActiveTimer timer, CancellationToken cancellationToken)
    {
        if (timer.TaskItemId is not null)
        {
            TaskItem? task = await context.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == timer.TaskItemId, cancellationToken);

            if (task is not null)
            {
                return (task.Color, task.CustomColor);
            }
        }

        if (timer.ProjectId is not null)
        {
            Project? project = await context.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == timer.ProjectId, cancellationToken);

            if (project is not null)
            {
                return (project.Color, project.CustomColor);
            }
        }

        return (new TimeEntry().Color, null);
    }
}

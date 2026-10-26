using Agender.Infrastructure.Data;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

public class TaskService(IAgendaDbContextFactory contextFactory) : ITaskService
{
    public async Task<IReadOnlyList<TaskItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);

        var rows = await context.Tasks
            .AsNoTracking()
            .Select(t => new { Task = t, IsScheduled = context.TimeEntries.Any(e => e.TaskItemId == t.Id) })
            .ToListAsync(cancellationToken);

        List<TaskItem> tasks = [];

        foreach (var row in rows)
        {
            row.Task.IsScheduled = row.IsScheduled;
            tasks.Add(row.Task);
        }

        return tasks
            .OrderBy(t => t.IsCompleted)
            .ThenByDescending(t => t.CreatedAt)
            .ToList();
    }

    public async Task<TaskItem> CreateAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        Normalize(task);

        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        await OwnershipGuard.EnsureProjectAsync(context, task.ProjectId, cancellationToken);
        task.AgendaId = await ResolveAgendaAsync(context, task, task.AgendaId, cancellationToken);
        context.Tasks.Add(task);
        await context.SaveChangesAsync(cancellationToken);

        return task;
    }

    public async Task UpdateAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        Normalize(task);

        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        TaskItem existing = await context.Tasks.FirstOrDefaultAsync(t => t.Id == task.Id, cancellationToken)
            ?? throw new NotFoundException($"Task {task.Id} was not found.");

        await OwnershipGuard.EnsureProjectAsync(context, task.ProjectId, cancellationToken);
        Guid agendaId = await ResolveAgendaAsync(context, task, existing.AgendaId, cancellationToken);
        context.Entry(existing).CurrentValues.SetValues(task);
        existing.AgendaId = agendaId;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        await context.Tasks.Where(t => t.Id == id).ExecuteDeleteAsync(cancellationToken);
    }

    private static async Task<Guid> ResolveAgendaAsync(AgenderDbContext context, TaskItem task, Guid requested, CancellationToken cancellationToken)
    {
        if (task.ProjectId is Guid projectId)
        {
            Guid projectAgenda = await context.Projects.Where(p => p.Id == projectId).Select(p => p.AgendaId).FirstAsync(cancellationToken);

            if (requested != Guid.Empty && projectAgenda != Guid.Empty && projectAgenda != requested)
            {
                throw new ArgumentException("The project belongs to another agenda.");
            }

            if (projectAgenda != Guid.Empty)
            {
                return projectAgenda;
            }
        }

        return await OwnershipGuard.ResolveAgendaAsync(context, requested, cancellationToken);
    }

    private static void Normalize(TaskItem task)
    {
        task.Title = task.Title.Trim();
        task.CustomColor = ColorHex.Normalize(task.CustomColor);

        if (task.Title.Length is 0 or > TaskItem.TitleMaxLength)
        {
            throw new ArgumentException($"Title must have 1 to {TaskItem.TitleMaxLength} characters.");
        }
    }
}

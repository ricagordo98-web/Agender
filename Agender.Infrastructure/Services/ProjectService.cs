using Agender.Infrastructure.Data;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

public class ProjectService(IAgendaDbContextFactory contextFactory) : IProjectService
{
    public async Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        List<Project> projects = await context.Projects.AsNoTracking().ToListAsync(cancellationToken);

        return projects
            .OrderBy(p => p.IsArchived)
            .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<Project> CreateAsync(Project project, CancellationToken cancellationToken = default)
    {
        Normalize(project);

        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        await EnsureUniqueNameAsync(context, project, cancellationToken);
        project.AgendaId = await OwnershipGuard.ResolveAgendaAsync(context, project.AgendaId, cancellationToken);

        context.Projects.Add(project);
        await context.SaveChangesAsync(cancellationToken);

        return project;
    }

    public async Task UpdateAsync(Project project, CancellationToken cancellationToken = default)
    {
        Normalize(project);

        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Project existing = await context.Projects.FirstOrDefaultAsync(p => p.Id == project.Id, cancellationToken)
            ?? throw new NotFoundException($"Project {project.Id} was not found.");

        await EnsureUniqueNameAsync(context, project, cancellationToken);

        Guid agendaId = existing.AgendaId;
        context.Entry(existing).CurrentValues.SetValues(project);
        existing.AgendaId = agendaId;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        await context.Projects.Where(p => p.Id == id).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetMinutesByProjectAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);

        var totals = await context.TimeEntries
            .Where(e => e.Date >= from && e.Date <= to && e.ProjectId != null)
            .GroupBy(e => e.ProjectId!.Value)
            .Select(g => new { ProjectId = g.Key, Minutes = g.Sum(e => e.EndMinute - e.StartMinute) })
            .ToListAsync(cancellationToken);

        return totals.ToDictionary(t => t.ProjectId, t => t.Minutes);
    }

    private static void Normalize(Project project)
    {
        project.Name = project.Name.Trim();
        project.CustomColor = ColorHex.Normalize(project.CustomColor);
        project.ClientName = string.IsNullOrWhiteSpace(project.ClientName) ? null : project.ClientName.Trim();

        if (project.Name.Length is 0 or > Project.NameMaxLength)
        {
            throw new ArgumentException($"The project name must have 1 to {Project.NameMaxLength} characters.");
        }

        if (project.ClientName?.Length > Project.ClientNameMaxLength)
        {
            throw new ArgumentException($"The client name exceeds {Project.ClientNameMaxLength} characters.");
        }
    }

    private static async Task EnsureUniqueNameAsync(AgenderDbContext context, Project project, CancellationToken cancellationToken)
    {
        string lowered = project.Name.ToLower();

        if (await context.Projects.AnyAsync(p => p.Id != project.Id && p.Name.ToLower() == lowered, cancellationToken))
        {
            throw new ArgumentException("A project with this name already exists.");
        }
    }
}

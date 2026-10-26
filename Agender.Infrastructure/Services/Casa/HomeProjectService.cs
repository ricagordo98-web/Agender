using Agender.Infrastructure.Data;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

public class HomeProjectService(IUserDbContextFactory contextFactory) : IHomeProjectService
{
    public async Task<IReadOnlyList<HomeProject>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        List<HomeProject> projects = await context.HomeProjects.AsNoTracking().Include(p => p.Tasks).Where(p => p.HouseholdId == householdId).ToListAsync(cancellationToken);

        foreach (HomeProject project in projects)
        {
            project.Tasks = [.. project.Tasks.OrderBy(t => t.Order)];
        }

        return [.. projects.OrderByDescending(p => p.Priority).ThenBy(p => p.DueDate ?? DateOnly.MaxValue)];
    }

    public async Task<HomeProject> CreateAsync(HomeProject project, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        HomeProject created = new() { HouseholdId = householdId };
        Apply(created, project);
        created.Tasks = BuildTasks(created.Id, project.Tasks);

        context.HomeProjects.Add(created);
        await context.SaveChangesAsync(cancellationToken);

        return created;
    }

    public async Task UpdateAsync(HomeProject project, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        HomeProject stored = await context.HomeProjects.FirstOrDefaultAsync(p => p.Id == project.Id && p.HouseholdId == householdId, cancellationToken)
            ?? throw new NotFoundException($"Project {project.Id} was not found.");

        Apply(stored, project);
        stored.UpdatedAt = DateTime.UtcNow;
        List<HomeTask> tasks = BuildTasks(stored.Id, project.Tasks);

        await context.HomeTasks.Where(t => t.ProjectId == stored.Id).ExecuteDeleteAsync(cancellationToken);
        context.HomeTasks.AddRange(tasks);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        int deleted = await context.HomeProjects.Where(p => p.Id == id && p.HouseholdId == householdId).ExecuteDeleteAsync(cancellationToken);

        if (deleted == 0)
        {
            throw new NotFoundException($"Project {id} was not found.");
        }
    }

    private static void Apply(HomeProject target, HomeProject source)
    {
        target.Title = HouseholdAccess.RequiredText(source.Title, "The title", HomeProject.TitleMaxLength);
        target.Description = HouseholdAccess.OptionalText(source.Description, "The description", HomeProject.DescriptionMaxLength);
        target.Kind = source.Kind;
        target.Priority = source.Priority;
        target.Status = source.Status;
        target.Room = HouseholdAccess.OptionalText(source.Room, "The room", 40);
        target.EstimatedCost = HouseholdAccess.OptionalMoney(source.EstimatedCost, "The estimated cost");
        target.ActualCost = HouseholdAccess.OptionalMoney(source.ActualCost, "The actual cost");
        target.DueDate = source.DueDate;
        target.Link = HouseholdAccess.OptionalText(source.Link, "The link", HomeProject.LinkMaxLength);
    }

    private static List<HomeTask> BuildTasks(Guid projectId, IEnumerable<HomeTask> tasks) =>
    [
        .. tasks.Select((task, index) => new HomeTask
        {
            ProjectId = projectId,
            Title = HouseholdAccess.RequiredText(task.Title, "The task title", HomeProject.TitleMaxLength),
            Done = task.Done,
            Order = index
        })
    ];
}

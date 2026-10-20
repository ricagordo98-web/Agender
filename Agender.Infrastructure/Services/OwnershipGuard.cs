using Agender.Infrastructure.Data;
using Agender.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

internal static class OwnershipGuard
{
    public static async Task EnsureProjectAsync(AgenderDbContext context, Guid? projectId, CancellationToken cancellationToken)
    {
        if (projectId is not null && !await context.Projects.AnyAsync(p => p.Id == projectId, cancellationToken))
        {
            throw new ArgumentException("The project does not exist.");
        }
    }

    public static async Task<Guid> GetDefaultAgendaIdAsync(AgenderDbContext context, CancellationToken cancellationToken)
    {
        Guid? existing = await context.Agendas.Where(a => a.IsDefault).Select(a => (Guid?)a.Id).FirstOrDefaultAsync(cancellationToken);

        if (existing is Guid id)
        {
            return id;
        }

        bool hasActive = await context.Agendas.AnyAsync(a => a.IsActive, cancellationToken);
        Agenda created = new() { Name = Agenda.DefaultName, IsDefault = true, IsActive = !hasActive, IsVisible = true };
        context.Agendas.Add(created);
        await context.SaveChangesAsync(cancellationToken);

        return created.Id;
    }

    public static async Task<Guid> ResolveAgendaAsync(AgenderDbContext context, Guid agendaId, CancellationToken cancellationToken)
    {
        if (agendaId == Guid.Empty)
        {
            return await GetDefaultAgendaIdAsync(context, cancellationToken);
        }

        if (!await context.Agendas.AnyAsync(a => a.Id == agendaId, cancellationToken))
        {
            throw new ArgumentException("The agenda does not exist.");
        }

        return agendaId;
    }

    public static async Task EnsureTaskAsync(AgenderDbContext context, Guid? taskId, CancellationToken cancellationToken)
    {
        if (taskId is not null && !await context.Tasks.AnyAsync(t => t.Id == taskId, cancellationToken))
        {
            throw new ArgumentException("The task does not exist.");
        }
    }

    public static async Task<List<Tag>> ResolveTagsAsync(AgenderDbContext context, IReadOnlyList<string> names, CancellationToken cancellationToken)
    {
        if (names.Count == 0)
        {
            return [];
        }

        string[] normalized = [.. names.Select(Tag.Normalize)];
        List<Tag> existing = await context.Tags.Where(t => normalized.Contains(t.NormalizedName)).ToListAsync(cancellationToken);
        List<Tag> resolved = [];

        foreach (string name in names)
        {
            string key = Tag.Normalize(name);
            Tag? tag = existing.Find(t => t.NormalizedName == key);

            if (tag is null)
            {
                tag = new Tag { Name = name, NormalizedName = key };
                context.Tags.Add(tag);
                existing.Add(tag);
            }

            resolved.Add(tag);
        }

        return resolved;
    }
}

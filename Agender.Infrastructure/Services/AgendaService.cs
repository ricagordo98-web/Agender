using Agender.Infrastructure.Data;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

public class AgendaService(IAgendaDbContextFactory contextFactory) : IAgendaService
{
    public async Task<IReadOnlyList<Agenda>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        await OwnershipGuard.GetDefaultAgendaIdAsync(context, cancellationToken);

        List<Agenda> agendas = await context.Agendas.AsNoTracking().ToListAsync(cancellationToken);

        return [.. agendas.OrderByDescending(a => a.IsDefault).ThenBy(a => a.CreatedAt)];
    }

    public async Task<Agenda> CreateAsync(Agenda agenda, CancellationToken cancellationToken = default)
    {
        Normalize(agenda);
        agenda.IsDefault = false;
        agenda.IsActive = false;
        agenda.IsVisible = true;

        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        await OwnershipGuard.GetDefaultAgendaIdAsync(context, cancellationToken);
        await EnsureUniqueNameAsync(context, agenda, cancellationToken);

        context.Agendas.Add(agenda);
        await context.SaveChangesAsync(cancellationToken);

        return agenda;
    }

    public async Task UpdateAsync(Agenda agenda, CancellationToken cancellationToken = default)
    {
        Normalize(agenda);

        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Agenda existing = await context.Agendas.FirstOrDefaultAsync(a => a.Id == agenda.Id, cancellationToken)
            ?? throw new NotFoundException($"Agenda {agenda.Id} was not found.");

        if (!agenda.IsVisible && existing.IsActive)
        {
            throw new ArgumentException("The active agenda cannot be hidden.");
        }

        await EnsureUniqueNameAsync(context, agenda, cancellationToken);

        existing.Name = agenda.Name;
        existing.Color = agenda.Color;
        existing.CustomColor = agenda.CustomColor;
        existing.IsVisible = agenda.IsVisible;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Agenda target = await context.Agendas.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Agenda {id} was not found.");

        await context.Agendas.Where(a => a.Id != id && a.IsActive).ExecuteUpdateAsync(set => set.SetProperty(a => a.IsActive, false), cancellationToken);

        target.IsActive = true;
        target.IsVisible = true;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Agenda existing = await context.Agendas.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Agenda {id} was not found.");

        if (existing.IsDefault)
        {
            throw new ArgumentException("The default agenda cannot be deleted.");
        }

        Guid defaultId = await OwnershipGuard.GetDefaultAgendaIdAsync(context, cancellationToken);

        await context.Projects.Where(p => p.AgendaId == id).ExecuteUpdateAsync(set => set.SetProperty(p => p.AgendaId, defaultId), cancellationToken);
        await context.Tasks.Where(t => t.AgendaId == id).ExecuteUpdateAsync(set => set.SetProperty(t => t.AgendaId, defaultId), cancellationToken);
        await context.TimeEntries.Where(e => e.AgendaId == id).ExecuteUpdateAsync(set => set.SetProperty(e => e.AgendaId, defaultId), cancellationToken);
        await context.ActiveTimers.Where(t => t.AgendaId == id).ExecuteUpdateAsync(set => set.SetProperty(t => t.AgendaId, defaultId), cancellationToken);

        if (existing.IsActive)
        {
            await context.Agendas.Where(a => a.Id == defaultId).ExecuteUpdateAsync(set => set.SetProperty(a => a.IsActive, true).SetProperty(a => a.IsVisible, true), cancellationToken);
        }

        await context.Agendas.Where(a => a.Id == id).ExecuteDeleteAsync(cancellationToken);
    }

    private static void Normalize(Agenda agenda)
    {
        agenda.Name = agenda.Name.Trim();
        agenda.CustomColor = ColorHex.Normalize(agenda.CustomColor);

        if (agenda.Name.Length is 0 or > Agenda.NameMaxLength)
        {
            throw new ArgumentException($"The agenda name must have 1 to {Agenda.NameMaxLength} characters.");
        }
    }

    private static async Task EnsureUniqueNameAsync(AgenderDbContext context, Agenda agenda, CancellationToken cancellationToken)
    {
        string lowered = agenda.Name.ToLower();

        if (await context.Agendas.AnyAsync(a => a.Id != agenda.Id && a.Name.ToLower() == lowered, cancellationToken))
        {
            throw new ArgumentException("An agenda with this name already exists.");
        }
    }
}

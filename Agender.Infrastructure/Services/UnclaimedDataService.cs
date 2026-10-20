using Agender.Infrastructure.Data;
using Agender.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

public interface IUnclaimedDataService
{
    Task ClaimAsync(string userId, CancellationToken cancellationToken = default);
}

public class UnclaimedDataService(IDbContextFactory<AgenderDbContext> contextFactory) : IUnclaimedDataService
{
    public async Task ClaimAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.UserId = AgenderDbContext.UnclaimedUserId;

        await ClaimAgendasAsync(context, userId, cancellationToken);

        await context.TimeEntries.ExecuteUpdateAsync(s => s.SetProperty(e => EF.Property<string>(e, AgenderDbContext.UserIdProperty), userId), cancellationToken);
        await context.Tasks.ExecuteUpdateAsync(s => s.SetProperty(t => EF.Property<string>(t, AgenderDbContext.UserIdProperty), userId), cancellationToken);
        await context.Projects.ExecuteUpdateAsync(p => p.SetProperty(x => EF.Property<string>(x, AgenderDbContext.UserIdProperty), userId), cancellationToken);
        await context.Tags.ExecuteUpdateAsync(t => t.SetProperty(x => EF.Property<string>(x, AgenderDbContext.UserIdProperty), userId), cancellationToken);
        await context.DayNotes.ExecuteUpdateAsync(s => s.SetProperty(n => EF.Property<string>(n, AgenderDbContext.UserIdProperty), userId), cancellationToken);
    }

    private static async Task ClaimAgendasAsync(AgenderDbContext unclaimed, string userId, CancellationToken cancellationToken)
    {
        bool userHasAgendas = await unclaimed.Agendas.IgnoreQueryFilters()
            .AnyAsync(a => EF.Property<string>(a, AgenderDbContext.UserIdProperty) == userId, cancellationToken);

        if (!userHasAgendas)
        {
            await unclaimed.Agendas.ExecuteUpdateAsync(a => a.SetProperty(x => EF.Property<string>(x, AgenderDbContext.UserIdProperty), userId), cancellationToken);
            return;
        }

        Guid target = await unclaimed.Agendas.IgnoreQueryFilters()
            .Where(a => EF.Property<string>(a, AgenderDbContext.UserIdProperty) == userId && a.IsDefault)
            .Select(a => a.Id)
            .FirstAsync(cancellationToken);

        await unclaimed.Projects.ExecuteUpdateAsync(x => x.SetProperty(p => p.AgendaId, target), cancellationToken);
        await unclaimed.Tasks.ExecuteUpdateAsync(x => x.SetProperty(t => t.AgendaId, target), cancellationToken);
        await unclaimed.TimeEntries.ExecuteUpdateAsync(x => x.SetProperty(e => e.AgendaId, target), cancellationToken);
        await unclaimed.Agendas.ExecuteDeleteAsync(cancellationToken);
    }
}

using Agender.Infrastructure.Identity;
using Agender.Infrastructure.Realtime;
using Agender.Shared.Abstractions;
using Agender.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Data;

public class AgendaDbContextFactory(
    IDbContextFactory<AgenderDbContext> inner,
    ICurrentUser currentUser,
    IAgendaScope scope,
    IChangePublisher? publisher = null) : IAgendaDbContextFactory
{
    public async Task<AgenderDbContext> CreateAsync(CancellationToken cancellationToken = default)
    {
        string me = await currentUser.GetUserIdAsync(cancellationToken);
        AgenderDbContext context = await inner.CreateDbContextAsync(cancellationToken);

        try
        {
            context.UserId = await ResolveAsync(context, me, scope.TargetId, cancellationToken);
            context.ActorId = me;
            context.ChangePublisher = publisher;

            return context;
        }
        catch
        {
            await context.DisposeAsync();
            throw;
        }
    }

    private static async Task<string> ResolveAsync(AgenderDbContext context, string me, string? target, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(target) || target == me)
        {
            return me;
        }

        Guid? household = await context.HouseholdMembers.AsNoTracking()
            .Where(m => m.UserId == me)
            .Select(m => (Guid?)m.HouseholdId)
            .FirstOrDefaultAsync(cancellationToken);

        if (household is null)
        {
            throw new NotFoundException("The agenda is not available.");
        }

        bool allowed = target == IAgendaScope.HouseholdPrefix + household
            || await context.HouseholdMembers.AnyAsync(m => m.UserId == target && m.HouseholdId == household, cancellationToken);

        return allowed ? target : throw new NotFoundException("The agenda is not available.");
    }
}

using Agender.Infrastructure.Data;
using Agender.Shared.Abstractions;
using Agender.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Realtime;

public sealed class ChangeBus(IDbContextFactory<AgenderDbContext> contextFactory) : IChangePublisher
{
    public event Func<ChangeNotice, IReadOnlyCollection<string>, Task>? Received;

    public void Publish(string scopeId, string actorId)
    {
        using (ExecutionContext.SuppressFlow())
        {
            _ = Task.Run(() => DispatchAsync(scopeId, actorId));
        }
    }

    public async Task DispatchAsync(string scopeId, string actorId)
    {
        try
        {
            IReadOnlyCollection<string> recipients = await ResolveRecipientsAsync(scopeId, actorId);
            Func<ChangeNotice, IReadOnlyCollection<string>, Task>? handlers = Received;

            if (recipients.Count == 0 || handlers is null)
            {
                return;
            }

            ChangeNotice notice = new(scopeId, actorId);

            foreach (Func<ChangeNotice, IReadOnlyCollection<string>, Task> handler in handlers.GetInvocationList().Cast<Func<ChangeNotice, IReadOnlyCollection<string>, Task>>())
            {
                try
                {
                    await handler(notice, recipients);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
        }
    }

    private async Task<IReadOnlyCollection<string>> ResolveRecipientsAsync(string scopeId, string actorId)
    {
        await using AgenderDbContext context = await contextFactory.CreateDbContextAsync();

        List<Guid> households = [];

        if (scopeId.StartsWith(IAgendaScope.HouseholdPrefix, StringComparison.Ordinal) && Guid.TryParse(scopeId.AsSpan(IAgendaScope.HouseholdPrefix.Length), out Guid scoped))
        {
            households.Add(scoped);
        }

        households.AddRange(await context.HouseholdMembers.AsNoTracking()
            .Where(m => m.UserId == scopeId || m.UserId == actorId)
            .Select(m => m.HouseholdId)
            .ToListAsync());

        if (households.Count == 0)
        {
            return [];
        }

        List<string> members = await context.HouseholdMembers.AsNoTracking()
            .Where(m => households.Contains(m.HouseholdId))
            .Select(m => m.UserId)
            .ToListAsync();

        return [.. members.Where(id => id != actorId).Distinct()];
    }
}

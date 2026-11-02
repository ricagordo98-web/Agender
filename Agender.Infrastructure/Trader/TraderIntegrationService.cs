using Agender.Infrastructure.Data;
using Agender.Shared.Contracts;
using Agender.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Trader;

public class TraderIntegrationService(IUserDbContextFactory contextFactory, TimeProvider time) : ITraderIntegrationService
{
    public async Task<TraderIntegrationStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        TraderKey? key = await context.TraderKeys.AsNoTracking().FirstOrDefaultAsync(k => k.UserId == context.ActorId, cancellationToken);
        int count = await context.TraderTrades.CountAsync(cancellationToken);

        return new TraderIntegrationStatus(key is not null, key?.CreatedAt, key?.LastSyncAt, count);
    }

    public async Task<TraderKeyCreated> CreateKeyAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        await context.TraderKeys.Where(k => k.UserId == context.ActorId).ExecuteDeleteAsync(cancellationToken);

        string key = TraderKey.Generate();
        context.TraderKeys.Add(new TraderKey
        {
            UserId = context.ActorId,
            KeyHash = TraderKey.Hash(key),
            CreatedAt = time.GetUtcNow().UtcDateTime
        });
        await context.SaveChangesAsync(cancellationToken);

        return new TraderKeyCreated(key);
    }

    public async Task RevokeKeyAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        await context.TraderKeys.Where(k => k.UserId == context.ActorId).ExecuteDeleteAsync(cancellationToken);
    }
}

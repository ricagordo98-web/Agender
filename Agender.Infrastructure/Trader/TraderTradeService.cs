using Agender.Infrastructure.Data;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Trader;

public class TraderTradeService(IUserDbContextFactory contextFactory) : ITraderTradeService
{
    public async Task<IReadOnlyList<TraderTradeDto>> GetRangeAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);

        List<TraderTrade> trades = await context.TraderTrades.AsNoTracking()
            .Where(t => (t.IsClosed && t.ExitDate >= from && t.ExitDate <= to) || (!t.IsClosed && t.EntryDate <= to))
            .ToListAsync(cancellationToken);

        return [.. trades.OrderBy(t => t.ExitTime ?? t.EntryTime).Select(t => t.ToDto())];
    }
}

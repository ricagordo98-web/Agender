using Agender.Infrastructure.Data;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Trader;

public interface ITraderIngestService
{
    Task<TraderSyncResult> SyncAsync(TraderSyncRequest request, CancellationToken cancellationToken = default);
}

public class TraderIngestService(IAgendaDbContextFactory contextFactory, TimeProvider time) : ITraderIngestService
{
    public const int MaxTrades = 20_000;

    public async Task<TraderSyncResult> SyncAsync(TraderSyncRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Trades.Count > MaxTrades)
        {
            throw new ArgumentException($"At most {MaxTrades} trades can be synchronised at once.");
        }

        if (request.Trades.GroupBy(t => t.ExternalId).Any(g => g.Count() > 1))
        {
            throw new ArgumentException("The trade ids must be unique.");
        }

        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        TimeZoneInfo zone = time.LocalTimeZone;
        DateTime now = time.GetUtcNow().UtcDateTime;

        Dictionary<long, TraderTrade> existing = await context.TraderTrades.ToDictionaryAsync(t => t.ExternalId, cancellationToken);

        int created = 0;
        int updated = 0;

        foreach (TraderTradeInput input in request.Trades)
        {
            if (!existing.TryGetValue(input.ExternalId, out TraderTrade? trade))
            {
                trade = new TraderTrade { ExternalId = input.ExternalId };
                context.TraderTrades.Add(trade);
                existing[input.ExternalId] = trade;
                created++;
            }
            else
            {
                updated++;
            }

            Apply(trade, input, zone, now);
        }

        HashSet<long> incoming = request.Trades.Select(t => t.ExternalId).ToHashSet();
        List<TraderTrade> removed = existing.Values.Where(t => !incoming.Contains(t.ExternalId)).ToList();

        context.TraderTrades.RemoveRange(removed);

        await context.SaveChangesAsync(cancellationToken);
        await context.TraderKeys.Where(k => k.UserId == context.ActorId).ExecuteUpdateAsync(set => set.SetProperty(k => k.LastSyncAt, now), cancellationToken);

        return new TraderSyncResult(created, updated, removed.Count);
    }

    private static void Apply(TraderTrade trade, TraderTradeInput input, TimeZoneInfo zone, DateTime now)
    {
        trade.Symbol = Cut(input.Symbol, TraderTrade.ShortTextMaxLength);
        trade.Name = Cut(input.Name, TraderTrade.ShortTextMaxLength);
        trade.IsBuy = input.IsBuy;
        trade.IsClosed = input.IsClosed && input.ExitTime is not null;
        trade.EntryTime = input.EntryTime.UtcDateTime;
        trade.EntryDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(trade.EntryTime, zone));
        trade.EntryPrice = input.EntryPrice;
        trade.Quantity = input.Quantity;
        trade.StopLoss = input.StopLoss;
        trade.TakeProfit = input.TakeProfit;
        trade.ExitTime = trade.IsClosed ? input.ExitTime!.Value.UtcDateTime : null;
        trade.ExitDate = trade.ExitTime is { } exit ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(exit, zone)) : null;
        trade.ExitPrice = trade.IsClosed ? input.ExitPrice : null;
        trade.Fees = input.Fees;
        trade.Invested = input.Invested;
        trade.RiskAmount = input.RiskAmount;
        trade.NetPnl = trade.IsClosed ? input.NetPnl : null;
        trade.RMultiple = trade.IsClosed ? input.RMultiple : null;
        trade.Currency = Cut(input.Currency, TraderTrade.CurrencyMaxLength);
        trade.Platform = Cut(input.Platform, TraderTrade.ShortTextMaxLength);
        trade.Notes = Cut(input.Notes, TraderTrade.TextMaxLength);
        trade.Setup = Cut(input.Setup, TraderTrade.ShortTextMaxLength);
        trade.Tags = Cut(input.Tags, TraderTrade.ShortTextMaxLength);
        trade.Emotion = Cut(input.Emotion, TraderTrade.ShortTextMaxLength);
        trade.UpdatedAt = now;
    }

    private static string Cut(string? value, int max)
    {
        string text = value?.Trim() ?? string.Empty;
        return text.Length > max ? text[..max] : text;
    }
}

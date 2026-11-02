using Agender.Shared.Domain;

namespace Agender.Shared.Contracts;

public record TraderTradeInput(
    long ExternalId,
    string Symbol,
    string Name,
    bool IsBuy,
    bool IsClosed,
    DateTimeOffset EntryTime,
    double EntryPrice,
    double Quantity,
    double? StopLoss,
    double? TakeProfit,
    DateTimeOffset? ExitTime,
    double? ExitPrice,
    double Fees,
    double Invested,
    double? RiskAmount,
    double? NetPnl,
    double? RMultiple,
    string Currency,
    string Platform,
    string Notes,
    string Setup,
    string Tags,
    string Emotion);

public record TraderSyncRequest(IReadOnlyList<TraderTradeInput> Trades);

public record TraderSyncResult(int Created, int Updated, int Removed);

public record TraderTradeDto(
    Guid Id,
    long ExternalId,
    string Symbol,
    string Name,
    bool IsBuy,
    bool IsClosed,
    DateTime EntryTime,
    DateOnly EntryDate,
    double EntryPrice,
    double Quantity,
    double? StopLoss,
    double? TakeProfit,
    DateTime? ExitTime,
    DateOnly? ExitDate,
    double? ExitPrice,
    double Fees,
    double Invested,
    double? RiskAmount,
    double? NetPnl,
    double? RMultiple,
    string Currency,
    string Platform,
    string Notes,
    string Setup,
    string Tags,
    string Emotion);

public record TraderIntegrationStatus(bool HasKey, DateTime? KeyCreatedAt, DateTime? LastSyncAt, int TradeCount);

public record TraderKeyCreated(string Key);

public static class TraderTradeMappings
{
    public static TraderTradeDto ToDto(this TraderTrade trade) => new(
        trade.Id,
        trade.ExternalId,
        trade.Symbol,
        trade.Name,
        trade.IsBuy,
        trade.IsClosed,
        DateTime.SpecifyKind(trade.EntryTime, DateTimeKind.Utc),
        trade.EntryDate,
        trade.EntryPrice,
        trade.Quantity,
        trade.StopLoss,
        trade.TakeProfit,
        trade.ExitTime is { } exit ? DateTime.SpecifyKind(exit, DateTimeKind.Utc) : null,
        trade.ExitDate,
        trade.ExitPrice,
        trade.Fees,
        trade.Invested,
        trade.RiskAmount,
        trade.NetPnl,
        trade.RMultiple,
        trade.Currency,
        trade.Platform,
        trade.Notes,
        trade.Setup,
        trade.Tags,
        trade.Emotion);
}

using Agender.Shared.Components;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;

namespace Agender.Shared.Reports;

public sealed record LedgerSource(string Key, string Name, string Emoji, string Color, bool IsTrades, IncomeCurrency Currency = IncomeCurrency.Eur);

public sealed record LedgerItem(DateOnly Date, string SourceKey, decimal Amount, string Platform, string Label, string Detail, decimal? OriginalAmount = null, IncomeCurrency OriginalCurrency = IncomeCurrency.Eur);

public sealed record LedgerTotals(decimal Gains, decimal Losses, int Count)
{
    public static readonly LedgerTotals Empty = new(0, 0, 0);

    public decimal Net => Gains - Losses;
}

public sealed record PendingTotals(decimal Amount, int Count)
{
    public static readonly PendingTotals Empty = new(0, 0);
}

public static class IncomeLedger
{
    public const string TradesKey = "trades";
    public const string TradesColor = "#6366f1";

    public static LedgerSource TradesSource { get; } = new(TradesKey, "Trades", "📈", TradesColor, true);

    public static LedgerSource ToSource(IncomeSourceDto source) =>
        new(SourceKey(source.Id), source.Name, string.IsNullOrEmpty(source.Emoji) ? "💼" : source.Emoji, EntryColorStyle.Hex(source.Color), false, source.Currency);

    public static string SourceKey(Guid sourceId) => sourceId.ToString("N");


    public static IReadOnlyList<LedgerItem> Build(IEnumerable<IncomeEntryDto> entries, IEnumerable<TraderTradeDto> trades)
    {
        IEnumerable<LedgerItem> manual = entries.Where(e => e.Status == IncomeEntryStatus.Earned).Select(e => new LedgerItem(e.Date, SourceKey(e.SourceId), e.Amount, e.Platform, e.Platform, e.Note));

        IEnumerable<LedgerItem> closed = trades
            .Where(t => t.IsClosed && t.ExitDate is not null && t.NetPnl is not null)
            .Select(t => new LedgerItem(
                t.ExitDate!.Value,
                TradesKey,
                Math.Round((decimal)t.NetPnl!.Value, 2),
                t.Symbol,
                $"{t.Symbol} · {(t.IsBuy ? "Compra" : "Venda")}",
                t.Name));

        return [.. manual.Concat(closed).OrderBy(i => i.Date)];
    }

    public static IReadOnlyList<LedgerItem> BuildInEuros(IEnumerable<IncomeEntryDto> entries, IEnumerable<TraderTradeDto> trades, IEnumerable<LedgerSource> sources)
    {
        Dictionary<string, IncomeCurrency> currencies = CurrencyMap(sources);
        List<IncomeEntryDto> euros = [];
        List<LedgerItem> converted = [];

        foreach (IncomeEntryDto entry in entries.Where(e => e.Status == IncomeEntryStatus.Earned))
        {
            IncomeCurrency currency = currencies.GetValueOrDefault(SourceKey(entry.SourceId), IncomeCurrency.Eur);

            if (currency == IncomeCurrency.Eur)
            {
                euros.Add(entry);
            }
            else if (entry.ConvertedAmount is { } value)
            {
                converted.Add(new LedgerItem(entry.Date, SourceKey(entry.SourceId), value, entry.Platform, entry.Platform, entry.Note, entry.Amount, currency));
            }
        }

        return [.. Build(euros, trades).Concat(converted).OrderBy(i => i.Date)];
    }

    public static IReadOnlyList<(IncomeCurrency Currency, PendingTotals Totals)> PendingByCurrency(IEnumerable<IncomeEntryDto> entries, IEnumerable<LedgerSource> sources) =>
        ByCurrency(entries.Where(e => e.Status == IncomeEntryStatus.Pending), sources, foreignOnly: false);

    public static IReadOnlyList<(IncomeCurrency Currency, PendingTotals Totals)> Unconverted(IEnumerable<IncomeEntryDto> entries, IEnumerable<LedgerSource> sources) =>
        ByCurrency(entries.Where(e => e.Status == IncomeEntryStatus.Earned && e.ConvertedAmount is null), sources, foreignOnly: true);

    private static IReadOnlyList<(IncomeCurrency Currency, PendingTotals Totals)> ByCurrency(IEnumerable<IncomeEntryDto> entries, IEnumerable<LedgerSource> sources, bool foreignOnly)
    {
        Dictionary<string, IncomeCurrency> currencies = CurrencyMap(sources);

        return [.. entries
            .Where(e => currencies.ContainsKey(SourceKey(e.SourceId)))
            .GroupBy(e => currencies[SourceKey(e.SourceId)])
            .Where(g => !foreignOnly || g.Key != IncomeCurrency.Eur)
            .OrderBy(g => g.Key)
            .Select(g => (g.Key, new PendingTotals(g.Sum(e => e.Amount), g.Count())))];
    }

    private static Dictionary<string, IncomeCurrency> CurrencyMap(IEnumerable<LedgerSource> sources) =>
        sources.Where(s => !s.IsTrades).ToDictionary(s => s.Key, s => s.Currency);

    public static PendingTotals Pending(IEnumerable<IncomeEntryDto> entries)
    {
        List<IncomeEntryDto> pending = [.. entries.Where(e => e.Status == IncomeEntryStatus.Pending)];
        return new PendingTotals(pending.Sum(e => e.Amount), pending.Count);
    }

    public static LedgerTotals Totals(IEnumerable<LedgerItem> items)
    {
        decimal gains = 0;
        decimal losses = 0;
        int count = 0;

        foreach (LedgerItem item in items)
        {
            count++;

            if (item.Amount >= 0)
            {
                gains += item.Amount;
            }
            else
            {
                losses -= item.Amount;
            }
        }

        return new LedgerTotals(gains, losses, count);
    }

    public static IEnumerable<LedgerItem> Between(IEnumerable<LedgerItem> items, DateOnly from, DateOnly to) =>
        items.Where(i => i.Date >= from && i.Date <= to);

    public static IReadOnlyDictionary<DateOnly, LedgerTotals> ByDay(IEnumerable<LedgerItem> items) =>
        items.GroupBy(i => i.Date).ToDictionary(g => g.Key, g => Totals(g));

    public static IReadOnlyList<(DateOnly Start, LedgerTotals Totals)> ByWeek(IEnumerable<LedgerItem> items) =>
        [.. items.GroupBy(i => TraderStats.WeekStart(i.Date)).OrderByDescending(g => g.Key).Select(g => (g.Key, Totals(g)))];

    public static IReadOnlyList<(DateOnly Start, LedgerTotals Totals)> ByMonth(IEnumerable<LedgerItem> items) =>
        [.. items.GroupBy(i => MonthStart(i.Date)).OrderByDescending(g => g.Key).Select(g => (g.Key, Totals(g)))];

    public static IReadOnlyList<(string Key, LedgerTotals Totals)> BySource(IEnumerable<LedgerItem> items) =>
        [.. items.GroupBy(i => i.SourceKey).Select(g => (g.Key, Totals(g))).OrderByDescending(x => x.Item2.Net)];

    public static IReadOnlyList<(string Platform, LedgerTotals Totals)> ByPlatform(IEnumerable<LedgerItem> items) =>
        [.. items
            .GroupBy(i => string.IsNullOrWhiteSpace(i.Platform) ? "Sem plataforma" : i.Platform.Trim(), StringComparer.CurrentCultureIgnoreCase)
            .Select(g => (g.Key, Totals(g)))
            .OrderByDescending(x => x.Item2.Net)];

    public static IReadOnlyList<DateOnly> Months(DateOnly from, DateOnly to)
    {
        List<DateOnly> months = [];

        for (DateOnly month = MonthStart(from); month <= to; month = month.AddMonths(1))
        {
            months.Add(month);
        }

        return months;
    }

    public static int WeekCount(DateOnly from, DateOnly to) =>
        to < from ? 0 : (TraderStats.WeekStart(to).DayNumber - TraderStats.WeekStart(from).DayNumber) / 7 + 1;

    public static decimal WeeklyAverage(IEnumerable<LedgerItem> items, DateOnly from, DateOnly to)
    {
        int weeks = WeekCount(from, to);
        return weeks == 0 ? 0 : Math.Round(Totals(Between(items, from, to)).Net / weeks, 2);
    }

    public static DateOnly MonthStart(DateOnly date) => new(date.Year, date.Month, 1);

    public static DateOnly MonthEnd(DateOnly date) => MonthStart(date).AddMonths(1).AddDays(-1);
}

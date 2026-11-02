using Agender.Shared.Contracts;

namespace Agender.Shared.Reports;

public sealed record TraderPeriodStats(int Closed, int Wins, int Losses, double NetPnl, double Invested)
{
    public static readonly TraderPeriodStats Empty = new(0, 0, 0, 0, 0);

    public double WinRate => Closed == 0 ? 0 : (double)Wins / Closed;
}

public static class TraderStats
{
    public static DateOnly WeekStart(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    public static TraderPeriodStats Summarize(IEnumerable<TraderTradeDto> trades)
    {
        int closed = 0;
        int wins = 0;
        int losses = 0;
        double net = 0;
        double invested = 0;

        foreach (TraderTradeDto trade in trades.Where(t => t.IsClosed && t.NetPnl is not null))
        {
            closed++;
            net += trade.NetPnl!.Value;
            invested += trade.Invested;

            if (trade.NetPnl > 0)
            {
                wins++;
            }
            else if (trade.NetPnl < 0)
            {
                losses++;
            }
        }

        return new TraderPeriodStats(closed, wins, losses, net, invested);
    }

    public static IReadOnlyDictionary<DateOnly, TraderPeriodStats> ByDay(IEnumerable<TraderTradeDto> trades) =>
        Closed(trades)
            .GroupBy(t => t.ExitDate!.Value)
            .ToDictionary(g => g.Key, g => Summarize(g));

    public static IReadOnlyList<(DateOnly Start, TraderPeriodStats Stats)> ByWeek(IEnumerable<TraderTradeDto> trades) =>
        Closed(trades)
            .GroupBy(t => WeekStart(t.ExitDate!.Value))
            .OrderByDescending(g => g.Key)
            .Select(g => (g.Key, Summarize(g)))
            .ToList();

    public static IReadOnlyList<(DateOnly Start, TraderPeriodStats Stats)> ByMonth(IEnumerable<TraderTradeDto> trades) =>
        Closed(trades)
            .GroupBy(t => new DateOnly(t.ExitDate!.Value.Year, t.ExitDate.Value.Month, 1))
            .OrderByDescending(g => g.Key)
            .Select(g => (g.Key, Summarize(g)))
            .ToList();

    private static IEnumerable<TraderTradeDto> Closed(IEnumerable<TraderTradeDto> trades) =>
        trades.Where(t => t.IsClosed && t.ExitDate is not null && t.NetPnl is not null);
}

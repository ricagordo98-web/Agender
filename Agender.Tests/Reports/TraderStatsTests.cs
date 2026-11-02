using Agender.Shared.Contracts;
using Agender.Shared.Reports;
using FluentAssertions;

namespace Agender.Tests.Reports;

public class TraderStatsTests
{
    private static TraderTradeDto Trade(string exitDate, double? pnl, double invested = 100, bool closed = true) => new(
        Guid.NewGuid(), 1, "AAPL", "Apple", true, closed, DateTime.UtcNow, DateOnly.Parse(exitDate), 10, 1, null, null,
        closed ? DateTime.UtcNow : null, closed ? DateOnly.Parse(exitDate) : null, 11, 0, invested, null, pnl, null, "EUR", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

    [Fact]
    public void Summarize_MixedTrades_CountsWinsLossesAndTotals()
    {
        TraderPeriodStats stats = TraderStats.Summarize([Trade("2026-11-02", 30), Trade("2026-11-02", -10), Trade("2026-11-02", 0), Trade("2026-11-02", null, closed: false)]);

        stats.Should().Be(new TraderPeriodStats(3, 1, 1, 20, 300));
        stats.WinRate.Should().BeApproximately(1.0 / 3, 1e-9);
    }

    [Fact]
    public void Summarize_NoTrades_HasZeroWinRate() => TraderStats.Summarize([]).WinRate.Should().Be(0);

    [Fact]
    public void ByDay_GroupsClosedTradesByExitDate()
    {
        IReadOnlyDictionary<DateOnly, TraderPeriodStats> days = TraderStats.ByDay([Trade("2026-11-02", 30), Trade("2026-11-02", -10), Trade("2026-11-03", 5)]);

        days[new DateOnly(2026, 11, 2)].NetPnl.Should().Be(20);
        days[new DateOnly(2026, 11, 3)].Closed.Should().Be(1);
    }

    [Fact]
    public void ByWeek_StartsOnMondayAndOrdersNewestFirst()
    {
        IReadOnlyList<(DateOnly Start, TraderPeriodStats Stats)> weeks = TraderStats.ByWeek([Trade("2026-11-01", 10), Trade("2026-11-02", 20), Trade("2026-11-08", 5)]);

        weeks.Select(w => w.Start).Should().Equal(new DateOnly(2026, 11, 2), new DateOnly(2026, 10, 26));
        weeks[0].Stats.NetPnl.Should().Be(25);
        weeks[1].Stats.NetPnl.Should().Be(10);
    }

    [Fact]
    public void ByMonth_GroupsByCalendarMonth()
    {
        IReadOnlyList<(DateOnly Start, TraderPeriodStats Stats)> months = TraderStats.ByMonth([Trade("2026-10-31", 10), Trade("2026-11-01", 20), Trade("2026-11-30", -5)]);

        months.Select(m => m.Start).Should().Equal(new DateOnly(2026, 11, 1), new DateOnly(2026, 10, 1));
        months[0].Stats.NetPnl.Should().Be(15);
    }
}

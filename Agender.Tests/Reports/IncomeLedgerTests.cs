using Agender.Shared.Contracts;
using Agender.Shared.Reports;
using FluentAssertions;

namespace Agender.Tests.Reports;

public sealed class IncomeLedgerTests
{
    private static readonly Guid Surveys = Guid.NewGuid();

    private static IncomeEntryDto Entry(string date, decimal amount, string platform = "Prolific") =>
        new(Guid.NewGuid(), Surveys, DateOnly.Parse(date), amount, platform, string.Empty);

    private static TraderTradeDto Trade(string exitDate, double? pnl, bool closed = true) => new(
        Guid.NewGuid(), 1, "AAPL", "Apple", true, closed, DateTime.UtcNow, DateOnly.Parse(exitDate), 100, 1, null, null,
        closed ? DateTime.UtcNow : null, closed ? DateOnly.Parse(exitDate) : null, null, 0, 100, null, pnl, null,
        "EUR", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

    [Fact]
    public void Build_MergesManualEntriesAndClosedTradesOnly()
    {
        IReadOnlyList<LedgerItem> items = IncomeLedger.Build(
            [Entry("2026-11-03", 3.2m)],
            [Trade("2026-11-02", 25.456), Trade("2026-11-02", null, closed: false)]);

        items.Should().HaveCount(2);
        items[0].Should().Match<LedgerItem>(i => i.SourceKey == IncomeLedger.TradesKey && i.Amount == 25.46m && i.Platform == "AAPL");
        items[1].SourceKey.Should().Be(IncomeLedger.SourceKey(Surveys));
    }

    [Fact]
    public void Totals_SplitsGainsAndLosses()
    {
        IReadOnlyList<LedgerItem> items = IncomeLedger.Build([Entry("2026-11-03", 10), Entry("2026-11-04", 5)], [Trade("2026-11-05", -4)]);

        LedgerTotals totals = IncomeLedger.Totals(items);

        totals.Should().Be(new LedgerTotals(15, 4, 3));
        totals.Net.Should().Be(11);
    }

    [Fact]
    public void ByWeek_StartsOnMondayAndCrossesMonths()
    {
        IReadOnlyList<LedgerItem> items = IncomeLedger.Build([Entry("2026-11-29", 1), Entry("2026-12-01", 2), Entry("2026-11-23", 4)], []);

        IReadOnlyList<(DateOnly Start, LedgerTotals Totals)> weeks = IncomeLedger.ByWeek(items);

        weeks.Select(w => w.Start).Should().Equal(new DateOnly(2026, 11, 30), new DateOnly(2026, 11, 23));
        weeks[0].Totals.Net.Should().Be(2);
        weeks[1].Totals.Net.Should().Be(5);
    }

    [Fact]
    public void ByPlatform_GroupsIgnoringCaseAndNamesEmptyOnes()
    {
        IReadOnlyList<LedgerItem> items = IncomeLedger.Build([Entry("2026-11-03", 1, "Prolific"), Entry("2026-11-04", 2, "prolific"), Entry("2026-11-04", 9, string.Empty)], []);

        IReadOnlyList<(string Platform, LedgerTotals Totals)> platforms = IncomeLedger.ByPlatform(items);

        platforms.Select(p => (p.Platform, p.Totals.Net)).Should().Equal(("Sem plataforma", 9m), ("Prolific", 3m));
    }

    [Fact]
    public void WeeklyAverage_DividesTheNetByTheWeeksInTheRange()
    {
        IReadOnlyList<LedgerItem> items = IncomeLedger.Build([Entry("2026-11-02", 10), Entry("2026-11-10", 20), Entry("2026-12-10", 99)], []);

        IncomeLedger.WeeklyAverage(items, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 15)).Should().Be(15);
    }

    [Fact]
    public void Months_ListsEveryMonthStartInTheRange()
    {
        IncomeLedger.Months(new DateOnly(2026, 11, 20), new DateOnly(2027, 1, 5))
            .Should().Equal(new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 1), new DateOnly(2027, 1, 1));
    }

    [Fact]
    public void ProfitFactor_IsWinsOverLossesAndNullWithoutLosses()
    {
        TraderStats.ProfitFactor([Trade("2026-11-02", 30), Trade("2026-11-02", -10)]).Should().Be(3);
        TraderStats.ProfitFactor([Trade("2026-11-02", 30)]).Should().BeNull();
    }
}

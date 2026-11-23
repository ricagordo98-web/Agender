using System.Text;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Reports;
using FluentAssertions;

namespace Agender.Tests.Reports;

public sealed class IncomePdfTests
{
    private static readonly IncomeSourceDto Surveys = new(Guid.NewGuid(), "Surveys", "📝", EntryColor.Teal, false, 0);

    private static (List<IncomeEntryDto> Entries, List<TraderTradeDto> Trades) Sample()
    {
        List<IncomeEntryDto> entries = [];
        List<TraderTradeDto> trades = [];
        DateOnly start = new(2026, 9, 1);

        for (int index = 0; index < 90; index++)
        {
            DateOnly date = start.AddDays(index);
            entries.Add(new IncomeEntryDto(Guid.NewGuid(), Surveys.Id, date, 0.5m + index % 7, index % 3 == 0 ? "Prolific" : "YouGov", index % 4 == 0 ? "Inquérito sobre hábitos de consumo" : string.Empty));

            if (index % 2 == 0)
            {
                double pnl = index % 3 == 0 ? -12.5 : 20.25;
                trades.Add(new TraderTradeDto(Guid.NewGuid(), index, index % 4 == 0 ? "AAPL" : "TSLA", "Ação", index % 2 == 0, true, DateTime.UtcNow, date, 100, 1, null, null,
                    DateTime.UtcNow, date, 101, 0.5, 1000, 50, pnl, pnl / 50, "EUR", "T212", string.Empty, string.Empty, string.Empty, string.Empty));
            }
        }

        return (entries, trades);
    }

    private static byte[] Build(IReadOnlyList<LedgerSource> sources, bool detail)
    {
        (List<IncomeEntryDto> entries, List<TraderTradeDto> trades) = Sample();
        List<IncomeEntryDto> pending = [new(Guid.NewGuid(), Surveys.Id, new DateOnly(2026, 11, 20), 15, "Prolific", string.Empty, IncomeEntryStatus.Pending)];
        IncomeReportInput input = new(new DateOnly(2026, 9, 1), new DateOnly(2026, 11, 30), sources, IncomeLedger.Build(entries, trades), trades, detail, new DateTime(2026, 11, 23, 18, 0, 0), pending);

        return IncomePdf.Build(input);
    }

    [Fact]
    public void Build_AllSourcesWithDetail_ProducesAMultiPagePdf()
    {
        byte[] pdf = Build([IncomeLedger.TradesSource, IncomeLedger.ToSource(Surveys)], detail: true);

        Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
        pdf.Length.Should().BeGreaterThan(10_000);
    }

    [Fact]
    public void Build_OneSourceWithoutData_StillProducesAPdf()
    {
        IncomeReportInput input = new(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), [IncomeLedger.ToSource(Surveys)], [], [], true, DateTime.Now);

        byte[] pdf = IncomePdf.Build(input);

        Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public void Build_OnlyTrades_ProducesAPdf()
    {
        byte[] pdf = Build([IncomeLedger.TradesSource], detail: false);

        Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
    }
}

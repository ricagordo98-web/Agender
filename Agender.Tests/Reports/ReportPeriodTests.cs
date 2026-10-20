using Agender.Shared.Reports;
using FluentAssertions;

namespace Agender.Tests.Reports;

public class ReportPeriodTests
{
    private static readonly DateOnly Wednesday = new(2026, 10, 14);

    [Fact]
    public void ThisWeek_Wednesday_ReturnsMondayToSunday()
    {
        (DateOnly from, DateOnly to) = ReportPeriod.ThisWeek(Wednesday);

        from.Should().Be(new DateOnly(2026, 10, 12));
        to.Should().Be(new DateOnly(2026, 10, 18));
    }

    [Fact]
    public void ThisWeek_Sunday_StaysInTheSameWeek()
    {
        (DateOnly from, DateOnly to) = ReportPeriod.ThisWeek(new DateOnly(2026, 10, 18));

        from.Should().Be(new DateOnly(2026, 10, 12));
        to.Should().Be(new DateOnly(2026, 10, 18));
    }

    [Fact]
    public void LastWeek_Wednesday_ReturnsPreviousMondayToSunday()
    {
        (DateOnly from, DateOnly to) = ReportPeriod.LastWeek(Wednesday);

        from.Should().Be(new DateOnly(2026, 10, 5));
        to.Should().Be(new DateOnly(2026, 10, 11));
    }

    [Fact]
    public void ThisMonth_Wednesday_ReturnsFirstToLastDay()
    {
        (DateOnly from, DateOnly to) = ReportPeriod.ThisMonth(Wednesday);

        from.Should().Be(new DateOnly(2026, 10, 1));
        to.Should().Be(new DateOnly(2026, 10, 31));
    }

    [Fact]
    public void LastMonth_January_ReturnsDecemberOfPreviousYear()
    {
        (DateOnly from, DateOnly to) = ReportPeriod.LastMonth(new DateOnly(2027, 1, 15));

        from.Should().Be(new DateOnly(2026, 12, 1));
        to.Should().Be(new DateOnly(2026, 12, 31));
    }
}

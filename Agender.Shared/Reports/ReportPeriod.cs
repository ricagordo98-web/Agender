namespace Agender.Shared.Reports;

public static class ReportPeriod
{
    public static (DateOnly From, DateOnly To) ThisWeek(DateOnly today)
    {
        DateOnly monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        return (monday, monday.AddDays(6));
    }

    public static (DateOnly From, DateOnly To) LastWeek(DateOnly today) => ThisWeek(today.AddDays(-7));

    public static (DateOnly From, DateOnly To) ThisMonth(DateOnly today)
    {
        DateOnly first = new(today.Year, today.Month, 1);
        return (first, first.AddMonths(1).AddDays(-1));
    }

    public static (DateOnly From, DateOnly To) LastMonth(DateOnly today) =>
        ThisMonth(new DateOnly(today.Year, today.Month, 1).AddDays(-1));
}

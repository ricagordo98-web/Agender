namespace Agender.Shared.Domain;

public readonly record struct TimerSegment(DateOnly Date, int StartMinute, int EndMinute);

public static class TimerSplitter
{
    public static IReadOnlyList<TimerSegment> Split(DateTime start, DateTime end)
    {
        if (end < start)
        {
            end = start;
        }

        DateOnly startDay = DateOnly.FromDateTime(start);
        DateOnly endDay = DateOnly.FromDateTime(end);
        int startMinute = start.Hour * 60 + start.Minute;
        int endMinute = end.Hour * 60 + end.Minute;

        if (startDay == endDay)
        {
            return [new TimerSegment(startDay, startMinute, Math.Max(endMinute, startMinute + 1))];
        }

        List<TimerSegment> segments = [new TimerSegment(startDay, startMinute, TimeSlot.MinutesPerDay)];

        for (DateOnly day = startDay.AddDays(1); day < endDay; day = day.AddDays(1))
        {
            segments.Add(new TimerSegment(day, 0, TimeSlot.MinutesPerDay));
        }

        if (endMinute > 0)
        {
            segments.Add(new TimerSegment(endDay, 0, endMinute));
        }

        return segments;
    }
}

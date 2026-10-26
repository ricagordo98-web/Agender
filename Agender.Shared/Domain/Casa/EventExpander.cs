namespace Agender.Shared.Domain;

public static class EventExpander
{
    private const int MaxOccurrences = 1000;

    public static IEnumerable<EventOccurrence> Expand(IEnumerable<FamilyEvent> events, DateTime from, DateTime to)
    {
        foreach (FamilyEvent item in events)
        {
            TimeSpan? duration = item.End is DateTime end ? end - item.Start : null;

            if (item.Recurrence == EventRecurrence.None)
            {
                DateTime last = item.End ?? item.Start;

                if (item.Start < to && last >= from)
                {
                    yield return new EventOccurrence(item, item.Start, item.End, null);
                }

                continue;
            }

            DateTime cursor = FastForward(item, from);
            int guard = 0;

            while (cursor < to && guard++ < MaxOccurrences)
            {
                if (cursor >= from || (duration is TimeSpan length && cursor + length >= from))
                {
                    int? years = item.Recurrence == EventRecurrence.Yearly ? cursor.Year - item.Start.Year : null;

                    yield return new EventOccurrence(item, cursor, duration is TimeSpan span ? cursor + span : null, years);
                }

                cursor = Next(item, cursor);
            }
        }
    }

    private static DateTime FastForward(FamilyEvent item, DateTime from)
    {
        DateTime cursor = item.Start;

        if (cursor >= from)
        {
            return cursor;
        }

        switch (item.Recurrence)
        {
            case EventRecurrence.Weekly:
                int weeks = (int)((from - cursor).TotalDays / 7);
                return cursor.AddDays(Math.Max(0, weeks - 1) * 7);
            case EventRecurrence.Monthly:
                int months = (from.Year - cursor.Year) * 12 + from.Month - cursor.Month;
                return item.Start.AddMonths(Math.Max(0, months - 1));
            default:
                return item.Start.AddYears(Math.Max(0, from.Year - cursor.Year - 1));
        }
    }

    private static DateTime Next(FamilyEvent item, DateTime cursor) => item.Recurrence switch
    {
        EventRecurrence.Weekly => cursor.AddDays(7),
        EventRecurrence.Monthly => item.Start.AddMonths((cursor.Year - item.Start.Year) * 12 + cursor.Month - item.Start.Month + 1),
        _ => item.Start.AddYears(cursor.Year - item.Start.Year + 1)
    };
}

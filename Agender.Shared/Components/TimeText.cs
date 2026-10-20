using Agender.Shared.Domain;

namespace Agender.Shared.Components;

public static class TimeText
{
    public static string Duration(int minutes)
    {
        int hours = minutes / 60;
        int rest = minutes % 60;

        return (hours, rest) switch
        {
            (0, _) => $"{rest}m",
            (_, 0) => $"{hours}h",
            _ => $"{hours}h {rest}m"
        };
    }

    public static string Elapsed(TimeSpan span)
    {
        TimeSpan value = span < TimeSpan.Zero ? TimeSpan.Zero : span;
        return $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}";
    }

    public static string Range(int startMinute, int endMinute) =>
        $"{TimeSlot.FormatMinute(startMinute)} – {TimeSlot.FormatMinute(endMinute)}";
}

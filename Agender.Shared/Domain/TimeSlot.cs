using System.Globalization;

namespace Agender.Shared.Domain;

public readonly record struct TimeSlot
{
    public const int MinutesPerDay = 24 * 60;

    public TimeSlot(int startMinute, int endMinute)
    {
        if (startMinute < 0 || endMinute > MinutesPerDay || endMinute <= startMinute)
        {
            throw new ArgumentException($"Invalid time slot {startMinute}-{endMinute}.");
        }

        StartMinute = startMinute;
        EndMinute = endMinute;
    }

    public int StartMinute { get; }

    public int EndMinute { get; }

    public int Duration => EndMinute - StartMinute;

    public bool OverlapsWith(TimeSlot other) => StartMinute < other.EndMinute && other.StartMinute < EndMinute;

    public static int Snap(int minute, int step) =>
        (int)Math.Round((double)minute / step, MidpointRounding.AwayFromZero) * step;

    public static string FormatMinute(int minute) => $"{minute / 60:00}:{minute % 60:00}";

    public static bool TryParseClock(string? text, out int minute)
    {
        if (TimeOnly.TryParse(text, CultureInfo.InvariantCulture, out TimeOnly time))
        {
            minute = time.Hour * 60 + time.Minute;
            return true;
        }

        minute = 0;
        return false;
    }
}

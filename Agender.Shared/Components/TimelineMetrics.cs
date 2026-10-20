using System.Globalization;
using Agender.Shared.Domain;

namespace Agender.Shared.Components;

public static class TimelineMetrics
{
    public const double PixelsPerMinute = 1.2;
    public const int DefaultEntryMinutes = 30;
    public const int CompactEntryMinutes = 40;
    public const int MetaEntryMinutes = 55;

    public static readonly string PixelsPerMinuteAttribute = PixelsPerMinute.ToString(CultureInfo.InvariantCulture);

    public static string Px(int minutes) => $"{Number(minutes * PixelsPerMinute)}px";

    public static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    public static string DateKey(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string DayHeight => Px(TimeSlot.MinutesPerDay);
}

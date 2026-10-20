using System.Globalization;

namespace Agender.Shared.Components;

public static class DateText
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("pt-PT");

    public static string Weekday(DateOnly date) => date.ToString("ddd", Culture).TrimEnd('.');

    public static string Month(DateOnly date) => date.ToString("MMM", Culture).TrimEnd('.');

    public static string Full(DateOnly date) => date.ToString("dddd, d 'de' MMMM 'de' yyyy", Culture);

    public static string Range(DateOnly from, DateOnly to) =>
        from.Month == to.Month && from.Year == to.Year
            ? $"{from.Day} – {to.ToString("d 'de' MMMM 'de' yyyy", Culture)}"
            : $"{from.ToString("d MMM", Culture).Replace(".", string.Empty)} – {to.ToString("d MMM yyyy", Culture).Replace(".", string.Empty)}";
}

using System.Globalization;

namespace Agender.Shared.Components.Work;

public static class WorkFormat
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("pt-PT");

    public static readonly string[] Weekdays = ["Seg", "Ter", "Qua", "Qui", "Sex", "Sáb", "Dom"];

    public static readonly string[] Emojis = ["📝", "💻", "🎨", "📦", "🛍️", "🚗", "🎧", "📚", "🎮", "🤝", "💼", "🧾", "💡", "🏠", "📸", "💰"];

    public static string Money(decimal value, bool signed = true) =>
        (signed ? value.ToString("+#,##0.00;-#,##0.00;0.00", Culture) : value.ToString("#,##0.00", Culture)) + " €";

    public static string Money(double value, bool signed = true) => Money((decimal)Math.Round(value, 2), signed);

    public static string Tone(decimal value) => value > 0 ? "win" : value < 0 ? "loss" : "flat";

    public static string Tone(double value) => Tone((decimal)value);

    public static string Month(DateOnly month) => Capitalize(month.ToString("MMMM yyyy", Culture));

    public static string Week(DateOnly start) => $"{start.ToString("d MMM", Culture)} – {start.AddDays(6).ToString("d MMM", Culture)}";

    public static string Day(DateOnly day) => Capitalize(day.ToString("dddd, d 'de' MMMM", Culture));

    public static string Count(int count, string singular, string plural) => $"{count} {(count == 1 ? singular : plural)}";

    public static string Change(decimal current, decimal previous)
    {
        if (previous == 0)
        {
            return current == 0 ? "sem variação" : "sem mês anterior";
        }

        decimal change = (current - previous) / Math.Abs(previous);
        return $"{change.ToString("+0%;-0%;0%", Culture)} vs mês anterior";
    }

    public static string ChangeShort(decimal current, decimal previous)
    {
        if (previous == 0)
        {
            return current == 0 ? "—" : "novo";
        }

        decimal change = (current - previous) / Math.Abs(previous);
        return $"{change.ToString("+0%;-0%;0%", Culture)} vs anterior";
    }

    public static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpper(text[0], Culture) + text[1..];
}

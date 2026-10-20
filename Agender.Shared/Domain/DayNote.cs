namespace Agender.Shared.Domain;

public class DayNote
{
    public const int TextMaxLength = 4000;

    public DateOnly Date { get; set; }

    public string Text { get; set; } = string.Empty;
}

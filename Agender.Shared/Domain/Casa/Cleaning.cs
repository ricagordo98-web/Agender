namespace Agender.Shared.Domain;

public enum CleaningUnit { Day = 0, Week = 1, Month = 2 }

public class CleaningRoom
{
    public const int NameMaxLength = 60;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Emoji { get; set; } = CleaningCatalog.RoomEmojis[0];

    public string Color { get; set; } = CasaCatalog.ListColors[0];

    public int Order { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CleaningChore
{
    public const int TitleMaxLength = 120;
    public const int MaxEvery = 365;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public Guid RoomId { get; set; }

    public string Title { get; set; } = string.Empty;

    public int Every { get; set; } = 1;

    public CleaningUnit Unit { get; set; } = CleaningUnit.Week;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CleaningCompletion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public Guid ChoreId { get; set; }

    public DateOnly Date { get; set; }

    public string UserId { get; set; } = string.Empty;

    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}

public class CleaningChoreStatus
{
    public CleaningChore Chore { get; set; } = new();

    public DateOnly Due { get; set; }

    public DateOnly? LastDone { get; set; }

    public string? LastDoneBy { get; set; }

    public bool DoneToday { get; set; }

    public bool IsDue { get; set; }

    public bool IsLate { get; set; }
}

public static class CleaningSchedule
{
    public static DateOnly PeriodStart(DateOnly date, CleaningUnit unit) => unit switch
    {
        CleaningUnit.Week => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)),
        CleaningUnit.Month => new DateOnly(date.Year, date.Month, 1),
        _ => date
    };

    public static DateOnly Advance(DateOnly date, CleaningUnit unit, int count) => unit switch
    {
        CleaningUnit.Week => date.AddDays(7 * count),
        CleaningUnit.Month => date.AddMonths(count),
        _ => date.AddDays(count)
    };

    public static CleaningChoreStatus Evaluate(CleaningChore chore, CleaningCompletion? last, string? lastDoneBy, DateOnly today)
    {
        DateOnly due = last is null
            ? PeriodStart(DateOnly.FromDateTime(chore.CreatedAt.ToLocalTime()), chore.Unit)
            : Advance(PeriodStart(last.Date, chore.Unit), chore.Unit, chore.Every);
        bool doneToday = last?.Date == today;

        return new CleaningChoreStatus
        {
            Chore = chore,
            Due = due,
            LastDone = last?.Date,
            LastDoneBy = lastDoneBy,
            DoneToday = doneToday,
            IsDue = !doneToday && due <= today,
            IsLate = !doneToday && Advance(due, chore.Unit, 1) <= today
        };
    }
}

public static class CleaningCatalog
{
    public static readonly string[] RoomEmojis =
        ["🛋️", "🍳", "🛏️", "🛁", "🚽", "🧺", "💻", "🚪", "🪴", "🚗", "🧸", "🏠"];

    public static string Frequency(CleaningChore chore) => (chore.Every, chore.Unit) switch
    {
        (1, CleaningUnit.Day) => "Todos os dias",
        (1, CleaningUnit.Week) => "1x por semana",
        (1, CleaningUnit.Month) => "1x por mês",
        (int n, CleaningUnit.Day) => $"De {n} em {n} dias",
        (int n, CleaningUnit.Week) => $"De {n} em {n} semanas",
        (int n, CleaningUnit.Month) => $"De {n} em {n} meses",
        _ => string.Empty
    };

    public static string UnitLabel(CleaningUnit unit, int count) => unit switch
    {
        CleaningUnit.Week => count == 1 ? "semana" : "semanas",
        CleaningUnit.Month => count == 1 ? "mês" : "meses",
        _ => count == 1 ? "dia" : "dias"
    };
}

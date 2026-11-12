namespace Agender.Shared.Domain;

public enum DailySection { Cleaning = 0, Meals = 1, Events = 2, Inventory = 3 }

public class DailyReminder
{
    public const int ErrorMaxLength = 300;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public DailySection Section { get; set; }

    public bool IsEnabled { get; set; }

    public string? Destination { get; set; }

    public TimeOnly SendTime { get; set; } = DailyCatalog.DefaultTime;

    public DateOnly? LastSentOn { get; set; }

    public DateTime? LastAttemptAt { get; set; }

    public string? LastError { get; set; }
}

public static class DailyCatalog
{
    public static readonly TimeOnly DefaultTime = new(8, 0);

    public static readonly DailySection[] Sections = [DailySection.Cleaning, DailySection.Meals, DailySection.Events, DailySection.Inventory];

    public static string Label(DailySection section) => section switch
    {
        DailySection.Cleaning => "Limpezas",
        DailySection.Meals => "Refeições",
        DailySection.Events => "Agenda da casa",
        _ => "Inventário"
    };

    public static string Title(DailySection section) => section switch
    {
        DailySection.Cleaning => "Limpezas de hoje",
        DailySection.Meals => "Refeições de hoje",
        DailySection.Events => "Agenda da casa hoje",
        _ => "Inventário a expirar"
    };

    public static string Emoji(DailySection section) => section switch
    {
        DailySection.Cleaning => "🧹",
        DailySection.Meals => "🍽️",
        DailySection.Events => "📅",
        _ => "📦"
    };

    public static string Description(DailySection section) => section switch
    {
        DailySection.Cleaning => "As limpezas que tocam hoje ou estão atrasadas.",
        DailySection.Meals => "O almoço, o jantar e a sobremesa planeados para hoje.",
        DailySection.Events => "Os eventos e aniversários de hoje.",
        _ => $"O que expira nos próximos {HomeMatcher.ExpiringDays} dias."
    };
}

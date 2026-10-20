namespace Agender.Shared.Domain;

public class UserSettings
{
    public static readonly IReadOnlyList<int> SnapOptions = [1, 5, 15];

    public static readonly IReadOnlyList<int> EntryDurationOptions = [15, 30, 45, 60, 90];

    public static readonly IReadOnlyList<int> StartScrollOptions = [0, 420, 540];

    public static readonly IReadOnlyList<int> TimerRoundingOptions = [0, 5, 15, 30];

    public Guid Id { get; set; } = Guid.NewGuid();

    public bool DefaultWeekView { get; set; } = true;

    public int SnapMinutes { get; set; } = 5;

    public int DefaultEntryMinutes { get; set; } = 30;

    public bool WeekStartsOnSunday { get; set; }

    public bool HideWeekends { get; set; }

    public bool HideDayNotes { get; set; }

    public bool HideEntryDetails { get; set; }

    public int StartScrollMinute { get; set; }

    public int TimerRoundingMinutes { get; set; }

    public bool ConfirmBeforeDelete { get; set; } = true;

    public bool RemoveFromAgendaWhenCompleted { get; set; }

    public UserSettings Clone() => (UserSettings)MemberwiseClone();
}

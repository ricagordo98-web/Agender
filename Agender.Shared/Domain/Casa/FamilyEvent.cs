namespace Agender.Shared.Domain;

public enum FamilyEventKind { Appointment = 0, Birthday = 1, Family = 2, Health = 3, School = 4, Reminder = 5 }

public enum EventRecurrence { None = 0, Weekly = 1, Monthly = 2, Yearly = 3 }

public class FamilyEvent
{
    public const int TitleMaxLength = 120;
    public const int LocationMaxLength = 200;
    public const int NotesMaxLength = 2000;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTime Start { get; set; } = DateTime.Today.AddHours(9);

    public DateTime? End { get; set; }

    public bool AllDay { get; set; }

    public FamilyEventKind Kind { get; set; } = FamilyEventKind.Appointment;

    public EventRecurrence Recurrence { get; set; } = EventRecurrence.None;

    public string? Location { get; set; }

    public string? Notes { get; set; }

    public string? MemberId { get; set; }

    public string? MemberName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public record EventOccurrence(FamilyEvent Event, DateTime Start, DateTime? End, int? Years);

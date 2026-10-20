namespace Agender.Shared.Domain;

public class TimeEntry
{
    public const int DescriptionMaxLength = 2000;

    public Guid Id { get; set; } = Guid.NewGuid();

    public DateOnly Date { get; set; }

    public int StartMinute { get; set; }

    public int EndMinute { get; set; }

    public string Description { get; set; } = string.Empty;

    public EntryColor Color { get; set; } = EntryColor.Indigo;

    public string? CustomColor { get; set; }

    public Guid AgendaId { get; set; }

    public Guid? TaskItemId { get; set; }

    public Guid? ProjectId { get; set; }

    public bool IsCompleted { get; set; }

    public bool SyncToGoogle { get; set; }

    public bool GoogleSyncFailed { get; set; }

    public string? GoogleEventId { get; set; }

    public List<Tag> Tags { get; set; } = [];

    public TimeSlot Slot => new(StartMinute, EndMinute);

    public TimeEntry Clone()
    {
        TimeEntry copy = (TimeEntry)MemberwiseClone();
        copy.Tags = [.. Tags];
        return copy;
    }
}

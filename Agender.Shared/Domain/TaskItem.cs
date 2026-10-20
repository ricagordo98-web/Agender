namespace Agender.Shared.Domain;

public class TaskItem
{
    public const int TitleMaxLength = 200;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    public EntryColor Color { get; set; } = EntryColor.Indigo;

    public string? CustomColor { get; set; }

    public Guid AgendaId { get; set; }

    public bool IsCompleted { get; set; }

    public Guid? ProjectId { get; set; }

    public bool IsScheduled { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

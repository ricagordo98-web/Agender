namespace Agender.Shared.Domain;

public class ActiveTimer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime StartedAt { get; set; }

    public string Description { get; set; } = string.Empty;

    public Guid? ProjectId { get; set; }

    public Guid? TaskItemId { get; set; }

    public Guid AgendaId { get; set; }
}

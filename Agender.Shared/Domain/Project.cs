namespace Agender.Shared.Domain;

public class Project
{
    public const int NameMaxLength = 100;
    public const int ClientNameMaxLength = 100;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string? ClientName { get; set; }

    public EntryColor Color { get; set; } = EntryColor.Indigo;

    public string? CustomColor { get; set; }

    public Guid AgendaId { get; set; }

    public bool IsArchived { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

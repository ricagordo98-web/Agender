namespace Agender.Shared.Domain;

public class Agenda
{
    public const int NameMaxLength = 60;
    public const string DefaultName = "Geral";

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public EntryColor Color { get; set; } = EntryColor.Indigo;

    public string? CustomColor { get; set; }

    public bool IsVisible { get; set; } = true;

    public bool IsActive { get; set; }

    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

using System.Text.Json.Serialization;

namespace Agender.Shared.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IncomeEntryStatus
{
    Earned,
    Pending,
    Rejected
}

public class IncomeSource
{
    public const int NameMaxLength = 60;
    public const int EmojiMaxLength = 16;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Emoji { get; set; } = string.Empty;

    public EntryColor Color { get; set; } = EntryColor.Indigo;

    public bool IsArchived { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class IncomeEntry
{
    public const int PlatformMaxLength = 60;
    public const int NoteMaxLength = 300;
    public const decimal MaxAmount = 1_000_000m;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SourceId { get; set; }

    public DateOnly Date { get; set; }

    public decimal Amount { get; set; }

    public IncomeEntryStatus Status { get; set; } = IncomeEntryStatus.Earned;

    public string Platform { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

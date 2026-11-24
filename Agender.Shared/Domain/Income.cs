using System.Text.Json.Serialization;

namespace Agender.Shared.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IncomeEntryStatus
{
    Earned,
    Pending,
    Rejected
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IncomeCurrency
{
    Eur,
    Usd,
    Gbp,
    Brl,
    Chf,
    Cad,
    Aud
}

public static class IncomeCurrencies
{
    public static string Symbol(IncomeCurrency currency) => currency switch
    {
        IncomeCurrency.Usd => "$",
        IncomeCurrency.Gbp => "£",
        IncomeCurrency.Brl => "R$",
        IncomeCurrency.Chf => "CHF",
        IncomeCurrency.Cad => "C$",
        IncomeCurrency.Aud => "A$",
        _ => "€"
    };

    public static string Name(IncomeCurrency currency) => currency switch
    {
        IncomeCurrency.Usd => "Dólar americano (USD)",
        IncomeCurrency.Gbp => "Libra (GBP)",
        IncomeCurrency.Brl => "Real (BRL)",
        IncomeCurrency.Chf => "Franco suíço (CHF)",
        IncomeCurrency.Cad => "Dólar canadiano (CAD)",
        IncomeCurrency.Aud => "Dólar australiano (AUD)",
        _ => "Euro (EUR)"
    };
}

public class IncomeSource
{
    public const int NameMaxLength = 60;
    public const int EmojiMaxLength = 16;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Emoji { get; set; } = string.Empty;

    public EntryColor Color { get; set; } = EntryColor.Indigo;

    public IncomeCurrency Currency { get; set; } = IncomeCurrency.Eur;

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

    public decimal? ConvertedAmount { get; set; }

    public IncomeEntryStatus Status { get; set; } = IncomeEntryStatus.Earned;

    public string Platform { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

namespace Agender.Shared.Domain;

public class TraderTrade
{
    public const int TextMaxLength = 2000;
    public const int ShortTextMaxLength = 200;
    public const int CurrencyMaxLength = 8;

    public Guid Id { get; set; } = Guid.NewGuid();

    public long ExternalId { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsBuy { get; set; }

    public bool IsClosed { get; set; }

    public DateTime EntryTime { get; set; }

    public DateOnly EntryDate { get; set; }

    public double EntryPrice { get; set; }

    public double Quantity { get; set; }

    public double? StopLoss { get; set; }

    public double? TakeProfit { get; set; }

    public DateTime? ExitTime { get; set; }

    public DateOnly? ExitDate { get; set; }

    public double? ExitPrice { get; set; }

    public double Fees { get; set; }

    public double Invested { get; set; }

    public double? RiskAmount { get; set; }

    public double? NetPnl { get; set; }

    public double? RMultiple { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public string Setup { get; set; } = string.Empty;

    public string Tags { get; set; } = string.Empty;

    public string Emotion { get; set; } = string.Empty;

    public Guid? TimeEntryId { get; set; }

    public DateTime UpdatedAt { get; set; }
}

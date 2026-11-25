using System.Text.Json.Serialization;

namespace Agender.Shared.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UtilityService
{
    Electricity,
    Gas,
    Water,
    TvInternet,
    Mobile
}

public class UtilityBill
{
    public const int ProviderMaxLength = 60;
    public const int NoteMaxLength = 200;
    public const decimal MaxAmount = 100_000m;
    public const decimal MaxConsumption = 1_000_000m;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public UtilityService Service { get; set; } = UtilityService.Electricity;

    public int Year { get; set; }

    public int Month { get; set; }

    public decimal Amount { get; set; }

    public decimal? Consumption { get; set; }

    public string? Provider { get; set; }

    public DateOnly? DueDate { get; set; }

    public DateOnly? PaidOn { get; set; }

    public Guid? PersonId { get; set; }

    public Guid? TransactionId { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsPaid => PaidOn is not null;

    public bool IsOverdue(DateOnly today) => PaidOn is null && DueDate is { } due && due < today;
}

public static class UtilityCatalog
{
    public const string ExpenseCategory = "Contas da casa";

    public record ServiceInfo(UtilityService Service, string Label, string Emoji, string Color, string? Unit);

    public static readonly ServiceInfo[] Services =
    [
        new(UtilityService.Electricity, "Luz", "💡", "#F2C94C", "kWh"),
        new(UtilityService.Gas, "Gás", "🔥", "#F2994A", "m³"),
        new(UtilityService.Water, "Água", "💧", "#56CCF2", "m³"),
        new(UtilityService.TvInternet, "TV / Net", "📡", "#9B8CFF", null),
        new(UtilityService.Mobile, "Telemóvel", "📱", "#6FCF97", null)
    ];

    public static ServiceInfo Info(UtilityService service) =>
        Services.FirstOrDefault(s => s.Service == service) ?? Services[0];

    public static string MonthLabel(int year, int month) =>
        CasaCatalog.Capitalize(new DateOnly(year, month, 1).ToString("MMMM yyyy", CasaCatalog.Culture));

    public static string Describe(UtilityBill bill)
    {
        ServiceInfo info = Info(bill.Service);
        string provider = string.IsNullOrWhiteSpace(bill.Provider) ? string.Empty : $" · {bill.Provider.Trim()}";
        return $"{info.Emoji} {info.Label} · {MonthLabel(bill.Year, bill.Month)}{provider}";
    }
}

public class UtilityTariff
{
    public const int SourceMaxLength = 80;
    public const decimal MaxPrice = 100m;
    public const decimal MaxReading = 100_000_000m;
    public const int MaxPeriodDays = 92;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public UtilityService Service { get; set; } = UtilityService.Electricity;

    public decimal EnergyPrice { get; set; }

    public decimal EnergyTax { get; set; }

    public decimal FixedDaily { get; set; }

    public decimal ConversionFactor { get; set; } = 1;

    public decimal MonthlyFees { get; set; }

    public decimal VatEnergy { get; set; } = 23;

    public decimal VatFixed { get; set; } = 23;

    public decimal? StartReading { get; set; }

    public DateOnly? StartDate { get; set; }

    public int PeriodDays { get; set; } = 30;

    public string? Source { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed record UtilityEstimate(
    decimal UsedUnits,
    decimal UsedKwh,
    decimal DailyKwh,
    decimal CostToDate,
    decimal ProjectedKwh,
    decimal Energy,
    decimal EnergyTax,
    decimal Fixed,
    decimal Vat,
    decimal Fees,
    decimal Total);

public static class UtilityCalculator
{
    public static readonly UtilityService[] Simulated = [UtilityService.Electricity, UtilityService.Gas];

    public static bool CanSimulate(UtilityService service) => Simulated.Contains(service);

    public static UtilityTariff Default(UtilityService service) => service == UtilityService.Gas
        ? new UtilityTariff { Service = service, EnergyPrice = 0.11m, EnergyTax = 0.0011m, FixedDaily = 0.13m, ConversionFactor = 11.2m, VatEnergy = 23, VatFixed = 6 }
        : new UtilityTariff { Service = service, EnergyPrice = 0.135m, EnergyTax = 0.001m, FixedDaily = 0.27m, MonthlyFees = 3.10m, VatEnergy = 23, VatFixed = 23 };

    public static UtilityEstimate? Estimate(UtilityTariff tariff, decimal startReading, decimal currentReading, int elapsedDays, int periodDays)
    {
        if (currentReading < startReading || elapsedDays <= 0 || periodDays <= 0)
        {
            return null;
        }

        decimal factor = tariff.ConversionFactor > 0 ? tariff.ConversionFactor : 1;
        decimal usedUnits = currentReading - startReading;
        decimal usedKwh = usedUnits * factor;
        decimal dailyKwh = usedKwh / elapsedDays;
        decimal projectedKwh = dailyKwh * periodDays;
        (decimal energy, decimal tax, decimal fixedCost, decimal vat, decimal fees) = Cost(tariff, projectedKwh, periodDays);
        (decimal energyToDate, decimal taxToDate, decimal fixedToDate, decimal vatToDate, decimal feesToDate) = Cost(tariff, usedKwh, elapsedDays);
        decimal costToDate = Math.Round(energyToDate + taxToDate + fixedToDate + vatToDate + feesToDate, 2);

        return new UtilityEstimate(usedUnits, usedKwh, dailyKwh, costToDate, projectedKwh,
            Math.Round(energy, 2), Math.Round(tax, 2), Math.Round(fixedCost, 2), Math.Round(vat, 2), Math.Round(fees, 2),
            Math.Round(energy + tax + fixedCost + vat + fees, 2));
    }

    private static (decimal Energy, decimal Tax, decimal Fixed, decimal Vat, decimal Fees) Cost(UtilityTariff tariff, decimal kwh, int days)
    {
        decimal energy = kwh * tariff.EnergyPrice;
        decimal tax = kwh * tariff.EnergyTax;
        decimal fixedCost = tariff.FixedDaily * days;
        decimal vat = (energy + tax) * tariff.VatEnergy / 100 + fixedCost * tariff.VatFixed / 100;
        decimal fees = tariff.MonthlyFees * days / 30;
        return (energy, tax, fixedCost, vat, fees);
    }
}

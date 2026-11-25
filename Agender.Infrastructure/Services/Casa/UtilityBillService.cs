using Agender.Infrastructure.Data;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

public class UtilityBillService(IUserDbContextFactory contextFactory) : IUtilityBillService
{
    private const int MinYear = 2000;
    private const int MaxYear = 2100;

    public async Task<List<UtilityBill>> GetAsync(int fromYear, int toYear, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        return await context.UtilityBills.AsNoTracking()
            .Where(b => b.HouseholdId == householdId && b.Year >= fromYear && b.Year <= toYear)
            .OrderBy(b => b.Year).ThenBy(b => b.Month).ThenBy(b => b.Service)
            .ToListAsync(cancellationToken);
    }

    public async Task<UtilityBill> CreateAsync(UtilityBill bill, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        UtilityBill created = new() { HouseholdId = householdId };
        await ApplyAsync(context, householdId, created, bill, cancellationToken);
        context.UtilityBills.Add(created);
        await SyncTransactionAsync(context, created, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return created;
    }

    public async Task<UtilityBill> UpdateAsync(UtilityBill bill, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        UtilityBill stored = await context.UtilityBills.FirstOrDefaultAsync(b => b.Id == bill.Id && b.HouseholdId == householdId, cancellationToken)
            ?? throw new NotFoundException($"Utility bill {bill.Id} was not found.");

        await ApplyAsync(context, householdId, stored, bill, cancellationToken);
        await SyncTransactionAsync(context, stored, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return stored;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        UtilityBill stored = await context.UtilityBills.FirstOrDefaultAsync(b => b.Id == id && b.HouseholdId == householdId, cancellationToken)
            ?? throw new NotFoundException($"Utility bill {id} was not found.");

        if (stored.TransactionId is Guid transactionId)
        {
            await context.FinanceTransactions.Where(t => t.Id == transactionId && t.HouseholdId == householdId).ExecuteDeleteAsync(cancellationToken);
        }

        context.UtilityBills.Remove(stored);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<UtilityTariff>> GetTariffsAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        return await context.UtilityTariffs.AsNoTracking().Where(t => t.HouseholdId == householdId).ToListAsync(cancellationToken);
    }

    public async Task<UtilityTariff> SaveTariffAsync(UtilityTariff tariff, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        if (!UtilityCalculator.CanSimulate(tariff.Service))
        {
            throw new ArgumentException("The service has no simulator.");
        }

        decimal[] prices = [tariff.EnergyPrice, tariff.EnergyTax, tariff.FixedDaily, tariff.ConversionFactor, tariff.MonthlyFees];

        if (prices.Any(p => p < 0 || p > UtilityTariff.MaxPrice) || tariff.ConversionFactor == 0)
        {
            throw new ArgumentException("A price is out of range.");
        }

        if (tariff.VatEnergy is < 0 or > 100 || tariff.VatFixed is < 0 or > 100)
        {
            throw new ArgumentException("The VAT rate is out of range.");
        }

        if (tariff.PeriodDays is < 1 or > UtilityTariff.MaxPeriodDays || tariff.StartReading is < 0 or > UtilityTariff.MaxReading)
        {
            throw new ArgumentException("The reading or period is out of range.");
        }

        UtilityTariff? stored = await context.UtilityTariffs.FirstOrDefaultAsync(t => t.HouseholdId == householdId && t.Service == tariff.Service, cancellationToken);

        if (stored is null)
        {
            stored = new UtilityTariff { HouseholdId = householdId, Service = tariff.Service };
            context.UtilityTariffs.Add(stored);
        }

        stored.EnergyPrice = tariff.EnergyPrice;
        stored.EnergyTax = tariff.EnergyTax;
        stored.FixedDaily = tariff.FixedDaily;
        stored.ConversionFactor = tariff.ConversionFactor;
        stored.MonthlyFees = tariff.MonthlyFees;
        stored.VatEnergy = tariff.VatEnergy;
        stored.VatFixed = tariff.VatFixed;
        stored.StartReading = tariff.StartReading;
        stored.StartDate = tariff.StartDate;
        stored.PeriodDays = tariff.PeriodDays;
        stored.Source = HouseholdAccess.OptionalText(tariff.Source, "The source", UtilityTariff.SourceMaxLength);
        stored.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return stored;
    }

    private static async Task ApplyAsync(AgenderDbContext context, Guid householdId, UtilityBill target, UtilityBill source, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(source.Service))
        {
            throw new ArgumentException("The service is not valid.");
        }

        if (source.Year is < MinYear or > MaxYear || source.Month is < 1 or > 12)
        {
            throw new ArgumentException("The month is not valid.");
        }

        decimal amount = Math.Round(source.Amount, 2);

        if (amount <= 0 || amount > UtilityBill.MaxAmount)
        {
            throw new ArgumentException("The amount is out of range.");
        }

        if (source.Consumption is { } consumption && (consumption < 0 || consumption > UtilityBill.MaxConsumption))
        {
            throw new ArgumentException("The consumption is out of range.");
        }

        if (source.PersonId is Guid personId && !await context.FinancePeople.AnyAsync(p => p.Id == personId && p.HouseholdId == householdId, cancellationToken))
        {
            throw new ArgumentException("The person does not exist.");
        }

        bool taken = await context.UtilityBills.AnyAsync(b => b.HouseholdId == householdId && b.Id != target.Id
            && b.Service == source.Service && b.Year == source.Year && b.Month == source.Month, cancellationToken);

        if (taken)
        {
            throw new ArgumentException("There is already a bill for this service and month.");
        }

        target.Service = source.Service;
        target.Year = source.Year;
        target.Month = source.Month;
        target.Amount = amount;
        target.Consumption = source.Consumption is { } value ? Math.Round(value, 2) : null;
        target.Provider = HouseholdAccess.OptionalText(source.Provider, "The provider", UtilityBill.ProviderMaxLength);
        target.Note = HouseholdAccess.OptionalText(source.Note, "The note", UtilityBill.NoteMaxLength);
        target.DueDate = source.DueDate;
        target.PaidOn = source.PaidOn;
        target.PersonId = source.PersonId;
    }

    private static async Task SyncTransactionAsync(AgenderDbContext context, UtilityBill bill, CancellationToken cancellationToken)
    {
        FinanceTransaction? transaction = bill.TransactionId is Guid id
            ? await context.FinanceTransactions.FirstOrDefaultAsync(t => t.Id == id && t.HouseholdId == bill.HouseholdId, cancellationToken)
            : null;

        if (bill.PaidOn is not { } paidOn)
        {
            if (transaction is not null)
            {
                context.FinanceTransactions.Remove(transaction);
            }

            bill.TransactionId = null;
            return;
        }

        if (transaction is null)
        {
            transaction = new FinanceTransaction { HouseholdId = bill.HouseholdId, Type = TransactionType.Expense };
            context.FinanceTransactions.Add(transaction);
            bill.TransactionId = transaction.Id;
        }

        transaction.Amount = bill.Amount;
        transaction.Category = UtilityCatalog.ExpenseCategory;
        transaction.Description = UtilityCatalog.Describe(bill);
        transaction.Date = paidOn;
        transaction.PersonId = bill.PersonId;
    }
}

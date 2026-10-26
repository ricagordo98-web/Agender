using Agender.Infrastructure.Data;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

public class FinanceService(IUserDbContextFactory contextFactory, TimeProvider? time = null) : IFinanceService
{
    public const string SharedOwner = "shared";

    private const int TrendMonths = 6;
    private const decimal MaxAmount = 100_000_000m;

    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public async Task<FinanceSummary> GetSummaryAsync(int year, int month, string? owner = null, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        return await BuildSummaryAsync(context, householdId, year, month, cancellationToken, owner);
    }

    public async Task<IReadOnlyList<FinanceTransaction>> GetTransactionsAsync(int year, int month, string? owner = null, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);
        DateOnly from = MonthStart(year, month);
        DateOnly to = from.AddMonths(1);

        List<FinanceTransaction> list = await ApplyOwner(context.FinanceTransactions.AsNoTracking(), owner)
            .Where(t => t.HouseholdId == householdId && t.Date >= from && t.Date < to)
            .ToListAsync(cancellationToken);

        return [.. list.OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAt)];
    }

    public async Task<FinanceTransaction> CreateTransactionAsync(FinanceTransaction transaction, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        FinanceTransaction created = new() { HouseholdId = householdId };
        await ApplyAsync(context, householdId, created, transaction, cancellationToken);

        context.FinanceTransactions.Add(created);
        await AdjustGoalAsync(context, householdId, created.GoalId, GoalDelta(created), cancellationToken);
        await AdjustAccountAsync(context, householdId, created.SavingsAccountId, GoalDelta(created), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return created;
    }

    public async Task UpdateTransactionAsync(FinanceTransaction transaction, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        FinanceTransaction stored = await context.FinanceTransactions.FirstOrDefaultAsync(t => t.Id == transaction.Id && t.HouseholdId == householdId, cancellationToken)
            ?? throw new NotFoundException($"Transaction {transaction.Id} was not found.");

        await AdjustGoalAsync(context, householdId, stored.GoalId, -GoalDelta(stored), cancellationToken);
        await AdjustAccountAsync(context, householdId, stored.SavingsAccountId, -GoalDelta(stored), cancellationToken);
        await ApplyAsync(context, householdId, stored, transaction, cancellationToken);
        stored.IsImported = false;
        await AdjustGoalAsync(context, householdId, stored.GoalId, GoalDelta(stored), cancellationToken);
        await AdjustAccountAsync(context, householdId, stored.SavingsAccountId, GoalDelta(stored), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteTransactionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        FinanceTransaction stored = await context.FinanceTransactions.FirstOrDefaultAsync(t => t.Id == id && t.HouseholdId == householdId, cancellationToken)
            ?? throw new NotFoundException($"Transaction {id} was not found.");

        await AdjustGoalAsync(context, householdId, stored.GoalId, -GoalDelta(stored), cancellationToken);
        await AdjustAccountAsync(context, householdId, stored.SavingsAccountId, -GoalDelta(stored), cancellationToken);
        context.FinanceTransactions.Remove(stored);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FinancePerson>> GetPeopleAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        return await FinancePeople.EnsureAsync(context, householdId, cancellationToken);
    }

    public async Task<FinancePerson> CreatePersonAsync(FinancePerson person, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);
        List<FinancePerson> people = await FinancePeople.EnsureAsync(context, householdId, cancellationToken);

        FinancePerson created = new() { HouseholdId = householdId, Order = people.Count == 0 ? 0 : people.Max(p => p.Order) + 1 };
        await ApplyPersonAsync(context, householdId, created, person, people, cancellationToken);

        context.FinancePeople.Add(created);
        await context.SaveChangesAsync(cancellationToken);

        return created;
    }

    public async Task UpdatePersonAsync(FinancePerson person, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);
        List<FinancePerson> people = await FinancePeople.EnsureAsync(context, householdId, cancellationToken);

        FinancePerson stored = people.FirstOrDefault(p => p.Id == person.Id) ?? throw new NotFoundException($"Person {person.Id} was not found.");
        stored = await context.FinancePeople.FirstAsync(p => p.Id == stored.Id, cancellationToken);

        await ApplyPersonAsync(context, householdId, stored, person, people, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeletePersonAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        FinancePerson stored = await context.FinancePeople.FirstOrDefaultAsync(p => p.Id == id && p.HouseholdId == householdId, cancellationToken)
            ?? throw new NotFoundException($"Person {id} was not found.");

        bool used = await context.FinanceTransactions.AnyAsync(t => t.PersonId == id, cancellationToken)
            || await context.SavingsAccounts.AnyAsync(a => a.PersonId == id, cancellationToken)
            || await context.SavingsSnapshots.AnyAsync(s => s.PersonId == id, cancellationToken);

        if (used)
        {
            throw new ArgumentException("The person still has movements or savings.");
        }

        context.FinancePeople.Remove(stored);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<BalanceSheet> GetBalanceAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        return await BuildBalanceAsync(context, householdId, DateOnly.FromDateTime(_time.GetLocalNow().DateTime), cancellationToken);
    }

    public async Task<IReadOnlyList<SavingsAccount>> GetSavingsAccountsAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        return await context.SavingsAccounts.AsNoTracking().Where(a => a.HouseholdId == householdId).OrderBy(a => a.Order).ThenBy(a => a.Name).ToListAsync(cancellationToken);
    }

    public async Task<SavingsAccount> CreateSavingsAccountAsync(SavingsAccount account, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        int next = await context.SavingsAccounts.Where(a => a.HouseholdId == householdId).Select(a => (int?)a.Order).MaxAsync(cancellationToken) ?? -1;
        SavingsAccount created = new() { HouseholdId = householdId, Order = next + 1 };
        await ApplyAccountAsync(context, householdId, created, account, cancellationToken);

        context.SavingsAccounts.Add(created);
        await context.SaveChangesAsync(cancellationToken);

        return created;
    }

    public async Task UpdateSavingsAccountAsync(SavingsAccount account, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        SavingsAccount stored = await context.SavingsAccounts.FirstOrDefaultAsync(a => a.Id == account.Id && a.HouseholdId == householdId, cancellationToken)
            ?? throw new NotFoundException($"Savings account {account.Id} was not found.");

        await ApplyAccountAsync(context, householdId, stored, account, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteSavingsAccountAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        await context.FinanceTransactions.Where(t => t.SavingsAccountId == id && t.HouseholdId == householdId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.SavingsAccountId, (Guid?)null), cancellationToken);
        int deleted = await context.SavingsAccounts.Where(a => a.Id == id && a.HouseholdId == householdId).ExecuteDeleteAsync(cancellationToken);

        if (deleted == 0)
        {
            throw new NotFoundException($"Savings account {id} was not found.");
        }
    }

    public async Task<IReadOnlyList<SavingsSnapshot>> GetSavingsSnapshotsAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        return await context.SavingsSnapshots.AsNoTracking().Where(s => s.HouseholdId == householdId).OrderBy(s => s.Year).ToListAsync(cancellationToken);
    }

    public async Task SetSavingsSnapshotAsync(int year, Guid personId, decimal value, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        if (year is < 2000 or > 2100 || value < 0 || value > MaxAmount)
        {
            throw new ArgumentException("The snapshot is out of range.");
        }

        await EnsurePersonAsync(context, householdId, personId, cancellationToken);

        SavingsSnapshot? existing = await context.SavingsSnapshots.FirstOrDefaultAsync(s => s.HouseholdId == householdId && s.Year == year && s.PersonId == personId, cancellationToken);

        if (existing is null)
        {
            context.SavingsSnapshots.Add(new SavingsSnapshot { HouseholdId = householdId, Year = year, PersonId = personId, Value = Math.Round(value, 2) });
        }
        else
        {
            existing.Value = Math.Round(value, 2);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteSavingsSnapshotAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        await context.SavingsSnapshots.Where(s => s.Id == id && s.HouseholdId == householdId).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SavingsGoal>> GetGoalsAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        return await context.SavingsGoals.AsNoTracking().Where(g => g.HouseholdId == householdId).OrderBy(g => g.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<SavingsGoal> CreateGoalAsync(SavingsGoal goal, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        SavingsGoal created = new() { HouseholdId = householdId };
        ApplyGoal(created, goal);

        context.SavingsGoals.Add(created);
        await context.SaveChangesAsync(cancellationToken);

        return created;
    }

    public async Task UpdateGoalAsync(SavingsGoal goal, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        SavingsGoal stored = await context.SavingsGoals.FirstOrDefaultAsync(g => g.Id == goal.Id && g.HouseholdId == householdId, cancellationToken)
            ?? throw new NotFoundException($"Goal {goal.Id} was not found.");

        ApplyGoal(stored, goal);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteGoalAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        SavingsGoal stored = await context.SavingsGoals.FirstOrDefaultAsync(g => g.Id == id && g.HouseholdId == householdId, cancellationToken)
            ?? throw new NotFoundException($"Goal {id} was not found.");

        await context.FinanceTransactions.Where(t => t.GoalId == id).ExecuteUpdateAsync(setters => setters.SetProperty(t => t.GoalId, (Guid?)null), cancellationToken);
        context.SavingsGoals.Remove(stored);
        await context.SaveChangesAsync(cancellationToken);
    }

    internal static async Task<BalanceSheet> BuildBalanceAsync(AgenderDbContext context, Guid householdId, DateOnly today, CancellationToken cancellationToken)
    {
        List<FinancePerson> people = await FinancePeople.EnsureAsync(context, householdId, cancellationToken);
        List<FinanceTransaction> transactions = await context.FinanceTransactions.AsNoTracking().Where(t => t.HouseholdId == householdId).ToListAsync(cancellationToken);
        List<SavingsAccount> accounts = await context.SavingsAccounts.AsNoTracking().Where(a => a.HouseholdId == householdId).ToListAsync(cancellationToken);
        List<SavingsSnapshot> snapshots = await context.SavingsSnapshots.AsNoTracking().Where(s => s.HouseholdId == householdId).ToListAsync(cancellationToken);

        return BalanceCalculator.Compute(people, transactions, accounts, snapshots, today);
    }

    internal static async Task<FinanceSummary> BuildSummaryAsync(AgenderDbContext context, Guid householdId, int year, int month, CancellationToken cancellationToken, string? owner = null)
    {
        DateOnly start = MonthStart(year, month);
        DateOnly trendFrom = start.AddMonths(-(TrendMonths - 1));
        DateOnly end = start.AddMonths(1);

        List<FinanceTransaction> window = await ApplyOwner(context.FinanceTransactions.AsNoTracking(), owner)
            .Where(t => t.HouseholdId == householdId && t.Date >= trendFrom && t.Date < end)
            .ToListAsync(cancellationToken);

        List<FinanceTransaction> current = [.. window.Where(t => t.Date >= start)];

        FinanceSummary summary = new()
        {
            Year = year,
            Month = month,
            Income = Income(current),
            Expenses = Expenses(current),
            Savings = Savings(current),
            ExpensesByCategory =
            [
                .. current.Where(t => t.Type == TransactionType.Expense && !t.IsTransfer)
                    .GroupBy(t => t.Category)
                    .Select(group => new CategoryTotal(group.Key, group.Sum(t => t.Amount)))
                    .OrderByDescending(c => c.Total)
            ]
        };

        for (DateOnly cursor = trendFrom; cursor < end; cursor = cursor.AddMonths(1))
        {
            List<FinanceTransaction> inMonth = [.. window.Where(t => t.Date.Year == cursor.Year && t.Date.Month == cursor.Month)];

            summary.Trend.Add(new MonthTotal(cursor.Year, cursor.Month, Income(inMonth), Expenses(inMonth), Savings(inMonth)));
        }

        List<decimal> goals = await context.SavingsGoals.AsNoTracking().Where(g => g.HouseholdId == householdId).Select(g => g.Current).ToListAsync(cancellationToken);
        summary.SavingsTotal = goals.Sum();

        return summary;
    }

    private static decimal Income(IEnumerable<FinanceTransaction> transactions) =>
        transactions.Where(t => t.Type == TransactionType.Income && !t.IsTransfer).Sum(t => t.Amount);

    private static decimal Expenses(IEnumerable<FinanceTransaction> transactions) =>
        transactions.Where(t => t.Type == TransactionType.Expense && !t.IsTransfer).Sum(t => t.Amount);

    private static decimal Savings(IEnumerable<FinanceTransaction> transactions) =>
        transactions.Where(t => t.Type == TransactionType.Saving).Sum(t => t.IsWithdrawal ? -t.Amount : t.Amount);

    private static IQueryable<FinanceTransaction> ApplyOwner(IQueryable<FinanceTransaction> query, string? owner)
    {
        if (string.IsNullOrWhiteSpace(owner))
        {
            return query;
        }

        if (owner == SharedOwner)
        {
            return query.Where(t => t.PersonId == null);
        }

        if (!Guid.TryParse(owner, out Guid personId))
        {
            throw new ArgumentException("The owner filter is not valid.");
        }

        return query.Where(t => t.PersonId == personId);
    }

    private static decimal GoalDelta(FinanceTransaction transaction) =>
        transaction.Type == TransactionType.Saving ? (transaction.IsWithdrawal ? -transaction.Amount : transaction.Amount) : 0;

    private static DateOnly MonthStart(int year, int month)
    {
        if (year is < 2000 or > 2100 || month is < 1 or > 12)
        {
            throw new ArgumentException("The month is out of range.");
        }

        return new DateOnly(year, month, 1);
    }

    private static async Task EnsurePersonAsync(AgenderDbContext context, Guid householdId, Guid personId, CancellationToken cancellationToken)
    {
        if (!await context.FinancePeople.AnyAsync(p => p.Id == personId && p.HouseholdId == householdId, cancellationToken))
        {
            throw new ArgumentException("The person does not exist.");
        }
    }

    private static async Task ApplyAsync(AgenderDbContext context, Guid householdId, FinanceTransaction target, FinanceTransaction source, CancellationToken cancellationToken)
    {
        decimal amount = Math.Round(Math.Abs(source.Amount), 2);

        if (amount <= 0 || amount > MaxAmount)
        {
            throw new ArgumentException("The amount is out of range.");
        }

        if (source.IsTransfer && (source.PersonId is null || source.Type == TransactionType.Saving))
        {
            throw new ArgumentException("A transfer needs a person and cannot be a saving.");
        }

        Guid? goalId = null;

        if (source.Type == TransactionType.Saving && source.GoalId is Guid requested)
        {
            if (!await context.SavingsGoals.AnyAsync(g => g.Id == requested && g.HouseholdId == householdId, cancellationToken))
            {
                throw new ArgumentException("The goal does not exist.");
            }

            goalId = requested;
        }

        Guid? accountId = null;

        if (source.Type == TransactionType.Saving && source.SavingsAccountId is Guid requestedAccount)
        {
            if (!await context.SavingsAccounts.AnyAsync(a => a.Id == requestedAccount && a.HouseholdId == householdId, cancellationToken))
            {
                throw new ArgumentException("The savings account does not exist.");
            }

            accountId = requestedAccount;
        }

        if (source.PersonId is Guid personId)
        {
            await FinancePeople.EnsureAsync(context, householdId, cancellationToken);
            await EnsurePersonAsync(context, householdId, personId, cancellationToken);
        }

        target.Type = source.Type;
        target.Amount = amount;
        target.Category = string.IsNullOrWhiteSpace(source.Category) ? DefaultCategory(source.Type) : HouseholdAccess.RequiredText(source.Category, "The category", 60);
        target.Description = HouseholdAccess.OptionalText(source.Description, "The description", FinanceTransaction.DescriptionMaxLength);
        target.Date = source.Date;
        target.GoalId = goalId;
        target.SavingsAccountId = accountId;
        target.PersonId = source.PersonId;
        target.IsTransfer = source.IsTransfer;
        target.IsWithdrawal = source.Type == TransactionType.Saving && source.IsWithdrawal;
    }

    private static string DefaultCategory(TransactionType type) => type switch
    {
        TransactionType.Income => CasaCatalog.DefaultIncomeCategory,
        TransactionType.Saving => CasaCatalog.SavingCategory,
        _ => CasaCatalog.DefaultExpenseCategory
    };

    private static async Task AdjustAccountAsync(AgenderDbContext context, Guid householdId, Guid? accountId, decimal delta, CancellationToken cancellationToken)
    {
        if (accountId is null || delta == 0)
        {
            return;
        }

        SavingsAccount? account = await context.SavingsAccounts.FirstOrDefaultAsync(a => a.Id == accountId && a.HouseholdId == householdId, cancellationToken);

        if (account is not null)
        {
            account.Value += delta;
        }
    }

    private static async Task AdjustGoalAsync(AgenderDbContext context, Guid householdId, Guid? goalId, decimal delta, CancellationToken cancellationToken)
    {
        if (goalId is null || delta == 0)
        {
            return;
        }

        SavingsGoal? goal = await context.SavingsGoals.FirstOrDefaultAsync(g => g.Id == goalId && g.HouseholdId == householdId, cancellationToken);

        if (goal is not null)
        {
            goal.Current = Math.Max(0, goal.Current + delta);
        }
    }

    private static void ApplyGoal(SavingsGoal target, SavingsGoal source)
    {
        decimal goalTarget = Math.Round(source.Target, 2);
        decimal current = Math.Round(source.Current, 2);

        if (goalTarget <= 0 || goalTarget > MaxAmount || current < 0 || current > MaxAmount)
        {
            throw new ArgumentException("The goal amounts are out of range.");
        }

        target.Name = HouseholdAccess.RequiredText(source.Name, "The name", SavingsGoal.NameMaxLength);
        target.Emoji = string.IsNullOrWhiteSpace(source.Emoji) ? target.Emoji : source.Emoji;
        target.Target = goalTarget;
        target.Current = current;
        target.Deadline = source.Deadline;
        target.Color = HouseholdAccess.NormalizeColor(source.Color, target.Color);
    }

    private static async Task ApplyPersonAsync(AgenderDbContext context, Guid householdId, FinancePerson target, FinancePerson source, List<FinancePerson> people, CancellationToken cancellationToken)
    {
        string name = HouseholdAccess.RequiredText(source.Name, "The name", FinancePerson.NameMaxLength);

        if (people.Any(p => p.Id != target.Id && FinancePeople.Key(p.Name) == FinancePeople.Key(name)))
        {
            throw new ArgumentException("There is already a person with that name.");
        }

        if (source.MemberUserId is { Length: > 0 } memberId)
        {
            bool isMember = await context.HouseholdMembers.AnyAsync(m => m.HouseholdId == householdId && m.UserId == memberId, cancellationToken);

            if (!isMember)
            {
                throw new ArgumentException("The member does not belong to the household.");
            }

            if (people.Any(p => p.Id != target.Id && p.MemberUserId == memberId))
            {
                throw new ArgumentException("That member is already linked to another person.");
            }
        }

        target.Name = name;
        target.Color = HouseholdAccess.NormalizeColor(source.Color, target.Color);
        target.MemberUserId = string.IsNullOrEmpty(source.MemberUserId) ? null : source.MemberUserId;
        target.Order = source.Order;
    }

    private static async Task ApplyAccountAsync(AgenderDbContext context, Guid householdId, SavingsAccount target, SavingsAccount source, CancellationToken cancellationToken)
    {
        decimal value = Math.Round(source.Value, 2);
        decimal initial = Math.Round(source.InitialValue, 2);

        if (value < 0 || value > MaxAmount || initial < 0 || initial > MaxAmount)
        {
            throw new ArgumentException("The savings amounts are out of range.");
        }

        if (source.PersonId is Guid personId)
        {
            await EnsurePersonAsync(context, householdId, personId, cancellationToken);
        }

        target.Name = HouseholdAccess.RequiredText(source.Name, "The name", SavingsAccount.NameMaxLength);
        target.Bank = HouseholdAccess.OptionalText(source.Bank, "The bank", SavingsAccount.BankMaxLength);
        target.PersonId = source.PersonId;
        target.Value = value;
        target.InitialValue = initial;
    }
}

using Agender.Shared.Contracts;

namespace Agender.Shared.Domain;

public static class BalanceCalculator
{
    public static BalanceSheet Compute(
        IReadOnlyList<FinancePerson> people,
        IReadOnlyList<FinanceTransaction> transactions,
        IReadOnlyList<SavingsAccount> accounts,
        IReadOnlyList<SavingsSnapshot> snapshots,
        DateOnly today)
    {
        List<FinancePerson> ordered = [.. people.OrderBy(p => p.Order).ThenBy(p => p.CreatedAt).ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)];
        BalanceSheet sheet = new() { People = [.. ordered.Select(p => new BalancePerson(p.Id, p.Name, p.Color))] };

        if (ordered.Count == 0)
        {
            return sheet;
        }

        int first = new[] { today.Year }
            .Concat(transactions.Select(t => t.Date.Year))
            .Concat(snapshots.Select(s => s.Year))
            .Min();
        int last = Math.Max(today.Year, transactions.Count == 0 ? today.Year : transactions.Max(t => t.Date.Year));

        for (int year = first; year <= last; year++)
        {
            sheet.Years.Add(ComputeYear(year, ordered, transactions, accounts, snapshots, today));
        }

        sheet.Current = sheet.Years.FirstOrDefault(y => y.Year == today.Year) ?? sheet.Years.LastOrDefault();

        return sheet;
    }

    public static decimal AccountBalance(FinancePerson person, int peopleCount, IEnumerable<FinanceTransaction> transactions, DateOnly cutoff)
    {
        decimal balance = 0;

        foreach (FinanceTransaction transaction in transactions)
        {
            if (transaction.Date > cutoff)
            {
                continue;
            }

            decimal value = transaction.SignedValue;

            if (transaction.IsTransfer)
            {
                if (transaction.PersonId == person.Id)
                {
                    balance += value;
                }
                else if (transaction.PersonId is not null && peopleCount > 1)
                {
                    balance -= value / (peopleCount - 1);
                }
            }
            else if (transaction.PersonId == person.Id)
            {
                balance += value;
            }
            else if (transaction.PersonId is null)
            {
                balance += value / peopleCount;
            }
        }

        return balance;
    }

    public static decimal CurrentSavings(FinancePerson person, int peopleCount, IEnumerable<SavingsAccount> accounts) =>
        accounts.Where(a => a.PersonId == person.Id).Sum(a => a.Value)
        + accounts.Where(a => a.PersonId is null).Sum(a => a.Value) / peopleCount;

    private static BalanceYear ComputeYear(
        int year,
        List<FinancePerson> people,
        IReadOnlyList<FinanceTransaction> transactions,
        IReadOnlyList<SavingsAccount> accounts,
        IReadOnlyList<SavingsSnapshot> snapshots,
        DateOnly today)
    {
        DateOnly cutoff = new(year, 12, 31);
        List<SavingsSnapshot> ofYear = [.. snapshots.Where(s => s.Year == year)];
        List<decimal> account = [];
        List<decimal> savings = [];

        foreach (FinancePerson person in people)
        {
            account.Add(Round(AccountBalance(person, people.Count, transactions, cutoff)));

            decimal saved = ofYear.Count > 0
                ? ofYear.FirstOrDefault(s => s.PersonId == person.Id)?.Value ?? 0
                : year == today.Year ? CurrentSavings(person, people.Count, accounts) : 0;

            savings.Add(Round(saved));
        }

        return new BalanceYear(year, account, savings, [.. account.Select((value, index) => value + savings[index])]);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

using Agender.Infrastructure.Data;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

public class FinanceImportService(IUserDbContextFactory contextFactory, TimeProvider? time = null) : IFinanceImportService
{
    public const int MaxWorkbookBytes = 25 * 1024 * 1024;

    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public async Task<ImportReport> ImportAsync(byte[] workbook, bool apply, CancellationToken cancellationToken = default)
    {
        if (workbook.Length == 0 || workbook.Length > MaxWorkbookBytes)
        {
            throw new ArgumentException("The file is empty or too large.");
        }

        ParsedWorkbook parsed = ReadWorkbook(workbook);

        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);
        DateOnly today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

        List<FinancePerson> existing = await FinancePeople.EnsureAsync(context, householdId, cancellationToken);
        List<string> names = PersonNames(parsed);
        List<string> missing = [.. names.Where(name => MatchExisting(existing, name) is null)];

        if (apply)
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            await context.FinanceTransactions.Where(t => t.HouseholdId == householdId && t.IsImported).ExecuteDeleteAsync(cancellationToken);

            if (parsed.Accounts.Count > 0 || parsed.Snapshots.Count > 0)
            {
                await context.FinanceTransactions.Where(t => t.HouseholdId == householdId && t.SavingsAccountId != null)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.SavingsAccountId, (Guid?)null), cancellationToken);
                await context.SavingsAccounts.Where(a => a.HouseholdId == householdId).ExecuteDeleteAsync(cancellationToken);
                await context.SavingsSnapshots.Where(s => s.HouseholdId == householdId).ExecuteDeleteAsync(cancellationToken);
            }

            int order = existing.Count == 0 ? 0 : existing.Max(p => p.Order) + 1;

            foreach (string name in missing)
            {
                context.FinancePeople.Add(new FinancePerson
                {
                    HouseholdId = householdId,
                    Name = name,
                    Color = CasaCatalog.MemberColors[(order) % CasaCatalog.MemberColors.Length],
                    Order = order++
                });
            }

            await context.SaveChangesAsync(cancellationToken);
            await LinkMembersAsync(context, householdId, cancellationToken);

            List<FinancePerson> all = await context.FinancePeople.Where(p => p.HouseholdId == householdId).ToListAsync(cancellationToken);
            Dictionary<string, Guid> ids = [];
            int position = 0;

            foreach (string name in names)
            {
                FinancePerson person = MatchExisting(all, name) ?? throw new InvalidOperationException($"Person '{name}' was not created.");
                person.Name = name;
                person.Order = position++;
                ids[FinancePeople.Key(name)] = person.Id;
            }

            foreach (FinancePerson other in all.Where(p => !ids.ContainsValue(p.Id)))
            {
                other.Order = position++;
            }

            await context.SaveChangesAsync(cancellationToken);

            context.FinanceTransactions.AddRange(parsed.Rows.Select(row => ToTransaction(row, householdId, ids)));
            context.SavingsAccounts.AddRange(parsed.Accounts.Select((account, index) => new SavingsAccount
            {
                HouseholdId = householdId,
                Name = Trim(account.Name, SavingsAccount.NameMaxLength),
                Bank = account.Bank is { Length: > 0 } ? Trim(account.Bank, SavingsAccount.BankMaxLength) : null,
                PersonId = account.Person is null ? null : ids[FinancePeople.Key(account.Person)],
                Value = account.Value,
                InitialValue = account.Initial,
                Order = index
            }));
            context.SavingsSnapshots.AddRange(parsed.Snapshots
                .Where(s => ids.ContainsKey(FinancePeople.Key(s.Person)))
                .GroupBy(s => (s.Year, Key: FinancePeople.Key(s.Person)))
                .Select(group => new SavingsSnapshot { HouseholdId = householdId, Year = group.Key.Year, PersonId = ids[group.Key.Key], Value = group.Last().Value }));

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        BalanceYear preview = await PreviewAsync(context, householdId, parsed, existing, names, today, apply, cancellationToken);

        return new ImportReport(
            apply,
            parsed.RowsRead,
            parsed.Rows.Count,
            parsed.RowsSkipped,
            parsed.Rows.Count(r => r.IsTransfer),
            parsed.Rows.Count(r => r.Person is null),
            parsed.Accounts.Count,
            parsed.Snapshots.Count,
            names,
            parsed.Normalized,
            parsed.Warnings,
            preview);
    }

    private static ParsedWorkbook ReadWorkbook(byte[] bytes)
    {
        try
        {
            using MemoryStream stream = new(bytes);
            using XLWorkbook workbook = new(stream);

            return ExcelFinanceParser.Parse(workbook);
        }
        catch (Exception ex) when (ex is not ArgumentException and not OperationCanceledException)
        {
            throw new ArgumentException("The file is not a valid Excel workbook.", ex);
        }
    }

    private static List<string> PersonNames(ParsedWorkbook parsed) =>
    [
        .. parsed.Rows.Select(r => r.Person)
            .Concat(parsed.Accounts.Select(a => a.Person))
            .Concat(parsed.Snapshots.Select(s => s.Person))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())
            .GroupBy(FinancePeople.Key)
            .Select(group => group.First())
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
    ];

    private static FinanceTransaction ToTransaction(ParsedRow row, Guid householdId, Dictionary<string, Guid> ids) => new()
    {
        HouseholdId = householdId,
        Type = row.Type,
        Amount = row.Amount,
        Category = Trim(row.Category, 60),
        Description = row.Description,
        Date = row.Date,
        PersonId = row.Person is null ? null : ids[FinancePeople.Key(row.Person)],
        IsTransfer = row.IsTransfer,
        IsWithdrawal = row.IsWithdrawal,
        IsImported = true
    };

    private async Task<BalanceYear> PreviewAsync(
        AgenderDbContext context,
        Guid householdId,
        ParsedWorkbook parsed,
        List<FinancePerson> existing,
        List<string> names,
        DateOnly today,
        bool applied,
        CancellationToken cancellationToken)
    {
        if (applied)
        {
            BalanceSheet stored = await FinanceService.BuildBalanceAsync(context, householdId, today, cancellationToken);

            return stored.Current ?? new BalanceYear(today.Year, [], [], []);
        }

        List<FinancePerson> people = [.. existing];
        int order = existing.Count == 0 ? 0 : existing.Max(p => p.Order) + 1;

        foreach (string name in names.Where(n => MatchExisting(existing, n) is null))
        {
            people.Add(new FinancePerson { Name = name, Order = order++ });
        }

        Dictionary<string, Guid> ids = names.ToDictionary(FinancePeople.Key, name => MatchExisting(people, name)!.Id);
        List<FinanceTransaction> others = await context.FinanceTransactions.AsNoTracking().Where(t => t.HouseholdId == householdId && !t.IsImported).ToListAsync(cancellationToken);
        List<FinanceTransaction> all = [.. others, .. parsed.Rows.Select(row => ToTransaction(row, householdId, ids))];

        List<SavingsAccount> accounts = parsed.Accounts.Count > 0
            ? [.. parsed.Accounts.Select(a => new SavingsAccount { Name = a.Name, PersonId = a.Person is null ? null : ids[FinancePeople.Key(a.Person)], Value = a.Value })]
            : await context.SavingsAccounts.AsNoTracking().Where(a => a.HouseholdId == householdId).ToListAsync(cancellationToken);

        List<SavingsSnapshot> snapshots = parsed.Snapshots.Count > 0
            ? [.. parsed.Snapshots.Where(s => ids.ContainsKey(FinancePeople.Key(s.Person))).Select(s => new SavingsSnapshot { Year = s.Year, PersonId = ids[FinancePeople.Key(s.Person)], Value = s.Value })]
            : await context.SavingsSnapshots.AsNoTracking().Where(s => s.HouseholdId == householdId).ToListAsync(cancellationToken);

        BalanceSheet sheet = BalanceCalculator.Compute(people, all, accounts, snapshots, today);

        return sheet.Current ?? new BalanceYear(today.Year, [], [], []);
    }

    private static async Task LinkMembersAsync(AgenderDbContext context, Guid householdId, CancellationToken cancellationToken)
    {
        List<FinancePerson> people = await context.FinancePeople.Where(p => p.HouseholdId == householdId).ToListAsync(cancellationToken);

        var members = await (
            from member in context.HouseholdMembers.AsNoTracking()
            join user in context.Users.AsNoTracking() on member.UserId equals user.Id
            where member.HouseholdId == householdId
            select new { member.UserId, user.DisplayName }).ToListAsync(cancellationToken);

        foreach (var member in members)
        {
            if (people.Any(p => p.MemberUserId == member.UserId))
            {
                continue;
            }

            string full = FinancePeople.Key(member.DisplayName);
            string first = FinancePeople.Key(member.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault());
            FinancePerson? match = people.FirstOrDefault(p => p.MemberUserId is null && (FinancePeople.Key(p.Name) == full || FinancePeople.Key(p.Name) == first));

            if (match is not null)
            {
                match.MemberUserId = member.UserId;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static FinancePerson? MatchExisting(IReadOnlyList<FinancePerson> people, string name)
    {
        string key = FinancePeople.Key(name);

        return people.FirstOrDefault(p => FinancePeople.Key(p.Name) == key)
            ?? people.FirstOrDefault(p => p.MemberUserId is not null && FinancePeople.Key(p.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()) == key);
    }

    private static string Trim(string text, int maxLength) => text.Length <= maxLength ? text : text[..maxLength];
}

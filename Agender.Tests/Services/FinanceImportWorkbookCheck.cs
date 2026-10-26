using Agender.Infrastructure.Services;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using ClosedXML.Excel;
using FluentAssertions;
using Xunit.Abstractions;

namespace Agender.Tests.Services;

public class FinanceImportWorkbookCheck(ITestOutputHelper output)
{
    public const string PathVariable = "AGENDER_FINANCE_XLSX";

    [Fact]
    public void WorkbookFromEnvironment_ReproducesTheOverviewSheetBalances()
    {
        if (Environment.GetEnvironmentVariable(PathVariable) is not { Length: > 0 } path || !File.Exists(path))
        {
            return;
        }

        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using XLWorkbook workbook = new(stream);
        ParsedWorkbook parsed = ExcelFinanceParser.Parse(workbook);

        List<FinancePerson> people = [.. parsed.Rows.Select(r => r.Person).Where(p => p is not null).Distinct().Order().Select((name, index) => new FinancePerson { Name = name!, Order = index })];
        Dictionary<string, FinancePerson> byName = people.ToDictionary(p => p.Name);

        List<FinanceTransaction> transactions =
        [
            .. parsed.Rows.Select(r => new FinanceTransaction
            {
                Type = r.Type,
                Amount = r.Amount,
                Date = r.Date,
                PersonId = r.Person is null ? null : byName[r.Person].Id,
                IsTransfer = r.IsTransfer,
                IsWithdrawal = r.IsWithdrawal,
                Category = r.Category
            })
        ];

        List<SavingsAccount> accounts = [.. parsed.Accounts.Select(a => new SavingsAccount { Name = a.Name, PersonId = a.Person is null ? null : byName[a.Person].Id, Value = a.Value })];
        List<SavingsSnapshot> snapshots = [.. parsed.Snapshots.Select(s => new SavingsSnapshot { Year = s.Year, PersonId = byName[s.Person].Id, Value = s.Value })];

        BalanceSheet sheet = BalanceCalculator.Compute(people, transactions, accounts, snapshots, DateOnly.FromDateTime(DateTime.Today));

        output.WriteLine($"rows read={parsed.RowsRead} imported={parsed.Rows.Count} skipped={parsed.RowsSkipped} accounts={parsed.Accounts.Count} snapshots={parsed.Snapshots.Count}");
        output.WriteLine("people=" + string.Join(",", people.Select(p => p.Name)));
        output.WriteLine("transfers=" + parsed.Rows.Count(r => r.IsTransfer) + " shared=" + parsed.Rows.Count(r => r.Person is null));
        output.WriteLine("types=" + string.Join(", ", parsed.Rows.GroupBy(r => r.Type).Select(g => $"{g.Key}:{g.Count()}")));
        output.WriteLine("normalized=" + string.Join(" | ", parsed.Normalized.Take(15).Select(n => $"{n.From}->{n.To} ({n.Count})")));
        output.WriteLine("categories=" + string.Join(", ", parsed.Rows.GroupBy(r => r.Category).OrderByDescending(g => g.Count()).Take(30).Select(g => $"{g.Key}:{g.Count()}")));
        output.WriteLine("warnings=" + string.Join(" | ", parsed.Warnings.Take(8)));

        foreach (BalanceYear year in sheet.Years)
        {
            output.WriteLine($"{year.Year}: conta {string.Join('/', year.Account)} poup {string.Join('/', year.Savings)} total {string.Join('/', year.Total)}");
        }

        sheet.Years.Should().NotBeEmpty();
        parsed.Rows.Should().NotBeEmpty();
    }
}

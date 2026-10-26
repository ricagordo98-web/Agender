using Agender.Infrastructure.Data;
using Agender.Infrastructure.Identity;
using Agender.Infrastructure.Services;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using ClosedXML.Excel;
using FluentAssertions;

namespace Agender.Tests.Services;

public sealed class FinanceImportTests : IDisposable
{
    private static readonly TimeProvider Time = new FixedTime();

    private readonly TestDb _db = new();

    public FinanceImportTests()
    {
        using AgenderDbContext context = _db.Create(string.Empty);
        context.Users.Add(new AgenderUser { Id = "ricardo", UserName = "r", NormalizedUserName = "R", Email = "r@x.pt", NormalizedEmail = "R@X.PT", DisplayName = "Ricardo Gordo" });
        context.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    public static byte[] BuildWorkbook(bool withSavings = true)
    {
        using XLWorkbook workbook = new();
        IXLWorksheet records = workbook.AddWorksheet("Registos Conta Principal");
        string[] headers = ["ID", "Data", "Valor", "Responsável", "Entrada", "Saída", "Poupança", "Troca", "Descrição", "Ano", "Mês"];

        for (int column = 0; column < headers.Length; column++)
        {
            records.Cell(1, column + 1).Value = headers[column];
        }

        int row = 2;

        void Add(string? date, double? value, string owner, string? entry = null, string? exit = null, string? saving = null, bool swap = false, string? description = null)
        {
            if (date is not null)
            {
                records.Cell(row, 2).Value = DateTime.Parse(date, System.Globalization.CultureInfo.InvariantCulture);
            }

            if (value is not null)
            {
                records.Cell(row, 3).Value = value.Value;
            }

            records.Cell(row, 4).Value = owner;

            if (entry is not null)
            {
                records.Cell(row, 5).Value = entry;
            }

            if (exit is not null)
            {
                records.Cell(row, 6).Value = exit;
            }

            if (saving is not null)
            {
                records.Cell(row, 7).Value = saving;
            }

            records.Cell(row, 8).Value = swap;

            if (description is not null)
            {
                records.Cell(row, 9).Value = description;
            }

            row++;
        }

        Add("2025-01-10", 1000, "Bruna", entry: "Salário", description: "Salário jan");
        Add("2025-01-11", -50, "Bruna", exit: "Supermercado", description: "Continente");
        Add("2025-01-12", -10, "Ricardo", entry: "Saída", exit: "Transportes");
        Add("2025-01-13", -200, "Ambos", saving: "Vida a Dois", description: "Depósito");
        Add("2025-01-14", 100, "Ambos", saving: "Vida a Dois", description: "Levantamento");
        Add("2025-01-15", -50, "Ricardo", entry: "Poupança", exit: "Férias");
        Add("2025-01-16", -30, "Bruna", entry: "Troca", swap: true, description: "Acerto");
        Add("2025-01-17", -100, "Ambos", exit: "Explicacoes", description: "Renda");
        Add("2025-01-18", -1, "Ambos", exit: "Explicacoes");
        Add("2025-01-19", -1, "Ambos", exit: "Explicacoes");
        Add("2025-01-20", -1, "Ambos", exit: "Explicações");
        Add("2025-01-21", -1, "Ambos", exit: "Explicações");
        Add(null, -5, "Bruna", exit: "Sem data");
        Add("2025-01-22", 0, "Bruna", exit: "Zero");
        Add("2025-01-23", 7, "Ricardo", entry: "Saldo Inicial");

        if (withSavings)
        {
            IXLWorksheet savings = workbook.AddWorksheet("Poupanças");
            savings.Cell(2, 4).Value = "Poupança";
            savings.Cell(2, 5).Value = "Titular";
            savings.Cell(2, 6).Value = "Conta";
            savings.Cell(2, 7).Value = "Valor";
            savings.Cell(2, 8).Value = "INICIAL";
            savings.Cell(3, 4).Value = "DP Bruna";
            savings.Cell(3, 5).Value = "Bruna";
            savings.Cell(3, 6).Value = "BPI";
            savings.Cell(3, 7).Value = 1000;
            savings.Cell(3, 8).Value = 500;
            savings.Cell(4, 4).Value = "DP Casal";
            savings.Cell(4, 5).Value = "Ambos";
            savings.Cell(4, 6).Value = "BPI";
            savings.Cell(4, 7).Value = 600;
            savings.Cell(4, 8).Value = 0;
            savings.Range(2, 4, 4, 8).CreateTable("Poupanças");

            IXLWorksheet overview = workbook.AddWorksheet("Consulta");
            overview.Cell(13, 4).Value = 2023;
            overview.Cell(13, 5).Value = 2024;
            overview.Cell(13, 6).Value = 2025;
            overview.Cell(14, 2).Value = "CONTA";
            overview.Cell(17, 2).Value = "POUPANÇAS";
            overview.Cell(17, 3).Value = "Bruna";
            overview.Cell(17, 4).Value = 0;
            overview.Cell(17, 5).Value = 900;
            overview.Cell(17, 6).Value = 1100;
            overview.Cell(18, 3).Value = "Ricardo";
            overview.Cell(18, 4).Value = 0;
            overview.Cell(18, 5).Value = 300;
            overview.Cell(18, 6).FormulaA1 = "=E18+1";
            overview.Cell(19, 3).Value = "TOTAL";
        }

        using MemoryStream stream = new();
        workbook.SaveAs(stream);

        return stream.ToArray();
    }

    private static ParsedWorkbook Parse(byte[]? bytes = null)
    {
        using MemoryStream stream = new(bytes ?? BuildWorkbook());
        using XLWorkbook workbook = new(stream);

        return ExcelFinanceParser.Parse(workbook);
    }

    private async Task<FinanceImportService> ServiceAsync()
    {
        await new HouseholdService(_db.ForUser("ricardo"), Time).CreateAsync("Casa");

        return new FinanceImportService(_db.ForUser("ricardo"), Time);
    }

    [Fact]
    public void Parse_ClassifiesRowsByColumnsAndSign()
    {
        ParsedWorkbook parsed = Parse();

        ParsedRow salary = parsed.Rows.Single(r => r.Description == "Salário jan");
        salary.Type.Should().Be(TransactionType.Income);
        salary.Category.Should().Be("Salário");
        salary.Amount.Should().Be(1000);
        salary.Person.Should().Be("Bruna");

        ParsedRow groceries = parsed.Rows.Single(r => r.Description == "Continente");
        groceries.Type.Should().Be(TransactionType.Expense);
        groceries.Category.Should().Be("Supermercado");

        parsed.Rows.Single(r => r.Category == "Transportes").Type.Should().Be(TransactionType.Expense);
    }

    [Fact]
    public void Parse_SavingsColumnAndLegacyPoupancaType_BecomeSavingsWithDirection()
    {
        ParsedWorkbook parsed = Parse();

        ParsedRow deposit = parsed.Rows.Single(r => r.Description == "Depósito");
        deposit.Type.Should().Be(TransactionType.Saving);
        deposit.IsWithdrawal.Should().BeFalse();
        deposit.Category.Should().Be("Vida a Dois");

        parsed.Rows.Single(r => r.Description == "Levantamento").IsWithdrawal.Should().BeTrue();

        ParsedRow legacy = parsed.Rows.Single(r => r.Category == "Férias");
        legacy.Type.Should().Be(TransactionType.Saving);
        legacy.IsWithdrawal.Should().BeFalse();
    }

    [Fact]
    public void Parse_TransfersSharedRowsAndInitialBalance()
    {
        ParsedWorkbook parsed = Parse();

        ParsedRow transfer = parsed.Rows.Single(r => r.Description == "Acerto");
        transfer.IsTransfer.Should().BeTrue();
        transfer.Person.Should().Be("Bruna");
        parsed.Rows.Where(r => r.Person is null).Should().HaveCountGreaterThan(5);
        parsed.Rows.Single(r => r.Category == "Saldo inicial").Type.Should().Be(TransactionType.Income);
    }

    [Fact]
    public void Parse_SkipsRowsWithoutDateOrValue_AndFillsBlankDescriptions()
    {
        ParsedWorkbook parsed = Parse();

        parsed.RowsRead.Should().Be(15);
        parsed.RowsSkipped.Should().Be(2);
        parsed.Rows.Should().HaveCount(13);
        parsed.Warnings.Should().Contain(w => w.Contains("2 linhas ignoradas"));
        parsed.Rows.Single(r => r.Category == "Transportes").Description.Should().Be(ExcelFinanceParser.EmptyDescription);
    }

    [Fact]
    public void Parse_HarmonisesCategoryNamesPreferringTheAccentedSpelling()
    {
        ParsedWorkbook parsed = Parse();

        parsed.Rows.Count(r => r.Category == "Explicações").Should().Be(5);
        parsed.Rows.Should().NotContain(r => r.Category == "Explicacoes");
        parsed.Normalized.Should().ContainSingle().Which.Should().Be(new NameChange("Explicacoes", "Explicações", 3));
    }

    [Fact]
    public void Parse_ReadsSavingsAccountsAndManualSnapshots_SkippingFormulaCells()
    {
        ParsedWorkbook parsed = Parse();

        parsed.Accounts.Should().HaveCount(2);
        parsed.Accounts[0].Should().Be(new ParsedAccount("DP Bruna", "Bruna", "BPI", 1000, 500));
        parsed.Accounts[1].Person.Should().BeNull();
        parsed.Snapshots.Should().BeEquivalentTo(
        [
            new ParsedSnapshot(2024, "Bruna", 900),
            new ParsedSnapshot(2025, "Bruna", 1100),
            new ParsedSnapshot(2024, "Ricardo", 300)
        ]);
    }

    [Fact]
    public void Parse_WorkbookWithoutRegistos_Throws()
    {
        using XLWorkbook workbook = new();
        workbook.AddWorksheet("Outra");

        Action act = () => ExcelFinanceParser.Parse(workbook);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Import_DryRun_ReportsWithoutWriting()
    {
        FinanceImportService service = await ServiceAsync();

        ImportReport report = await service.ImportAsync(BuildWorkbook(), apply: false);

        report.Applied.Should().BeFalse();
        report.RowsImported.Should().Be(13);
        report.RowsSkipped.Should().Be(2);
        report.People.Should().Equal("Bruna", "Ricardo");
        report.TransfersImported.Should().Be(1);
        report.AccountsImported.Should().Be(2);
        report.SnapshotsImported.Should().Be(3);
        report.Preview.Total.Should().HaveCount(2);
        using AgenderDbContext context = _db.Create(string.Empty);
        context.FinanceTransactions.Should().BeEmpty();
    }

    [Fact]
    public async Task Import_Apply_StoresPeopleTransactionsAndSavings_AndLinksTheMemberByName()
    {
        FinanceImportService service = await ServiceAsync();

        ImportReport report = await service.ImportAsync(BuildWorkbook(), apply: true);

        FinanceService finance = new(_db.ForUser("ricardo"), Time);
        IReadOnlyList<FinancePerson> people = await finance.GetPeopleAsync();
        report.Applied.Should().BeTrue();
        people.Select(p => p.Name).Should().Equal("Bruna", "Ricardo");
        people.Single(p => p.Name == "Bruna").MemberUserId.Should().BeNull();
        people.Single(p => p.Name == "Ricardo").MemberUserId.Should().Be("ricardo");
        (await finance.GetSavingsAccountsAsync()).Should().HaveCount(2);
        (await finance.GetSavingsSnapshotsAsync()).Should().HaveCount(3);
        using AgenderDbContext context = _db.Create(string.Empty);
        context.FinanceTransactions.Should().HaveCount(13).And.OnlyContain(t => t.IsImported);
    }

    [Fact]
    public async Task Import_Apply_ReusesAnExistingPersonOfTheSameName()
    {
        FinanceImportService service = await ServiceAsync();
        FinanceService finance = new(_db.ForUser("ricardo"), Time);
        FinancePerson existing = (await finance.GetPeopleAsync()).Single();
        await finance.UpdatePersonAsync(new FinancePerson { Id = existing.Id, Name = "Ricardo", MemberUserId = "ricardo" });

        await service.ImportAsync(BuildWorkbook(), apply: true);

        (await finance.GetPeopleAsync()).Select(p => p.Name).Should().Equal("Bruna", "Ricardo");
    }

    [Fact]
    public async Task Import_Twice_ReplacesTheImportedRowsAndKeepsManualOnes()
    {
        FinanceImportService service = await ServiceAsync();
        FinanceService finance = new(_db.ForUser("ricardo"), Time);
        await service.ImportAsync(BuildWorkbook(), apply: true);
        await finance.CreateTransactionAsync(new FinanceTransaction { Amount = 9, Description = "Manual", Date = new DateOnly(2026, 1, 1) });

        await service.ImportAsync(BuildWorkbook(), apply: true);

        using AgenderDbContext context = _db.Create(string.Empty);
        context.FinanceTransactions.Count(t => t.IsImported).Should().Be(13);
        context.FinanceTransactions.Count(t => !t.IsImported).Should().Be(1);
        context.SavingsAccounts.Should().HaveCount(2);
    }

    [Fact]
    public async Task Import_WithoutSavingsSheets_KeepsTheExistingAccounts()
    {
        FinanceImportService service = await ServiceAsync();
        FinanceService finance = new(_db.ForUser("ricardo"), Time);
        await finance.CreateSavingsAccountAsync(new SavingsAccount { Name = "Minha", Value = 5 });

        await service.ImportAsync(BuildWorkbook(withSavings: false), apply: true);

        (await finance.GetSavingsAccountsAsync()).Should().ContainSingle().Which.Name.Should().Be("Minha");
    }

    [Fact]
    public async Task Import_Apply_BalancesMatchTheSpreadsheetRules()
    {
        FinanceImportService service = await ServiceAsync();

        ImportReport report = await service.ImportAsync(BuildWorkbook(), apply: true);

        // people: Ricardo Gordo, Bruna. Bruna = 1000-50 own, -(30) transfer... Ricardo = -10-50+7.
        FinanceService finance = new(_db.ForUser("ricardo"), Time);
        BalanceSheet sheet = await finance.GetBalanceAsync();
        sheet.Current!.Year.Should().Be(2026);
        report.Preview.Account.Sum().Should().Be(sheet.Current.Account.Sum());
    }

    [Fact]
    public async Task Import_InvalidFile_Throws()
    {
        FinanceImportService service = await ServiceAsync();

        Func<Task> notAWorkbook = () => service.ImportAsync([1, 2, 3, 4], apply: false);
        Func<Task> empty = () => service.ImportAsync([], apply: false);

        await notAWorkbook.Should().ThrowAsync<ArgumentException>();
        await empty.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Import_WithoutAHousehold_Throws()
    {
        FinanceImportService service = new(_db.ForUser("ricardo"), Time);

        Func<Task> act = () => service.ImportAsync(BuildWorkbook(), apply: false);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}

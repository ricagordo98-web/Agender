using System.Globalization;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using ClosedXML.Excel;

namespace Agender.Infrastructure.Services;

public record ParsedRow(DateOnly Date, decimal Amount, string? Person, TransactionType Type, bool IsWithdrawal, bool IsTransfer, string Category, string Description);

public record ParsedAccount(string Name, string? Person, string? Bank, decimal Value, decimal Initial);

public record ParsedSnapshot(int Year, string Person, decimal Value);

public class ParsedWorkbook
{
    public List<ParsedRow> Rows { get; } = [];

    public List<ParsedAccount> Accounts { get; } = [];

    public List<ParsedSnapshot> Snapshots { get; } = [];

    public List<NameChange> Normalized { get; } = [];

    public List<string> Warnings { get; } = [];

    public int RowsRead { get; set; }

    public int RowsSkipped { get; set; }
}

public static class ExcelFinanceParser
{
    public const string EmptyDescription = "Sem descrição";

    private static readonly HashSet<string> GenericLabels = ["entrada", "saida", "despesa", "troca", "saldoinicial"];

    public static ParsedWorkbook Parse(XLWorkbook workbook)
    {
        ParsedWorkbook parsed = new();

        IXLWorksheet records = FindSheet(workbook, "registos") ?? throw new ArgumentException("The workbook has no 'Registos' sheet.");
        ParseRecords(records, parsed);

        if (FindSheet(workbook, "poupanc") is IXLWorksheet savings)
        {
            ParseAccounts(savings, parsed);
        }

        if (FindSheet(workbook, "consulta") is IXLWorksheet overview)
        {
            ParseSnapshots(overview, parsed);
        }

        Normalize(parsed);

        return parsed;
    }

    private static IXLWorksheet? FindSheet(XLWorkbook workbook, string key) =>
        workbook.Worksheets.FirstOrDefault(sheet => KeyOf(sheet.Name).StartsWith(key, StringComparison.Ordinal));

    private static void ParseRecords(IXLWorksheet sheet, ParsedWorkbook parsed)
    {
        int headerRow = 1;
        Dictionary<string, int> columns = [];

        foreach (IXLCell cell in sheet.Row(headerRow).CellsUsed())
        {
            columns[KeyOf(TextOf(cell))] = cell.Address.ColumnNumber;
        }

        int Column(string name) => columns.TryGetValue(name, out int number) ? number : throw new ArgumentException($"The 'Registos' sheet has no '{name}' column.");

        int date = Column("data");
        int value = Column("valor");
        int owner = Column("responsavel");
        int entry = columns.GetValueOrDefault("entrada");
        int exit = columns.GetValueOrDefault("saida");
        int saving = columns.GetValueOrDefault("poupanca");
        int swap = columns.GetValueOrDefault("troca");
        int description = columns.GetValueOrDefault("descricao");

        int lastRow = sheet.LastRowUsed()?.RowNumber() ?? headerRow;

        for (int rowNumber = headerRow + 1; rowNumber <= lastRow; rowNumber++)
        {
            IXLRow row = sheet.Row(rowNumber);

            if (row.IsEmpty())
            {
                continue;
            }

            parsed.RowsRead++;

            if (!TryDate(row.Cell(date), out DateOnly day) || !TryDecimal(row.Cell(value), out decimal amount) || amount == 0)
            {
                parsed.RowsSkipped++;
                continue;
            }

            string e = Text(row, entry);
            string f = Text(row, exit);
            string g = Text(row, saving);
            string who = Text(row, owner);
            bool isTransfer = (swap > 0 && IsTrue(row.Cell(swap))) || KeyOf(e) == "troca";
            string text = Text(row, description);

            (TransactionType type, string category, bool withdrawal) = Classify(amount, e, f, g);

            if (isTransfer && type == TransactionType.Saving)
            {
                isTransfer = false;
                parsed.Warnings.Add($"Linha {rowNumber}: troca com poupança tratada como poupança.");
            }

            string? person = KeyOf(who) is "" or "ambos" ? null : who;

            if (who.Length == 0)
            {
                parsed.Warnings.Add($"Linha {rowNumber}: sem responsável, tratada como 'Ambos'.");
            }

            parsed.Rows.Add(new ParsedRow(
                day,
                Math.Abs(Math.Round(amount, 2)),
                person,
                type,
                withdrawal,
                isTransfer,
                category,
                text.Length == 0 ? EmptyDescription : text.Length > FinanceTransaction.DescriptionMaxLength ? text[..FinanceTransaction.DescriptionMaxLength] : text));
        }

        if (parsed.RowsSkipped > 0)
        {
            parsed.Warnings.Add($"{parsed.RowsSkipped} linhas ignoradas (sem data ou sem valor).");
        }
    }

    private static (TransactionType Type, string Category, bool Withdrawal) Classify(decimal amount, string e, string f, string g)
    {
        if (g.Length > 0)
        {
            return (TransactionType.Saving, KeyOf(g) == "poupanca" ? CasaCatalog.SavingCategory : g, amount > 0);
        }

        if (KeyOf(e) == "poupanca")
        {
            return (TransactionType.Saving, f.Length > 0 && !IsGeneric(f) ? f : CasaCatalog.SavingCategory, amount > 0);
        }

        TransactionType type = amount > 0 ? TransactionType.Income : TransactionType.Expense;
        string category = KeyOf(e) == "saldoinicial"
            ? "Saldo inicial"
            : type == TransactionType.Income ? First(e, f) : First(f, e);

        return (type, category.Length == 0 ? (KeyOf(e) == "troca" ? "Troca" : "Outros") : category, false);
    }

    private static string First(string preferred, string fallback) =>
        preferred.Length > 0 && !IsGeneric(preferred) ? preferred : fallback.Length > 0 && !IsGeneric(fallback) ? fallback : string.Empty;

    private static bool IsGeneric(string text) => GenericLabels.Contains(KeyOf(text));

    private static void ParseAccounts(IXLWorksheet sheet, ParsedWorkbook parsed)
    {
        IXLTable? table = sheet.Tables.FirstOrDefault();
        int headerRow = table?.HeadersRow().RowNumber() ?? FindHeaderRow(sheet, "titular");

        if (headerRow == 0)
        {
            return;
        }

        Dictionary<string, int> columns = sheet.Row(headerRow).CellsUsed().ToDictionary(c => KeyOf(TextOf(c)), c => c.Address.ColumnNumber, StringComparer.Ordinal);

        if (!columns.TryGetValue("poupanca", out int nameColumn) || !columns.TryGetValue("valor", out int valueColumn))
        {
            return;
        }

        int ownerColumn = columns.GetValueOrDefault("titular");
        int bankColumn = columns.GetValueOrDefault("conta");
        int initialColumn = columns.GetValueOrDefault("inicial");
        int lastRow = table is not null ? table.DataRange.LastRow().RowNumber() : sheet.LastRowUsed()?.RowNumber() ?? headerRow;

        for (int rowNumber = headerRow + 1; rowNumber <= lastRow; rowNumber++)
        {
            IXLRow row = sheet.Row(rowNumber);
            string name = TextOf(row.Cell(nameColumn)).Trim();

            if (name.Length == 0)
            {
                break;
            }

            string owner = ownerColumn > 0 ? TextOf(row.Cell(ownerColumn)).Trim() : string.Empty;
            TryDecimal(row.Cell(valueColumn), out decimal amount);
            decimal initial = 0;

            if (initialColumn > 0)
            {
                TryDecimal(row.Cell(initialColumn), out initial);
            }

            parsed.Accounts.Add(new ParsedAccount(name, KeyOf(owner) is "" or "ambos" ? null : owner, bankColumn > 0 ? TextOf(row.Cell(bankColumn)).Trim() : null, Math.Max(0, amount), Math.Max(0, initial)));
        }
    }

    private static void ParseSnapshots(IXLWorksheet sheet, ParsedWorkbook parsed)
    {
        IXLCell? label = sheet.CellsUsed().FirstOrDefault(c => KeyOf(TextOf(c)) == "poupancas" && c.Address.ColumnNumber <= 3);

        if (label is null)
        {
            return;
        }

        int labelRow = label.Address.RowNumber;
        int? headerRow = null;

        for (int rowNumber = labelRow - 1; rowNumber >= Math.Max(1, labelRow - 6); rowNumber--)
        {
            if (sheet.Row(rowNumber).CellsUsed().Count(c => TryYear(c, out _)) >= 2)
            {
                headerRow = rowNumber;
                break;
            }
        }

        if (headerRow is null)
        {
            return;
        }

        List<(int Column, int Year)> years = [];

        foreach (IXLCell cell in sheet.Row(headerRow.Value).CellsUsed())
        {
            if (TryYear(cell, out int year))
            {
                years.Add((cell.Address.ColumnNumber, year));
            }
        }

        int personColumn = label.Address.ColumnNumber + 1;

        for (int rowNumber = labelRow; rowNumber < labelRow + 12; rowNumber++)
        {
            string person = TextOf(sheet.Cell(rowNumber, personColumn)).Trim();

            if (person.Length == 0 || KeyOf(person) == "total")
            {
                break;
            }

            foreach ((int column, int year) in years)
            {
                IXLCell cell = sheet.Cell(rowNumber, column);

                if (!cell.HasFormula && TryDecimal(cell, out decimal amount) && amount > 0)
                {
                    parsed.Snapshots.Add(new ParsedSnapshot(year, person, Math.Round(amount, 2)));
                }
            }
        }
    }

    private static void Normalize(ParsedWorkbook parsed)
    {
        Dictionary<string, List<string>> groups = [];

        foreach (string category in parsed.Rows.Select(r => r.Category))
        {
            string key = KeyOf(category);

            if (!groups.TryGetValue(key, out List<string>? list))
            {
                groups[key] = list = [];
            }

            list.Add(category);
        }

        Dictionary<string, string> canonical = [];

        foreach ((string key, List<string> variants) in groups)
        {
            List<IGrouping<string, string>> counted = [.. variants.GroupBy(v => v).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)];
            string best = counted.FirstOrDefault(g => HasDiacritics(g.Key) && g.Count() >= 2)?.Key ?? counted[0].Key;

            canonical[key] = best;

            foreach (IGrouping<string, string> variant in variants.GroupBy(v => v).Where(g => g.Key != best))
            {
                parsed.Normalized.Add(new NameChange(variant.Key, best, variant.Count()));
            }
        }

        for (int index = 0; index < parsed.Rows.Count; index++)
        {
            ParsedRow row = parsed.Rows[index];
            string name = canonical[KeyOf(row.Category)];

            if (name != row.Category)
            {
                parsed.Rows[index] = row with { Category = name };
            }
        }

        parsed.Normalized.Sort((a, b) => b.Count.CompareTo(a.Count));
    }

    private static bool HasDiacritics(string text) => text.Normalize(System.Text.NormalizationForm.FormD).Any(c => CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark);

    private static string Text(IXLRow row, int column)
    {
        if (column <= 0)
        {
            return string.Empty;
        }

        return string.Join(' ', TextOf(row.Cell(column)).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool TryDate(IXLCell cell, out DateOnly date)
    {
        date = default;
        XLCellValue value = ValueOf(cell);

        if (value.IsDateTime)
        {
            date = DateOnly.FromDateTime(value.GetDateTime());
            return true;
        }

        if (value.IsNumber && value.GetNumber() is > 20000 and < 80000)
        {
            date = DateOnly.FromDateTime(DateTime.FromOADate(value.GetNumber()));
            return true;
        }

        return value.IsText && DateTime.TryParse(value.GetText(), CultureInfo.GetCultureInfo("pt-PT"), DateTimeStyles.None, out DateTime parsed) && Assign(out date, DateOnly.FromDateTime(parsed));
    }

    private static bool Assign(out DateOnly target, DateOnly value)
    {
        target = value;
        return true;
    }

    private static bool TryDecimal(IXLCell cell, out decimal amount)
    {
        amount = 0;
        XLCellValue value = ValueOf(cell);

        if (value.IsNumber)
        {
            amount = (decimal)value.GetNumber();
            return true;
        }

        return value.IsText && decimal.TryParse(value.GetText().Replace(',', '.'), NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out amount);
    }

    private static bool TryYear(IXLCell cell, out int year)
    {
        year = 0;

        XLCellValue value = ValueOf(cell);

        if (!cell.HasFormula && value.IsNumber && value.GetNumber() is >= 2000 and <= 2100)
        {
            year = (int)value.GetNumber();
            return true;
        }

        return false;
    }

    private static bool IsTrue(IXLCell cell)
    {
        XLCellValue value = ValueOf(cell);

        return value.IsBoolean ? value.GetBoolean() : value.IsText && value.GetText().Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static int FindHeaderRow(IXLWorksheet sheet, string key)
    {
        for (int rowNumber = 1; rowNumber <= Math.Min(sheet.LastRowUsed()?.RowNumber() ?? 0, 10); rowNumber++)
        {
            if (sheet.Row(rowNumber).CellsUsed().Any(c => KeyOf(TextOf(c)) == key))
            {
                return rowNumber;
            }
        }

        return 0;
    }

    private static XLCellValue ValueOf(IXLCell cell) => cell.HasFormula ? cell.CachedValue : cell.Value;

    private static string TextOf(IXLCell cell)
    {
        XLCellValue value = ValueOf(cell);

        return value.IsBlank ? string.Empty : value.IsText ? value.GetText() : value.ToString(CultureInfo.InvariantCulture);
    }

    private static string KeyOf(string? text) => FinancePeople.Key(text);
}

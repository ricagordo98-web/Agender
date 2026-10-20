using System.Text;
using Agender.Shared.Domain;
using Agender.Shared.Reports;
using ClosedXML.Excel;
using FluentAssertions;

namespace Agender.Tests.Reports;

public class ReportExporterTests
{
    private static readonly Project Site = new() { Name = "Site", ClientName = "Acme" };

    private static readonly IReadOnlyList<Project> Projects = [Site];

    private static TimeEntry Entry(DateOnly date, int start, int end, string description, params string[] tags) => new()
    {
        Date = date,
        StartMinute = start,
        EndMinute = end,
        Description = description,
        ProjectId = Site.Id,
        Tags = [.. tags.Select(name => new Tag { Name = name })]
    };

    private static string[] CsvLines(byte[] bytes)
    {
        bytes.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
        return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
    }

    [Fact]
    public void Hours_NinetyMinutes_UsesDecimalCommaWithTwoPlaces()
    {
        ReportExporter.Hours(90).Should().Be("1,50");
    }

    [Fact]
    public void ToCsv_WritesHeaderAndOneRowPerEntryOrderedByDateAndStart()
    {
        DateOnly day = new(2026, 10, 12);
        List<TimeEntry> entries =
        [
            Entry(day.AddDays(1), 540, 600, "Later day"),
            Entry(day, 600, 660, "Second"),
            Entry(day, 540, 630, "First", "dev", "urgent")
        ];

        string[] lines = CsvLines(ReportExporter.ToCsv(entries, Projects));

        lines.Should().HaveCount(4);
        lines[0].Should().Be("Data;Início;Fim;Minutos;Horas;Projeto;Cliente;Etiquetas;Descrição");
        lines[1].Should().Be("2026-10-12;09:00;10:30;90;1,50;Site;Acme;dev, urgent;First");
        lines[2].Should().StartWith("2026-10-12;10:00;11:00;60;1,00;");
        lines[3].Should().StartWith("2026-10-13;");
    }

    [Fact]
    public void ToCsv_DescriptionWithSeparatorAndQuotes_IsEscaped()
    {
        TimeEntry entry = Entry(new DateOnly(2026, 10, 12), 60, 120, "Fix \"login\"; retry");

        string[] lines = CsvLines(ReportExporter.ToCsv([entry], Projects));

        lines[1].Should().EndWith(";\"Fix \"\"login\"\"; retry\"");
    }

    [Fact]
    public void ToCsv_EntryWithoutProject_LeavesProjectAndClientEmpty()
    {
        TimeEntry entry = new() { Date = new DateOnly(2026, 10, 12), StartMinute = 60, EndMinute = 90, Description = "Misc" };

        string[] lines = CsvLines(ReportExporter.ToCsv([entry], Projects));

        lines[1].Should().Be("2026-10-12;01:00;01:30;30;0,50;;;;Misc");
    }

    [Fact]
    public void ToXlsx_ContainsDetailAndSummarySheets()
    {
        TimeEntry entry = Entry(new DateOnly(2026, 10, 12), 540, 630, "First", "dev");
        ReportResult summary = ReportBuilder.Build([entry], Projects, ReportGroupBy.ClientProject);

        byte[] bytes = ReportExporter.ToXlsx([entry], Projects, summary);

        using XLWorkbook workbook = new(new MemoryStream(bytes));
        IXLWorksheet detail = workbook.Worksheet("Detalhe");
        detail.Cell(1, 1).GetString().Should().Be("Data");
        detail.Cell(2, 2).GetString().Should().Be("09:00");
        detail.Cell(2, 4).GetValue<int>().Should().Be(90);
        detail.Cell(2, 5).GetValue<double>().Should().BeApproximately(1.5, 0.001);
        detail.Cell(2, 6).GetString().Should().Be("Site");
        detail.Cell(2, 9).GetString().Should().Be("First");

        IXLWorksheet sheet = workbook.Worksheet("Resumo");
        sheet.Cell(2, 1).GetString().Should().Be("Acme");
        sheet.Cell(2, 2).GetValue<int>().Should().Be(90);
        sheet.Cell(3, 1).GetString().Should().Be("Site");
        sheet.Cell(4, 1).GetString().Should().Be("Total");
        sheet.Cell(4, 2).GetValue<int>().Should().Be(90);
    }
}

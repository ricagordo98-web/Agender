using System.Globalization;
using System.Text;
using Agender.Shared.Domain;
using ClosedXML.Excel;

namespace Agender.Shared.Reports;

public static class ReportExporter
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("pt-PT");

    private static readonly string[] DetailHeaders =
        ["Data", "Início", "Fim", "Minutos", "Horas", "Projeto", "Cliente", "Etiquetas", "Descrição"];

    public static string Hours(int minutes) => (minutes / 60.0).ToString("0.00", Culture);

    public static byte[] ToCsv(IReadOnlyList<TimeEntry> entries, IReadOnlyList<Project> projects)
    {
        Dictionary<Guid, Project> byId = projects.ToDictionary(p => p.Id);
        StringBuilder builder = new();

        builder.Append(string.Join(';', DetailHeaders)).Append("\r\n");

        foreach (TimeEntry entry in Ordered(entries))
        {
            Project? project = Find(entry, byId);
            int minutes = ReportBuilder.Minutes(entry);

            string[] fields =
            [
                entry.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                TimeSlot.FormatMinute(entry.StartMinute),
                TimeSlot.FormatMinute(entry.EndMinute),
                minutes.ToString(CultureInfo.InvariantCulture),
                Hours(minutes),
                project?.Name ?? string.Empty,
                project?.ClientName ?? string.Empty,
                string.Join(", ", entry.Tags.Select(t => t.Name)),
                entry.Description
            ];

            builder.Append(string.Join(';', fields.Select(Escape))).Append("\r\n");
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(builder.ToString())];
    }

    public static byte[] ToXlsx(IReadOnlyList<TimeEntry> entries, IReadOnlyList<Project> projects, ReportResult summary)
    {
        Dictionary<Guid, Project> byId = projects.ToDictionary(p => p.Id);
        using XLWorkbook workbook = new();

        WriteDetail(workbook.AddWorksheet("Detalhe"), entries, byId);
        WriteSummary(workbook.AddWorksheet("Resumo"), summary);

        using MemoryStream stream = new();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteDetail(IXLWorksheet sheet, IReadOnlyList<TimeEntry> entries, Dictionary<Guid, Project> byId)
    {
        for (int column = 0; column < DetailHeaders.Length; column++)
        {
            sheet.Cell(1, column + 1).Value = DetailHeaders[column];
        }

        sheet.Row(1).Style.Font.Bold = true;

        int row = 2;

        foreach (TimeEntry entry in Ordered(entries))
        {
            Project? project = Find(entry, byId);
            int minutes = ReportBuilder.Minutes(entry);

            sheet.Cell(row, 1).Value = entry.Date.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(row, 1).Style.DateFormat.Format = "yyyy-mm-dd";
            sheet.Cell(row, 2).Value = TimeSlot.FormatMinute(entry.StartMinute);
            sheet.Cell(row, 3).Value = TimeSlot.FormatMinute(entry.EndMinute);
            sheet.Cell(row, 4).Value = minutes;
            sheet.Cell(row, 5).Value = minutes / 60.0;
            sheet.Cell(row, 5).Style.NumberFormat.Format = "0.00";
            sheet.Cell(row, 6).Value = project?.Name ?? string.Empty;
            sheet.Cell(row, 7).Value = project?.ClientName ?? string.Empty;
            sheet.Cell(row, 8).Value = string.Join(", ", entry.Tags.Select(t => t.Name));
            sheet.Cell(row, 9).Value = entry.Description;
            row++;
        }

        sheet.Columns().AdjustToContents(1, row);
    }

    private static void WriteSummary(IXLWorksheet sheet, ReportResult summary)
    {
        sheet.Cell(1, 1).Value = "Grupo";
        sheet.Cell(1, 2).Value = "Minutos";
        sheet.Cell(1, 3).Value = "Horas";
        sheet.Row(1).Style.Font.Bold = true;

        int row = 2;

        foreach (ReportGroup group in summary.Groups)
        {
            WriteSummaryRow(sheet, row++, group, indent: 0);

            foreach (ReportGroup child in group.Children)
            {
                WriteSummaryRow(sheet, row++, child, indent: 1);
            }
        }

        WriteSummaryRow(sheet, row, new ReportGroup("Total", summary.TotalMinutes, []), indent: 0);
        sheet.Row(row).Style.Font.Bold = true;
        sheet.Columns().AdjustToContents(1, row);
    }

    private static void WriteSummaryRow(IXLWorksheet sheet, int row, ReportGroup group, int indent)
    {
        sheet.Cell(row, 1).Value = group.Label;
        sheet.Cell(row, 1).Style.Alignment.Indent = indent;
        sheet.Cell(row, 2).Value = group.Minutes;
        sheet.Cell(row, 3).Value = group.Minutes / 60.0;
        sheet.Cell(row, 3).Style.NumberFormat.Format = "0.00";
    }

    private static IEnumerable<TimeEntry> Ordered(IEnumerable<TimeEntry> entries) =>
        entries.OrderBy(e => e.Date).ThenBy(e => e.StartMinute);

    private static Project? Find(TimeEntry entry, Dictionary<Guid, Project> byId) =>
        entry.ProjectId is Guid id && byId.TryGetValue(id, out Project? project) ? project : null;

    private static string Escape(string value) =>
        value.IndexOfAny([';', '"', '\r', '\n']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}

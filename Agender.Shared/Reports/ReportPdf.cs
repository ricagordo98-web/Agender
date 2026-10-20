using System.Globalization;
using Agender.Shared.Components;
using Agender.Shared.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Agender.Shared.Reports;

public record ReportPdfOptions(DateOnly From, DateOnly To, string GroupByLabel, string? ProjectName, string? Tag, DateTime GeneratedAt);

public static class ReportPdf
{
    private const string Brand = "#4F46E5";
    private const string BrandDark = "#312E81";
    private const string BrandSoft = "#EEF2FF";
    private const string Ink = "#1F2937";
    private const string Muted = "#6B7280";
    private const string Line = "#E5E7EB";
    private const string Zebra = "#F9FAFB";

    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("pt-PT");

    static ReportPdf() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] Build(IReadOnlyList<TimeEntry> entries, IReadOnlyList<Project> projects, ReportResult summary, ReportPdfOptions options)
    {
        Dictionary<Guid, Project> byId = projects.ToDictionary(p => p.Id);
        List<TimeEntry> ordered = [.. entries.OrderBy(e => e.Date).ThenBy(e => e.StartMinute)];
        int days = ordered.Select(e => e.Date).Distinct().Count();

        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(0);
            page.DefaultTextStyle(style => style.FontSize(9.5f).FontColor(Ink));

            page.Header().Element(header => ComposeHeader(header, options));
            page.Content().PaddingHorizontal(36).PaddingVertical(18).Column(column =>
            {
                column.Spacing(18);
                column.Item().Element(item => ComposeKpis(item, summary.TotalMinutes, ordered.Count, days));
                column.Item().Element(item => ComposeSummary(item, summary, options.GroupByLabel));
                column.Item().Element(item => ComposeDetail(item, ordered, byId));
            });
            page.Footer().Element(footer => ComposeFooter(footer, options.GeneratedAt));
        })).GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, ReportPdfOptions options)
    {
        container.Background(Brand).PaddingHorizontal(36).PaddingVertical(22).Column(column =>
        {
            column.Spacing(4);
            column.Item().Text("AGENDER").FontSize(9).Bold().LetterSpacing(0.25f).FontColor("#C7D2FE");
            column.Item().Text("Relatório de tempo").FontSize(24).Bold().FontColor(Colors.White);
            column.Item().Text(PeriodText(options.From, options.To)).FontSize(11).FontColor("#E0E7FF");

            string filters = FiltersText(options);

            column.Item().PaddingTop(4).Text(filters).FontSize(9).FontColor("#C7D2FE");
        });
    }

    private static void ComposeKpis(IContainer container, int totalMinutes, int entryCount, int days)
    {
        container.Row(row =>
        {
            row.Spacing(10);
            Kpi(row.RelativeItem(), "Tempo total", TimeText.Duration(totalMinutes));
            Kpi(row.RelativeItem(), "Horas", ReportExporter.Hours(totalMinutes));
            Kpi(row.RelativeItem(), "Registos", entryCount.ToString(Culture));
            Kpi(row.RelativeItem(), "Dias com atividade", days.ToString(Culture));
        });
    }

    private static void Kpi(IContainer container, string label, string value) =>
        container.Background(BrandSoft).Border(1).BorderColor("#C7D2FE").CornerRadius(6).Padding(10).Column(column =>
        {
            column.Item().Text(label.ToUpperInvariant()).FontSize(7).Bold().LetterSpacing(0.1f).FontColor(Muted);
            column.Item().PaddingTop(2).Text(value).FontSize(18).Bold().FontColor(BrandDark);
        });

    private static void ComposeSummary(IContainer container, ReportResult summary, string groupBy)
    {
        container.Column(column =>
        {
            column.Item().Element(item => SectionTitle(item, $"Resumo · {groupBy}"));
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(4);
                    columns.RelativeColumn(2.5f);
                    columns.ConstantColumn(54);
                    columns.ConstantColumn(54);
                    columns.ConstantColumn(40);
                });

                table.Header(header =>
                {
                    HeaderCell(header.Cell(), "Grupo");
                    HeaderCell(header.Cell(), string.Empty);
                    HeaderCell(header.Cell(), "Tempo", alignRight: true);
                    HeaderCell(header.Cell(), "Horas", alignRight: true);
                    HeaderCell(header.Cell(), "%", alignRight: true);
                });

                foreach (ReportGroup group in summary.Groups)
                {
                    SummaryRow(table, group, summary.TotalMinutes, isChild: false);

                    foreach (ReportGroup child in group.Children)
                    {
                        SummaryRow(table, child, summary.TotalMinutes, isChild: true);
                    }
                }

                table.Cell().ColumnSpan(2).Background(BrandSoft).BorderTop(1.5f).BorderColor(Brand).PaddingVertical(6).PaddingHorizontal(6)
                    .Text("Total").Bold().FontColor(BrandDark);
                table.Cell().Background(BrandSoft).BorderTop(1.5f).BorderColor(Brand).PaddingVertical(6).AlignRight()
                    .Text(TimeText.Duration(summary.TotalMinutes)).Bold().FontColor(BrandDark);
                table.Cell().Background(BrandSoft).BorderTop(1.5f).BorderColor(Brand).PaddingVertical(6).AlignRight()
                    .Text(ReportExporter.Hours(summary.TotalMinutes)).Bold().FontColor(BrandDark);
                table.Cell().Background(BrandSoft).BorderTop(1.5f).BorderColor(Brand).PaddingVertical(6).PaddingRight(6).AlignRight()
                    .Text(string.Empty);
            });
        });
    }

    private static void SummaryRow(TableDescriptor table, ReportGroup group, int total, bool isChild)
    {
        double share = total == 0 ? 0 : group.Minutes * 100.0 / total;
        string background = isChild ? Colors.White : Zebra;
        float indent = isChild ? 16 : 6;

        table.Cell().Background(background).BorderBottom(0.5f).BorderColor(Line).PaddingVertical(5).PaddingLeft(indent)
            .Text(group.Label).FontSize(isChild ? 9 : 9.5f).SemiBold().FontColor(isChild ? Muted : Ink);
        table.Cell().Background(background).BorderBottom(0.5f).BorderColor(Line).PaddingVertical(5).PaddingHorizontal(6).AlignMiddle()
            .Element(cell => ShareBar(cell, share, isChild));
        table.Cell().Background(background).BorderBottom(0.5f).BorderColor(Line).PaddingVertical(5).AlignRight()
            .Text(TimeText.Duration(group.Minutes)).SemiBold();
        table.Cell().Background(background).BorderBottom(0.5f).BorderColor(Line).PaddingVertical(5).AlignRight()
            .Text(ReportExporter.Hours(group.Minutes)).FontColor(Muted);
        table.Cell().Background(background).BorderBottom(0.5f).BorderColor(Line).PaddingVertical(5).PaddingRight(6).AlignRight()
            .Text($"{share.ToString("0", Culture)}%").FontColor(Muted);
    }

    private static void ShareBar(IContainer container, double share, bool isChild)
    {
        float filled = (float)Math.Clamp(share, 0, 100);

        container.Height(6).Background(Line).CornerRadius(3).Row(row =>
        {
            if (filled > 0)
            {
                row.RelativeItem(filled).Background(isChild ? "#A5B4FC" : Brand).CornerRadius(3);
            }

            if (filled < 100)
            {
                row.RelativeItem(100 - filled);
            }
        });
    }

    private static void ComposeDetail(IContainer container, IReadOnlyList<TimeEntry> entries, Dictionary<Guid, Project> byId)
    {
        container.Column(column =>
        {
            column.Item().Element(item => SectionTitle(item, "Detalhe dos registos"));
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(70);
                    columns.ConstantColumn(86);
                    columns.ConstantColumn(56);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(3.2f);
                });

                table.Header(header =>
                {
                    HeaderCell(header.Cell(), "Data");
                    HeaderCell(header.Cell(), "Horário");
                    HeaderCell(header.Cell(), "Duração", alignRight: true);
                    HeaderCell(header.Cell(), "Projeto");
                    HeaderCell(header.Cell(), "Descrição");
                });

                for (int index = 0; index < entries.Count; index++)
                {
                    TimeEntry entry = entries[index];
                    Project? project = entry.ProjectId is Guid id && byId.TryGetValue(id, out Project? found) ? found : null;
                    string background = index % 2 == 0 ? Colors.White : Zebra;
                    string tags = string.Join(", ", entry.Tags.Select(t => t.Name));

                    table.Cell().Element(cell => DetailCell(cell, background)).Text(entry.Date.ToString("dd/MM/yyyy", Culture));
                    table.Cell().Element(cell => DetailCell(cell, background))
                        .Text($"{TimeSlot.FormatMinute(entry.StartMinute)} – {TimeSlot.FormatMinute(entry.EndMinute)}");
                    table.Cell().Element(cell => DetailCell(cell, background)).AlignRight()
                        .Text(TimeText.Duration(ReportBuilder.Minutes(entry))).Bold();
                    table.Cell().Element(cell => DetailCell(cell, background)).Column(cellColumn =>
                    {
                        cellColumn.Item().Text(project?.Name ?? ReportBuilder.NoProjectLabel).FontColor(project is null ? Muted : Ink);

                        if (!string.IsNullOrWhiteSpace(project?.ClientName))
                        {
                            cellColumn.Item().Text(project.ClientName).FontSize(8).FontColor(Muted);
                        }
                    });
                    table.Cell().Element(cell => DetailCell(cell, background)).Column(cellColumn =>
                    {
                        cellColumn.Item().Text(string.IsNullOrWhiteSpace(entry.Description) ? "—" : entry.Description);

                        if (tags.Length > 0)
                        {
                            cellColumn.Item().Text(tags).FontSize(8).FontColor(Brand);
                        }
                    });
                }
            });
        });
    }

    private static IContainer DetailCell(IContainer container, string background) =>
        container.Background(background).BorderBottom(0.5f).BorderColor(Line).PaddingVertical(5).PaddingHorizontal(6);

    private static void SectionTitle(IContainer container, string title) =>
        container.PaddingBottom(6).Row(row =>
        {
            row.ConstantItem(4).Height(14).Background(Brand);
            row.RelativeItem().PaddingLeft(8).Text(title).FontSize(13).Bold().FontColor(BrandDark);
        });

    private static void HeaderCell(IContainer container, string text, bool alignRight = false)
    {
        IContainer cell = container.Background(BrandDark).PaddingVertical(6).PaddingHorizontal(6);
        cell = alignRight ? cell.AlignRight() : cell;
        cell.Text(text.ToUpperInvariant()).FontSize(7.5f).Bold().LetterSpacing(0.08f).FontColor(Colors.White);
    }

    private static void ComposeFooter(IContainer container, DateTime generatedAt) =>
        container.PaddingHorizontal(36).PaddingVertical(14).BorderTop(0.5f).BorderColor(Line).Row(row =>
        {
            row.RelativeItem().Text($"Gerado em {generatedAt.ToString("dd/MM/yyyy 'às' HH:mm", Culture)} · Agender").FontSize(8).FontColor(Muted);
            row.RelativeItem().AlignRight().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                text.Span("Página ");
                text.CurrentPageNumber();
                text.Span(" de ");
                text.TotalPages();
            });
        });

    private static string PeriodText(DateOnly from, DateOnly to) =>
        from == to
            ? from.ToString("d 'de' MMMM 'de' yyyy", Culture)
            : $"{from.ToString("d MMM yyyy", Culture)}  →  {to.ToString("d MMM yyyy", Culture)}";

    private static string FiltersText(ReportPdfOptions options)
    {
        List<string> parts = [$"Agrupado por {options.GroupByLabel.ToLower(Culture)}"];

        if (options.ProjectName is not null)
        {
            parts.Add($"Projeto: {options.ProjectName}");
        }

        if (!string.IsNullOrEmpty(options.Tag))
        {
            parts.Add($"Etiqueta: {options.Tag}");
        }

        return string.Join("   ·   ", parts);
    }
}

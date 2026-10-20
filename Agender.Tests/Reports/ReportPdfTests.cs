using System.Text;
using Agender.Shared.Domain;
using Agender.Shared.Reports;
using FluentAssertions;

namespace Agender.Tests.Reports;

public class ReportPdfTests
{
    private static readonly DateOnly Start = new(2026, 10, 1);

    private static (List<TimeEntry> Entries, List<Project> Projects) Sample(int count)
    {
        List<Project> projects =
        [
            new() { Name = "Website", ClientName = "Acme" },
            new() { Name = "App móvel", ClientName = "Acme" },
            new() { Name = "Consultoria" }
        ];

        List<TimeEntry> entries = [];

        for (int index = 0; index < count; index++)
        {
            entries.Add(new TimeEntry
            {
                Date = Start.AddDays(index / 3),
                StartMinute = 540 + index % 3 * 120,
                EndMinute = 600 + index % 3 * 120 + index % 4 * 15,
                Description = $"Trabalho com acentuação e descrição {index}",
                ProjectId = index % 5 == 0 ? null : projects[index % 3].Id,
                Tags = index % 2 == 0 ? [new Tag { Name = "reunião" }] : []
            });
        }

        return (entries, projects);
    }

    private static byte[] Build(int count, ReportGroupBy groupBy = ReportGroupBy.ClientProject)
    {
        (List<TimeEntry> entries, List<Project> projects) = Sample(count);
        ReportResult result = ReportBuilder.Build(entries, projects, groupBy);

        return ReportPdf.Build(entries, projects, result, new ReportPdfOptions(Start, Start.AddDays(30), "Cliente e projeto", "Website", "reunião", new DateTime(2026, 10, 31, 18, 30, 0)));
    }

    [Fact]
    public void Build_WithEntries_ProducesAPdfDocument()
    {
        byte[] pdf = Build(8);

        Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public void Build_ManyEntries_SpansSeveralPages()
    {
        byte[] small = Build(5);
        byte[] large = Build(120);

        large.Length.Should().BeGreaterThan(small.Length);
    }

    [Fact]
    public void Build_NoEntries_StillProducesAPdfDocument()
    {
        byte[] pdf = ReportPdf.Build([], [], new ReportResult(0, []), new ReportPdfOptions(Start, Start, "Dia", null, null, DateTime.Now));

        Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
    }
}

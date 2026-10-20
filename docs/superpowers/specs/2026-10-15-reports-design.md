# Reports and Export – Design

Approved by the user on 2026-10-15.

## Goal
Let the user bill clients from logged time: pick a period, group the totals, and export the detail to CSV or XLSX. Replaces the placeholder in `Agender.Shared/Pages/Reports.razor`.

## Approach
Client-side aggregation. The page loads entries with `ITimeEntryService.GetRangeAsync` and projects with `IProjectService.GetAllAsync`, then a pure `ReportBuilder` computes the totals. No new API endpoint, contract or migration; it behaves the same on Web and MAUI.

## Logic (Agender.Shared)
- `ReportGroupBy` enum: `ClientProject` (default), `Project`, `Tag`, `Day`.
- `ReportBuilder.Build(IReadOnlyList<TimeEntry> entries, IReadOnlyList<Project> projects, ReportGroupBy groupBy)` returns a `ReportResult { TotalMinutes, Groups }`.
- `ReportGroup { Label, Minutes, Children }`; `ClientProject` has two levels (client, then project), the other modes one level.
- Entries without a project go to a "Sem projeto" group; projects without a client go to "Sem cliente".
- An entry with several tags counts once in each tag group. `TotalMinutes` is always the sum over the entries, never over the groups, so by-tag groups can add up to more than the total.
- Minutes of an entry = `EndMinute - StartMinute`.
- Filters (project, tag) are applied to the entries before grouping.
- `ReportExporter.ToCsv(entries, projects)` and `ToXlsx(entries, projects)` return `byte[]`.
  - CSV: `;` separator, UTF-8 with BOM, quoted fields where needed. Columns: date, start, end, minutes, hours, project, client, tags, description.
  - XLSX (ClosedXML, only new dependency): sheet "Detalhe" with the same columns, sheet "Resumo" with the grouped totals.
  - Hours are decimal (minutes / 60, 2 places).

## UI (Reports.razor)
- Period: shortcuts (this week, last week, this month, last month) plus free from/to dates. Default: this month.
- Filters: project, tag. Grouping selector.
- Expandable table with duration (`TimeText.Duration`) and decimal hours per group, and the overall total.
- Buttons "Exportar CSV" and "Exportar XLSX" export the filtered entries.
- Empty range shows the existing `EmptyState`; load/export failures use the existing toast service.
- Download goes through a collocated JS module (`Blob` + anchor). If it does not work in the MAUI WebView, fall back to saving through the native file picker.

## Out of scope
Charts, rates/money, comparison between periods.

## Testing (xUnit + FluentAssertions)
- `ReportBuilder`: each grouping, "Sem projeto"/"Sem cliente", multi-tag entries and total, filters, empty input.
- `ReportExporter`: CSV content (separator, BOM, escaping, hours); XLSX reopened with ClosedXML to check both sheets.

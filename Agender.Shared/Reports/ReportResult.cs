namespace Agender.Shared.Reports;

public record ReportGroup(string Label, int Minutes, IReadOnlyList<ReportGroup> Children);

public record ReportResult(int TotalMinutes, IReadOnlyList<ReportGroup> Groups);

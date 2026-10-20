using Agender.Shared.Components;
using Agender.Shared.Domain;

namespace Agender.Shared.Reports;

public static class ReportBuilder
{
    public const string NoProjectLabel = "Sem projeto";
    public const string NoClientLabel = "Sem cliente";
    public const string NoTagLabel = "Sem etiqueta";

    public static int Minutes(TimeEntry entry) => entry.EndMinute - entry.StartMinute;

    public static IReadOnlyList<TimeEntry> Filter(IEnumerable<TimeEntry> entries, Guid? projectId, string? tag) =>
    [
        .. entries.Where(entry =>
            (projectId is null || entry.ProjectId == projectId)
            && (string.IsNullOrEmpty(tag) || entry.Tags.Any(t => string.Equals(t.Name, tag, StringComparison.OrdinalIgnoreCase))))
    ];

    public static ReportResult Build(IReadOnlyList<TimeEntry> entries, IReadOnlyList<Project> projects, ReportGroupBy groupBy)
    {
        Dictionary<Guid, Project> byId = projects.ToDictionary(p => p.Id);

        IReadOnlyList<ReportGroup> groups = groupBy switch
        {
            ReportGroupBy.ClientProject => ByClientProject(entries, byId),
            ReportGroupBy.Project => ByProject(entries, byId),
            ReportGroupBy.Tag => ByTag(entries),
            _ => ByDay(entries)
        };

        return new ReportResult(entries.Sum(Minutes), groups);
    }

    private static Project? Resolve(TimeEntry entry, Dictionary<Guid, Project> byId) =>
        entry.ProjectId is Guid id && byId.TryGetValue(id, out Project? project) ? project : null;

    private static IReadOnlyList<ReportGroup> Sorted(IEnumerable<ReportGroup> groups) =>
    [
        .. groups.OrderByDescending(g => g.Minutes).ThenBy(g => g.Label, StringComparer.OrdinalIgnoreCase)
    ];

    private static IReadOnlyList<ReportGroup> ByProject(IEnumerable<TimeEntry> entries, Dictionary<Guid, Project> byId) =>
        Sorted(entries
            .GroupBy(entry => Resolve(entry, byId)?.Id)
            .Select(group => new ReportGroup(
                group.Key is Guid id ? byId[id].Name : NoProjectLabel,
                group.Sum(Minutes),
                [])));

    private static IReadOnlyList<ReportGroup> ByClientProject(IReadOnlyList<TimeEntry> entries, Dictionary<Guid, Project> byId)
    {
        List<ReportGroup> groups = [];

        IEnumerable<TimeEntry> withProject = entries.Where(entry => Resolve(entry, byId) is not null);

        groups.AddRange(withProject
            .GroupBy(entry => ClientLabel(Resolve(entry, byId)!), StringComparer.OrdinalIgnoreCase)
            .Select(client => new ReportGroup(client.Key, client.Sum(Minutes), ByProject(client, byId))));

        List<TimeEntry> withoutProject = [.. entries.Where(entry => Resolve(entry, byId) is null)];

        if (withoutProject.Count > 0)
        {
            groups.Add(new ReportGroup(NoProjectLabel, withoutProject.Sum(Minutes), []));
        }

        return Sorted(groups);
    }

    private static string ClientLabel(Project project) =>
        string.IsNullOrWhiteSpace(project.ClientName) ? NoClientLabel : project.ClientName.Trim();

    private static IReadOnlyList<ReportGroup> ByTag(IReadOnlyList<TimeEntry> entries) =>
        Sorted(entries
            .SelectMany(entry => TagNames(entry).Select(name => (Name: name, Entry: entry)))
            .GroupBy(pair => pair.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ReportGroup(group.Key, group.Sum(pair => Minutes(pair.Entry)), [])));

    private static IEnumerable<string> TagNames(TimeEntry entry) =>
        entry.Tags.Count == 0 ? new[] { NoTagLabel } : entry.Tags.Select(t => t.Name);

    private static IReadOnlyList<ReportGroup> ByDay(IReadOnlyList<TimeEntry> entries) =>
    [
        .. entries
            .GroupBy(entry => entry.Date)
            .OrderBy(group => group.Key)
            .Select(group => new ReportGroup(DateText.Full(group.Key), group.Sum(Minutes), []))
    ];
}

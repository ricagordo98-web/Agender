using Agender.Shared.Components;
using Agender.Shared.Domain;
using Agender.Shared.Reports;
using FluentAssertions;

namespace Agender.Tests.Reports;

public class ReportBuilderTests
{
    private static readonly DateOnly Monday = new(2026, 10, 12);

    private static readonly Project Site = new() { Name = "Site", ClientName = "Acme" };
    private static readonly Project App = new() { Name = "App", ClientName = "Acme" };
    private static readonly Project Blog = new() { Name = "Blog", ClientName = "Beta" };
    private static readonly Project Internal = new() { Name = "Internal" };

    private static readonly IReadOnlyList<Project> Projects = [Site, App, Blog, Internal];

    private static TimeEntry Entry(int minutes, Project? project = null, DateOnly? date = null, params string[] tags) => new()
    {
        Date = date ?? Monday,
        StartMinute = 60,
        EndMinute = 60 + minutes,
        ProjectId = project?.Id,
        Tags = [.. tags.Select(name => new Tag { Name = name })]
    };

    [Fact]
    public void Build_ClientProject_GroupsByClientThenProject()
    {
        List<TimeEntry> entries = [Entry(60, Site), Entry(30, App), Entry(45, Blog)];

        ReportResult result = ReportBuilder.Build(entries, Projects, ReportGroupBy.ClientProject);

        result.TotalMinutes.Should().Be(135);
        result.Groups.Select(g => (g.Label, g.Minutes)).Should().Equal(("Acme", 90), ("Beta", 45));
        result.Groups[0].Children.Select(c => (c.Label, c.Minutes)).Should().Equal(("Site", 60), ("App", 30));
    }

    [Fact]
    public void Build_ClientProject_ProjectWithoutClient_GoesUnderNoClient()
    {
        ReportResult result = ReportBuilder.Build([Entry(20, Internal)], Projects, ReportGroupBy.ClientProject);

        result.Groups.Should().ContainSingle().Which.Label.Should().Be(ReportBuilder.NoClientLabel);
        result.Groups[0].Children.Should().ContainSingle().Which.Label.Should().Be("Internal");
    }

    [Fact]
    public void Build_ClientProject_EntryWithoutProject_GoesToNoProjectGroup()
    {
        ReportResult result = ReportBuilder.Build([Entry(15), Entry(60, Site)], Projects, ReportGroupBy.ClientProject);

        ReportGroup noProject = result.Groups.Single(g => g.Label == ReportBuilder.NoProjectLabel);
        noProject.Minutes.Should().Be(15);
        noProject.Children.Should().BeEmpty();
    }

    [Fact]
    public void Build_Project_ReturnsFlatGroupsOrderedByMinutes()
    {
        ReportResult result = ReportBuilder.Build([Entry(30, App), Entry(90, Site), Entry(10)], Projects, ReportGroupBy.Project);

        result.Groups.Select(g => (g.Label, g.Minutes)).Should().Equal(("Site", 90), ("App", 30), (ReportBuilder.NoProjectLabel, 10));
        result.Groups.Should().OnlyContain(g => g.Children.Count == 0);
    }

    [Fact]
    public void Build_Tag_EntryCountsInEachTagButTotalCountsOnce()
    {
        ReportResult result = ReportBuilder.Build([Entry(60, Site, null, "dev", "urgent")], Projects, ReportGroupBy.Tag);

        result.TotalMinutes.Should().Be(60);
        result.Groups.Select(g => (g.Label, g.Minutes)).Should().BeEquivalentTo([("dev", 60), ("urgent", 60)]);
    }

    [Fact]
    public void Build_Tag_UntaggedEntry_GoesToNoTagGroup()
    {
        ReportResult result = ReportBuilder.Build([Entry(25, Site)], Projects, ReportGroupBy.Tag);

        result.Groups.Should().ContainSingle().Which.Label.Should().Be(ReportBuilder.NoTagLabel);
    }

    [Fact]
    public void Build_Day_OrdersByDateAscending()
    {
        DateOnly tuesday = Monday.AddDays(1);

        ReportResult result = ReportBuilder.Build([Entry(30, Site, tuesday), Entry(60, Site, Monday)], Projects, ReportGroupBy.Day);

        result.Groups.Select(g => g.Label).Should().Equal(DateText.Full(Monday), DateText.Full(tuesday));
    }

    [Fact]
    public void Build_NoEntries_ReturnsZeroTotalAndNoGroups()
    {
        ReportResult result = ReportBuilder.Build([], Projects, ReportGroupBy.ClientProject);

        result.TotalMinutes.Should().Be(0);
        result.Groups.Should().BeEmpty();
    }

    [Fact]
    public void Filter_ByProjectAndTag_KeepsOnlyMatchingEntries()
    {
        TimeEntry match = Entry(30, Site, null, "Dev");
        List<TimeEntry> entries = [match, Entry(30, Site, null, "ops"), Entry(30, App, null, "dev")];

        IReadOnlyList<TimeEntry> filtered = ReportBuilder.Filter(entries, Site.Id, "dev");

        filtered.Should().ContainSingle().Which.Should().BeSameAs(match);
    }

    [Fact]
    public void Filter_NoCriteria_ReturnsAllEntries()
    {
        ReportBuilder.Filter([Entry(10), Entry(20, Site)], null, null).Should().HaveCount(2);
    }
}

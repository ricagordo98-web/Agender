using Agender.Infrastructure.Services;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using FluentAssertions;

namespace Agender.Tests.Services;

public sealed class ProjectAndTagTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 10, 12);

    private readonly TestDb _db = new();
    private readonly ProjectService _projects;
    private readonly TimeEntryService _entries;
    private readonly TaskService _tasks;

    public ProjectAndTagTests()
    {
        _projects = new ProjectService(_db);
        _entries = new TimeEntryService(_db);
        _tasks = new TaskService(_db);
    }

    public void Dispose() => _db.Dispose();

    private static TimeEntry NewEntry(string description = "Work", Guid? projectId = null, params string[] tags) => new()
    {
        Date = Day,
        StartMinute = 60,
        EndMinute = 120,
        Description = description,
        ProjectId = projectId,
        Tags = [.. tags.Select(name => new Tag { Name = name })]
    };

    [Fact]
    public async Task CreateAsync_DuplicateProjectName_Throws()
    {
        await _projects.CreateAsync(new Project { Name = "Website" });

        Func<Task> act = () => _projects.CreateAsync(new Project { Name = "  website " });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteAsync_ProjectWithEntries_KeepsEntriesWithoutProject()
    {
        Project project = await _projects.CreateAsync(new Project { Name = "Website" });
        await _entries.CreateAsync(NewEntry(projectId: project.Id));

        await _projects.DeleteAsync(project.Id);

        (await _entries.GetRangeAsync(Day, Day)).Single().ProjectId.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ProjectOfAnotherUser_Throws()
    {
        Project foreign = await new ProjectService(_db.ForUser("user-b")).CreateAsync(new Project { Name = "Secret" });

        Func<Task> act = () => _entries.CreateAsync(NewEntry(projectId: foreign.Id));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_Tags_AreSavedAndReusedCaseInsensitively()
    {
        await _entries.CreateAsync(NewEntry("One", null, "Urgent", "client"));
        await _entries.CreateAsync(NewEntry("Two", null, "urgent"));

        IReadOnlyList<string> tags = await new TagService(_db).GetAllAsync();

        tags.Should().BeEquivalentTo(["client", "Urgent"]);
        (await _entries.GetRangeAsync(Day, Day)).SelectMany(e => e.Tags).Select(t => t.NormalizedName).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateAsync_ChangedTags_ReplacesTheEntryTags()
    {
        TimeEntry entry = await _entries.CreateAsync(NewEntry("One", null, "a", "b"));

        TimeEntry update = entry.Clone();
        update.Tags = [new Tag { Name = "b" }, new Tag { Name = "c" }];
        await _entries.UpdateAsync(update);

        (await _entries.GetRangeAsync(Day, Day)).Single().Tags.Select(t => t.Name).Should().BeEquivalentTo(["b", "c"]);
    }

    [Fact]
    public async Task CreateAsync_TooManyTags_Throws()
    {
        string[] tags = [.. Enumerable.Range(1, Tag.MaxPerEntry + 1).Select(i => $"t{i}")];

        Func<Task> act = () => _entries.CreateAsync(NewEntry(tags: tags));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SearchAsync_MatchesDescriptionTagAndProjectName()
    {
        Project project = await _projects.CreateAsync(new Project { Name = "Website redesign" });
        await _entries.CreateAsync(NewEntry("Quarterly report"));
        await _entries.CreateAsync(NewEntry("Meeting", null, "budget"));
        await _entries.CreateAsync(NewEntry("Coding", project.Id));

        (await _entries.SearchAsync("REPORT", 0, 10)).Select(e => e.Description).Should().Equal("Quarterly report");
        (await _entries.SearchAsync("budget", 0, 10)).Select(e => e.Description).Should().Equal("Meeting");
        (await _entries.SearchAsync("redesign", 0, 10)).Select(e => e.Description).Should().Equal("Coding");
        (await _entries.SearchAsync("   ", 0, 10)).Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_PercentSign_IsTreatedLiterally()
    {
        await _entries.CreateAsync(NewEntry("Raised budget 50% more"));
        await _entries.CreateAsync(NewEntry("Plain"));

        (await _entries.SearchAsync("%", 0, 10)).Select(e => e.Description).Should().Equal("Raised budget 50% more");
    }

    [Fact]
    public async Task SearchAsync_Paging_ReturnsRequestedWindow()
    {
        for (int i = 1; i <= 5; i++)
        {
            await _entries.CreateAsync(new TimeEntry { Date = Day.AddDays(i), StartMinute = 60, EndMinute = 90, Description = $"match {i}" });
        }

        IReadOnlyList<TimeEntry> page = await _entries.SearchAsync("match", 2, 2);

        page.Select(e => e.Description).Should().Equal("match 3", "match 2");
    }

    [Fact]
    public async Task GetMinutesByProjectAsync_SumsDurationsInRange()
    {
        Project project = await _projects.CreateAsync(new Project { Name = "Website" });
        await _entries.CreateAsync(NewEntry(projectId: project.Id));
        await _entries.CreateAsync(new TimeEntry { Date = Day.AddDays(1), StartMinute = 0, EndMinute = 30, ProjectId = project.Id });
        await _entries.CreateAsync(new TimeEntry { Date = Day.AddDays(40), StartMinute = 0, EndMinute = 30, ProjectId = project.Id });

        IReadOnlyDictionary<Guid, int> totals = await _projects.GetMinutesByProjectAsync(Day, Day.AddDays(7));

        totals[project.Id].Should().Be(90);
    }

    [Fact]
    public async Task CreateAsync_TaskWithProjectOfAnotherUser_Throws()
    {
        Project foreign = await new ProjectService(_db.ForUser("user-b")).CreateAsync(new Project { Name = "Secret" });

        Func<Task> act = () => _tasks.CreateAsync(new TaskItem { Title = "Task", ProjectId = foreign.Id });

        await act.Should().ThrowAsync<ArgumentException>();
    }
}

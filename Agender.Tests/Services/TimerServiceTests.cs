using Agender.Infrastructure.Services;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using FluentAssertions;

namespace Agender.Tests.Services;

public sealed class TimerServiceTests : IDisposable
{
    private static readonly DateTime Start = new(2026, 10, 14, 9, 0, 0);

    private readonly TestDb _db = new();
    private readonly TimerService _sut;

    public TimerServiceTests() => _sut = new TimerService(_db);

    public void Dispose() => _db.Dispose();

    [Theory]
    [InlineData(5, 12, 15)]
    [InlineData(15, 16, 30)]
    [InlineData(15, 15, 15)]
    [InlineData(30, 1, 30)]
    public async Task StopAsync_WithRounding_RoundsTheDurationUp(int rounding, int elapsedMinutes, int expectedMinutes)
    {
        await new UserSettingsService(_db).SaveAsync(new UserSettings { TimerRoundingMinutes = rounding });
        await _sut.StartAsync(new ActiveTimer { StartedAt = Start });

        IReadOnlyList<TimeEntry> entries = await _sut.StopAsync(Start.AddMinutes(elapsedMinutes));

        entries.Single().EndMinute.Should().Be(entries.Single().StartMinute + expectedMinutes);
    }

    [Fact]
    public async Task StopAsync_TaskWithCustomColor_TheEntryInheritsIt()
    {
        TaskItem task = await new TaskService(_db).CreateAsync(new TaskItem { Title = "Design", CustomColor = "#12ab34" });
        await _sut.StartAsync(new ActiveTimer { StartedAt = Start, TaskItemId = task.Id });

        IReadOnlyList<TimeEntry> entries = await _sut.StopAsync(Start.AddMinutes(10));

        entries.Single().CustomColor.Should().Be("#12AB34");
    }

    [Fact]
    public async Task StopAsync_WithoutRounding_KeepsTheExactDuration()
    {
        await _sut.StartAsync(new ActiveTimer { StartedAt = Start });

        IReadOnlyList<TimeEntry> entries = await _sut.StopAsync(Start.AddMinutes(12));

        entries.Single().EndMinute.Should().Be(entries.Single().StartMinute + 12);
    }

    [Fact]
    public async Task GetActiveAsync_NothingRunning_ReturnsNull()
    {
        (await _sut.GetActiveAsync()).Should().BeNull();
    }

    [Fact]
    public async Task StartAsync_ThenGetActive_ReturnsTheRunningTimer()
    {
        await _sut.StartAsync(new ActiveTimer { StartedAt = Start, Description = "  Review  " });

        ActiveTimer? active = await _sut.GetActiveAsync();

        active.Should().NotBeNull();
        active!.StartedAt.Should().Be(Start);
        active.Description.Should().Be("Review");
    }

    [Fact]
    public async Task StartAsync_TimerAlreadyRunning_Throws()
    {
        await _sut.StartAsync(new ActiveTimer { StartedAt = Start });

        Func<Task> act = () => _sut.StartAsync(new ActiveTimer { StartedAt = Start.AddHours(1) });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task StartAsync_UnknownProject_Throws()
    {
        Func<Task> act = () => _sut.StartAsync(new ActiveTimer { StartedAt = Start, ProjectId = Guid.NewGuid() });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task StopAsync_CreatesEntryAndClearsTimer()
    {
        Project project = await new ProjectService(_db).CreateAsync(new Project { Name = "Site", Color = EntryColor.Teal });
        await _sut.StartAsync(new ActiveTimer { StartedAt = Start, Description = "Review", ProjectId = project.Id });

        IReadOnlyList<TimeEntry> created = await _sut.StopAsync(Start.AddMinutes(90));

        TimeEntry entry = created.Should().ContainSingle().Subject;
        entry.Date.Should().Be(new DateOnly(2026, 10, 14));
        entry.StartMinute.Should().Be(540);
        entry.EndMinute.Should().Be(630);
        entry.Description.Should().Be("Review");
        entry.ProjectId.Should().Be(project.Id);
        entry.Color.Should().Be(EntryColor.Teal);

        (await _sut.GetActiveAsync()).Should().BeNull();
        IReadOnlyList<TimeEntry> stored = await new TimeEntryService(_db).GetRangeAsync(entry.Date, entry.Date);
        stored.Should().ContainSingle().Which.Id.Should().Be(entry.Id);
    }

    [Fact]
    public async Task StopAsync_TaskColorWinsOverProjectColor()
    {
        Project project = await new ProjectService(_db).CreateAsync(new Project { Name = "Site", Color = EntryColor.Teal });
        TaskItem task = await new TaskService(_db).CreateAsync(new TaskItem { Title = "Fix", Color = EntryColor.Rose, ProjectId = project.Id });
        await _sut.StartAsync(new ActiveTimer { StartedAt = Start, ProjectId = project.Id, TaskItemId = task.Id });

        IReadOnlyList<TimeEntry> created = await _sut.StopAsync(Start.AddMinutes(10));

        created.Should().ContainSingle().Which.Color.Should().Be(EntryColor.Rose);
        created[0].TaskItemId.Should().Be(task.Id);
    }

    [Fact]
    public async Task StopAsync_CrossesMidnight_CreatesOneEntryPerDay()
    {
        await _sut.StartAsync(new ActiveTimer { StartedAt = new DateTime(2026, 10, 14, 22, 0, 0), Description = "Night" });

        IReadOnlyList<TimeEntry> created = await _sut.StopAsync(new DateTime(2026, 10, 15, 1, 30, 0));

        created.Select(e => (e.Date, e.StartMinute, e.EndMinute)).Should().Equal(
            (new DateOnly(2026, 10, 14), 1320, 1440),
            (new DateOnly(2026, 10, 15), 0, 90));
    }

    [Fact]
    public async Task StopAsync_NothingRunning_ThrowsNotFound()
    {
        Func<Task> act = () => _sut.StopAsync(Start);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetActiveAsync_OtherUser_DoesNotSeeTheTimer()
    {
        await _sut.StartAsync(new ActiveTimer { StartedAt = Start });

        TimerService other = new(_db.ForUser("user-b"));

        (await other.GetActiveAsync()).Should().BeNull();
        await other.StartAsync(new ActiveTimer { StartedAt = Start });
        (await other.GetActiveAsync()).Should().NotBeNull();
    }
}

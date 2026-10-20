using Agender.Infrastructure.Services;
using Agender.Shared.Domain;
using FluentAssertions;

namespace Agender.Tests.Services;

public sealed class TaskSchedulingTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 10, 14);

    private readonly TestDb _db = new();
    private readonly TaskService _tasks;
    private readonly TimeEntryService _entries;

    public TaskSchedulingTests()
    {
        _tasks = new TaskService(_db);
        _entries = new TimeEntryService(_db);
    }

    public void Dispose() => _db.Dispose();

    private Task<TimeEntry> EntryFor(TaskItem task) => _entries.CreateAsync(new TimeEntry
    {
        Date = Day,
        StartMinute = 540,
        EndMinute = 570,
        Description = task.Title,
        TaskItemId = task.Id
    });

    [Fact]
    public async Task GetAllAsync_TaskWithoutEntries_IsNotScheduled()
    {
        await _tasks.CreateAsync(new TaskItem { Title = "Write report" });

        (await _tasks.GetAllAsync()).Should().ContainSingle().Which.IsScheduled.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllAsync_TaskWithAnEntry_IsScheduled()
    {
        TaskItem task = await _tasks.CreateAsync(new TaskItem { Title = "Write report" });
        await _tasks.CreateAsync(new TaskItem { Title = "Other" });
        await EntryFor(task);

        IReadOnlyList<TaskItem> all = await _tasks.GetAllAsync();

        all.Single(t => t.Id == task.Id).IsScheduled.Should().BeTrue();
        all.Single(t => t.Title == "Other").IsScheduled.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllAsync_AfterTheEntryIsDeleted_TheTaskIsNotScheduledAnymore()
    {
        TaskItem task = await _tasks.CreateAsync(new TaskItem { Title = "Write report" });
        TimeEntry entry = await EntryFor(task);

        await _entries.DeleteAsync(entry.Id);

        (await _tasks.GetAllAsync()).Single().IsScheduled.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllAsync_TaskWithSeveralEntries_ReturnsItOnce()
    {
        TaskItem task = await _tasks.CreateAsync(new TaskItem { Title = "Write report" });
        await EntryFor(task);
        await EntryFor(task);

        (await _tasks.GetAllAsync()).Should().ContainSingle().Which.IsScheduled.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_ScheduledTask_KeepsBeingScheduledAndDoesNotPersistTheFlag()
    {
        TaskItem task = await _tasks.CreateAsync(new TaskItem { Title = "Write report" });
        await EntryFor(task);

        await _tasks.UpdateAsync(new TaskItem { Id = task.Id, Title = "Renamed", Color = task.Color, CreatedAt = task.CreatedAt, IsScheduled = false });

        TaskItem stored = (await _tasks.GetAllAsync()).Single();
        stored.Title.Should().Be("Renamed");
        stored.IsScheduled.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_EntryOfAnotherUserLinkedToMyTask_IsRejectedAndDoesNotScheduleIt()
    {
        TaskItem task = await _tasks.CreateAsync(new TaskItem { Title = "Mine" });
        TimeEntryService other = new(_db.ForUser("user-b"));

        Func<Task> act = () => other.CreateAsync(new TimeEntry { Date = Day, StartMinute = 540, EndMinute = 570, TaskItemId = task.Id });

        await act.Should().ThrowAsync<ArgumentException>();
        (await _tasks.GetAllAsync()).Single().IsScheduled.Should().BeFalse();
    }
}

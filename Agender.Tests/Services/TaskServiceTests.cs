using Agender.Shared.Domain;
using Agender.Infrastructure.Services;
using Agender.Shared.Services;
using FluentAssertions;

namespace Agender.Tests.Services;

public sealed class TaskServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly TaskService _sut;

    public TaskServiceTests() => _sut = new TaskService(_db);

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task CreateAsync_EmptyTitle_Throws()
    {
        Func<Task> act = () => _sut.CreateAsync(new TaskItem { Title = "   " });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetAllAsync_CompletedTask_IsListedLast()
    {
        TaskItem done = await _sut.CreateAsync(new TaskItem { Title = "Done" });
        await _sut.CreateAsync(new TaskItem { Title = "Open" });
        done.IsCompleted = true;
        await _sut.UpdateAsync(done);

        IReadOnlyList<TaskItem> result = await _sut.GetAllAsync();

        result.Select(t => t.Title).Should().Equal("Open", "Done");
    }

    [Fact]
    public async Task DeleteAsync_TaskLinkedToEntry_KeepsEntryAndClearsLink()
    {
        TaskItem task = await _sut.CreateAsync(new TaskItem { Title = "Linked" });
        DateOnly day = new(2026, 10, 12);
        TimeEntryService entries = new(_db);
        await entries.CreateAsync(new TimeEntry { Date = day, StartMinute = 60, EndMinute = 90, TaskItemId = task.Id });

        await _sut.DeleteAsync(task.Id);

        (await entries.GetRangeAsync(day, day)).Single().TaskItemId.Should().BeNull();
    }
}

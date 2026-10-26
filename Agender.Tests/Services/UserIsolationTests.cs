using Agender.Infrastructure.Data;
using Agender.Infrastructure.Services;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using FluentAssertions;

namespace Agender.Tests.Services;

public sealed class UserIsolationTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 10, 12);

    private readonly TestDb _db = new();
    private readonly IAgendaDbContextFactory _userA;
    private readonly IAgendaDbContextFactory _userB;

    public UserIsolationTests()
    {
        _userA = _db.ForUser("user-a");
        _userB = _db.ForUser("user-b");
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetRangeAsync_OtherUsersEntries_AreNotVisible()
    {
        await new TimeEntryService(_userA).CreateAsync(new TimeEntry { Date = Day, StartMinute = 60, EndMinute = 120, Description = "A" });

        IReadOnlyList<TimeEntry> result = await new TimeEntryService(_userB).GetRangeAsync(Day, Day);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_EntryOfAnotherUser_ThrowsNotFound()
    {
        TimeEntry entry = await new TimeEntryService(_userA).CreateAsync(new TimeEntry { Date = Day, StartMinute = 60, EndMinute = 120 });
        entry.Description = "hijacked";

        Func<Task> act = () => new TimeEntryService(_userB).UpdateAsync(entry);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_EntryOfAnotherUser_DoesNothing()
    {
        TimeEntry entry = await new TimeEntryService(_userA).CreateAsync(new TimeEntry { Date = Day, StartMinute = 60, EndMinute = 120 });

        await new TimeEntryService(_userB).DeleteAsync(entry.Id);

        (await new TimeEntryService(_userA).GetRangeAsync(Day, Day)).Should().ContainSingle();
    }

    [Fact]
    public async Task UpdateAsync_OwnEntry_KeepsOwnership()
    {
        TimeEntryService service = new(_userA);
        TimeEntry entry = await service.CreateAsync(new TimeEntry { Date = Day, StartMinute = 60, EndMinute = 120 });

        entry.EndMinute = 150;
        await service.UpdateAsync(entry);

        (await service.GetRangeAsync(Day, Day)).Single().EndMinute.Should().Be(150);
        (await new TimeEntryService(_userB).GetRangeAsync(Day, Day)).Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_OtherUsersTasks_AreNotVisible()
    {
        await new TaskService(_userA).CreateAsync(new TaskItem { Title = "Private" });

        (await new TaskService(_userB).GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_SameDayDifferentUsers_KeepsSeparateNotes()
    {
        await new DayNoteService(_userA).SaveAsync(Day, "note A");
        await new DayNoteService(_userB).SaveAsync(Day, "note B");

        (await new DayNoteService(_userA).GetRangeAsync(Day, Day)).Single().Text.Should().Be("note A");
        (await new DayNoteService(_userB).GetRangeAsync(Day, Day)).Single().Text.Should().Be("note B");
    }
}

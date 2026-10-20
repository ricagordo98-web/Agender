using Agender.Shared.Domain;
using Agender.Infrastructure.Services;
using Agender.Shared.Services;
using FluentAssertions;

namespace Agender.Tests.Services;

public sealed class TimeEntryServiceTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 10, 12);

    private readonly TestDb _db = new();
    private readonly TimeEntryService _sut;

    public TimeEntryServiceTests() => _sut = new TimeEntryService(_db);

    public void Dispose() => _db.Dispose();

    private static TimeEntry NewEntry(DateOnly date, int start, int end, string description = "Work") =>
        new() { Date = date, StartMinute = start, EndMinute = end, Description = description };

    [Fact]
    public async Task CreateAsync_CustomColor_IsNormalizedAndPersisted()
    {
        TimeEntry entry = NewEntry(Day, 540, 600);
        entry.CustomColor = "#ff8800";

        await _sut.CreateAsync(entry);

        (await _sut.GetRangeAsync(Day, Day)).Single().CustomColor.Should().Be("#FF8800");
    }

    [Fact]
    public async Task CreateAsync_InvalidCustomColor_Throws()
    {
        TimeEntry entry = NewEntry(Day, 540, 600);
        entry.CustomColor = "orange";

        Func<Task> act = () => _sut.CreateAsync(entry);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdateAsync_CompletedEntry_IsLocked()
    {
        TimeEntry entry = await _sut.CreateAsync(NewEntry(Day, 540, 600));
        entry.IsCompleted = true;
        await _sut.UpdateAsync(entry);
        entry.Description = "changed";

        Func<Task> act = () => _sut.UpdateAsync(entry);

        await act.Should().ThrowAsync<ArgumentException>();
        (await _sut.GetRangeAsync(Day, Day)).Single().Description.Should().NotBe("changed");
    }

    [Fact]
    public async Task UpdateAsync_ReopeningACompletedEntry_IsAllowed()
    {
        TimeEntry entry = await _sut.CreateAsync(NewEntry(Day, 540, 600));
        entry.IsCompleted = true;
        await _sut.UpdateAsync(entry);
        entry.IsCompleted = false;
        entry.Description = "edited after reopening";

        await _sut.UpdateAsync(entry);

        (await _sut.GetRangeAsync(Day, Day)).Single().Description.Should().Be("edited after reopening");
    }

    [Fact]
    public async Task CreateAsync_ValidEntry_PersistsTrimmedDescription()
    {
        await _sut.CreateAsync(NewEntry(Day, 540, 600, "  Standup  "));

        IReadOnlyList<TimeEntry> result = await _sut.GetRangeAsync(Day, Day);

        result.Should().ContainSingle().Which.Description.Should().Be("Standup");
    }

    [Fact]
    public async Task CreateAsync_InvalidSlot_Throws()
    {
        Func<Task> act = () => _sut.CreateAsync(NewEntry(Day, 600, 540));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetRangeAsync_MultipleDays_ReturnsOnlyRangeOrderedByStart()
    {
        await _sut.CreateAsync(NewEntry(Day, 600, 660, "B"));
        await _sut.CreateAsync(NewEntry(Day, 60, 120, "A"));
        await _sut.CreateAsync(NewEntry(Day.AddDays(3), 60, 120, "Outside"));

        IReadOnlyList<TimeEntry> result = await _sut.GetRangeAsync(Day, Day.AddDays(1));

        result.Select(e => e.Description).Should().Equal("A", "B");
    }

    [Fact]
    public async Task UpdateAsync_ChangedSlot_IsPersisted()
    {
        TimeEntry entry = await _sut.CreateAsync(NewEntry(Day, 60, 120));

        entry.StartMinute = 90;
        entry.EndMinute = 135;
        await _sut.UpdateAsync(entry);

        (await _sut.GetRangeAsync(Day, Day)).Single().Slot.Should().Be(new TimeSlot(90, 135));
    }

    [Fact]
    public async Task DeleteAsync_ExistingEntry_RemovesIt()
    {
        TimeEntry entry = await _sut.CreateAsync(NewEntry(Day, 60, 120));

        await _sut.DeleteAsync(entry.Id);

        (await _sut.GetRangeAsync(Day, Day)).Should().BeEmpty();
    }
}

using Agender.Infrastructure.Data;
using Agender.Infrastructure.Services;
using Agender.Shared.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Agender.Tests.Services;

public sealed class UnclaimedDataServiceTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 10, 12);

    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ClaimAsync_UnclaimedRows_BecomeVisibleToTheUser()
    {
        await using (AgenderDbContext legacy = _db.Create(AgenderDbContext.UnclaimedUserId))
        {
            legacy.TimeEntries.Add(new TimeEntry { Date = Day, StartMinute = 60, EndMinute = 90, Description = "legacy" });
            legacy.Tasks.Add(new TaskItem { Title = "legacy task" });
            legacy.DayNotes.Add(new DayNote { Date = Day, Text = "legacy note" });
            await legacy.SaveChangesAsync();
        }

        IUserDbContextFactory owner = _db.ForUser("user-a");
        (await new TimeEntryService(owner).GetRangeAsync(Day, Day)).Should().BeEmpty();

        await new UnclaimedDataService(new FixedContextFactory(_db)).ClaimAsync("user-a");

        (await new TimeEntryService(owner).GetRangeAsync(Day, Day)).Should().ContainSingle();
        (await new TaskService(owner).GetAllAsync()).Should().ContainSingle();
        (await new DayNoteService(owner).GetRangeAsync(Day, Day)).Should().ContainSingle();
    }

    private sealed class FixedContextFactory(TestDb db) : IDbContextFactory<AgenderDbContext>
    {
        public AgenderDbContext CreateDbContext() => db.Create(string.Empty);
    }
}

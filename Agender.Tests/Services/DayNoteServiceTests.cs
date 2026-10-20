using Agender.Shared.Domain;
using Agender.Infrastructure.Services;
using Agender.Shared.Services;
using FluentAssertions;

namespace Agender.Tests.Services;

public sealed class DayNoteServiceTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 10, 12);

    private readonly TestDb _db = new();
    private readonly DayNoteService _sut;

    public DayNoteServiceTests() => _sut = new DayNoteService(_db);

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task SaveAsync_ExistingNote_UpdatesText()
    {
        await _sut.SaveAsync(Day, "first");
        await _sut.SaveAsync(Day, "second");

        IReadOnlyList<DayNote> result = await _sut.GetRangeAsync(Day, Day);

        result.Should().ContainSingle().Which.Text.Should().Be("second");
    }

    [Fact]
    public async Task SaveAsync_BlankText_RemovesNote()
    {
        await _sut.SaveAsync(Day, "something");
        await _sut.SaveAsync(Day, "   ");

        (await _sut.GetRangeAsync(Day, Day)).Should().BeEmpty();
    }
}

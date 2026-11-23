using Agender.Infrastructure.Services;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using FluentAssertions;

namespace Agender.Tests.Services;

public sealed class IncomeServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly IncomeService _sut;

    public IncomeServiceTests() => _sut = new IncomeService(_db, TimeProvider.System);

    public void Dispose() => _db.Dispose();

    private Task<IncomeSourceDto> SurveysAsync() => _sut.CreateSourceAsync(new IncomeSourceInput("Surveys", "📝", EntryColor.Teal, false));

    [Fact]
    public async Task CreateSourceAsync_Valid_IsListedInCreationOrder()
    {
        await SurveysAsync();
        await _sut.CreateSourceAsync(new IncomeSourceInput("  Freelance ", string.Empty, EntryColor.Amber, false));

        IReadOnlyList<IncomeSourceDto> sources = await _sut.GetSourcesAsync();

        sources.Select(s => s.Name).Should().Equal("Surveys", "Freelance");
        sources.Select(s => s.SortOrder).Should().Equal(0, 1);
    }

    [Fact]
    public async Task CreateSourceAsync_DuplicateName_Throws()
    {
        await SurveysAsync();

        Func<Task> act = () => _sut.CreateSourceAsync(new IncomeSourceInput("surveys", string.Empty, EntryColor.Teal, false));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateEntryAsync_ZeroAmount_Throws()
    {
        IncomeSourceDto source = await SurveysAsync();

        Func<Task> act = () => _sut.CreateEntryAsync(new IncomeEntryInput(source.Id, new DateOnly(2026, 11, 2), 0, "Prolific", string.Empty));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateEntryAsync_UnknownSource_ThrowsNotFound()
    {
        Func<Task> act = () => _sut.CreateEntryAsync(new IncomeEntryInput(Guid.NewGuid(), new DateOnly(2026, 11, 2), 3, "Prolific", string.Empty));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetEntriesAsync_FiltersByRangeAndSource()
    {
        IncomeSourceDto surveys = await SurveysAsync();
        IncomeSourceDto other = await _sut.CreateSourceAsync(new IncomeSourceInput("Freelance", string.Empty, EntryColor.Amber, false));
        await _sut.CreateEntryAsync(new IncomeEntryInput(surveys.Id, new DateOnly(2026, 11, 2), 3.456m, " Prolific ", "nota"));
        await _sut.CreateEntryAsync(new IncomeEntryInput(surveys.Id, new DateOnly(2026, 12, 2), 1, "Prolific", string.Empty));
        await _sut.CreateEntryAsync(new IncomeEntryInput(other.Id, new DateOnly(2026, 11, 3), 50, string.Empty, string.Empty));

        IReadOnlyList<IncomeEntryDto> entries = await _sut.GetEntriesAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), surveys.Id);

        entries.Should().ContainSingle().Which.Should().Match<IncomeEntryDto>(e => e.Amount == 3.46m && e.Platform == "Prolific" && e.Note == "nota");
        (await _sut.GetEntriesAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30))).Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateEntryAsync_ChangesTheValues()
    {
        IncomeSourceDto source = await SurveysAsync();
        IncomeEntryDto entry = await _sut.CreateEntryAsync(new IncomeEntryInput(source.Id, new DateOnly(2026, 11, 2), 3, "Prolific", string.Empty));

        IncomeEntryDto updated = await _sut.UpdateEntryAsync(entry.Id, new IncomeEntryInput(source.Id, new DateOnly(2026, 11, 4), 5, "YouGov", "x"));

        updated.Should().Be(new IncomeEntryDto(entry.Id, source.Id, new DateOnly(2026, 11, 4), 5, "YouGov", "x"));
    }

    [Fact]
    public async Task DeleteSourceAsync_RemovesItsEntries()
    {
        IncomeSourceDto source = await SurveysAsync();
        await _sut.CreateEntryAsync(new IncomeEntryInput(source.Id, new DateOnly(2026, 11, 2), 3, "Prolific", string.Empty));

        await _sut.DeleteSourceAsync(source.Id);

        (await _sut.GetSourcesAsync()).Should().BeEmpty();
        (await _sut.GetEntriesAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31))).Should().BeEmpty();
    }

    [Fact]
    public async Task GetPlatformsAsync_ReturnsDistinctMostRecentFirst()
    {
        IncomeSourceDto source = await SurveysAsync();
        await _sut.CreateEntryAsync(new IncomeEntryInput(source.Id, new DateOnly(2026, 11, 1), 1, "Prolific", string.Empty));
        await _sut.CreateEntryAsync(new IncomeEntryInput(source.Id, new DateOnly(2026, 11, 3), 1, "YouGov", string.Empty));
        await _sut.CreateEntryAsync(new IncomeEntryInput(source.Id, new DateOnly(2026, 11, 2), 1, "prolific", string.Empty));
        await _sut.CreateEntryAsync(new IncomeEntryInput(source.Id, new DateOnly(2026, 11, 2), 1, string.Empty, string.Empty));

        IReadOnlyList<string> platforms = await _sut.GetPlatformsAsync(source.Id);

        platforms.Should().Equal("YouGov", "prolific");
    }

    [Fact]
    public async Task OtherUsers_CannotSeeOrChangeTheSources()
    {
        IncomeSourceDto source = await SurveysAsync();
        IncomeService other = new((Agender.Infrastructure.Data.IUserDbContextFactory)_db.ForUser("user-b"), TimeProvider.System);

        (await other.GetSourcesAsync()).Should().BeEmpty();
        Func<Task> act = () => other.CreateEntryAsync(new IncomeEntryInput(source.Id, new DateOnly(2026, 11, 2), 3, string.Empty, string.Empty));
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateEntryAsync_PendingApproved_BecomesEarnedOnTheSameDate()
    {
        IncomeSourceDto source = await SurveysAsync();
        IncomeEntryDto pending = await _sut.CreateEntryAsync(new IncomeEntryInput(source.Id, new DateOnly(2026, 11, 2), 15, "Prolific", string.Empty, IncomeEntryStatus.Pending));

        IncomeEntryDto approved = await _sut.UpdateEntryAsync(pending.Id, new IncomeEntryInput(source.Id, pending.Date, pending.Amount, pending.Platform, pending.Note, IncomeEntryStatus.Earned));

        pending.Status.Should().Be(IncomeEntryStatus.Pending);
        approved.Should().Match<IncomeEntryDto>(e => e.Status == IncomeEntryStatus.Earned && e.Date == new DateOnly(2026, 11, 2));
    }

    [Fact]
    public async Task CreateEntryAsync_WithoutStatus_IsEarned()
    {
        IncomeSourceDto source = await SurveysAsync();

        IncomeEntryDto entry = await _sut.CreateEntryAsync(new IncomeEntryInput(source.Id, new DateOnly(2026, 11, 2), 5, "Prolific", string.Empty));

        entry.Status.Should().Be(IncomeEntryStatus.Earned);
    }
}

using Agender.Infrastructure.Data;
using Agender.Infrastructure.Trader;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Agender.Tests.Services;

public sealed class TraderIngestServiceTests : IDisposable
{
    private static readonly TimeZoneInfo Lisbon = TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");

    private readonly TestDb _db = new();
    private readonly TraderIngestService _sut;

    public TraderIngestServiceTests()
    {
        _sut = new TraderIngestService(_db, new LisbonTime());
    }

    public void Dispose() => _db.Dispose();

    private static TraderTradeInput Closed(long id, string entry, string exit, double pnl, double invested = 1000) => new(
        id, "AAPL", "Apple", true, true, DateTimeOffset.Parse(entry), 100, 10, 95, 110, DateTimeOffset.Parse(exit), 100 + pnl / 10, 1, invested, 50, pnl, pnl / 50, "EUR", "T212", "nota", "breakout", "a,b", "calmo");

    private static TraderTradeInput Open(long id, string entry) => new(
        id, "TSLA", "Tesla", false, false, DateTimeOffset.Parse(entry), 200, 5, 210, 180, null, null, 0, 1000, 50, null, null, "EUR", "T212", string.Empty, string.Empty, string.Empty, string.Empty);

    private async Task<(List<TraderTrade> Trades, List<TimeEntry> Entries, List<Agenda> Agendas, List<Project> Projects)> ReadAsync()
    {
        await using AgenderDbContext context = _db.Create(TestDb.DefaultUserId);
        return (await context.TraderTrades.ToListAsync(), await context.TimeEntries.ToListAsync(), await context.Agendas.ToListAsync(), await context.Projects.ToListAsync());
    }

    [Fact]
    public async Task SyncAsync_NewTrades_StoresThemAndCreatesTheTraderAgendaWithEntries()
    {
        TraderSyncResult result = await _sut.SyncAsync(new TraderSyncRequest([Closed(1, "2026-11-02T09:00:00Z", "2026-11-02T10:30:00Z", 25), Open(2, "2026-11-02T11:00:00Z")]));

        result.Should().Be(new TraderSyncResult(2, 0, 0));
        (List<TraderTrade> trades, List<TimeEntry> entries, List<Agenda> agendas, List<Project> projects) = await ReadAsync();
        trades.Should().HaveCount(2);
        agendas.Should().ContainSingle(a => a.Name == TraderIngestService.AgendaName);
        projects.Select(p => p.Name).Should().BeEquivalentTo(TraderIngestService.WinProject, TraderIngestService.LossProject, TraderIngestService.OpenProject);
        entries.Should().HaveCount(2);
        entries.Should().OnlyContain(e => e.AgendaId == agendas.Single().Id);
    }

    [Fact]
    public async Task SyncAsync_SameTradeTwice_UpdatesWithoutDuplicating()
    {
        await _sut.SyncAsync(new TraderSyncRequest([Open(1, "2026-11-02T09:00:00Z")]));

        TraderSyncResult result = await _sut.SyncAsync(new TraderSyncRequest([Closed(1, "2026-11-02T09:00:00Z", "2026-11-02T10:30:00Z", -30)]));

        result.Should().Be(new TraderSyncResult(0, 1, 0));
        (List<TraderTrade> trades, List<TimeEntry> entries, _, List<Project> projects) = await ReadAsync();
        trades.Should().ContainSingle().Which.IsClosed.Should().BeTrue();
        entries.Should().ContainSingle().Which.ProjectId.Should().Be(projects.Single(p => p.Name == TraderIngestService.LossProject).Id);
    }

    [Fact]
    public async Task SyncAsync_TradeMissingFromTheSnapshot_IsRemovedWithItsEntry()
    {
        await _sut.SyncAsync(new TraderSyncRequest([Open(1, "2026-11-02T09:00:00Z"), Open(2, "2026-11-02T09:30:00Z")]));

        TraderSyncResult result = await _sut.SyncAsync(new TraderSyncRequest([Open(2, "2026-11-02T09:30:00Z")]));

        result.Removed.Should().Be(1);
        (List<TraderTrade> trades, List<TimeEntry> entries, _, _) = await ReadAsync();
        trades.Should().ContainSingle().Which.ExternalId.Should().Be(2);
        entries.Should().ContainSingle();
    }

    [Fact]
    public async Task SyncAsync_DuplicateIds_Throws()
    {
        Func<Task> act = () => _sut.SyncAsync(new TraderSyncRequest([Open(1, "2026-11-02T09:00:00Z"), Open(1, "2026-11-02T09:30:00Z")]));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SyncAsync_StoresLocalDatesUsingTheServerTimeZone()
    {
        await _sut.SyncAsync(new TraderSyncRequest([Closed(1, "2026-06-30T23:30:00Z", "2026-07-01T09:00:00Z", 10)]));

        (List<TraderTrade> trades, _, _, _) = await ReadAsync();
        trades.Single().EntryDate.Should().Be(new DateOnly(2026, 7, 1));
        trades.Single().ExitDate.Should().Be(new DateOnly(2026, 7, 1));
    }

    [Fact]
    public async Task SyncAsync_ManualDeletionOfTheEntry_IsRecreatedOnNextSync()
    {
        await _sut.SyncAsync(new TraderSyncRequest([Open(1, "2026-11-02T09:00:00Z")]));
        await using (AgenderDbContext context = _db.Create(TestDb.DefaultUserId))
        {
            await context.TimeEntries.ExecuteDeleteAsync();
        }

        await _sut.SyncAsync(new TraderSyncRequest([Open(1, "2026-11-02T09:00:00Z")]));

        (_, List<TimeEntry> entries, _, _) = await ReadAsync();
        entries.Should().ContainSingle();
    }

    [Fact]
    public async Task SyncAsync_OtherUsersData_IsNotTouched()
    {
        await _sut.SyncAsync(new TraderSyncRequest([Open(1, "2026-11-02T09:00:00Z")]));
        TraderIngestService other = new(_db.ForUser("user-b"), new LisbonTime());

        await other.SyncAsync(new TraderSyncRequest([]));

        (List<TraderTrade> trades, _, _, _) = await ReadAsync();
        trades.Should().ContainSingle();
    }

    [Fact]
    public void Place_ClosedSameDayTrade_SpansEntryToExit()
    {
        TraderTrade trade = new() { IsClosed = true, EntryTime = DateTime.Parse("2026-11-02T09:00:00Z").ToUniversalTime(), ExitTime = DateTime.Parse("2026-11-02T10:30:00Z").ToUniversalTime() };

        (DateOnly date, int start, int end) = TraderIngestService.Place(trade, Lisbon);

        date.Should().Be(new DateOnly(2026, 11, 2));
        (start, end).Should().Be((9 * 60, 10 * 60 + 30));
    }

    [Fact]
    public void Place_ClosedMultiDayTrade_UsesAMarkerEndingAtTheExit()
    {
        TraderTrade trade = new() { IsClosed = true, EntryTime = DateTime.Parse("2026-11-01T09:00:00Z").ToUniversalTime(), ExitTime = DateTime.Parse("2026-11-02T10:30:00Z").ToUniversalTime() };

        (DateOnly date, int start, int end) = TraderIngestService.Place(trade, Lisbon);

        date.Should().Be(new DateOnly(2026, 11, 2));
        (start, end).Should().Be((10 * 60, 10 * 60 + 30));
    }

    [Fact]
    public void Place_OpenTradeLateInTheDay_StaysInsideTheDay()
    {
        TraderTrade trade = new() { EntryTime = DateTime.Parse("2026-11-02T23:50:00Z").ToUniversalTime() };

        (_, int start, int end) = TraderIngestService.Place(trade, Lisbon);

        end.Should().BeLessThanOrEqualTo(TimeSlot.MinutesPerDay);
        (end - start).Should().Be(30);
    }

    [Fact]
    public void Describe_ClosedTrade_ShowsDirectionAndSignedResult()
    {
        TraderTrade trade = new() { Symbol = "AAPL", IsBuy = true, IsClosed = true, NetPnl = -12.5, Currency = "EUR" };

        TraderIngestService.Describe(trade).Should().Be("AAPL ▲ Compra · -12,50 EUR");
    }

    private sealed class LisbonTime : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => Lisbon;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.Parse("2026-11-02T12:00:00Z");
    }
}

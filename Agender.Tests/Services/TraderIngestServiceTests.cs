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
    public async Task SyncAsync_NewTrades_StoresThemWithoutTouchingTheAgenda()
    {
        TraderSyncResult result = await _sut.SyncAsync(new TraderSyncRequest([Closed(1, "2026-11-02T09:00:00Z", "2026-11-02T10:30:00Z", 25), Open(2, "2026-11-02T11:00:00Z")]));

        result.Should().Be(new TraderSyncResult(2, 0, 0));
        (List<TraderTrade> trades, List<TimeEntry> entries, List<Agenda> agendas, List<Project> projects) = await ReadAsync();
        trades.Should().HaveCount(2);
        entries.Should().BeEmpty();
        agendas.Should().BeEmpty();
        projects.Should().BeEmpty();
    }

    [Fact]
    public async Task SyncAsync_SameTradeTwice_UpdatesWithoutDuplicating()
    {
        await _sut.SyncAsync(new TraderSyncRequest([Open(1, "2026-11-02T09:00:00Z")]));

        TraderSyncResult result = await _sut.SyncAsync(new TraderSyncRequest([Closed(1, "2026-11-02T09:00:00Z", "2026-11-02T10:30:00Z", -30)]));

        result.Should().Be(new TraderSyncResult(0, 1, 0));
        (List<TraderTrade> trades, _, _, _) = await ReadAsync();
        trades.Should().ContainSingle().Which.NetPnl.Should().Be(-30);
    }

    [Fact]
    public async Task SyncAsync_TradeMissingFromTheSnapshot_IsRemoved()
    {
        await _sut.SyncAsync(new TraderSyncRequest([Open(1, "2026-11-02T09:00:00Z"), Open(2, "2026-11-02T09:30:00Z")]));

        TraderSyncResult result = await _sut.SyncAsync(new TraderSyncRequest([Open(2, "2026-11-02T09:30:00Z")]));

        result.Removed.Should().Be(1);
        (List<TraderTrade> trades, _, _, _) = await ReadAsync();
        trades.Should().ContainSingle().Which.ExternalId.Should().Be(2);
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
    public async Task SyncAsync_OtherUsersData_IsNotTouched()
    {
        await _sut.SyncAsync(new TraderSyncRequest([Open(1, "2026-11-02T09:00:00Z")]));
        TraderIngestService other = new(_db.ForUser("user-b"), new LisbonTime());

        await other.SyncAsync(new TraderSyncRequest([]));

        (List<TraderTrade> trades, _, _, _) = await ReadAsync();
        trades.Should().ContainSingle();
    }

    private sealed class LisbonTime : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => Lisbon;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.Parse("2026-11-02T12:00:00Z");
    }
}

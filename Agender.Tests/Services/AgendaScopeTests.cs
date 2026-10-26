using Agender.Infrastructure.Data;
using Agender.Infrastructure.Google;
using Agender.Infrastructure.Identity;
using Agender.Infrastructure.Realtime;
using Agender.Infrastructure.Services;
using Agender.Shared.Abstractions;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using Agender.Shared.State;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Agender.Tests.Services;

public sealed class AgendaScopeTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 10, 12);
    private static readonly TimeProvider Time = new FixedTime();

    private readonly TestDb _db = new();

    public AgendaScopeTests()
    {
        using AgenderDbContext context = _db.Create(string.Empty);
        context.Users.AddRange(User("ana"), User("rui"), User("eva"));
        context.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private static AgenderUser User(string id) => new()
    {
        Id = id,
        UserName = id,
        NormalizedUserName = id.ToUpperInvariant(),
        Email = $"{id}@x.pt",
        NormalizedEmail = $"{id}@x.pt".ToUpperInvariant(),
        DisplayName = id
    };

    private async Task<Guid> CoupleAsync()
    {
        HouseholdDto household = await new HouseholdService(_db.ForUser("ana"), Time).CreateAsync("Casa");
        await new HouseholdService(_db.ForUser("rui"), Time).JoinAsync(household.InviteCode);

        return household.Id;
    }

    private AgendaDbContextFactory Factory(string me, string? target, IChangePublisher? publisher = null)
    {
        AgendaScope scope = new() { MyUserId = me };
        scope.Select(target);

        return new AgendaDbContextFactory(new ContextFactory(_db), new FixedUser(me), scope, publisher);
    }

    [Fact]
    public async Task Create_NoTarget_UsesTheCurrentUser()
    {
        await using AgenderDbContext context = await Factory("ana", null).CreateAsync();

        context.UserId.Should().Be("ana");
        context.ActorId.Should().Be("ana");
    }

    [Fact]
    public async Task Create_TargetIsAHouseholdMember_ActsAsThatMember()
    {
        await CoupleAsync();

        await using AgenderDbContext context = await Factory("ana", "rui").CreateAsync();

        context.UserId.Should().Be("rui");
        context.ActorId.Should().Be("ana");
    }

    [Fact]
    public async Task Create_TargetOutsideTheHousehold_IsNotFound()
    {
        await CoupleAsync();

        Func<Task> act = () => Factory("ana", "eva").CreateAsync();

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Create_UserWithoutHousehold_CannotActAsAnyone()
    {
        Func<Task> act = () => Factory("eva", "rui").CreateAsync();

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Create_OwnHouseholdScope_IsAllowed()
    {
        Guid household = await CoupleAsync();

        await using AgenderDbContext context = await Factory("rui", AgendaScope.HouseholdScopeId(household)).CreateAsync();

        context.UserId.Should().Be($"h:{household}");
    }

    [Fact]
    public async Task Create_AnotherHouseholdsScope_IsNotFound()
    {
        await CoupleAsync();
        HouseholdDto other = await new HouseholdService(_db.ForUser("eva"), Time).CreateAsync("Outra");

        Func<Task> act = () => Factory("ana", AgendaScope.HouseholdScopeId(other.Id)).CreateAsync();

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Services_ActingAsAPartner_ReadAndWriteThePartnersAgenda()
    {
        await CoupleAsync();
        await new TimeEntryService(_db.ForUser("rui")).CreateAsync(new TimeEntry { Date = Day, StartMinute = 540, EndMinute = 600, Description = "Do Rui" });

        TimeEntryService asRui = new(Factory("ana", "rui"));
        IReadOnlyList<TimeEntry> seen = await asRui.GetRangeAsync(Day, Day);
        TimeEntry created = await asRui.CreateAsync(new TimeEntry { Date = Day, StartMinute = 660, EndMinute = 720, Description = "Feito pela Ana" });

        seen.Should().ContainSingle().Which.Description.Should().Be("Do Rui");
        (await new TimeEntryService(_db.ForUser("rui")).GetRangeAsync(Day, Day)).Should().Contain(e => e.Id == created.Id);
        (await new TimeEntryService(_db.ForUser("ana")).GetRangeAsync(Day, Day)).Should().BeEmpty();
    }

    [Fact]
    public async Task Services_HouseholdScope_SharesOneAgendaBetweenMembers()
    {
        Guid household = await CoupleAsync();
        string scope = AgendaScope.HouseholdScopeId(household);

        await new TimeEntryService(Factory("ana", scope)).CreateAsync(new TimeEntry { Date = Day, StartMinute = 540, EndMinute = 600, Description = "Jantar" });

        (await new TimeEntryService(Factory("rui", scope)).GetRangeAsync(Day, Day)).Should().ContainSingle().Which.Description.Should().Be("Jantar");
        (await new TimeEntryService(_db.ForUser("ana")).GetRangeAsync(Day, Day)).Should().BeEmpty();
        (await new AgendaService(Factory("ana", scope)).GetAllAsync()).Should().ContainSingle().Which.Name.Should().Be(Agenda.DefaultName);
    }

    [Fact]
    public async Task Leave_LastMember_RemovesTheHouseholdAgendaData()
    {
        Guid household = await CoupleAsync();
        string scope = AgendaScope.HouseholdScopeId(household);
        await new TimeEntryService(Factory("ana", scope)).CreateAsync(new TimeEntry { Date = Day, StartMinute = 540, EndMinute = 600 });
        await new HouseholdService(_db.ForUser("rui"), Time).LeaveAsync();
        await new HouseholdService(_db.ForUser("ana"), Time).LeaveAsync();

        using AgenderDbContext context = _db.Create(scope);

        context.TimeEntries.Should().BeEmpty();
        context.Agendas.Should().BeEmpty();
    }

    [Fact]
    public async Task GoogleSync_ActingAsAnotherUser_IsSkipped()
    {
        await CoupleAsync();
        StubSync sync = new();
        AgendaScope scope = new() { MyUserId = "ana" };
        scope.Select("rui");
        GoogleSyncingTimeEntryService sut = new(new TimeEntryService(Factory("ana", "rui")), sync, scope);

        TimeEntry created = await sut.CreateAsync(new TimeEntry { Date = Day, StartMinute = 540, EndMinute = 600, SyncToGoogle = true });
        await sut.UpdateAsync(created);
        await sut.DeleteAsync(created.Id);

        sync.Calls.Should().Be(0);
    }

    [Fact]
    public async Task GoogleSync_OwnAgenda_StillSyncs()
    {
        StubSync sync = new();
        GoogleSyncingTimeEntryService sut = new(new TimeEntryService(_db.ForUser("ana")), sync, new AgendaScope());

        await sut.CreateAsync(new TimeEntry { Date = Day, StartMinute = 540, EndMinute = 600, SyncToGoogle = true });

        sync.Calls.Should().Be(1);
    }

    [Fact]
    public async Task ChangeBus_Dispatch_NotifiesTheOtherHouseholdMembersButNotTheActor()
    {
        Guid household = await CoupleAsync();
        ChangeBus bus = new(new ContextFactory(_db));
        List<(ChangeNotice Notice, string[] Recipients)> received = [];
        bus.Received += (notice, recipients) =>
        {
            received.Add((notice, [.. recipients]));
            return Task.CompletedTask;
        };

        await bus.DispatchAsync("rui", "ana");
        await bus.DispatchAsync(AgendaScope.HouseholdScopeId(household), "rui");

        received.Should().HaveCount(2);
        received[0].Notice.Should().Be(new ChangeNotice("rui", "ana"));
        received[0].Recipients.Should().Equal("rui");
        received[1].Recipients.Should().Equal("ana");
    }

    [Fact]
    public async Task ChangeBus_Dispatch_UserWithoutHousehold_NotifiesNobody()
    {
        ChangeBus bus = new(new ContextFactory(_db));
        int calls = 0;
        bus.Received += (_, _) =>
        {
            calls++;
            return Task.CompletedTask;
        };

        await bus.DispatchAsync("eva", "eva");

        calls.Should().Be(0);
    }

    [Fact]
    public async Task InProcessFeed_RaisesChangedOnlyForItsUser()
    {
        await CoupleAsync();
        ChangeBus bus = new(new ContextFactory(_db));
        InProcessChangeFeed ruiFeed = new(bus, new FixedUser("rui"));
        InProcessChangeFeed evaFeed = new(bus, new FixedUser("eva"));
        List<ChangeNotice> forRui = [];
        List<ChangeNotice> forEva = [];
        ruiFeed.Changed += notice =>
        {
            forRui.Add(notice);
            return Task.CompletedTask;
        };
        evaFeed.Changed += notice =>
        {
            forEva.Add(notice);
            return Task.CompletedTask;
        };
        await ruiFeed.StartAsync();
        await evaFeed.StartAsync();

        await bus.DispatchAsync("ana", "ana");

        forRui.Should().ContainSingle();
        forEva.Should().BeEmpty();
    }

    [Fact]
    public async Task Interceptor_WriteThroughAService_PublishesOnceAfterTheContextIsDisposed()
    {
        using SqliteConnection connection = new("Data Source=:memory:");
        connection.Open();
        DbContextOptions<AgenderDbContext> options = new DbContextOptionsBuilder<AgenderDbContext>().UseSqlite(connection).AddInterceptors(new WriteTrackingInterceptor()).Options;

        using (AgenderDbContext setup = new(options))
        {
            setup.Database.EnsureCreated();
        }

        RecordingPublisher publisher = new();
        TimeEntryService service = new(new InlineFactory(options, "ana", publisher));

        await service.GetRangeAsync(Day, Day);
        publisher.Published.Should().BeEmpty();

        await service.CreateAsync(new TimeEntry { Date = Day, StartMinute = 540, EndMinute = 600 });

        publisher.Published.Should().ContainSingle().Which.Should().Be(("ana", "ana"));
    }

    [Fact]
    public async Task ChangeBus_Publish_DoesNotLeakTheCallersAmbientContextIntoTheHandlers()
    {
        await CoupleAsync();
        ChangeBus bus = new(new ContextFactory(_db));
        AsyncLocal<string?> ambient = new();
        TaskCompletionSource<string?> seen = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bus.Received += (_, _) =>
        {
            seen.TrySetResult(ambient.Value);
            return Task.CompletedTask;
        };

        ambient.Value = "ana-request";
        bus.Publish("rui", "ana");

        (await seen.Task.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeNull();
    }

    [Fact]
    public void AgendaScope_Select_NormalizesOwnIdAndRaisesChangedOnlyOnChange()
    {
        AgendaScope scope = new() { MyUserId = "ana" };
        int changes = 0;
        scope.Changed += () => changes++;

        scope.Select("ana");
        scope.Select(null);
        scope.Select("rui");
        scope.Select("rui");
        scope.Select("ana");

        changes.Should().Be(2);
        scope.IsForeign.Should().BeFalse();
        scope.EffectiveId.Should().Be("ana");
    }

    private sealed class ContextFactory(TestDb db) : IDbContextFactory<AgenderDbContext>
    {
        public AgenderDbContext CreateDbContext() => db.Create(string.Empty);
    }

    private sealed class InlineFactory(DbContextOptions<AgenderDbContext> options, string userId, IChangePublisher publisher) : IAgendaDbContextFactory
    {
        public Task<AgenderDbContext> CreateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AgenderDbContext(options) { UserId = userId, ActorId = userId, ChangePublisher = publisher });
    }

    private sealed class RecordingPublisher : IChangePublisher
    {
        public List<(string ScopeId, string ActorId)> Published { get; } = [];

        public void Publish(string scopeId, string actorId) => Published.Add((scopeId, actorId));
    }

    private sealed class FixedUser(string id) : ICurrentUser
    {
        public Task<string> GetUserIdAsync(CancellationToken cancellationToken = default) => Task.FromResult(id);
    }

    private sealed class StubSync : IGoogleEntrySync
    {
        public int Calls { get; private set; }

        public Task<bool> SyncEntryAsync(Guid entryId, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(true);
        }

        public Task ForgetEntryAsync(Guid entryId, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}

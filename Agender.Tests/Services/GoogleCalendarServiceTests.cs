using Agender.Infrastructure.Data;
using Agender.Infrastructure.Google;
using Agender.Infrastructure.Identity;
using Agender.Infrastructure.Services;
using Agender.Shared.Domain;
using Agender.Shared.Google;
using Agender.Shared.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agender.Tests.Services;

public sealed class GoogleCalendarServiceTests : IDisposable
{
    private const string Json = "{\"type\":\"service_account\",\"client_email\":\"bot@project.iam.gserviceaccount.com\",\"private_key\":\"k\"}";
    private const string Calendar = "me@example.com";

    private static readonly DateOnly Day = new(2026, 10, 14);

    private readonly TestDb _db = new();
    private readonly FakeGoogleApi _api = new();
    private readonly EphemeralDataProtectionProvider _protection = new();

    public void Dispose() => _db.Dispose();

    private GoogleCalendarService CreateService(string userId = TestDb.DefaultUserId) => new(
        new RawFactory(_db),
        new FixedUser(userId),
        _api,
        _protection,
        TimeProvider.System,
        NullLogger<GoogleCalendarService>.Instance);

    private async Task<GoogleCalendarService> ConnectedServiceAsync(string userId = TestDb.DefaultUserId, string? writeCalendar = null)
    {
        GoogleCalendarService service = CreateService(userId);
        await service.ConnectAsync(Json, Calendar, writeCalendar);

        return service;
    }

    private async Task<TimeEntry> NewEntryAsync(bool sync, string description = "Review")
    {
        TimeEntryService entries = new(_db);

        return await entries.CreateAsync(new TimeEntry { Date = Day, StartMinute = 540, EndMinute = 600, Description = description, SyncToGoogle = sync });
    }

    private async Task<TimeEntry> ReloadAsync(Guid id)
    {
        await using AgenderDbContext context = await _db.CreateAsync();
        return await context.TimeEntries.FindAsync(id) ?? throw new InvalidOperationException();
    }

    [Fact]
    public async Task GetStatusAsync_NotConnected_ReportsNotConnected()
    {
        (await CreateService().GetStatusAsync()).Should().Be(new GoogleStatus(false));
    }

    [Fact]
    public async Task ConnectAsync_ValidKey_StoresTheConnectionAndExposesOnlyTheEmailAndCalendars()
    {
        GoogleCalendarService service = await ConnectedServiceAsync();

        GoogleStatus status = await service.GetStatusAsync();

        status.Should().Be(new GoogleStatus(true, "bot@project.iam.gserviceaccount.com", Calendar, Calendar));
    }

    [Fact]
    public async Task ConnectAsync_Credentials_AreStoredProtected()
    {
        await ConnectedServiceAsync();

        await using AgenderDbContext context = await _db.CreateAsync();
        GoogleConnection connection = context.GoogleConnections.Single();
        connection.ProtectedCredentials.Should().NotContain("private_key").And.NotBeEmpty();
        connection.TimeZone.Should().Be("Europe/Lisbon");
    }

    [Fact]
    public async Task ConnectAsync_WithAWriteCalendar_UsesItForEntries()
    {
        GoogleCalendarService service = await ConnectedServiceAsync(writeCalendar: "agender@group.calendar.google.com");

        GoogleStatus status = await service.GetStatusAsync();

        status.CalendarId.Should().Be(Calendar);
        status.WriteCalendarId.Should().Be("agender@group.calendar.google.com");
    }

    [Fact]
    public async Task ConnectAsync_NoAccessToTheCalendar_Throws()
    {
        _api.FailValidation = true;

        Func<Task> act = () => CreateService().ConnectAsync(Json, Calendar, null);

        await act.Should().ThrowAsync<ArgumentException>();
        (await CreateService().GetStatusAsync()).IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task ConnectAsync_EmptyCalendarId_Throws()
    {
        Func<Task> act = () => CreateService().ConnectAsync(Json, "  ", null);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ConnectAsync_ChangingTheWriteCalendar_UnlinksExistingEvents()
    {
        GoogleCalendarService service = await ConnectedServiceAsync();
        TimeEntry entry = await NewEntryAsync(sync: true);
        await service.SyncEntryAsync(entry.Id, default);

        await service.ConnectAsync(Json, Calendar, "other@group.calendar.google.com");

        TimeEntry stored = await ReloadAsync(entry.Id);
        stored.GoogleEventId.Should().BeNull();
        stored.SyncToGoogle.Should().BeTrue();
    }

    [Fact]
    public async Task ConnectAsync_ForOneUser_DoesNotConnectAnother()
    {
        await ConnectedServiceAsync();

        (await CreateService("user-b").GetStatusAsync()).IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task GetEventsAsync_NotConnected_Throws()
    {
        Func<Task> act = () => CreateService().GetEventsAsync(Day, Day);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetEventsAsync_Connected_ReadsFromTheReadCalendar()
    {
        GoogleCalendarService service = await ConnectedServiceAsync(writeCalendar: "agender@group.calendar.google.com");
        _api.Events = [new GoogleEvent("e1", "Standup", Day, 540, 555), new GoogleEvent("e2", "Holiday", Day, null, null)];

        IReadOnlyList<GoogleEvent> events = await service.GetEventsAsync(Day, Day);

        events.Select(e => e.Title).Should().Equal("Holiday", "Standup");
        events[0].IsAllDay.Should().BeTrue();
        _api.ListedCalendars.Should().Equal(Calendar);
    }

    [Fact]
    public async Task GetEventsAsync_TagsEachEventWithItsCalendar()
    {
        GoogleCalendarService service = await ConnectedServiceAsync(writeCalendar: "agender@group.calendar.google.com");
        _api.EventsByCalendar[Calendar] = [new GoogleEvent("e1", "Standup", Day, 540, 555)];
        _api.EventsByCalendar["agender@group.calendar.google.com"] = [new GoogleEvent("e2", "Review", Day, 700, 760, true, Guid.NewGuid())];

        IReadOnlyList<GoogleEvent> events = await service.GetEventsAsync(Day, Day, includeOwn: true);

        events.Single(e => e.Id == "e1").CalendarId.Should().Be(Calendar);
        events.Single(e => e.Id == "e2").CalendarId.Should().Be("agender@group.calendar.google.com");
    }

    [Fact]
    public async Task DeleteEventAsync_ReadCalendarEvent_DeletesItInGoogle()
    {
        GoogleCalendarService service = await ConnectedServiceAsync();

        await service.DeleteEventAsync(Calendar, "e1");

        _api.DeletedFrom.Should().Equal((Calendar, "e1"));
    }

    [Fact]
    public async Task DeleteEventAsync_WriteCalendarEvent_IsAllowed()
    {
        GoogleCalendarService service = await ConnectedServiceAsync(writeCalendar: "agender@group.calendar.google.com");

        await service.DeleteEventAsync("agender@group.calendar.google.com", "e2");

        _api.DeletedFrom.Should().Equal(("agender@group.calendar.google.com", "e2"));
    }

    [Fact]
    public async Task DeleteEventAsync_CalendarThatIsNotConnected_IsRejected()
    {
        GoogleCalendarService service = await ConnectedServiceAsync();

        Func<Task> act = () => service.DeleteEventAsync("someone.else@example.com", "e1");

        await act.Should().ThrowAsync<ArgumentException>();
        _api.DeletedFrom.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteEventAsync_NotConnected_Throws()
    {
        Func<Task> act = () => CreateService().DeleteEventAsync(Calendar, "e1");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteEventAsync_EventWrittenForAnEntry_UnlinksTheEntryButKeepsIt()
    {
        GoogleCalendarService service = await ConnectedServiceAsync();
        TimeEntry entry = await NewEntryAsync(sync: true);
        await service.SyncEntryAsync(entry.Id, default);

        await service.DeleteEventAsync(Calendar, "event-1");

        TimeEntry stored = await ReloadAsync(entry.Id);
        stored.SyncToGoogle.Should().BeFalse();
        stored.GoogleEventId.Should().BeNull();
        stored.GoogleSyncFailed.Should().BeFalse();
        _api.DeletedFrom.Should().Equal((Calendar, "event-1"));
    }

    [Fact]
    public async Task DeleteEventAsync_GoogleRefuses_SurfacesAPersistenceErrorAndKeepsTheEntryLinked()
    {
        GoogleCalendarService service = await ConnectedServiceAsync();
        TimeEntry entry = await NewEntryAsync(sync: true);
        await service.SyncEntryAsync(entry.Id, default);
        _api.FailDeletes = true;

        Func<Task> act = () => service.DeleteEventAsync(Calendar, "event-1");

        await act.Should().ThrowAsync<PersistenceException>();
        (await ReloadAsync(entry.Id)).GoogleEventId.Should().Be("event-1");
    }

    [Fact]
    public async Task GetEventsAsync_ByDefault_DoesNotAskForTheEventsWrittenByTheApp()
    {
        GoogleCalendarService service = await ConnectedServiceAsync();

        await service.GetEventsAsync(Day, Day);

        _api.IncludeOwnRequests.Should().Equal(false);
    }

    [Fact]
    public async Task GetEventsAsync_IncludingOwn_ReadsBothCalendarsAndMergesWithoutDuplicates()
    {
        GoogleCalendarService service = await ConnectedServiceAsync(writeCalendar: "agender@group.calendar.google.com");
        GoogleEvent shared = new("same", "Shared", Day, 600, 660, true);
        _api.EventsByCalendar[Calendar] = [new GoogleEvent("e1", "Standup", Day, 540, 555), shared];
        _api.EventsByCalendar["agender@group.calendar.google.com"] = [shared, new GoogleEvent("e2", "Review", Day, 700, 760, true, Guid.NewGuid())];

        IReadOnlyList<GoogleEvent> events = await service.GetEventsAsync(Day, Day, includeOwn: true);

        events.Select(e => e.Id).Should().Equal("e1", "same", "e2");
        _api.ListedCalendars.Should().Equal(Calendar, "agender@group.calendar.google.com");
        _api.IncludeOwnRequests.Should().OnlyContain(flag => flag);
    }

    [Fact]
    public async Task GetEventsAsync_IncludingOwnWithASingleCalendar_ReadsItOnce()
    {
        GoogleCalendarService service = await ConnectedServiceAsync();

        await service.GetEventsAsync(Day, Day, includeOwn: true);

        _api.ListedCalendars.Should().Equal(Calendar);
    }

    [Fact]
    public async Task GetEventsAsync_OrdersByDayAndStartWithAllDayFirst()
    {
        GoogleCalendarService service = await ConnectedServiceAsync();
        _api.Events =
        [
            new GoogleEvent("late", "Late", Day, 900, 960),
            new GoogleEvent("allday", "All day", Day, null, null),
            new GoogleEvent("early", "Early", Day, 540, 600),
            new GoogleEvent("next", "Next day", Day.AddDays(1), 480, 540)
        ];

        IReadOnlyList<GoogleEvent> events = await service.GetEventsAsync(Day, Day.AddDays(1));

        events.Select(e => e.Id).Should().Equal("allday", "early", "late", "next");
    }

    [Fact]
    public async Task SyncEntryAsync_FlaggedEntry_TagsTheEventWithTheEntryId()
    {
        GoogleCalendarService service = await ConnectedServiceAsync();
        TimeEntry entry = await NewEntryAsync(sync: true);

        await service.SyncEntryAsync(entry.Id, default);

        _api.Upserts.Should().ContainSingle().Which.Draft.EntryId.Should().Be(entry.Id);
    }

    [Fact]
    public async Task SyncEntryAsync_FlaggedEntry_CreatesTheEventInTheWriteCalendar()
    {
        GoogleCalendarService service = await ConnectedServiceAsync(writeCalendar: "agender@group.calendar.google.com");
        TimeEntry entry = await NewEntryAsync(sync: true);

        bool ok = await service.SyncEntryAsync(entry.Id, default);

        ok.Should().BeTrue();
        TimeEntry stored = await ReloadAsync(entry.Id);
        stored.GoogleEventId.Should().Be("event-1");
        stored.GoogleSyncFailed.Should().BeFalse();
        _api.Upserts.Should().ContainSingle().Which.Should().Match<(string? EventId, string CalendarId, GoogleEventDraft Draft)>(
            u => u.CalendarId == "agender@group.calendar.google.com" && u.Draft.Title == "Review" && u.Draft.StartMinute == 540 && u.Draft.EndMinute == 600);
    }

    [Fact]
    public async Task SyncEntryAsync_EntryAlreadyLinked_UpdatesTheSameEvent()
    {
        GoogleCalendarService service = await ConnectedServiceAsync();
        TimeEntry entry = await NewEntryAsync(sync: true);
        await service.SyncEntryAsync(entry.Id, default);

        await service.SyncEntryAsync(entry.Id, default);

        _api.Upserts.Select(u => u.EventId).Should().Equal(null, "event-1");
    }

    [Fact]
    public async Task SyncEntryAsync_FlagRemoved_DeletesTheEventAndClearsTheLink()
    {
        GoogleCalendarService service = await ConnectedServiceAsync();
        TimeEntry entry = await NewEntryAsync(sync: true);
        await service.SyncEntryAsync(entry.Id, default);

        await using (AgenderDbContext context = await _db.CreateAsync())
        {
            TimeEntry tracked = await context.TimeEntries.FindAsync(entry.Id) ?? throw new InvalidOperationException();
            tracked.SyncToGoogle = false;
            await context.SaveChangesAsync();
        }

        await service.SyncEntryAsync(entry.Id, default);

        _api.Deleted.Should().Equal("event-1");
        (await ReloadAsync(entry.Id)).GoogleEventId.Should().BeNull();
    }

    [Fact]
    public async Task SyncEntryAsync_GoogleFails_MarksTheEntryAsFailedWithoutThrowing()
    {
        GoogleCalendarService service = await ConnectedServiceAsync();
        TimeEntry entry = await NewEntryAsync(sync: true);
        _api.FailUpserts = true;

        bool ok = await service.SyncEntryAsync(entry.Id, default);

        ok.Should().BeFalse();
        (await ReloadAsync(entry.Id)).GoogleSyncFailed.Should().BeTrue();
    }

    [Fact]
    public async Task SyncEntryAsync_NotConnected_DoesNothing()
    {
        TimeEntry entry = await NewEntryAsync(sync: true);

        bool ok = await CreateService().SyncEntryAsync(entry.Id, default);

        ok.Should().BeTrue();
        _api.Upserts.Should().BeEmpty();
    }

    [Fact]
    public async Task RetryFailedAsync_AfterGoogleRecovers_SyncsTheFailedEntries()
    {
        GoogleCalendarService service = await ConnectedServiceAsync();
        TimeEntry entry = await NewEntryAsync(sync: true);
        _api.FailUpserts = true;
        await service.SyncEntryAsync(entry.Id, default);
        _api.FailUpserts = false;

        int failed = await service.RetryFailedAsync();

        failed.Should().Be(0);
        TimeEntry stored = await ReloadAsync(entry.Id);
        stored.GoogleEventId.Should().Be("event-1");
        stored.GoogleSyncFailed.Should().BeFalse();
    }

    [Fact]
    public async Task RetryFailedAsync_GoogleStillDown_ReportsHowManyFailed()
    {
        GoogleCalendarService service = await ConnectedServiceAsync();
        await NewEntryAsync(sync: true);
        await NewEntryAsync(sync: true, description: "Other");
        _api.FailUpserts = true;

        (await service.RetryFailedAsync()).Should().Be(2);
    }

    [Fact]
    public async Task ForgetEntryAsync_LinkedEntry_DeletesTheEvent()
    {
        GoogleCalendarService service = await ConnectedServiceAsync();
        TimeEntry entry = await NewEntryAsync(sync: true);
        await service.SyncEntryAsync(entry.Id, default);

        await service.ForgetEntryAsync(entry.Id, default);

        _api.Deleted.Should().Equal("event-1");
    }

    [Fact]
    public async Task ForgetEntryAsync_GoogleFails_DoesNotThrow()
    {
        GoogleCalendarService service = await ConnectedServiceAsync();
        TimeEntry entry = await NewEntryAsync(sync: true);
        await service.SyncEntryAsync(entry.Id, default);
        _api.FailDeletes = true;

        Func<Task> act = () => service.ForgetEntryAsync(entry.Id, default);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisconnectAsync_RemovesTheConnectionAndClearsSyncFlags()
    {
        GoogleCalendarService service = await ConnectedServiceAsync();
        TimeEntry entry = await NewEntryAsync(sync: true);
        await service.SyncEntryAsync(entry.Id, default);

        await service.DisconnectAsync();

        (await service.GetStatusAsync()).IsConnected.Should().BeFalse();
        TimeEntry stored = await ReloadAsync(entry.Id);
        stored.SyncToGoogle.Should().BeFalse();
        stored.GoogleEventId.Should().BeNull();
    }

    [Fact]
    public async Task Decorator_UpdateKeepsTheLinkToTheGoogleEvent()
    {
        GoogleCalendarService service = await ConnectedServiceAsync();
        GoogleSyncingTimeEntryService sut = new(new TimeEntryService(_db), service);

        TimeEntry created = await sut.CreateAsync(new TimeEntry { Date = Day, StartMinute = 540, EndMinute = 600, Description = "Review", SyncToGoogle = true });
        created.GoogleSyncFailed.Should().BeFalse();

        await sut.UpdateAsync(new TimeEntry { Id = created.Id, Date = Day, StartMinute = 540, EndMinute = 660, Description = "Review longer", SyncToGoogle = true });

        _api.Upserts.Select(u => u.EventId).Should().Equal(null, "event-1");
        (await ReloadAsync(created.Id)).GoogleEventId.Should().Be("event-1");
    }

    [Fact]
    public async Task Decorator_DeleteRemovesTheGoogleEvent()
    {
        GoogleCalendarService service = await ConnectedServiceAsync();
        GoogleSyncingTimeEntryService sut = new(new TimeEntryService(_db), service);
        TimeEntry created = await sut.CreateAsync(new TimeEntry { Date = Day, StartMinute = 540, EndMinute = 600, SyncToGoogle = true });

        await sut.DeleteAsync(created.Id);

        _api.Deleted.Should().Equal("event-1");
    }

    [Fact]
    public async Task Decorator_GoogleFails_StillSavesTheEntryAndFlagsIt()
    {
        GoogleCalendarService service = await ConnectedServiceAsync();
        GoogleSyncingTimeEntryService sut = new(new TimeEntryService(_db), service);
        _api.FailUpserts = true;

        TimeEntry created = await sut.CreateAsync(new TimeEntry { Date = Day, StartMinute = 540, EndMinute = 600, SyncToGoogle = true });

        created.GoogleSyncFailed.Should().BeTrue();
        (await ReloadAsync(created.Id)).GoogleSyncFailed.Should().BeTrue();
    }

    private sealed class RawFactory(TestDb db) : IDbContextFactory<AgenderDbContext>
    {
        public AgenderDbContext CreateDbContext() => db.Create(string.Empty);
    }

    private sealed class FixedUser(string userId) : ICurrentUser
    {
        public Task<string> GetUserIdAsync(CancellationToken cancellationToken = default) => Task.FromResult(userId);
    }

    private sealed class FakeGoogleApi : IGoogleCalendarApi
    {
        private int _counter;

        public IReadOnlyList<GoogleEvent> Events { get; set; } = [];

        public bool FailValidation { get; set; }

        public bool FailUpserts { get; set; }

        public bool FailDeletes { get; set; }

        public Dictionary<string, IReadOnlyList<GoogleEvent>> EventsByCalendar { get; } = [];

        public List<bool> IncludeOwnRequests { get; } = [];

        public List<string> ListedCalendars { get; } = [];

        public List<(string? EventId, string CalendarId, GoogleEventDraft Draft)> Upserts { get; } = [];

        public List<string> Deleted { get; } = [];

        public List<(string CalendarId, string EventId)> DeletedFrom { get; } = [];

        public Task<GoogleAccountCheck> ValidateAsync(string serviceAccountJson, string calendarId, string writeCalendarId, CancellationToken cancellationToken) =>
            FailValidation
                ? throw new ArgumentException("No access.")
                : Task.FromResult(new GoogleAccountCheck("bot@project.iam.gserviceaccount.com", "Europe/Lisbon"));

        public Task<IReadOnlyList<GoogleEvent>> ListEventsAsync(string serviceAccountJson, string calendarId, DateOnly from, DateOnly to, bool includeOwn, CancellationToken cancellationToken)
        {
            ListedCalendars.Add(calendarId);
            IncludeOwnRequests.Add(includeOwn);
            return Task.FromResult(EventsByCalendar.TryGetValue(calendarId, out IReadOnlyList<GoogleEvent>? events) ? events : Events);
        }

        public Task<string> UpsertEventAsync(string serviceAccountJson, GoogleCalendarInfo calendar, string? eventId, GoogleEventDraft draft, CancellationToken cancellationToken)
        {
            if (FailUpserts)
            {
                throw new HttpRequestException("Google is down.");
            }

            serviceAccountJson.Should().Be(Json);
            Upserts.Add((eventId, calendar.CalendarId, draft));
            return Task.FromResult(eventId ?? $"event-{++_counter}");
        }

        public Task DeleteEventAsync(string serviceAccountJson, string calendarId, string eventId, CancellationToken cancellationToken)
        {
            if (FailDeletes)
            {
                throw new HttpRequestException("Google is down.");
            }

            Deleted.Add(eventId);
            DeletedFrom.Add((calendarId, eventId));
            return Task.CompletedTask;
        }
    }
}

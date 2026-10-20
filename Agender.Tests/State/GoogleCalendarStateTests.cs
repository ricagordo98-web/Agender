using Agender.Shared.Abstractions;
using Agender.Shared.Google;
using Agender.Shared.Services;
using Agender.Shared.State;
using FluentAssertions;

namespace Agender.Tests.State;

public sealed class GoogleCalendarStateTests : IDisposable
{
    private static readonly DateOnly Monday = new(2026, 10, 12);
    private static readonly DateOnly Sunday = new(2026, 10, 18);

    private readonly FakeService _service = new();
    private readonly ToastService _toasts = new();
    private readonly GoogleCalendarState _sut;

    public GoogleCalendarStateTests() => _sut = new GoogleCalendarState(_service, _toasts, new NullAppActivation());

    public void Dispose()
    {
        _sut.Dispose();
        _toasts.Dispose();
    }

    private async Task<GoogleCalendarState> ConnectedAsync()
    {
        _service.Status = new GoogleStatus(true, "bot@x.com", "me@x.com", "me@x.com");
        await _sut.EnsureStatusAsync();
        return _sut;
    }

    [Fact]
    public async Task SetShowGoogle_NotConnected_StaysOff()
    {
        await _sut.EnsureStatusAsync();

        _sut.SetShowGoogle(true);

        _sut.ShowGoogle.Should().BeFalse();
    }

    [Fact]
    public async Task SetShowGoogle_Connected_TurnsTheGoogleViewOn()
    {
        await ConnectedAsync();

        _sut.SetShowGoogle(true);

        _sut.ShowGoogle.Should().BeTrue();
    }

    [Fact]
    public async Task LoadRangeAsync_GoogleViewOff_DoesNotCallTheService()
    {
        await ConnectedAsync();

        await _sut.LoadRangeAsync(Monday, Sunday);

        _service.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadRangeAsync_GroupsTheEventsByDayAndAsksForTheOwnOnesToo()
    {
        await ConnectedAsync();
        _sut.SetShowGoogle(true);
        _service.Events = [new GoogleEvent("a", "Standup", Monday, 570, 585), new GoogleEvent("b", "Review", Monday, 600, 660), new GoogleEvent("c", "Demo", Monday.AddDays(2), 840, 900)];

        await _sut.LoadRangeAsync(Monday, Sunday);

        _sut.EventsOn(Monday).Select(e => e.Id).Should().Equal("a", "b");
        _sut.EventsOn(Monday.AddDays(2)).Should().ContainSingle();
        _sut.EventsOn(Monday.AddDays(1)).Should().BeEmpty();
        _service.Requests.Should().Equal((Monday, Sunday, true));
    }

    [Fact]
    public async Task LoadRangeAsync_SameRangeTwice_LoadsOnlyOnceUnlessForced()
    {
        await ConnectedAsync();
        _sut.SetShowGoogle(true);

        await _sut.LoadRangeAsync(Monday, Sunday);
        await _sut.LoadRangeAsync(Monday, Sunday);
        _service.Requests.Should().HaveCount(1);

        await _sut.LoadRangeAsync(Monday, Sunday, force: true);
        _service.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadRangeAsync_NewRange_ReplacesThePreviousEvents()
    {
        await ConnectedAsync();
        _sut.SetShowGoogle(true);
        _service.Events = [new GoogleEvent("a", "Standup", Monday, 570, 585)];
        await _sut.LoadRangeAsync(Monday, Sunday);

        _service.Events = [new GoogleEvent("n", "Next week", Monday.AddDays(7), 570, 585)];
        await _sut.LoadRangeAsync(Monday.AddDays(7), Sunday.AddDays(7));

        _sut.EventsOn(Monday).Should().BeEmpty();
        _sut.EventsOn(Monday.AddDays(7)).Should().ContainSingle();
    }

    [Fact]
    public async Task LoadRangeAsync_GoogleFails_FlagsTheErrorAndKeepsTheStateUsable()
    {
        await ConnectedAsync();
        _sut.SetShowGoogle(true);
        _service.FailEvents = true;

        await _sut.LoadRangeAsync(Monday, Sunday);

        _sut.HasEventsError.Should().BeTrue();
        _sut.EventsOn(Monday).Should().BeEmpty();
        _sut.IsLoadingEvents.Should().BeFalse();
    }

    [Fact]
    public async Task DisconnectAsync_TurnsTheGoogleViewOffAndClearsTheEvents()
    {
        await ConnectedAsync();
        _sut.SetShowGoogle(true);
        _service.Events = [new GoogleEvent("a", "Standup", Monday, 570, 585)];
        await _sut.LoadRangeAsync(Monday, Sunday);
        _service.Status = new GoogleStatus(false);

        await _sut.DisconnectAsync();

        _sut.ShowGoogle.Should().BeFalse();
        _sut.Status.IsConnected.Should().BeFalse();
        _sut.EventsOn(Monday).Should().BeEmpty();
    }

    [Fact]
    public async Task ConnectAsync_InvalidKey_ShowsAnErrorAndStaysDisconnected()
    {
        _service.FailConnect = true;

        bool connected = await _sut.ConnectAsync("{}", "me@x.com", null);

        connected.Should().BeFalse();
        _toasts.Messages.Should().ContainSingle().Which.Kind.Should().Be(ToastKind.Error);
    }

    [Fact]
    public async Task DeleteEventAsync_RemovesTheEventFromTheViewAndAnnouncesIt()
    {
        await ConnectedAsync();
        _sut.SetShowGoogle(true);
        GoogleEvent standup = new("a", "Standup", Monday, 570, 585, false, null, "me@x.com");
        _service.Events = [standup, new GoogleEvent("b", "Review", Monday, 600, 660, false, null, "me@x.com")];
        await _sut.LoadRangeAsync(Monday, Sunday);

        bool deleted = await _sut.DeleteEventAsync(standup);

        deleted.Should().BeTrue();
        _service.Deleted.Should().Equal(("me@x.com", "a"));
        _sut.EventsOn(Monday).Select(e => e.Id).Should().Equal("b");
        _toasts.Messages.Should().ContainSingle().Which.Kind.Should().Be(ToastKind.Success);
    }

    [Fact]
    public async Task DeleteEventAsync_GoogleRefuses_KeepsTheEventAndExplainsWhy()
    {
        await ConnectedAsync();
        _sut.SetShowGoogle(true);
        GoogleEvent standup = new("a", "Standup", Monday, 570, 585, false, null, "me@x.com");
        _service.Events = [standup];
        await _sut.LoadRangeAsync(Monday, Sunday);
        _service.FailDelete = true;

        bool deleted = await _sut.DeleteEventAsync(standup);

        deleted.Should().BeFalse();
        _sut.EventsOn(Monday).Should().ContainSingle();
        _toasts.Messages.Should().ContainSingle().Which.Text.Should().Contain("permissão");
    }

    [Fact]
    public async Task DeleteEventAsync_EventWithoutACalendar_DoesNothing()
    {
        await ConnectedAsync();

        bool deleted = await _sut.DeleteEventAsync(new GoogleEvent("a", "Standup", Monday, 570, 585));

        deleted.Should().BeFalse();
        _service.Deleted.Should().BeEmpty();
    }

    private sealed class FakeService : IGoogleCalendarService
    {
        public GoogleStatus Status { get; set; } = new(false);

        public IReadOnlyList<GoogleEvent> Events { get; set; } = [];

        public bool FailEvents { get; set; }

        public bool FailConnect { get; set; }

        public List<(DateOnly From, DateOnly To, bool IncludeOwn)> Requests { get; } = [];

        public Task<GoogleStatus> GetStatusAsync(CancellationToken cancellationToken = default) => Task.FromResult(Status);

        public Task ConnectAsync(string serviceAccountJson, string calendarId, string? writeCalendarId, CancellationToken cancellationToken = default) =>
            FailConnect ? throw new ArgumentException("No access.") : Task.CompletedTask;

        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<GoogleEvent>> GetEventsAsync(DateOnly from, DateOnly to, bool includeOwn = false, CancellationToken cancellationToken = default)
        {
            Requests.Add((from, to, includeOwn));
            return FailEvents ? throw new ServerUnavailableException("down") : Task.FromResult(Events);
        }

        public List<(string CalendarId, string EventId)> Deleted { get; } = [];

        public bool FailDelete { get; set; }

        public Task DeleteEventAsync(string calendarId, string eventId, CancellationToken cancellationToken = default)
        {
            if (FailDelete)
            {
                throw new PersistenceException("refused");
            }

            Deleted.Add((calendarId, eventId));
            return Task.CompletedTask;
        }

        public Task<int> RetryFailedAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }
}

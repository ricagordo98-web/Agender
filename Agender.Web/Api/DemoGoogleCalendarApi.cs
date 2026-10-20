using System.Collections.Concurrent;
using Agender.Infrastructure.Google;
using Agender.Shared.Google;

namespace Agender.Web.Api;

public sealed class DemoGoogleCalendarApi : IGoogleCalendarApi
{
    private const int StandupStart = 9 * 60 + 30;
    private const int MeetingStart = 14 * 60;

    private readonly ConcurrentDictionary<string, (string CalendarId, GoogleEventDraft Draft)> _written = new();
    private readonly ConcurrentDictionary<string, bool> _deleted = new();

    public Task<GoogleAccountCheck> ValidateAsync(string serviceAccountJson, string calendarId, string writeCalendarId, CancellationToken cancellationToken) =>
        Task.FromResult(new GoogleAccountCheck("demo@agender.local", "Europe/Lisbon"));

    public Task<IReadOnlyList<GoogleEvent>> ListEventsAsync(string serviceAccountJson, string calendarId, DateOnly from, DateOnly to, bool includeOwn, CancellationToken cancellationToken)
    {
        List<GoogleEvent> events = [];

        for (DateOnly day = from; day <= to; day = day.AddDays(1))
        {
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            events.Add(new GoogleEvent($"demo-standup-{day:yyyyMMdd}", "Standup", day, StandupStart, StandupStart + 15));

            if (day.DayOfWeek == DayOfWeek.Wednesday)
            {
                events.Add(new GoogleEvent($"demo-meeting-{day:yyyyMMdd}", "Reunião de cliente", day, MeetingStart, MeetingStart + 90));
            }

            if (day.DayOfWeek == DayOfWeek.Friday)
            {
                events.Add(new GoogleEvent($"demo-team-{day:yyyyMMdd}", "Dia de equipa", day, null, null));
            }
        }

        if (includeOwn)
        {
            foreach ((string id, (string writtenCalendar, GoogleEventDraft draft)) in _written)
            {
                if (writtenCalendar == calendarId && draft.Date >= from && draft.Date <= to)
                {
                    events.Add(new GoogleEvent(id, draft.Title, draft.Date, draft.StartMinute, draft.EndMinute, true, draft.EntryId));
                }
            }
        }

        return Task.FromResult<IReadOnlyList<GoogleEvent>>([.. events.Where(e => !_deleted.ContainsKey(e.Id))]);
    }

    public Task<string> UpsertEventAsync(string serviceAccountJson, GoogleCalendarInfo calendar, string? eventId, GoogleEventDraft draft, CancellationToken cancellationToken)
    {
        string id = eventId ?? $"demo-{Guid.NewGuid():N}";
        _written[id] = (calendar.CalendarId, draft);

        return Task.FromResult(id);
    }

    public Task DeleteEventAsync(string serviceAccountJson, string calendarId, string eventId, CancellationToken cancellationToken)
    {
        _written.TryRemove(eventId, out _);
        _deleted[eventId] = true;
        return Task.CompletedTask;
    }
}

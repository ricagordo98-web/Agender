using Agender.Shared.Google;

namespace Agender.Infrastructure.Google;

public record GoogleCalendarInfo(string CalendarId, string TimeZone);

public record GoogleAccountCheck(string ServiceAccountEmail, string WriteTimeZone);

public record GoogleEventDraft(Guid EntryId, string Title, string Description, DateOnly Date, int StartMinute, int EndMinute);

public interface IGoogleCalendarApi
{
    Task<GoogleAccountCheck> ValidateAsync(string serviceAccountJson, string calendarId, string writeCalendarId, CancellationToken cancellationToken);

    Task<IReadOnlyList<GoogleEvent>> ListEventsAsync(string serviceAccountJson, string calendarId, DateOnly from, DateOnly to, bool includeOwn, CancellationToken cancellationToken);

    Task<string> UpsertEventAsync(string serviceAccountJson, GoogleCalendarInfo calendar, string? eventId, GoogleEventDraft draft, CancellationToken cancellationToken);

    Task DeleteEventAsync(string serviceAccountJson, string calendarId, string eventId, CancellationToken cancellationToken);
}

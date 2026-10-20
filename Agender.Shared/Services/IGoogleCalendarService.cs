using Agender.Shared.Google;

namespace Agender.Shared.Services;

public interface IGoogleCalendarService
{
    Task<GoogleStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task ConnectAsync(string serviceAccountJson, string calendarId, string? writeCalendarId, CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GoogleEvent>> GetEventsAsync(DateOnly from, DateOnly to, bool includeOwn = false, CancellationToken cancellationToken = default);

    Task DeleteEventAsync(string calendarId, string eventId, CancellationToken cancellationToken = default);

    Task<int> RetryFailedAsync(CancellationToken cancellationToken = default);
}

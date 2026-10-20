using Agender.Auth;
using Agender.Shared.Contracts;
using Agender.Shared.Google;
using Agender.Shared.Services;

namespace Agender.Api;

public sealed class ApiGoogleCalendarService(IHttpClientFactory httpClientFactory, AuthSession session)
    : ApiClientBase(httpClientFactory, session), IGoogleCalendarService
{
    public async Task<GoogleStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        GoogleStatusDto status = await GetAsync<GoogleStatusDto>("/api/google/status", cancellationToken);
        return new GoogleStatus(status.IsConnected, status.ServiceAccountEmail, status.CalendarId, status.WriteCalendarId);
    }

    public Task ConnectAsync(string serviceAccountJson, string calendarId, string? writeCalendarId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, "/api/google", new GoogleConnectRequest(serviceAccountJson, calendarId, writeCalendarId), cancellationToken);

    public Task DisconnectAsync(CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, "/api/google", null, cancellationToken);

    public async Task<IReadOnlyList<GoogleEvent>> GetEventsAsync(DateOnly from, DateOnly to, bool includeOwn = false, CancellationToken cancellationToken = default)
    {
        GoogleEventDto[] events = await GetAsync<GoogleEventDto[]>($"/api/google/events?from={Date(from)}&to={Date(to)}&includeOwn={includeOwn.ToString().ToLowerInvariant()}", cancellationToken);
        return [.. events.Select(e => new GoogleEvent(e.Id, e.Title, e.Date, e.StartMinute, e.EndMinute, e.IsAgender, e.EntryId, e.CalendarId))];
    }

    public Task DeleteEventAsync(string calendarId, string eventId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"/api/google/events?calendarId={Uri.EscapeDataString(calendarId)}&eventId={Uri.EscapeDataString(eventId)}", null, cancellationToken);

    public async Task<int> RetryFailedAsync(CancellationToken cancellationToken = default)
    {
        GoogleRetryDto result = await SendAsync<GoogleRetryDto>(HttpMethod.Post, "/api/google/retry", null, cancellationToken);
        return result.Failed;
    }
}

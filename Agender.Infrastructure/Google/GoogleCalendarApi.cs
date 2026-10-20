using System.Globalization;
using System.Net;
using Agender.Shared.Google;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;

namespace Agender.Infrastructure.Google;

public sealed class GoogleCalendarApi : IGoogleCalendarApi
{
    public const string AgenderMarkerKey = "agender";

    private const string ApplicationName = "Agender";
    private const string LocalDateTimeFormat = "yyyy-MM-dd'T'HH:mm:ss";
    private const int MaxEvents = 250;

    private static readonly string[] Scopes = [CalendarService.Scope.CalendarEvents];

    public async Task<GoogleAccountCheck> ValidateAsync(string serviceAccountJson, string calendarId, string writeCalendarId, CancellationToken cancellationToken)
    {
        ServiceAccountKey key = ServiceAccountKey.Parse(serviceAccountJson);
        using CalendarService service = CreateService(key);

        await ProbeAsync(service, key, calendarId, cancellationToken);
        string writeTimeZone = await ProbeAsync(service, key, writeCalendarId, cancellationToken);

        return new GoogleAccountCheck(key.ClientEmail, writeTimeZone);
    }

    public async Task<IReadOnlyList<GoogleEvent>> ListEventsAsync(string serviceAccountJson, string calendarId, DateOnly from, DateOnly to, bool includeOwn, CancellationToken cancellationToken)
    {
        using CalendarService service = CreateService(ServiceAccountKey.Parse(serviceAccountJson));

        EventsResource.ListRequest request = service.Events.List(calendarId);
        request.TimeMinDateTimeOffset = new DateTimeOffset(from.AddDays(-1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        request.TimeMaxDateTimeOffset = new DateTimeOffset(to.AddDays(2).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        request.SingleEvents = true;
        request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
        request.MaxResults = MaxEvents;

        Events response = await request.ExecuteAsync(cancellationToken);
        List<GoogleEvent> events = [];

        foreach (Event item in response.Items ?? [])
        {
            GoogleEvent? mapped = Map(item);

            if (mapped is null || mapped.Date < from || mapped.Date > to || (mapped.IsAgender && !includeOwn))
            {
                continue;
            }

            events.Add(mapped);
        }

        return events;
    }

    public async Task<string> UpsertEventAsync(string serviceAccountJson, GoogleCalendarInfo calendar, string? eventId, GoogleEventDraft draft, CancellationToken cancellationToken)
    {
        using CalendarService service = CreateService(ServiceAccountKey.Parse(serviceAccountJson));
        Event body = ToEvent(draft, calendar.TimeZone);

        if (eventId is not null)
        {
            try
            {
                Event updated = await service.Events.Update(body, calendar.CalendarId, eventId).ExecuteAsync(cancellationToken);
                return updated.Id;
            }
            catch (GoogleApiException ex) when (IsGone(ex))
            {
            }
        }

        Event inserted = await service.Events.Insert(body, calendar.CalendarId).ExecuteAsync(cancellationToken);
        return inserted.Id;
    }

    public async Task DeleteEventAsync(string serviceAccountJson, string calendarId, string eventId, CancellationToken cancellationToken)
    {
        using CalendarService service = CreateService(ServiceAccountKey.Parse(serviceAccountJson));

        try
        {
            await service.Events.Delete(calendarId, eventId).ExecuteAsync(cancellationToken);
        }
        catch (GoogleApiException ex) when (IsGone(ex))
        {
        }
    }

    private static async Task<string> ProbeAsync(CalendarService service, ServiceAccountKey key, string calendarId, CancellationToken cancellationToken)
    {
        EventsResource.ListRequest request = service.Events.List(calendarId);
        request.MaxResults = 1;

        try
        {
            Events response = await request.ExecuteAsync(cancellationToken);
            return string.IsNullOrWhiteSpace(response.TimeZone) ? "UTC" : response.TimeZone;
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
        {
            throw new ArgumentException($"The service account {key.ClientEmail} cannot access the calendar '{calendarId}'. Share the calendar with it.", ex);
        }
    }

    private static bool IsGone(GoogleApiException ex) => ex.HttpStatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone;

    private static GoogleEvent? Map(Event item)
    {
        string title = string.IsNullOrWhiteSpace(item.Summary) ? "(sem título)" : item.Summary;
        string? marker = null;
        item.ExtendedProperties?.Private__?.TryGetValue(AgenderMarkerKey, out marker);
        bool isAgender = marker is not null;
        Guid? entryId = Guid.TryParse(marker, out Guid parsed) ? parsed : null;

        if (item.Start?.DateTimeDateTimeOffset is DateTimeOffset start)
        {
            DateTime wallStart = start.DateTime;
            int startMinute = wallStart.Hour * 60 + wallStart.Minute;
            int endMinute = startMinute + 1;

            if (item.End?.DateTimeDateTimeOffset is DateTimeOffset end)
            {
                DateTime wallEnd = end.DateTime;
                endMinute = DateOnly.FromDateTime(wallEnd) == DateOnly.FromDateTime(wallStart)
                    ? wallEnd.Hour * 60 + wallEnd.Minute
                    : 24 * 60;
            }

            return new GoogleEvent(item.Id, title, DateOnly.FromDateTime(wallStart), startMinute, Math.Max(endMinute, startMinute + 1), isAgender, entryId);
        }

        if (DateOnly.TryParseExact(item.Start?.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly day))
        {
            return new GoogleEvent(item.Id, title, day, null, null, isAgender, entryId);
        }

        return null;
    }

    private static Event ToEvent(GoogleEventDraft draft, string timeZone)
    {
        DateTime start = draft.Date.ToDateTime(TimeOnly.MinValue).AddMinutes(draft.StartMinute);
        DateTime end = draft.Date.ToDateTime(TimeOnly.MinValue).AddMinutes(draft.EndMinute);

        return new Event
        {
            Summary = draft.Title,
            Description = draft.Description,
            Start = new EventDateTime { DateTimeRaw = start.ToString(LocalDateTimeFormat, CultureInfo.InvariantCulture), TimeZone = timeZone },
            End = new EventDateTime { DateTimeRaw = end.ToString(LocalDateTimeFormat, CultureInfo.InvariantCulture), TimeZone = timeZone },
            ExtendedProperties = new Event.ExtendedPropertiesData
            {
                Private__ = new Dictionary<string, string> { [AgenderMarkerKey] = draft.EntryId.ToString() }
            }
        };
    }

    private static CalendarService CreateService(ServiceAccountKey key)
    {
        ServiceAccountCredential credential = new(new ServiceAccountCredential.Initializer(key.ClientEmail)
        {
            Scopes = Scopes
        }.FromPrivateKey(key.PrivateKey));

        return new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName
        });
    }
}

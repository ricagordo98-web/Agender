namespace Agender.Shared.Google;

public record GoogleStatus(bool IsConnected, string? ServiceAccountEmail = null, string? CalendarId = null, string? WriteCalendarId = null);

public record GoogleEvent(string Id, string Title, DateOnly Date, int? StartMinute, int? EndMinute, bool IsAgender = false, Guid? EntryId = null, string? CalendarId = null)
{
    public bool IsAllDay => StartMinute is null;
}

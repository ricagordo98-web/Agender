using Agender.Infrastructure.Data;
using Agender.Infrastructure.Identity;
using Agender.Shared.Domain;
using Agender.Shared.Google;
using Agender.Shared.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Agender.Infrastructure.Google;

public class GoogleCalendarService(
    IDbContextFactory<AgenderDbContext> contextFactory,
    ICurrentUser currentUser,
    IGoogleCalendarApi api,
    IDataProtectionProvider dataProtection,
    TimeProvider time,
    ILogger<GoogleCalendarService> logger) : IGoogleCalendarService, IGoogleEntrySync
{
    private const string NoDescriptionTitle = "(sem descrição)";
    private const string NotConnectedMessage = "Google Calendar is not connected.";

    private readonly IDataProtector _credentialsProtector = dataProtection.CreateProtector("Agender.Google.ServiceAccount");

    public async Task<GoogleStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await OpenAsync(cancellationToken);
        GoogleConnection? connection = await context.GoogleConnections.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        return connection is null
            ? new GoogleStatus(false)
            : new GoogleStatus(true, connection.ServiceAccountEmail, connection.CalendarId, connection.WriteCalendarId);
    }

    public async Task ConnectAsync(string serviceAccountJson, string calendarId, string? writeCalendarId, CancellationToken cancellationToken = default)
    {
        string readCalendar = calendarId.Trim();
        string writeCalendar = string.IsNullOrWhiteSpace(writeCalendarId) ? readCalendar : writeCalendarId.Trim();

        if (readCalendar.Length == 0 || readCalendar.Length > GoogleConnection.CalendarIdMaxLength || writeCalendar.Length > GoogleConnection.CalendarIdMaxLength)
        {
            throw new ArgumentException("The calendar id is missing or too long.");
        }

        GoogleAccountCheck check = await api.ValidateAsync(serviceAccountJson, readCalendar, writeCalendar, cancellationToken);

        await using AgenderDbContext context = await OpenAsync(cancellationToken);
        GoogleConnection? existing = await context.GoogleConnections.FirstOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            existing = new GoogleConnection();
            context.GoogleConnections.Add(existing);
        }
        else if (existing.WriteCalendarId != writeCalendar)
        {
            await context.TimeEntries
                .Where(e => e.GoogleEventId != null)
                .ExecuteUpdateAsync(set => set.SetProperty(e => e.GoogleEventId, (string?)null), cancellationToken);
        }

        existing.ProtectedCredentials = _credentialsProtector.Protect(serviceAccountJson);
        existing.ServiceAccountEmail = check.ServiceAccountEmail;
        existing.CalendarId = readCalendar;
        existing.WriteCalendarId = writeCalendar;
        existing.TimeZone = check.WriteTimeZone;
        existing.ConnectedAt = time.GetUtcNow().UtcDateTime;

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await OpenAsync(cancellationToken);
        GoogleConnection? connection = await context.GoogleConnections.FirstOrDefaultAsync(cancellationToken);

        if (connection is null)
        {
            return;
        }

        await context.TimeEntries
            .Where(e => e.SyncToGoogle || e.GoogleEventId != null || e.GoogleSyncFailed)
            .ExecuteUpdateAsync(set => set
                .SetProperty(e => e.SyncToGoogle, false)
                .SetProperty(e => e.GoogleSyncFailed, false)
                .SetProperty(e => e.GoogleEventId, (string?)null), cancellationToken);

        context.GoogleConnections.Remove(connection);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GoogleEvent>> GetEventsAsync(DateOnly from, DateOnly to, bool includeOwn = false, CancellationToken cancellationToken = default)
    {
        if (to < from)
        {
            throw new ArgumentException("The range must be ascending.");
        }

        await using AgenderDbContext context = await OpenAsync(cancellationToken);
        GoogleConnection connection = await context.GoogleConnections.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new ArgumentException(NotConnectedMessage);

        string credentials = _credentialsProtector.Unprotect(connection.ProtectedCredentials);
        List<GoogleEvent> events =
        [
            .. (await api.ListEventsAsync(credentials, connection.CalendarId, from, to, includeOwn, cancellationToken))
                .Select(e => e with { CalendarId = connection.CalendarId })
        ];

        if (includeOwn && connection.WriteCalendarId != connection.CalendarId)
        {
            IReadOnlyList<GoogleEvent> written = await api.ListEventsAsync(credentials, connection.WriteCalendarId, from, to, includeOwn, cancellationToken);
            events.AddRange(written
                .Where(w => events.All(e => e.Id != w.Id))
                .Select(w => w with { CalendarId = connection.WriteCalendarId }));
        }

        return [.. events.OrderBy(e => e.Date).ThenBy(e => e.StartMinute ?? -1)];
    }

    public async Task DeleteEventAsync(string calendarId, string eventId, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await OpenAsync(cancellationToken);
        GoogleConnection connection = await context.GoogleConnections.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new ArgumentException(NotConnectedMessage);

        if (string.IsNullOrWhiteSpace(eventId) || (calendarId != connection.CalendarId && calendarId != connection.WriteCalendarId))
        {
            throw new ArgumentException("The event does not belong to one of the connected calendars.");
        }

        try
        {
            await api.DeleteEventAsync(_credentialsProtector.Unprotect(connection.ProtectedCredentials), calendarId, eventId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not delete the Google event {EventId}.", eventId);
            throw new PersistenceException("Could not delete the Google event.", ex);
        }

        await context.TimeEntries
            .Where(e => e.GoogleEventId == eventId)
            .ExecuteUpdateAsync(set => set
                .SetProperty(e => e.SyncToGoogle, false)
                .SetProperty(e => e.GoogleSyncFailed, false)
                .SetProperty(e => e.GoogleEventId, (string?)null), cancellationToken);
    }

    public async Task<int> RetryFailedAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await OpenAsync(cancellationToken);
        GoogleConnection connection = await context.GoogleConnections.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new ArgumentException(NotConnectedMessage);

        List<TimeEntry> pending = await context.TimeEntries
            .Where(e => e.GoogleSyncFailed || (e.SyncToGoogle && e.GoogleEventId == null) || (!e.SyncToGoogle && e.GoogleEventId != null))
            .ToListAsync(cancellationToken);

        int failed = 0;

        foreach (TimeEntry entry in pending)
        {
            if (!await SyncAsync(context, connection, entry, cancellationToken))
            {
                failed++;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        return failed;
    }

    public async Task<bool> SyncEntryAsync(Guid entryId, CancellationToken cancellationToken)
    {
        await using AgenderDbContext context = await OpenAsync(cancellationToken);
        GoogleConnection? connection = await context.GoogleConnections.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        TimeEntry? entry = connection is null ? null : await context.TimeEntries.FirstOrDefaultAsync(e => e.Id == entryId, cancellationToken);

        if (connection is null || entry is null)
        {
            return true;
        }

        bool succeeded = await SyncAsync(context, connection, entry, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return succeeded;
    }

    public async Task ForgetEntryAsync(Guid entryId, CancellationToken cancellationToken)
    {
        await using AgenderDbContext context = await OpenAsync(cancellationToken);
        GoogleConnection? connection = await context.GoogleConnections.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        string? eventId = connection is null
            ? null
            : await context.TimeEntries.Where(e => e.Id == entryId).Select(e => e.GoogleEventId).FirstOrDefaultAsync(cancellationToken);

        if (connection is null || eventId is null)
        {
            return;
        }

        try
        {
            await api.DeleteEventAsync(_credentialsProtector.Unprotect(connection.ProtectedCredentials), connection.WriteCalendarId, eventId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not delete the Google event of entry {EntryId}.", entryId);
        }
    }

    private async Task<bool> SyncAsync(AgenderDbContext context, GoogleConnection connection, TimeEntry entry, CancellationToken cancellationToken)
    {
        if (!entry.SyncToGoogle && entry.GoogleEventId is null && !entry.GoogleSyncFailed)
        {
            return true;
        }

        try
        {
            string credentials = _credentialsProtector.Unprotect(connection.ProtectedCredentials);

            if (entry.SyncToGoogle)
            {
                string? projectName = entry.ProjectId is null
                    ? null
                    : await context.Projects.Where(p => p.Id == entry.ProjectId).Select(p => p.Name).FirstOrDefaultAsync(cancellationToken);

                GoogleEventDraft draft = new(
                    entry.Id,
                    string.IsNullOrWhiteSpace(entry.Description) ? NoDescriptionTitle : entry.Description,
                    projectName is null ? "Registado no Agender" : $"Registado no Agender · {projectName}",
                    entry.Date,
                    entry.StartMinute,
                    entry.EndMinute);

                entry.GoogleEventId = await api.UpsertEventAsync(credentials, new GoogleCalendarInfo(connection.WriteCalendarId, connection.TimeZone), entry.GoogleEventId, draft, cancellationToken);
            }
            else if (entry.GoogleEventId is not null)
            {
                await api.DeleteEventAsync(credentials, connection.WriteCalendarId, entry.GoogleEventId, cancellationToken);
                entry.GoogleEventId = null;
            }

            entry.GoogleSyncFailed = false;
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not sync entry {EntryId} with Google Calendar.", entry.Id);
            entry.GoogleSyncFailed = true;
            return false;
        }
    }

    private async Task<AgenderDbContext> OpenAsync(CancellationToken cancellationToken)
    {
        AgenderDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.UserId = await currentUser.GetUserIdAsync(cancellationToken);

        return context;
    }
}

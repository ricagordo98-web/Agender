using Agender.Infrastructure.Data;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

public class UserSettingsService(IUserDbContextFactory contextFactory) : IUserSettingsService
{
    public async Task<UserSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);

        return await context.UserSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken) ?? new UserSettings();
    }

    public async Task SaveAsync(UserSettings settings, CancellationToken cancellationToken = default)
    {
        if (!UserSettings.SnapOptions.Contains(settings.SnapMinutes)
            || !UserSettings.EntryDurationOptions.Contains(settings.DefaultEntryMinutes)
            || !UserSettings.StartScrollOptions.Contains(settings.StartScrollMinute)
            || !UserSettings.TimerRoundingOptions.Contains(settings.TimerRoundingMinutes))
        {
            throw new ArgumentException("A setting is not one of the allowed options.");
        }

        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        UserSettings? existing = await context.UserSettings.FirstOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            settings.Id = Guid.NewGuid();
            context.UserSettings.Add(settings);
        }
        else
        {
            existing.DefaultWeekView = settings.DefaultWeekView;
            existing.SnapMinutes = settings.SnapMinutes;
            existing.DefaultEntryMinutes = settings.DefaultEntryMinutes;
            existing.WeekStartsOnSunday = settings.WeekStartsOnSunday;
            existing.HideWeekends = settings.HideWeekends;
            existing.HideDayNotes = settings.HideDayNotes;
            existing.HideEntryDetails = settings.HideEntryDetails;
            existing.StartScrollMinute = settings.StartScrollMinute;
            existing.TimerRoundingMinutes = settings.TimerRoundingMinutes;
            existing.ConfirmBeforeDelete = settings.ConfirmBeforeDelete;
            existing.RemoveFromAgendaWhenCompleted = settings.RemoveFromAgendaWhenCompleted;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}

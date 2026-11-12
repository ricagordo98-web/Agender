using Agender.Infrastructure.Data;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

public class DailyReminderDispatcher(
    IDbContextFactory<AgenderDbContext> contextFactory,
    WhatsAppSender sender,
    IDataProtectionProvider dataProtection,
    TimeProvider time)
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(30);

    public async Task DispatchDueAsync(CancellationToken cancellationToken)
    {
        await using AgenderDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        DateTime now = time.GetLocalNow().DateTime;
        DateOnly today = DateOnly.FromDateTime(now);
        TimeOnly clock = TimeOnly.FromDateTime(now);

        List<DailyReminder> due = [.. (await context.DailyReminders
                .Where(r => r.IsEnabled && (r.LastSentOn == null || r.LastSentOn < today))
                .ToListAsync(cancellationToken))
            .Where(r => clock >= r.SendTime && !string.IsNullOrWhiteSpace(r.Destination))
            .Where(r => r.LastAttemptAt is not DateTime last || DateTime.UtcNow - last >= RetryInterval)];

        foreach (IGrouping<Guid, DailyReminder> household in due.GroupBy(r => r.HouseholdId))
        {
            MealSettings? settings = await context.MealPreferences.AsNoTracking().FirstOrDefaultAsync(s => s.HouseholdId == household.Key, cancellationToken);

            foreach (DailyReminder reminder in household)
            {
                reminder.LastAttemptAt = DateTime.UtcNow;

                if (!MealMessages.IsConnected(settings))
                {
                    reminder.LastError = "A ligação ao WhatsApp não está configurada.";
                    continue;
                }

                try
                {
                    DailyDigestSection digest = await DailyDigests.BuildAsync(context, household.Key, reminder.Section, today, cancellationToken);

                    if (digest.Lines.Count > 0)
                    {
                        await MealMessages.SendToAsync(sender, dataProtection, settings!, reminder.Destination!, DailyDigests.Format(digest, today), cancellationToken);
                    }

                    reminder.LastSentOn = today;
                    reminder.LastError = null;
                }
                catch (ArgumentException ex)
                {
                    reminder.LastError = MealService.Truncate(ex.Message, DailyReminder.ErrorMaxLength);
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}

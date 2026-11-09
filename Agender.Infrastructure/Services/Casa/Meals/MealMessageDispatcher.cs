using Agender.Infrastructure.Data;
using Agender.Shared.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

public class MealMessageDispatcher(
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

        List<MealSettings> enabled = await context.MealPreferences.AsNoTracking().Where(s => s.WhatsAppEnabled).ToListAsync(cancellationToken);

        foreach (MealSettings settings in enabled.Where(MealMessages.IsReady))
        {
            DateTime moment = MealCatalog.LastScheduled(now, settings.SendDay, settings.SendTime);
            DateOnly target = MealCatalog.TargetWeek(DateOnly.FromDateTime(moment));

            if (today > target)
            {
                continue;
            }

            MealWeek? week = await context.MealWeeks.FirstOrDefaultAsync(w => w.HouseholdId == settings.HouseholdId && w.WeekStart == target, cancellationToken);

            if (week?.SentAt is not null || week?.LastAttemptAt is DateTime last && DateTime.UtcNow - last < RetryInterval)
            {
                continue;
            }

            DateOnly end = target.AddDays(6);

            if (!await context.MealPlanEntries.AnyAsync(e => e.HouseholdId == settings.HouseholdId && e.Date >= target && e.Date <= end, cancellationToken))
            {
                continue;
            }

            if (week is null)
            {
                week = new MealWeek { HouseholdId = settings.HouseholdId, WeekStart = target };
                context.MealWeeks.Add(week);
            }

            week.LastAttemptAt = DateTime.UtcNow;

            try
            {
                string? partial = await MealMessages.SendWeekAsync(sender, dataProtection, settings, context, settings.HouseholdId, target, cancellationToken);
                week.SentAt = DateTime.UtcNow;
                week.LastError = partial is null ? null : MealService.Truncate(partial, MealWeek.ErrorMaxLength);
            }
            catch (ArgumentException ex)
            {
                week.LastError = MealService.Truncate(ex.Message, MealWeek.ErrorMaxLength);
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}

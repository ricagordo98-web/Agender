using Agender.Infrastructure.Data;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

public class DailyReminderService(IUserDbContextFactory contextFactory, WhatsAppSender whatsApp, IDataProtectionProvider dataProtection, TimeProvider time) : IDailyReminderService
{
    private const string ConnectionMissing = "Configura primeiro a ligação ao WhatsApp em Definições → Refeições (endereço, instância e chave).";

    public async Task<DailyRemindersDto> GetAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        return await LoadAsync(context, householdId, cancellationToken);
    }

    public async Task<DailyRemindersDto> SaveAsync(List<DailyReminderDto> reminders, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        if (reminders.Any(r => !Enum.IsDefined(r.Section)) || reminders.Select(r => r.Section).Distinct().Count() != reminders.Count)
        {
            throw new ArgumentException("The reminders are not valid.");
        }

        List<DailyReminder> stored = await context.DailyReminders.Where(r => r.HouseholdId == householdId).ToListAsync(cancellationToken);
        DateTime now = time.GetLocalNow().DateTime;

        foreach (DailyReminderDto reminder in reminders)
        {
            string? destination = MealSettingsService.NormalizeDestination(HouseholdAccess.OptionalText(reminder.Destination, "The destination", MealSettings.TextMaxLength));

            if (reminder.IsEnabled && destination is null)
            {
                throw new ArgumentException($"Escolhe o grupo ou o número para «{DailyCatalog.Label(reminder.Section)}» antes de ativar o envio.");
            }

            TimeOnly sendTime = new(reminder.SendTime.Hour, reminder.SendTime.Minute);
            DailyReminder? target = stored.FirstOrDefault(r => r.Section == reminder.Section);

            if (target is null)
            {
                target = new DailyReminder { HouseholdId = householdId, Section = reminder.Section };
                context.DailyReminders.Add(target);
            }

            bool scheduleChanged = reminder.IsEnabled && (!target.IsEnabled || target.SendTime != sendTime);

            if (scheduleChanged && TimeOnly.FromDateTime(now) >= sendTime)
            {
                target.LastSentOn = DateOnly.FromDateTime(now);
            }

            if (target.Destination != destination || scheduleChanged)
            {
                target.LastError = null;
                target.LastAttemptAt = null;
            }

            target.IsEnabled = reminder.IsEnabled;
            target.Destination = destination;
            target.SendTime = sendTime;
        }

        if (reminders.Any(r => r.IsEnabled) && !MealMessages.IsConnected(await LoadConnectionAsync(context, householdId, cancellationToken)))
        {
            throw new ArgumentException(ConnectionMissing);
        }

        await context.SaveChangesAsync(cancellationToken);

        return await LoadAsync(context, householdId, cancellationToken);
    }

    public async Task<List<WhatsAppGroupDto>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        MealSettings settings = await RequireConnectionAsync(context, householdId, cancellationToken);

        return await whatsApp.FetchGroupsAsync(settings.WhatsAppApiUrl!, settings.WhatsAppInstance!, MealMessages.ApiKey(dataProtection, settings), cancellationToken);
    }

    public async Task SendNowAsync(DailySection section, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        MealSettings settings = await RequireConnectionAsync(context, householdId, cancellationToken);
        DailyReminder? reminder = await context.DailyReminders.AsNoTracking()
            .FirstOrDefaultAsync(r => r.HouseholdId == householdId && r.Section == section, cancellationToken);

        if (string.IsNullOrWhiteSpace(reminder?.Destination))
        {
            throw new ArgumentException("Escolhe e guarda o grupo ou o número antes de enviar.");
        }

        DateOnly today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        DailyDigestSection digest = await DailyDigests.BuildAsync(context, householdId, section, today, cancellationToken);
        string text = digest.Lines.Count == 0
            ? $"{DailyCatalog.Emoji(section)} *{digest.Title}*: nada para hoje."
            : DailyDigests.Format(digest, today);

        await MealMessages.SendToAsync(whatsApp, dataProtection, settings, reminder.Destination, text, cancellationToken);
    }

    public async Task<DailyDigestDto> GetDigestAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);
        DateOnly today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);

        List<DailyDigestSection> sections = [];

        foreach (DailySection section in DailyCatalog.Sections)
        {
            DailyDigestSection digest = await DailyDigests.BuildAsync(context, householdId, section, today, cancellationToken);

            if (digest.Lines.Count > 0)
            {
                sections.Add(digest);
            }
        }

        return new DailyDigestDto(today, sections);
    }

    private static Task<MealSettings?> LoadConnectionAsync(AgenderDbContext context, Guid householdId, CancellationToken cancellationToken) =>
        context.MealPreferences.AsNoTracking().FirstOrDefaultAsync(s => s.HouseholdId == householdId, cancellationToken);

    private static async Task<MealSettings> RequireConnectionAsync(AgenderDbContext context, Guid householdId, CancellationToken cancellationToken)
    {
        MealSettings? settings = await LoadConnectionAsync(context, householdId, cancellationToken);

        return MealMessages.IsConnected(settings) ? settings! : throw new ArgumentException(ConnectionMissing);
    }

    private static async Task<DailyRemindersDto> LoadAsync(AgenderDbContext context, Guid householdId, CancellationToken cancellationToken)
    {
        List<DailyReminder> stored = await context.DailyReminders.AsNoTracking().Where(r => r.HouseholdId == householdId).ToListAsync(cancellationToken);

        return new DailyRemindersDto
        {
            WhatsAppReady = MealMessages.IsConnected(await LoadConnectionAsync(context, householdId, cancellationToken)),
            Reminders = [.. DailyCatalog.Sections.Select(section => stored.FirstOrDefault(r => r.Section == section) is DailyReminder r
                ? new DailyReminderDto { Section = section, IsEnabled = r.IsEnabled, Destination = r.Destination, SendTime = r.SendTime, LastSentOn = r.LastSentOn, LastError = r.LastError }
                : new DailyReminderDto { Section = section })]
        };
    }
}

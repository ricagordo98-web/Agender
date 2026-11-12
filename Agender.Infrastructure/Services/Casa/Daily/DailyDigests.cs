using System.Text;
using Agender.Infrastructure.Data;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

internal static class DailyDigests
{
    public static async Task<DailyDigestSection> BuildAsync(AgenderDbContext context, Guid householdId, DailySection section, DateOnly today, CancellationToken cancellationToken)
    {
        List<string> lines = section switch
        {
            DailySection.Cleaning => await CleaningLinesAsync(context, householdId, today, cancellationToken),
            DailySection.Meals => await MealLinesAsync(context, householdId, today, cancellationToken),
            DailySection.Events => await EventLinesAsync(context, householdId, today, cancellationToken),
            _ => await InventoryLinesAsync(context, householdId, today, cancellationToken)
        };

        return new DailyDigestSection(section, DailyCatalog.Title(section), lines);
    }

    public static string Format(DailyDigestSection digest, DateOnly date)
    {
        StringBuilder text = new();
        text.Append(DailyCatalog.Emoji(digest.Section)).Append(" *").Append(digest.Title).Append("* — ")
            .AppendLine(CasaCatalog.Capitalize(date.ToString("dddd, d 'de' MMMM", CasaCatalog.Culture)));

        foreach (string line in digest.Lines)
        {
            text.Append("• ").AppendLine(line);
        }

        return text.ToString().TrimEnd();
    }

    private static async Task<List<string>> CleaningLinesAsync(AgenderDbContext context, Guid householdId, DateOnly today, CancellationToken cancellationToken)
    {
        List<CleaningChoreStatus> due = [.. (await CleaningService.BuildStatusesAsync(context, householdId, today, cancellationToken)).Where(s => s.IsDue)];

        if (due.Count == 0)
        {
            return [];
        }

        Dictionary<Guid, CleaningRoom> rooms = await context.CleaningRooms.AsNoTracking().Where(r => r.HouseholdId == householdId)
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        return [.. due
            .OrderBy(s => rooms.GetValueOrDefault(s.Chore.RoomId)?.Order ?? int.MaxValue)
            .ThenBy(s => rooms.GetValueOrDefault(s.Chore.RoomId)?.Name)
            .ThenBy(s => s.Chore.Title)
            .Select(s => (rooms.GetValueOrDefault(s.Chore.RoomId) is CleaningRoom room ? $"{room.Emoji} {room.Name}: " : string.Empty)
                + s.Chore.Title + (s.IsLate ? " (atrasada)" : string.Empty))];
    }

    private static async Task<List<string>> MealLinesAsync(AgenderDbContext context, Guid householdId, DateOnly today, CancellationToken cancellationToken)
    {
        List<MealPlanEntry> entries = await context.MealPlanEntries.AsNoTracking()
            .Where(e => e.HouseholdId == householdId && e.Date == today)
            .OrderBy(e => e.Slot).ThenBy(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        List<Guid> recipeIds = [.. entries.Where(e => e.RecipeId is not null).Select(e => e.RecipeId!.Value).Distinct()];
        Dictionary<Guid, string> titles = await context.Recipes.AsNoTracking()
            .Where(r => r.HouseholdId == householdId && recipeIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Title, cancellationToken);

        return [.. entries.Select(e =>
        {
            string title = e.RecipeId is Guid id && titles.TryGetValue(id, out string? recipe) ? recipe : e.Title ?? "Sem título";
            return $"{MealCatalog.SlotEmoji(e.Slot)} {MealCatalog.SlotLabel(e.Slot)}: {title}{(e.IsLeftover ? " (restos)" : string.Empty)}";
        })];
    }

    private static async Task<List<string>> EventLinesAsync(AgenderDbContext context, Guid householdId, DateOnly today, CancellationToken cancellationToken)
    {
        DateTime from = today.ToDateTime(TimeOnly.MinValue);
        DateTime to = from.AddDays(1);

        List<FamilyEvent> events = await context.FamilyEvents.AsNoTracking()
            .Where(e => e.HouseholdId == householdId && (e.Recurrence != EventRecurrence.None || e.Start < to))
            .ToListAsync(cancellationToken);

        return [.. EventExpander.Expand(events, from, to)
            .OrderBy(o => !o.Event.AllDay).ThenBy(o => o.Start)
            .Select(o =>
            {
                string line = (o.Event.AllDay ? string.Empty : $"{o.Start:HH:mm} ") + o.Event.Title;

                if (o.Event.Kind == FamilyEventKind.Birthday && o.Years > 0 && o.Event.Start.Year > 1900)
                {
                    line += $" (faz {o.Years} anos)";
                }

                if (!string.IsNullOrWhiteSpace(o.Event.MemberName))
                {
                    line += $" · {o.Event.MemberName}";
                }

                return string.IsNullOrWhiteSpace(o.Event.Location) ? line : $"{line} · {o.Event.Location}";
            })];
    }

    private static async Task<List<string>> InventoryLinesAsync(AgenderDbContext context, Guid householdId, DateOnly today, CancellationToken cancellationToken)
    {
        List<InventoryItem> items = await context.InventoryItems.AsNoTracking()
            .Where(i => i.HouseholdId == householdId && i.ExpiresOn != null)
            .ToListAsync(cancellationToken);

        return [.. items.Where(i => HomeMatcher.IsExpiring(i, today))
            .OrderBy(i => i.ExpiresOn).ThenBy(i => i.Name)
            .Select(i => $"{i.Name} — {HomeMatcher.ExpiryLabel(i.ExpiresOn, today)!.ToLower(CasaCatalog.Culture)}")];
    }
}

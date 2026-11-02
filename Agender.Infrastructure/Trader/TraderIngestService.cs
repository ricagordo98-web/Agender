using System.Globalization;
using Agender.Infrastructure.Data;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Trader;

public interface ITraderIngestService
{
    Task<TraderSyncResult> SyncAsync(TraderSyncRequest request, CancellationToken cancellationToken = default);
}

public class TraderIngestService(IAgendaDbContextFactory contextFactory, TimeProvider time) : ITraderIngestService
{
    public const string AgendaName = "Trader";
    public const string WinProject = "Ganho";
    public const string LossProject = "Perda";
    public const string OpenProject = "Aberta";
    public const int MaxTrades = 20_000;

    private const int MarkerMinutes = 30;
    private const int MinSpanMinutes = 15;
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("pt-PT");

    public async Task<TraderSyncResult> SyncAsync(TraderSyncRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Trades.Count > MaxTrades)
        {
            throw new ArgumentException($"At most {MaxTrades} trades can be synchronised at once.");
        }

        if (request.Trades.GroupBy(t => t.ExternalId).Any(g => g.Count() > 1))
        {
            throw new ArgumentException("The trade ids must be unique.");
        }

        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        TimeZoneInfo zone = time.LocalTimeZone;
        DateTime now = time.GetUtcNow().UtcDateTime;

        Dictionary<long, TraderTrade> existing = await context.TraderTrades.ToDictionaryAsync(t => t.ExternalId, cancellationToken);
        Dictionary<Guid, TimeEntry> entries = await context.TimeEntries.Where(e => context.TraderTrades.Any(t => t.TimeEntryId == e.Id)).ToDictionaryAsync(e => e.Id, cancellationToken);
        TraderAgenda agenda = await EnsureAgendaAsync(context, cancellationToken);

        int created = 0;
        int updated = 0;

        foreach (TraderTradeInput input in request.Trades)
        {
            if (!existing.TryGetValue(input.ExternalId, out TraderTrade? trade))
            {
                trade = new TraderTrade { ExternalId = input.ExternalId };
                context.TraderTrades.Add(trade);
                existing[input.ExternalId] = trade;
                created++;
            }
            else
            {
                updated++;
            }

            Apply(trade, input, zone, now);
            UpsertEntry(context, trade, entries, agenda, zone);
        }

        HashSet<long> incoming = request.Trades.Select(t => t.ExternalId).ToHashSet();
        List<TraderTrade> removed = existing.Values.Where(t => !incoming.Contains(t.ExternalId)).ToList();

        foreach (TraderTrade trade in removed)
        {
            if (trade.TimeEntryId is { } entryId && entries.TryGetValue(entryId, out TimeEntry? entry))
            {
                context.TimeEntries.Remove(entry);
            }

            context.TraderTrades.Remove(trade);
        }

        await context.SaveChangesAsync(cancellationToken);
        await context.TraderKeys.Where(k => k.UserId == context.ActorId).ExecuteUpdateAsync(set => set.SetProperty(k => k.LastSyncAt, now), cancellationToken);

        return new TraderSyncResult(created, updated, removed.Count);
    }

    public static (DateOnly Date, int Start, int End) Place(TraderTrade trade, TimeZoneInfo zone)
    {
        DateTime entry = TimeZoneInfo.ConvertTimeFromUtc(trade.EntryTime, zone);

        if (!trade.IsClosed || trade.ExitTime is null)
        {
            int start = Math.Min(Minute(entry), TimeSlot.MinutesPerDay - MarkerMinutes);
            return (DateOnly.FromDateTime(entry), start, start + MarkerMinutes);
        }

        DateTime exit = TimeZoneInfo.ConvertTimeFromUtc(trade.ExitTime.Value, zone);
        DateOnly exitDate = DateOnly.FromDateTime(exit);
        int exitMinute = Minute(exit);

        if (DateOnly.FromDateTime(entry) == exitDate && exitMinute - Minute(entry) >= MinSpanMinutes)
        {
            return (exitDate, Minute(entry), exitMinute);
        }

        int markerStart = Math.Max(0, exitMinute - MarkerMinutes);
        return (exitDate, markerStart, markerStart + MarkerMinutes);
    }

    public static string Describe(TraderTrade trade)
    {
        string direction = trade.IsBuy ? "▲ Compra" : "▼ Venda";

        if (!trade.IsClosed || trade.NetPnl is not { } pnl)
        {
            return $"{trade.Symbol} {direction} · aberta";
        }

        string amount = pnl.ToString("+0.00;-0.00;0.00", Culture);
        return $"{trade.Symbol} {direction} · {amount} {trade.Currency}".TrimEnd();
    }

    private static int Minute(DateTime local) => local.Hour * 60 + local.Minute;

    private static void Apply(TraderTrade trade, TraderTradeInput input, TimeZoneInfo zone, DateTime now)
    {
        trade.Symbol = Cut(input.Symbol, TraderTrade.ShortTextMaxLength);
        trade.Name = Cut(input.Name, TraderTrade.ShortTextMaxLength);
        trade.IsBuy = input.IsBuy;
        trade.IsClosed = input.IsClosed && input.ExitTime is not null;
        trade.EntryTime = input.EntryTime.UtcDateTime;
        trade.EntryDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(trade.EntryTime, zone));
        trade.EntryPrice = input.EntryPrice;
        trade.Quantity = input.Quantity;
        trade.StopLoss = input.StopLoss;
        trade.TakeProfit = input.TakeProfit;
        trade.ExitTime = trade.IsClosed ? input.ExitTime!.Value.UtcDateTime : null;
        trade.ExitDate = trade.ExitTime is { } exit ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(exit, zone)) : null;
        trade.ExitPrice = trade.IsClosed ? input.ExitPrice : null;
        trade.Fees = input.Fees;
        trade.Invested = input.Invested;
        trade.RiskAmount = input.RiskAmount;
        trade.NetPnl = trade.IsClosed ? input.NetPnl : null;
        trade.RMultiple = trade.IsClosed ? input.RMultiple : null;
        trade.Currency = Cut(input.Currency, TraderTrade.CurrencyMaxLength);
        trade.Platform = Cut(input.Platform, TraderTrade.ShortTextMaxLength);
        trade.Notes = Cut(input.Notes, TraderTrade.TextMaxLength);
        trade.Setup = Cut(input.Setup, TraderTrade.ShortTextMaxLength);
        trade.Tags = Cut(input.Tags, TraderTrade.ShortTextMaxLength);
        trade.Emotion = Cut(input.Emotion, TraderTrade.ShortTextMaxLength);
        trade.UpdatedAt = now;
    }

    private static string Cut(string? value, int max)
    {
        string text = value?.Trim() ?? string.Empty;
        return text.Length > max ? text[..max] : text;
    }

    private static void UpsertEntry(AgenderDbContext context, TraderTrade trade, Dictionary<Guid, TimeEntry> entries, TraderAgenda agenda, TimeZoneInfo zone)
    {
        (DateOnly date, int start, int end) = Place(trade, zone);
        Project project = !trade.IsClosed ? agenda.Open : trade.NetPnl >= 0 ? agenda.Win : agenda.Loss;

        TimeEntry? entry = null;

        if (trade.TimeEntryId is { } entryId)
        {
            entries.TryGetValue(entryId, out entry);
        }

        if (entry is null)
        {
            entry = new TimeEntry();
            context.TimeEntries.Add(entry);
            entries[entry.Id] = entry;
            trade.TimeEntryId = entry.Id;
        }

        entry.Date = date;
        entry.StartMinute = start;
        entry.EndMinute = end;
        entry.Description = Describe(trade);
        entry.AgendaId = agenda.Id;
        entry.ProjectId = project.Id;
        entry.Color = project.Color;
        entry.CustomColor = project.CustomColor;
    }

    private static async Task<TraderAgenda> EnsureAgendaAsync(AgenderDbContext context, CancellationToken cancellationToken)
    {
        string lowered = AgendaName.ToLower();
        Agenda? agenda = await context.Agendas.FirstOrDefaultAsync(a => a.Name.ToLower() == lowered, cancellationToken);

        if (agenda is null)
        {
            agenda = new Agenda { Name = AgendaName, Color = EntryColor.Emerald, IsVisible = true };
            context.Agendas.Add(agenda);
        }

        List<Project> projects = await context.Projects.Where(p => p.AgendaId == agenda.Id).ToListAsync(cancellationToken);

        Project Ensure(string name, EntryColor color)
        {
            Project? project = projects.FirstOrDefault(p => p.Name == name);

            if (project is null)
            {
                project = new Project { Name = name, Color = color, AgendaId = agenda.Id };
                context.Projects.Add(project);
                projects.Add(project);
            }

            project.IsArchived = false;
            return project;
        }

        return new TraderAgenda(agenda.Id, Ensure(WinProject, EntryColor.Emerald), Ensure(LossProject, EntryColor.Rose), Ensure(OpenProject, EntryColor.Sky));
    }

    private sealed record TraderAgenda(Guid Id, Project Win, Project Loss, Project Open);
}

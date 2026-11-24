using Agender.Infrastructure.Data;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

public class IncomeService(IUserDbContextFactory contextFactory, TimeProvider time) : IIncomeService
{
    private const int MaxRangeDays = 4000;
    private const int MaxPlatformSuggestions = 30;

    public async Task<IReadOnlyList<IncomeSourceDto>> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        List<IncomeSource> sources = await context.IncomeSources.AsNoTracking().ToListAsync(cancellationToken);

        return [.. sources.OrderBy(s => s.SortOrder).ThenBy(s => s.CreatedAt).Select(s => s.ToDto())];
    }

    public async Task<IncomeSourceDto> CreateSourceAsync(IncomeSourceInput input, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        IncomeSource source = new() { CreatedAt = time.GetUtcNow().UtcDateTime };
        await ApplyAsync(context, source, input, cancellationToken);
        int? lastOrder = await context.IncomeSources.Select(s => (int?)s.SortOrder).MaxAsync(cancellationToken);
        source.SortOrder = (lastOrder ?? -1) + 1;

        context.IncomeSources.Add(source);
        await context.SaveChangesAsync(cancellationToken);

        return source.ToDto();
    }

    public async Task<IncomeSourceDto> UpdateSourceAsync(Guid id, IncomeSourceInput input, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        IncomeSource source = await context.IncomeSources.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Income source {id} was not found.");

        await ApplyAsync(context, source, input, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return source.ToDto();
    }

    public async Task DeleteSourceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        IncomeSource source = await context.IncomeSources.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Income source {id} was not found.");

        await context.IncomeEntries.Where(e => e.SourceId == id).ExecuteDeleteAsync(cancellationToken);
        context.IncomeSources.Remove(source);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IncomeEntryDto>> GetEntriesAsync(DateOnly from, DateOnly to, Guid? sourceId = null, CancellationToken cancellationToken = default)
    {
        if (to < from || to.DayNumber - from.DayNumber > MaxRangeDays)
        {
            throw new ArgumentException("The date range is not valid.");
        }

        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        IQueryable<IncomeEntry> query = context.IncomeEntries.AsNoTracking().Where(e => e.Date >= from && e.Date <= to);

        if (sourceId is { } id)
        {
            query = query.Where(e => e.SourceId == id);
        }

        List<IncomeEntry> entries = await query.ToListAsync(cancellationToken);

        return [.. entries.OrderBy(e => e.Date).ThenBy(e => e.CreatedAt).Select(e => e.ToDto())];
    }

    public async Task<IncomeEntryDto> CreateEntryAsync(IncomeEntryInput input, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        DateTime now = time.GetUtcNow().UtcDateTime;
        IncomeEntry entry = new() { CreatedAt = now };
        await ApplyAsync(context, entry, input, now, cancellationToken);

        context.IncomeEntries.Add(entry);
        await context.SaveChangesAsync(cancellationToken);

        return entry.ToDto();
    }

    public async Task<IncomeEntryDto> UpdateEntryAsync(Guid id, IncomeEntryInput input, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        IncomeEntry entry = await context.IncomeEntries.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Income entry {id} was not found.");

        await ApplyAsync(context, entry, input, time.GetUtcNow().UtcDateTime, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return entry.ToDto();
    }

    public async Task DeleteEntryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        IncomeEntry entry = await context.IncomeEntries.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Income entry {id} was not found.");

        context.IncomeEntries.Remove(entry);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetPlatformsAsync(Guid sourceId, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);

        var used = await context.IncomeEntries.AsNoTracking()
            .Where(e => e.SourceId == sourceId && e.Platform != string.Empty)
            .Select(e => new { e.Platform, e.Date, e.CreatedAt })
            .ToListAsync(cancellationToken);

        return [.. used
            .GroupBy(e => e.Platform, StringComparer.CurrentCultureIgnoreCase)
            .OrderByDescending(g => g.Max(e => e.Date))
            .ThenByDescending(g => g.Max(e => e.CreatedAt))
            .Select(g => g.OrderByDescending(e => e.CreatedAt).First().Platform)
            .Take(MaxPlatformSuggestions)];
    }

    private static async Task ApplyAsync(AgenderDbContext context, IncomeSource source, IncomeSourceInput input, CancellationToken cancellationToken)
    {
        string name = input.Name?.Trim() ?? string.Empty;
        string emoji = input.Emoji?.Trim() ?? string.Empty;

        if (name.Length is 0 or > IncomeSource.NameMaxLength)
        {
            throw new ArgumentException($"The source name must have 1 to {IncomeSource.NameMaxLength} characters.");
        }

        if (emoji.Length > IncomeSource.EmojiMaxLength)
        {
            throw new ArgumentException($"The emoji exceeds {IncomeSource.EmojiMaxLength} characters.");
        }

        if (!Enum.IsDefined(input.Color))
        {
            throw new ArgumentException("The color is not valid.");
        }

        if (!Enum.IsDefined(input.Currency))
        {
            throw new ArgumentException("The currency is not valid.");
        }

        string lowered = name.ToLower();

        if (await context.IncomeSources.AnyAsync(s => s.Id != source.Id && s.Name.ToLower() == lowered, cancellationToken))
        {
            throw new ArgumentException("A source with this name already exists.");
        }

        source.Name = name;
        source.Emoji = emoji;
        source.Color = input.Color;
        source.Currency = input.Currency;
        source.IsArchived = input.IsArchived;
    }

    private static async Task ApplyAsync(AgenderDbContext context, IncomeEntry entry, IncomeEntryInput input, DateTime now, CancellationToken cancellationToken)
    {
        string platform = input.Platform?.Trim() ?? string.Empty;
        string note = input.Note?.Trim() ?? string.Empty;

        if (input.Amount == 0 || Math.Abs(input.Amount) > IncomeEntry.MaxAmount)
        {
            throw new ArgumentException("The amount must be different from zero and within limits.");
        }

        if (!Enum.IsDefined(input.Status))
        {
            throw new ArgumentException("The status is not valid.");
        }

        if (platform.Length > IncomeEntry.PlatformMaxLength)
        {
            throw new ArgumentException($"The platform exceeds {IncomeEntry.PlatformMaxLength} characters.");
        }

        if (note.Length > IncomeEntry.NoteMaxLength)
        {
            throw new ArgumentException($"The note exceeds {IncomeEntry.NoteMaxLength} characters.");
        }

        if (!await context.IncomeSources.AnyAsync(s => s.Id == input.SourceId, cancellationToken))
        {
            throw new NotFoundException($"Income source {input.SourceId} was not found.");
        }

        entry.SourceId = input.SourceId;
        entry.Date = input.Date;
        entry.Amount = Math.Round(input.Amount, 2);
        entry.Platform = platform;
        entry.Note = note;
        entry.Status = input.Status;
        entry.UpdatedAt = now;
    }
}

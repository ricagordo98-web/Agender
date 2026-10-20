using Agender.Infrastructure.Data;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

public class DayNoteService(IUserDbContextFactory contextFactory) : IDayNoteService
{
    public async Task<IReadOnlyList<DayNote>> GetRangeAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);

        return await context.DayNotes
            .AsNoTracking()
            .Where(n => n.Date >= from && n.Date <= to)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveAsync(DateOnly date, string text, CancellationToken cancellationToken = default)
    {
        string trimmed = text.Trim();

        if (trimmed.Length > DayNote.TextMaxLength)
        {
            throw new ArgumentException($"Note exceeds {DayNote.TextMaxLength} characters.");
        }

        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        DayNote? existing = await context.DayNotes.FirstOrDefaultAsync(n => n.Date == date, cancellationToken);

        if (trimmed.Length == 0)
        {
            if (existing is not null)
            {
                context.DayNotes.Remove(existing);
            }
        }
        else if (existing is null)
        {
            context.DayNotes.Add(new DayNote { Date = date, Text = trimmed });
        }
        else
        {
            existing.Text = trimmed;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}

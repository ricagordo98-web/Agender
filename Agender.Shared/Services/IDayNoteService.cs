using Agender.Shared.Domain;

namespace Agender.Shared.Services;

public interface IDayNoteService
{
    Task<IReadOnlyList<DayNote>> GetRangeAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    Task SaveAsync(DateOnly date, string text, CancellationToken cancellationToken = default);
}

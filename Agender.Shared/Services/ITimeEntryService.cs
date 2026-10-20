using Agender.Shared.Domain;

namespace Agender.Shared.Services;

public interface ITimeEntryService
{
    Task<IReadOnlyList<TimeEntry>> GetRangeAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    Task<TimeEntry> CreateAsync(TimeEntry entry, CancellationToken cancellationToken = default);

    Task UpdateAsync(TimeEntry entry, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimeEntry>> SearchAsync(string query, int skip, int take, CancellationToken cancellationToken = default);
}

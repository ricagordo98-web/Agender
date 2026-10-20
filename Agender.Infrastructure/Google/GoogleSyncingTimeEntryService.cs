using Agender.Shared.Domain;
using Agender.Shared.Services;

namespace Agender.Infrastructure.Google;

public class GoogleSyncingTimeEntryService(ITimeEntryService inner, IGoogleEntrySync sync) : ITimeEntryService
{
    public Task<IReadOnlyList<TimeEntry>> GetRangeAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
        inner.GetRangeAsync(from, to, cancellationToken);

    public Task<IReadOnlyList<TimeEntry>> SearchAsync(string query, int skip, int take, CancellationToken cancellationToken = default) =>
        inner.SearchAsync(query, skip, take, cancellationToken);

    public async Task<TimeEntry> CreateAsync(TimeEntry entry, CancellationToken cancellationToken = default)
    {
        TimeEntry created = await inner.CreateAsync(entry, cancellationToken);

        if (created.SyncToGoogle)
        {
            created.GoogleSyncFailed = !await sync.SyncEntryAsync(created.Id, cancellationToken);
        }

        return created;
    }

    public async Task UpdateAsync(TimeEntry entry, CancellationToken cancellationToken = default)
    {
        await inner.UpdateAsync(entry, cancellationToken);
        entry.GoogleSyncFailed = !await sync.SyncEntryAsync(entry.Id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await sync.ForgetEntryAsync(id, cancellationToken);
        await inner.DeleteAsync(id, cancellationToken);
    }
}

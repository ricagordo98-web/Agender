namespace Agender.Infrastructure.Google;

public interface IGoogleEntrySync
{
    Task<bool> SyncEntryAsync(Guid entryId, CancellationToken cancellationToken);

    Task ForgetEntryAsync(Guid entryId, CancellationToken cancellationToken);
}

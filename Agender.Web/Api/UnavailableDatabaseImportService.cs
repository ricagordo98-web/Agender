using Agender.Shared.Services;

namespace Agender.Web.Api;

public sealed class UnavailableDatabaseImportService : IDatabaseImportService
{
    public bool IsAvailable => false;

    public Task ImportAsync(byte[] database, CancellationToken cancellationToken = default) =>
        throw new UnauthorizedAccessException("Loading a database is only available in the desktop app.");
}

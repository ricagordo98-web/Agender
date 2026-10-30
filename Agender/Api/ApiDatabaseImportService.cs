using Agender.Auth;
using Agender.Shared.Services;

namespace Agender.Api;

public sealed class ApiDatabaseImportService(IHttpClientFactory httpClientFactory, AuthSession session)
    : ApiClientBase(httpClientFactory, session), IDatabaseImportService
{
    public bool IsAvailable => true;

    public Task ImportAsync(byte[] database, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, "/api/database/import", database, cancellationToken, TimeSpan.FromMinutes(3));
}

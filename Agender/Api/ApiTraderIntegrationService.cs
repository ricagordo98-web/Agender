using Agender.Auth;
using Agender.Shared.Contracts;
using Agender.Shared.Services;

namespace Agender.Api;

public sealed class ApiTraderIntegrationService(IHttpClientFactory httpClientFactory, AuthSession session)
    : ApiClientBase(httpClientFactory, session), ITraderIntegrationService
{
    public Task<TraderIntegrationStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        GetAsync<TraderIntegrationStatus>("/api/trader/integration", cancellationToken);

    public Task<TraderKeyCreated> CreateKeyAsync(CancellationToken cancellationToken = default) =>
        SendAsync<TraderKeyCreated>(HttpMethod.Post, "/api/trader/integration/key", null, cancellationToken);

    public Task RevokeKeyAsync(CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, "/api/trader/integration/key", null, cancellationToken);
}

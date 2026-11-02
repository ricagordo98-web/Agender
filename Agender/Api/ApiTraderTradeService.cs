using Agender.Auth;
using Agender.Shared.Contracts;
using Agender.Shared.Services;

namespace Agender.Api;

public sealed class ApiTraderTradeService(IHttpClientFactory httpClientFactory, AuthSession session)
    : ApiClientBase(httpClientFactory, session), ITraderTradeService
{
    public async Task<IReadOnlyList<TraderTradeDto>> GetRangeAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
        await GetAsync<TraderTradeDto[]>($"/api/trader/trades?from={Date(from)}&to={Date(to)}", cancellationToken);
}

using Agender.Auth;
using Agender.Shared.Services;

namespace Agender.Api;

public sealed class ApiTagService(IHttpClientFactory httpClientFactory, AuthSession session)
    : ApiClientBase(httpClientFactory, session), ITagService
{
    public async Task<IReadOnlyList<string>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<string[]>("/api/tags", cancellationToken);
}

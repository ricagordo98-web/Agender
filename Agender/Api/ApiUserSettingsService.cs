using Agender.Auth;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;

namespace Agender.Api;

public sealed class ApiUserSettingsService(IHttpClientFactory httpClientFactory, AuthSession session)
    : ApiClientBase(httpClientFactory, session), IUserSettingsService
{
    public async Task<UserSettings> GetAsync(CancellationToken cancellationToken = default) =>
        (await GetAsync<UserSettingsDto>("/api/settings", cancellationToken)).ToEntity();

    public Task SaveAsync(UserSettings settings, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, "/api/settings", settings.ToDto(), cancellationToken);
}

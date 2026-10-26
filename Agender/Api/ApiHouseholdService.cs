using Agender.Auth;
using Agender.Shared.Contracts;
using Agender.Shared.Services;

namespace Agender.Api;

public sealed class ApiHouseholdService(IHttpClientFactory httpClientFactory, AuthSession session)
    : ApiClientBase(httpClientFactory, session), IHouseholdService
{
    public async Task<HouseholdDto?> GetAsync(CancellationToken cancellationToken = default)
    {
        HouseholdEnvelope envelope = await GetAsync<HouseholdEnvelope>("/api/casa/household", cancellationToken);
        return envelope.Household;
    }

    public Task<HouseholdDto> CreateAsync(string name, CancellationToken cancellationToken = default) =>
        SendAsync<HouseholdDto>(HttpMethod.Post, "/api/casa/household", new CreateHouseholdRequest(name), cancellationToken);

    public Task<HouseholdDto> JoinAsync(string code, CancellationToken cancellationToken = default) =>
        SendAsync<HouseholdDto>(HttpMethod.Post, "/api/casa/household/join", new JoinHouseholdRequest(code), cancellationToken);

    public Task<HouseholdDto> RenameAsync(string name, CancellationToken cancellationToken = default) =>
        SendAsync<HouseholdDto>(HttpMethod.Put, "/api/casa/household", new RenameHouseholdRequest(name), cancellationToken);

    public Task<HouseholdDto> NewInviteCodeAsync(CancellationToken cancellationToken = default) =>
        SendAsync<HouseholdDto>(HttpMethod.Post, "/api/casa/household/code", null, cancellationToken);

    public Task<HouseholdDto> SetMyColorAsync(string color, CancellationToken cancellationToken = default) =>
        SendAsync<HouseholdDto>(HttpMethod.Put, "/api/casa/household/color", new MemberColorRequest(color), cancellationToken);

    public Task<HouseholdDto> InviteAsync(string email, CancellationToken cancellationToken = default) =>
        SendAsync<HouseholdDto>(HttpMethod.Post, "/api/casa/household/invites", new InviteRequest(email), cancellationToken);

    public Task<HouseholdDto> CancelInviteAsync(Guid inviteId, CancellationToken cancellationToken = default) =>
        SendAsync<HouseholdDto>(HttpMethod.Delete, $"/api/casa/household/invites/{inviteId}", null, cancellationToken);

    public Task LeaveAsync(CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, "/api/casa/household/leave", null, cancellationToken);

    public Task<CasaDashboard> GetDashboardAsync(CancellationToken cancellationToken = default) =>
        GetAsync<CasaDashboard>("/api/casa/dashboard", cancellationToken);
}

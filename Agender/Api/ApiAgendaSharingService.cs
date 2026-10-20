using Agender.Auth;
using Agender.Shared.Abstractions;
using Agender.Shared.Contracts;
using Agender.Shared.Services;

namespace Agender.Api;

public sealed class ApiAgendaSharingService(IHttpClientFactory httpClientFactory, AuthSession session)
    : ApiClientBase(httpClientFactory, session), IAgendaSharingService, IShareLinkBuilder
{
    private static readonly TimeSpan LinkCreationTimeout = TimeSpan.FromMinutes(3);

    private readonly AuthSession _session = session;

    public async Task<IReadOnlyList<ShareDto>> GetSharesAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<ShareDto[]>("/api/sharing", cancellationToken);

    public Task<ShareDto> ShareAsync(string email, CancellationToken cancellationToken = default) =>
        SendAsync<ShareDto>(HttpMethod.Post, "/api/sharing", new ShareRequest(email), cancellationToken);

    public Task RevokeAsync(Guid shareId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"/api/sharing/{shareId}", null, cancellationToken);

    public async Task<IReadOnlyList<SharedWithMeDto>> GetSharedWithMeAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<SharedWithMeDto[]>("/api/sharing/received", cancellationToken);

    public Task<SharedAgendaDto> GetSharedAgendaAsync(Guid shareId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
        GetAsync<SharedAgendaDto>($"/api/sharing/received/{shareId}/agenda?from={Date(from)}&to={Date(to)}", cancellationToken);

    public Task<ShareLinkStatusDto> GetLinkAsync(CancellationToken cancellationToken = default) =>
        GetAsync<ShareLinkStatusDto>("/api/sharing/link", cancellationToken);

    public Task<NewShareLinkDto> CreateLinkAsync(CancellationToken cancellationToken = default) =>
        SendAsync<NewShareLinkDto>(HttpMethod.Post, "/api/sharing/link", null, cancellationToken, LinkCreationTimeout);

    public Task RevokeLinkAsync(CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, "/api/sharing/link", null, cancellationToken);

    public string Build(string token) => $"{_session.ServerUrl}/p/{token}";
}

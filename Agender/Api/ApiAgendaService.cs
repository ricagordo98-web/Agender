using Agender.Auth;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;

namespace Agender.Api;

public sealed class ApiAgendaService(IHttpClientFactory httpClientFactory, AuthSession session)
    : ApiClientBase(httpClientFactory, session), IAgendaService
{
    public async Task<IReadOnlyList<Agenda>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        AgendaDto[] agendas = await GetAsync<AgendaDto[]>("/api/agendas", cancellationToken);
        return [.. agendas.Select(a => a.ToEntity())];
    }

    public async Task<Agenda> CreateAsync(Agenda agenda, CancellationToken cancellationToken = default)
    {
        AgendaDto created = await SendAsync<AgendaDto>(HttpMethod.Post, "/api/agendas", agenda.ToDto(), cancellationToken);
        return created.ToEntity();
    }

    public Task UpdateAsync(Agenda agenda, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"/api/agendas/{agenda.Id}", agenda.ToDto(), cancellationToken);

    public Task SetActiveAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"/api/agendas/{id}/activate", null, cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"/api/agendas/{id}", null, cancellationToken);
}

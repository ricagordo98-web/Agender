using Agender.Auth;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;

namespace Agender.Api;

public sealed class ApiProjectService(IHttpClientFactory httpClientFactory, AuthSession session)
    : ApiClientBase(httpClientFactory, session), IProjectService
{
    public async Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        ProjectDto[] projects = await GetAsync<ProjectDto[]>("/api/projects", cancellationToken);
        return [.. projects.Select(p => p.ToEntity())];
    }

    public async Task<Project> CreateAsync(Project project, CancellationToken cancellationToken = default)
    {
        ProjectDto created = await SendAsync<ProjectDto>(HttpMethod.Post, "/api/projects", project.ToDto(), cancellationToken);
        return created.ToEntity();
    }

    public Task UpdateAsync(Project project, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"/api/projects/{project.Id}", project.ToDto(), cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"/api/projects/{id}", null, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, int>> GetMinutesByProjectAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        ProjectMinutesDto[] totals = await GetAsync<ProjectMinutesDto[]>($"/api/projects/stats?from={Date(from)}&to={Date(to)}", cancellationToken);
        return totals.ToDictionary(t => t.ProjectId, t => t.Minutes);
    }
}

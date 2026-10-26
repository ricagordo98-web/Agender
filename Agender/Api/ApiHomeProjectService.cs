using Agender.Auth;
using Agender.Shared.Domain;
using Agender.Shared.Services;

namespace Agender.Api;

public sealed class ApiHomeProjectService(IHttpClientFactory httpClientFactory, AuthSession session)
    : ApiClientBase(httpClientFactory, session), IHomeProjectService
{
    public async Task<IReadOnlyList<HomeProject>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<HomeProject[]>("/api/casa/projects", cancellationToken);

    public Task<HomeProject> CreateAsync(HomeProject project, CancellationToken cancellationToken = default) =>
        SendAsync<HomeProject>(HttpMethod.Post, "/api/casa/projects", project, cancellationToken);

    public Task UpdateAsync(HomeProject project, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"/api/casa/projects/{project.Id}", project, cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"/api/casa/projects/{id}", null, cancellationToken);
}

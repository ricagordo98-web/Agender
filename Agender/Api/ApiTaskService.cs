using Agender.Auth;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;

namespace Agender.Api;

public sealed class ApiTaskService(IHttpClientFactory httpClientFactory, AuthSession session)
    : ApiClientBase(httpClientFactory, session), ITaskService
{
    public async Task<IReadOnlyList<TaskItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        TaskDto[] tasks = await GetAsync<TaskDto[]>("/api/tasks", cancellationToken);
        return [.. tasks.Select(t => t.ToEntity())];
    }

    public async Task<TaskItem> CreateAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        TaskDto created = await SendAsync<TaskDto>(HttpMethod.Post, "/api/tasks", task.ToDto(), cancellationToken);
        return created.ToEntity();
    }

    public Task UpdateAsync(TaskItem task, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"/api/tasks/{task.Id}", task.ToDto(), cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"/api/tasks/{id}", null, cancellationToken);
}

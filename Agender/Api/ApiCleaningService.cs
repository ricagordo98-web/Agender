using Agender.Auth;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;

namespace Agender.Api;

public sealed class ApiCleaningService(IHttpClientFactory httpClientFactory, AuthSession session)
    : ApiClientBase(httpClientFactory, session), ICleaningService
{
    private const string Root = "/api/casa/cleaning";

    public Task<CleaningOverview> GetAsync(CancellationToken cancellationToken = default) =>
        GetAsync<CleaningOverview>(Root, cancellationToken);

    public Task<CleaningRoom> SaveRoomAsync(CleaningRoom room, CancellationToken cancellationToken = default) =>
        SendAsync<CleaningRoom>(HttpMethod.Post, $"{Root}/rooms", room, cancellationToken);

    public Task DeleteRoomAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"{Root}/rooms/{id}", null, cancellationToken);

    public Task<CleaningChore> SaveChoreAsync(CleaningChore chore, CancellationToken cancellationToken = default) =>
        SendAsync<CleaningChore>(HttpMethod.Post, $"{Root}/chores", chore, cancellationToken);

    public Task DeleteChoreAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"{Root}/chores/{id}", null, cancellationToken);

    public Task CompleteAsync(Guid choreId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"{Root}/chores/{choreId}/done", null, cancellationToken);

    public Task UndoAsync(Guid choreId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"{Root}/chores/{choreId}/done", null, cancellationToken);
}

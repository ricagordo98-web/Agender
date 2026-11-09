using Agender.Auth;
using Agender.Shared.Domain;
using Agender.Shared.Services;

namespace Agender.Api;

public sealed class ApiInventoryService(IHttpClientFactory httpClientFactory, AuthSession session)
    : ApiClientBase(httpClientFactory, session), IInventoryService
{
    private const string Root = "/api/casa/inventory";

    public Task<List<InventoryItem>> GetAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<InventoryItem>>(Root, cancellationToken);

    public Task<InventoryItem> AddAsync(InventoryItem item, CancellationToken cancellationToken = default) =>
        SendAsync<InventoryItem>(HttpMethod.Post, Root, item, cancellationToken);

    public Task<InventoryItem> UpdateAsync(InventoryItem item, CancellationToken cancellationToken = default) =>
        SendAsync<InventoryItem>(HttpMethod.Put, $"{Root}/{item.Id}", item, cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"{Root}/{id}", null, cancellationToken);
}

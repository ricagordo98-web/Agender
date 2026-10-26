using Agender.Auth;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;

namespace Agender.Api;

public sealed class ApiShoppingService(IHttpClientFactory httpClientFactory, AuthSession session)
    : ApiClientBase(httpClientFactory, session), IShoppingService
{
    public async Task<IReadOnlyList<ShoppingList>> GetListsAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<ShoppingList[]>("/api/casa/shopping/lists", cancellationToken);

    public Task<ShoppingList> GetListAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<ShoppingList>($"/api/casa/shopping/lists/{id}", cancellationToken);

    public Task<ShoppingList> CreateListAsync(ShoppingList list, CancellationToken cancellationToken = default) =>
        SendAsync<ShoppingList>(HttpMethod.Post, "/api/casa/shopping/lists", list, cancellationToken);

    public Task UpdateListAsync(ShoppingList list, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"/api/casa/shopping/lists/{list.Id}", list, cancellationToken);

    public Task DeleteListAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"/api/casa/shopping/lists/{id}", null, cancellationToken);

    public Task<ShoppingItem> AddItemAsync(Guid listId, ShoppingItem item, CancellationToken cancellationToken = default) =>
        SendAsync<ShoppingItem>(HttpMethod.Post, $"/api/casa/shopping/lists/{listId}/items", item, cancellationToken);

    public Task UpdateItemAsync(ShoppingItem item, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"/api/casa/shopping/items/{item.Id}", item, cancellationToken);

    public Task DeleteItemAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"/api/casa/shopping/items/{id}", null, cancellationToken);

    public Task ClearCheckedAsync(Guid listId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"/api/casa/shopping/lists/{listId}/clear", null, cancellationToken);

    public Task<ListShare> ShareAsync(Guid listId, string email, bool canEdit, CancellationToken cancellationToken = default) =>
        SendAsync<ListShare>(HttpMethod.Post, $"/api/casa/shopping/lists/{listId}/shares", new ShareListRequest(email, canEdit), cancellationToken);

    public Task UnshareAsync(Guid listId, Guid shareId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"/api/casa/shopping/lists/{listId}/shares/{shareId}", null, cancellationToken);
}

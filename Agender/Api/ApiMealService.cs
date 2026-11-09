using Agender.Auth;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;

namespace Agender.Api;

public sealed class ApiMealService(IHttpClientFactory httpClientFactory, AuthSession session)
    : ApiClientBase(httpClientFactory, session), IMealService
{
    private const string Root = "/api/casa/meals";

    private static readonly TimeSpan RemoteTimeout = TimeSpan.FromMinutes(1);

    public Task<MealWeekOverview> GetWeekAsync(DateOnly weekStart, CancellationToken cancellationToken = default) =>
        GetAsync<MealWeekOverview>($"{Root}/week?start={Date(weekStart)}", cancellationToken);

    public Task<MealPlanEntry> SaveEntryAsync(MealPlanEntry entry, CancellationToken cancellationToken = default) =>
        SendAsync<MealPlanEntry>(HttpMethod.Post, $"{Root}/entries", entry, cancellationToken);

    public Task DeleteEntryAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"{Root}/entries/{id}", null, cancellationToken);

    public Task<List<Recipe>> GetRecipesAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<Recipe>>($"{Root}/recipes", cancellationToken);

    public Task<Recipe> SaveRecipeAsync(Recipe recipe, CancellationToken cancellationToken = default) =>
        SendAsync<Recipe>(HttpMethod.Post, $"{Root}/recipes", recipe, cancellationToken);

    public Task DeleteRecipeAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"{Root}/recipes/{id}", null, cancellationToken);

    public Task<Recipe> ImportRecipeAsync(string url, CancellationToken cancellationToken = default) =>
        SendAsync<Recipe>(HttpMethod.Post, $"{Root}/recipes/import", new ImportRecipeRequest(url), cancellationToken, RemoteTimeout);

    public Task<CatalogPage> SearchCatalogAsync(string? query, string? category, int skip, CancellationToken cancellationToken = default) =>
        GetAsync<CatalogPage>($"{Root}/catalog?q={Uri.EscapeDataString(query ?? string.Empty)}&category={Uri.EscapeDataString(category ?? string.Empty)}&skip={skip}", cancellationToken);

    public Task<List<CatalogRecipe>> LoadCatalogDetailsAsync(List<Guid> catalogIds, CancellationToken cancellationToken = default) =>
        SendAsync<List<CatalogRecipe>>(HttpMethod.Post, $"{Root}/catalog/details", catalogIds, cancellationToken, TimeSpan.FromMinutes(2));

    public Task<CatalogRecipe> GetCatalogRecipeAsync(Guid catalogId, CancellationToken cancellationToken = default) =>
        GetAsync<CatalogRecipe>($"{Root}/catalog/{catalogId}", cancellationToken);

    public Task<Recipe> AdoptCatalogRecipeAsync(Guid catalogId, CancellationToken cancellationToken = default) =>
        SendAsync<Recipe>(HttpMethod.Post, $"{Root}/catalog/{catalogId}/adopt", null, cancellationToken, RemoteTimeout);

    public Task<List<MealIngredientLine>> GetShoppingPreviewAsync(DateOnly weekStart, CancellationToken cancellationToken = default) =>
        GetAsync<List<MealIngredientLine>>($"{Root}/shopping?start={Date(weekStart)}", cancellationToken);

    public Task<ShoppingList> CreateShoppingListAsync(DateOnly weekStart, List<MealIngredientLine> lines, CancellationToken cancellationToken = default) =>
        SendAsync<ShoppingList>(HttpMethod.Post, $"{Root}/shopping", new MealShoppingRequest(weekStart, lines), cancellationToken);

    public async Task<string> GetShareTextAsync(DateOnly weekStart, CancellationToken cancellationToken = default) =>
        (await GetAsync<MealShareText>($"{Root}/share?start={Date(weekStart)}", cancellationToken)).Text;

    public Task SendWhatsAppAsync(DateOnly weekStart, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"{Root}/send?start={Date(weekStart)}", null, cancellationToken, RemoteTimeout);
}

public sealed class ApiMealSettingsService(IHttpClientFactory httpClientFactory, AuthSession session)
    : ApiClientBase(httpClientFactory, session), IMealSettingsService
{
    private const string Root = "/api/casa/meal-settings";

    private static readonly TimeSpan RemoteTimeout = TimeSpan.FromMinutes(1);

    public Task<MealSettingsDto> GetAsync(CancellationToken cancellationToken = default) =>
        GetAsync<MealSettingsDto>(Root, cancellationToken);

    public Task<MealSettingsDto> SaveAsync(MealSettingsDto settings, CancellationToken cancellationToken = default) =>
        SendAsync<MealSettingsDto>(HttpMethod.Put, Root, settings, cancellationToken);

    public Task<RecipeSource> SaveSourceAsync(RecipeSource source, CancellationToken cancellationToken = default) =>
        SendAsync<RecipeSource>(HttpMethod.Post, $"{Root}/sources", source, cancellationToken);

    public Task DeleteSourceAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"{Root}/sources/{id}", null, cancellationToken);

    public Task<int> AddSuggestedSourcesAsync(CancellationToken cancellationToken = default) =>
        SendAsync<int>(HttpMethod.Post, $"{Root}/sources/suggested", null, cancellationToken);

    public Task RefreshSourcesAsync(CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"{Root}/sources/refresh", null, cancellationToken);

    public Task SendTestAsync(CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"{Root}/test", null, cancellationToken, RemoteTimeout);
}

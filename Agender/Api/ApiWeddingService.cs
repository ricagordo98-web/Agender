using Agender.Auth;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;

namespace Agender.Api;

public sealed class ApiWeddingService(IHttpClientFactory httpClientFactory, AuthSession session)
    : ApiClientBase(httpClientFactory, session), IWeddingService
{
    private const string Root = "/api/casa/wedding";

    public Task<WeddingOverview> GetAsync(CancellationToken cancellationToken = default) =>
        GetAsync<WeddingOverview>(Root, cancellationToken);

    public Task<WeddingSettings> SaveSettingsAsync(WeddingSettings settings, CancellationToken cancellationToken = default) =>
        SendAsync<WeddingSettings>(HttpMethod.Put, $"{Root}/settings", settings, cancellationToken);

    public Task<WeddingGuest> SaveGuestAsync(WeddingGuest guest, CancellationToken cancellationToken = default) =>
        SendAsync<WeddingGuest>(HttpMethod.Post, $"{Root}/guests", guest, cancellationToken);

    public Task DeleteGuestAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"{Root}/guests/{id}", null, cancellationToken);

    public Task<WeddingCategory> SaveCategoryAsync(WeddingCategory category, CancellationToken cancellationToken = default) =>
        SendAsync<WeddingCategory>(HttpMethod.Post, $"{Root}/categories", category, cancellationToken);

    public Task DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"{Root}/categories/{id}", null, cancellationToken);

    public Task<WeddingPayment> SavePaymentAsync(WeddingPayment payment, CancellationToken cancellationToken = default) =>
        SendAsync<WeddingPayment>(HttpMethod.Post, $"{Root}/payments", payment, cancellationToken);

    public Task DeletePaymentAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"{Root}/payments/{id}", null, cancellationToken);

    public Task<WeddingVendor> SaveVendorAsync(WeddingVendor vendor, CancellationToken cancellationToken = default) =>
        SendAsync<WeddingVendor>(HttpMethod.Post, $"{Root}/vendors", vendor, cancellationToken);

    public Task DeleteVendorAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"{Root}/vendors/{id}", null, cancellationToken);

    public Task<WeddingTask> SaveTaskAsync(WeddingTask task, CancellationToken cancellationToken = default) =>
        SendAsync<WeddingTask>(HttpMethod.Post, $"{Root}/tasks", task, cancellationToken);

    public Task DeleteTaskAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"{Root}/tasks/{id}", null, cancellationToken);

    public Task<int> AddSuggestedCategoriesAsync(CancellationToken cancellationToken = default) =>
        SendAsync<int>(HttpMethod.Post, $"{Root}/categories/suggested", null, cancellationToken);

    public Task<int> AddSuggestedTasksAsync(CancellationToken cancellationToken = default) =>
        SendAsync<int>(HttpMethod.Post, $"{Root}/tasks/suggested", null, cancellationToken);
}

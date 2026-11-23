using Agender.Auth;
using Agender.Shared.Contracts;
using Agender.Shared.Services;

namespace Agender.Api;

public sealed class ApiIncomeService(IHttpClientFactory httpClientFactory, AuthSession session)
    : ApiClientBase(httpClientFactory, session), IIncomeService
{
    public async Task<IReadOnlyList<IncomeSourceDto>> GetSourcesAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<IncomeSourceDto[]>("/api/income/sources", cancellationToken);

    public Task<IncomeSourceDto> CreateSourceAsync(IncomeSourceInput input, CancellationToken cancellationToken = default) =>
        SendAsync<IncomeSourceDto>(HttpMethod.Post, "/api/income/sources", input, cancellationToken);

    public Task<IncomeSourceDto> UpdateSourceAsync(Guid id, IncomeSourceInput input, CancellationToken cancellationToken = default) =>
        SendAsync<IncomeSourceDto>(HttpMethod.Put, $"/api/income/sources/{id}", input, cancellationToken);

    public Task DeleteSourceAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"/api/income/sources/{id}", null, cancellationToken);

    public async Task<IReadOnlyList<IncomeEntryDto>> GetEntriesAsync(DateOnly from, DateOnly to, Guid? sourceId = null, CancellationToken cancellationToken = default)
    {
        string source = sourceId is { } id ? $"&sourceId={id}" : string.Empty;
        return await GetAsync<IncomeEntryDto[]>($"/api/income/entries?from={Date(from)}&to={Date(to)}{source}", cancellationToken);
    }

    public Task<IncomeEntryDto> CreateEntryAsync(IncomeEntryInput input, CancellationToken cancellationToken = default) =>
        SendAsync<IncomeEntryDto>(HttpMethod.Post, "/api/income/entries", input, cancellationToken);

    public Task<IncomeEntryDto> UpdateEntryAsync(Guid id, IncomeEntryInput input, CancellationToken cancellationToken = default) =>
        SendAsync<IncomeEntryDto>(HttpMethod.Put, $"/api/income/entries/{id}", input, cancellationToken);

    public Task DeleteEntryAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"/api/income/entries/{id}", null, cancellationToken);

    public async Task<IReadOnlyList<string>> GetPlatformsAsync(Guid sourceId, CancellationToken cancellationToken = default) =>
        await GetAsync<string[]>($"/api/income/sources/{sourceId}/platforms", cancellationToken);
}

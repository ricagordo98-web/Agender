using Agender.Auth;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;

namespace Agender.Api;

public sealed class ApiTimeEntryService(IHttpClientFactory httpClientFactory, AuthSession session)
    : ApiClientBase(httpClientFactory, session), ITimeEntryService
{
    public async Task<IReadOnlyList<TimeEntry>> GetRangeAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        TimeEntryDto[] entries = await GetAsync<TimeEntryDto[]>($"/api/entries?from={Date(from)}&to={Date(to)}", cancellationToken);
        return [.. entries.Select(e => e.ToEntity())];
    }

    public async Task<TimeEntry> CreateAsync(TimeEntry entry, CancellationToken cancellationToken = default)
    {
        TimeEntryDto created = await SendAsync<TimeEntryDto>(HttpMethod.Post, "/api/entries", entry.ToDto(), cancellationToken);
        return created.ToEntity();
    }

    public Task UpdateAsync(TimeEntry entry, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"/api/entries/{entry.Id}", entry.ToDto(), cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"/api/entries/{id}", null, cancellationToken);

    public async Task<IReadOnlyList<TimeEntry>> SearchAsync(string query, int skip, int take, CancellationToken cancellationToken = default)
    {
        TimeEntryDto[] entries = await GetAsync<TimeEntryDto[]>(
            $"/api/entries/search?q={Uri.EscapeDataString(query)}&skip={skip}&take={take}", cancellationToken);
        return [.. entries.Select(e => e.ToEntity())];
    }
}

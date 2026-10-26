using System.Globalization;
using Agender.Auth;
using Agender.Shared.Domain;
using Agender.Shared.Services;

namespace Agender.Api;

public sealed class ApiFamilyEventService(IHttpClientFactory httpClientFactory, AuthSession session)
    : ApiClientBase(httpClientFactory, session), IFamilyEventService
{
    public async Task<IReadOnlyList<EventOccurrence>> GetAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default) =>
        await GetAsync<EventOccurrence[]>($"/api/casa/events?from={Stamp(from)}&to={Stamp(to)}", cancellationToken);

    public Task<FamilyEvent> CreateAsync(FamilyEvent familyEvent, CancellationToken cancellationToken = default) =>
        SendAsync<FamilyEvent>(HttpMethod.Post, "/api/casa/events", familyEvent, cancellationToken);

    public Task UpdateAsync(FamilyEvent familyEvent, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"/api/casa/events/{familyEvent.Id}", familyEvent, cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"/api/casa/events/{id}", null, cancellationToken);

    private static string Stamp(DateTime value) => value.ToString("s", CultureInfo.InvariantCulture);
}

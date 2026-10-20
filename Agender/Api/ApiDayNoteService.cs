using Agender.Auth;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;

namespace Agender.Api;

public sealed class ApiDayNoteService(IHttpClientFactory httpClientFactory, AuthSession session)
    : ApiClientBase(httpClientFactory, session), IDayNoteService
{
    public async Task<IReadOnlyList<DayNote>> GetRangeAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        DayNoteDto[] notes = await GetAsync<DayNoteDto[]>($"/api/notes?from={Date(from)}&to={Date(to)}", cancellationToken);
        return [.. notes.Select(n => n.ToEntity())];
    }

    public Task SaveAsync(DateOnly date, string text, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"/api/notes/{Date(date)}", new SaveNoteRequest(text), cancellationToken);
}
